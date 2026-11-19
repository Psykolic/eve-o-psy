using EveOPreview.Configuration;
using EveOPreview.Mediator.Messages;
using EveOPreview.Services.Interop;
using EveOPreview.UI.Hotkeys;
using EveOPreview.View;
using MediatR;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Reflection.Metadata;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Threading;

namespace EveOPreview.Services
{
	sealed class ThumbnailManager : IThumbnailManager
	{
		#region Private constants
		private const int WINDOW_POSITION_THRESHOLD_LOW = -10_000;
		private const int WINDOW_POSITION_THRESHOLD_HIGH = 31_000;
		private const int WINDOW_SIZE_THRESHOLD = 10;
		private const int FORCED_REFRESH_CYCLE_THRESHOLD = 2;
		private const int DEFAULT_LOCATION_CHANGE_NOTIFICATION_DELAY = 2;

		private const string DEFAULT_CLIENT_TITLE = ThumbnailSizePolicy.DefaultClientTitle;
		#endregion

		#region Private fields
		private readonly IMediator _mediator;
		private readonly IProcessMonitor _processMonitor;
		private readonly IWindowManager _windowManager;
		private readonly IThumbnailConfiguration _configuration;
		private readonly DispatcherTimer _thumbnailUpdateTimer;
		private readonly IThumbnailViewFactory _thumbnailViewFactory;

		// Confinado à thread de UI (issue #59). O DispatcherTimer, os eventos das views e as
		// continuações de ativação já rodam nessa thread; por isso não há lock aqui: a
		// invariante é verificada por EnsureThumbnailViewsThread a cada entrada pública.
		private readonly Dictionary<IntPtr, IThumbnailView> _thumbnailViews;
		private readonly Dictionary<IntPtr, IThumbnailView> _spotlightViews;

		// Thread dona do dicionário de views: a mesma que criou o DispatcherTimer (o Tick
		// só dispara na thread do Dispatcher associado ao timer).
		private readonly int _uiThreadId;

		// Rastreia o trabalho assíncrono dos handlers para que StopAsync possa aguardá-lo.
		private readonly AsyncOperationTracker _asyncOperations;

		// Vida do manager: criado em Start(), cancelado em StopAsync() para que o trabalho
		// em voo termine de forma cooperativa.
		private CancellationTokenSource _lifetimeCancellation;

		private (IntPtr Handle, string Title) _activeClient;
		private IntPtr _externalApplication;

		private readonly object _locationChangeNotificationSyncRoot;
		private (IntPtr Handle, string Title, string ActiveClient, Point Location, int Delay) _enqueuedLocationChangeNotification;

		private bool _ignoreViewEvents;
		private bool _isHoverEffectActive;

		private int _refreshCycleCount;
		private int _hideThumbnailsDelay;

		private bool _spotlightsVisible;
		private bool _spotlightLayoutDirty;

		private List<HotkeyHandler> _cycleClientHotkeyHandlers = new List<HotkeyHandler>();
		private List<HotkeyHandler> _spotlightHotkeyHandlers = new List<HotkeyHandler>();
		#endregion

		public ThumbnailManager(IMediator mediator, IThumbnailConfiguration configuration, IProcessMonitor processMonitor, IWindowManager windowManager, IThumbnailViewFactory factory)
		{
			this._mediator = mediator;
			this._processMonitor = processMonitor;
			this._windowManager = windowManager;
			this._configuration = configuration;
			this._thumbnailViewFactory = factory;

			this._activeClient = (IntPtr.Zero, ThumbnailManager.DEFAULT_CLIENT_TITLE);

			this.EnableViewEvents();
			this._isHoverEffectActive = false;

			this._refreshCycleCount = 0;
			this._locationChangeNotificationSyncRoot = new object();
			this._enqueuedLocationChangeNotification = (IntPtr.Zero, null, null, Point.Empty, -1);

			this._thumbnailViews = new Dictionary<IntPtr, IThumbnailView>();
			this._spotlightViews = new Dictionary<IntPtr, IThumbnailView>();
			this._asyncOperations = new AsyncOperationTracker();
			this._spotlightsVisible = true;
			this._spotlightLayoutDirty = false;

			//  DispatcherTimer setup
			this._thumbnailUpdateTimer = new DispatcherTimer();
			this._thumbnailUpdateTimer.Tick += ThumbnailUpdateTimerTick;
			this._thumbnailUpdateTimer.Interval = new TimeSpan(0, 0, 0, 0, configuration.ThumbnailRefreshPeriod);
			this._uiThreadId = Environment.CurrentManagedThreadId;

			this._hideThumbnailsDelay = this._configuration.HideThumbnailsDelay;

			RegisterCycleClientHotkey(this._configuration.CycleGroup1ForwardHotkeys?.Select(x => this._configuration.StringToKey(x)), true, this._configuration.CycleGroup1ClientsOrder);
			RegisterCycleClientHotkey(this._configuration.CycleGroup1BackwardHotkeys?.Select(x => this._configuration.StringToKey(x)), false, this._configuration.CycleGroup1ClientsOrder);

			RegisterCycleClientHotkey(this._configuration.CycleGroup2ForwardHotkeys?.Select(x => this._configuration.StringToKey(x)), true, this._configuration.CycleGroup2ClientsOrder);
			RegisterCycleClientHotkey(this._configuration.CycleGroup2BackwardHotkeys?.Select(x => this._configuration.StringToKey(x)), false, this._configuration.CycleGroup2ClientsOrder);

			RegisterCycleClientHotkey(this._configuration.CycleGroup3ForwardHotkeys?.Select(x => this._configuration.StringToKey(x)), true, this._configuration.CycleGroup3ClientsOrder);
			RegisterCycleClientHotkey(this._configuration.CycleGroup3BackwardHotkeys?.Select(x => this._configuration.StringToKey(x)), false, this._configuration.CycleGroup3ClientsOrder);

			RegisterCycleClientHotkey(this._configuration.CycleGroup4ForwardHotkeys?.Select(x => this._configuration.StringToKey(x)), true, this._configuration.CycleGroup4ClientsOrder);
			RegisterCycleClientHotkey(this._configuration.CycleGroup4BackwardHotkeys?.Select(x => this._configuration.StringToKey(x)), false, this._configuration.CycleGroup4ClientsOrder);

			RegisterCycleClientHotkey(this._configuration.CycleGroup5ForwardHotkeys?.Select(x => this._configuration.StringToKey(x)), true, this._configuration.CycleGroup5ClientsOrder);
			RegisterCycleClientHotkey(this._configuration.CycleGroup5BackwardHotkeys?.Select(x => this._configuration.StringToKey(x)), false, this._configuration.CycleGroup5ClientsOrder);

			RegisterMinimizeAllClientsHotkey(this._configuration.MinimizeAllClientsHotkeys?.Select(x => this._configuration.StringToKey(x)));

			RegisterSpotlightHotkey(this._configuration.ToggleSpotlightHotkeys?.Select(x => this._configuration.StringToKey(x)));
		}

		// Invariante de concorrência do dicionário de views (issue #59 / auditoria TM-01 P1).
		// Não existe lock: o dicionário é confinado à thread de UI. Um acesso a partir de
		// outra thread é defeito de programação e falha alto, em vez de corromper o estado.
		internal static void EnsureUiThread(int uiThreadId, string operation)
		{
			int currentThreadId = Environment.CurrentManagedThreadId;
			if (currentThreadId != uiThreadId)
			{
				throw new InvalidOperationException(
					$"{operation} deve ser executado na thread de UI (esperada {uiThreadId}, atual {currentThreadId}).");
			}
		}

		private void EnsureThumbnailViewsThread(string operation)
		{
			EnsureUiThread(this._uiThreadId, operation);
		}

		public IThumbnailView GetClientByTitle(string title)
		{
			this.EnsureThumbnailViewsThread(nameof(GetClientByTitle));
			return _thumbnailViews.FirstOrDefault(x => x.Value.Title == title).Value;
		}

		public IThumbnailView GetClientByPointer(IntPtr ptr)
		{
			this.EnsureThumbnailViewsThread(nameof(GetClientByPointer));
			return _thumbnailViews.FirstOrDefault(x => x.Key == ptr).Value;
		}

		public IThumbnailView GetActiveClient()
		{
			this.EnsureThumbnailViewsThread(nameof(GetActiveClient));
			return GetClientByPointer(this._activeClient.Handle);
		}

		public void SetActive(KeyValuePair<IntPtr, IThumbnailView> newClient)
		{
			this.EnsureThumbnailViewsThread(nameof(SetActive));
			this.GetActiveClient()?.ClearBorder();
#if LINUX
			this._windowManager.ActivateWindow(newClient.Key, newClient.Value.Title);
#else
			this._windowManager.ActivateWindow(newClient.Key, this._configuration.WindowsAnimationStyle);
#endif
			this.SwitchActiveClient(newClient.Key, newClient.Value.Title);

			newClient.Value.SetHighlight();
			newClient.Value.Refresh(true);
		}

		public void MinimizeAllClients()
		{
			this.EnsureThumbnailViewsThread(nameof(MinimizeAllClients));
			foreach (var x in _thumbnailViews.Reverse())
			{
				this._windowManager.MinimizeWindow(x.Value.Id, this._configuration.WindowsAnimationStyle, false);
			}
		}
		public void CycleNextClient(bool isForwards, Dictionary<string, int> cycleOrder)
		{
			this.EnsureThumbnailViewsThread(nameof(CycleNextClient));
			IOrderedEnumerable<KeyValuePair<string, int>> clientOrder;
			Dictionary<string, int> _cycleOrder = new Dictionary<string, int>(cycleOrder);

			if ( _cycleOrder.Count == 0 )
			{
				int order = 0;
				foreach( var x in _thumbnailViews )
				{
					_cycleOrder.Add(x.Value.Title, order++);
				}
			}

			if (isForwards)
			{
				clientOrder = _cycleOrder.OrderBy(x => x.Value);
			}
			else
			{
				clientOrder = _cycleOrder.OrderByDescending(x => x.Value);
			}

			bool setNextClient = false;
			IThumbnailView lastClient = null;

			foreach (var t in clientOrder)
			{
				if (t.Key == _activeClient.Title && t.Key != DEFAULT_CLIENT_TITLE)
				{
					setNextClient = true;
					lastClient = _thumbnailViews.FirstOrDefault(x => x.Value.Title == t.Key).Value;
					continue;
				}

				// cycle through login screens ?
				if (t.Key == _activeClient.Title && t.Key == DEFAULT_CLIENT_TITLE)
				{
					lastClient = _thumbnailViews.FirstOrDefault(x => x.Value.Title == t.Key && x.Value.Id == _activeClient.Handle).Value;
					if (lastClient == null)
					{
						setNextClient = true;
						continue;
					}
					var possibleClients = (isForwards ? _thumbnailViews.OrderBy(x => x.Value.Id.ToInt64()) : _thumbnailViews.OrderByDescending(x => x.Value.Id.ToInt64())).Where(x => x.Value.Title == t.Key && ! x.Value.IsExcludedFromCycleGroup);
					foreach (var pc in possibleClients)
					{
						if ( pc.Value.Id.Equals(lastClient.Id) )
						{
							setNextClient = true;
							continue;
						}

						if (!setNextClient)
						{
							continue;
						}

						// this is the next client (at login screen)
						SetActive(pc);
						return;
					}

					// rolled off top of list - back to first (if any there!)
					// set next client ?
					continue;
				}

				if (!setNextClient)
				{
					continue;
				}

				if (_thumbnailViews.Any(x => x.Value.Title == t.Key && !x.Value.IsExcludedFromCycleGroup))
				{
					var ptr = t.Key.Equals(DEFAULT_CLIENT_TITLE) ?
						(isForwards ? _thumbnailViews.OrderBy(x => x.Value.Id.ToInt64()) : _thumbnailViews.OrderByDescending(x => x.Value.Id.ToInt64())).FirstOrDefault(x => x.Value.Title == t.Key && ! x.Value.IsExcludedFromCycleGroup)
						: _thumbnailViews.First(x => x.Value.Title == t.Key && !x.Value.IsExcludedFromCycleGroup);
					SetActive(ptr);
					return;
				}
			}

			// we didn't get a next one. just get the first one from the start.
			foreach (var t in clientOrder)
			{
				if (_thumbnailViews.Any(x => x.Value.Title == t.Key && !x.Value.IsExcludedFromCycleGroup))
				{
					var ptr = t.Key.Equals(DEFAULT_CLIENT_TITLE) ?
						(isForwards ? _thumbnailViews.OrderBy(x => x.Value.Id.ToInt64()) : _thumbnailViews.OrderByDescending(x => x.Value.Id.ToInt64())).FirstOrDefault(x => x.Value.Title == t.Key && !x.Value.IsExcludedFromCycleGroup)
						: _thumbnailViews.First(x => x.Value.Title == t.Key && !x.Value.IsExcludedFromCycleGroup);
					SetActive(ptr);
					_activeClient = (ptr.Key, t.Key);
					return;
				}
			}

			// unable to select anything !
			return;
		}

		public void RegisterCycleClientHotkey(IEnumerable<Keys> keys, bool isForwards, Dictionary<string, int> cycleOrder)
		{
			if (keys == null)
			{
				return;
			}

			foreach (var hotkey in keys)
			{
				if (hotkey == Keys.None)
				{
					return;
				}

				var newHandler = new HotkeyHandler(default(IntPtr), hotkey);
				newHandler.Pressed += (object s, HandledEventArgs e) =>
				{
					this.CycleNextClient(isForwards, cycleOrder);
					e.Handled = true;
				};

				newHandler.Register();
				this._cycleClientHotkeyHandlers.Add(newHandler);
			}
		}
		public void RegisterMinimizeAllClientsHotkey(IEnumerable<Keys> keys)
		{
			if (keys == null)
			{
				return;
			}

			foreach (var hotkey in keys)
			{
				if (hotkey == Keys.None)
				{
					return;
				}

				var newHandler = new HotkeyHandler(default(IntPtr), hotkey);
				newHandler.Pressed += (object s, HandledEventArgs e) =>
				{
					this.MinimizeAllClients();
					e.Handled = true;
				};

				newHandler.Register();
				this._cycleClientHotkeyHandlers.Add(newHandler);
			}
		}

		private void RegisterSpotlightHotkey(IEnumerable<Keys> keys)
		{
			if (keys == null)
			{
				return;
			}

			foreach (var hotkey in keys)
			{
				if (hotkey == Keys.None)
				{
					continue;
				}

				var newHandler = new HotkeyHandler(default(IntPtr), hotkey);
				newHandler.Pressed += (object s, HandledEventArgs e) =>
				{
					this._spotlightsVisible = !this._spotlightsVisible;

					this.DisableViewEvents();
					this.RefreshSpotlightViews(false);
					this.EnableViewEvents();

					e.Handled = true;
				};

				newHandler.Register();
				this._spotlightHotkeyHandlers.Add(newHandler);
			}
		}

		private bool IsSpotlightClient(string title)
		{
			return !string.IsNullOrEmpty(title)
				&& title != ThumbnailManager.DEFAULT_CLIENT_TITLE
				&& this._configuration.SpotlightClients != null
				&& this._configuration.SpotlightClients.Contains(title);
		}

		private void SyncSpotlightViews()
		{
			foreach (KeyValuePair<IntPtr, IThumbnailView> entry in this._spotlightViews.ToList())
			{
				if (!this._thumbnailViews.TryGetValue(entry.Key, out IThumbnailView thumbnail) || !this.IsSpotlightClient(thumbnail.Title))
				{
					this.CloseSpotlightView(entry.Key);
				}
			}

			foreach (KeyValuePair<IntPtr, IThumbnailView> entry in this._thumbnailViews)
			{
				this.EnsureSpotlightView(entry.Key, entry.Value.Title);
			}
		}

		private void EnsureSpotlightView(IntPtr id, string title)
		{
			if (!this.IsSpotlightClient(title))
			{
				return;
			}

			if (this._spotlightViews.TryGetValue(id, out IThumbnailView existingView))
			{
				if (existingView.Title != title)
				{
					existingView.Title = title;
				}

				return;
			}

			Point location = this._configuration.GetSpotlightLocation(title, this.GetDefaultSpotlightLocation(title, id));
			Size size = this.GetSpotlightDisplaySize(title, id, location);

			IThumbnailView view = this._thumbnailViewFactory.Create(id, title, size);
			view.IsSpotlight = true;
			view.IsOverlayEnabled = this._configuration.ShowThumbnailOverlays;
			view.IsExcludedFromCycleGroup = false;
			view.SetFrames(false);
			view.SetSizeLimitations(new Size(160, 90), new Size(7680, 4320));
			view.SetTopMost(this._configuration.SpotlightAlwaysOnTop);
			view.SetOpacity(this._configuration.SpotlightOpacity);
			view.SourceRegion = this.GetClampedSpotlightRegion(title, id);
			view.ThumbnailLocation = location;
			view.SetHighlight(false, 0);

			view.ThumbnailResized = this.SpotlightViewResized;
			view.ThumbnailMoved = this.SpotlightViewMoved;
			view.ThumbnailActivated = this.ThumbnailActivated;
			view.ThumbnailRegionSelected = this.SpotlightViewRegionSelected;

			this._spotlightViews.Add(id, view);
		}

		private void CloseSpotlightView(IntPtr id)
		{
			if (!this._spotlightViews.TryGetValue(id, out IThumbnailView view))
			{
				return;
			}

			this._spotlightViews.Remove(id);

			view.ThumbnailResized = null;
			view.ThumbnailMoved = null;
			view.ThumbnailActivated = null;
			view.ThumbnailRegionSelected = null;
			view.Close();
		}

		private void RefreshSpotlightViews(bool forceRefresh)
		{
			foreach (IThumbnailView view in this._spotlightViews.Values)
			{
				if (!this._spotlightsVisible)
				{
					if (view.IsActive)
					{
						view.Hide();
					}

					continue;
				}

				view.ThumbnailSize = this.GetSpotlightDisplaySize(view.Title, view.Id, view.ThumbnailLocation);
				view.ThumbnailLocation = this._configuration.GetSpotlightLocation(view.Title, this.GetDefaultSpotlightLocation(view.Title, view.Id));
				view.SourceRegion = this.GetClampedSpotlightRegion(view.Title, view.Id);
				view.IsOverlayEnabled = this._configuration.ShowThumbnailOverlays;
				view.SetOpacity(this._configuration.SpotlightOpacity);
				view.SetTopMost(this._configuration.SpotlightAlwaysOnTop);
				view.SetHighlight(
					this._configuration.EnableActiveClientHighlight && (view.Id == this._activeClient.Handle),
					this._configuration.ActiveClientHighlightThickness);

				if (!view.IsActive)
				{
					view.Show();
				}
				else
				{
					view.Refresh(forceRefresh);
				}
			}

			this.SaveSpotlightConfigurationIfNeeded();
		}

		private Point GetDefaultSpotlightLocation(string title, IntPtr id)
		{
			Rectangle area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
			Size size = this.GetSpotlightDisplaySize(title, id, new Point(area.Left + 20, area.Top + 20));
			List<string> clients = this._configuration.SpotlightClients ?? new List<string>();
			int index = clients.IndexOf(title);
			if (index < 0)
			{
				index = 0;
			}

			const int margin = 20;
			const int gap = 10;
			int columns = Math.Max(1, (area.Width - margin) / Math.Max(1, size.Width + gap));
			int column = index % columns;
			int row = index / columns;

			return new Point(
				area.Left + margin + (column * (size.Width + gap)),
				area.Top + margin + (row * (size.Height + gap)));
		}

		private Size GetSpotlightDisplaySize(string title, IntPtr id, Point location)
		{
			Size configuredSize = this._configuration.GetSpotlightSize(title, Size.Empty);
			if (!configuredSize.IsEmpty)
			{
				return configuredSize;
			}

			Size size = this._configuration.SpotlightSize;
			Size clientSize = WindowGeometry.GetClientSize(id);
			if (clientSize.Width <= 0 || clientSize.Height <= 0)
			{
				return size;
			}

			Rectangle area = Screen.FromPoint(location).WorkingArea;
			Size minimumSize = new Size(160, 90);
			Size maximumSize = new Size(Math.Max(160, area.Width - 40), Math.Max(90, area.Height - 40));

			return SpotlightRegionCalculator.FitAspect(
				size,
				new Rectangle(Point.Empty, clientSize),
				minimumSize,
				maximumSize);
		}

		private Rectangle GetClampedSpotlightRegion(string title, IntPtr id)
		{
			Rectangle region = this._configuration.GetSpotlightRegion(title);
			if (region.IsEmpty)
			{
				return Rectangle.Empty;
			}

			return SpotlightRegionCalculator.ClampToClient(region, WindowGeometry.GetClientSize(id));
		}

		private void SpotlightViewRegionSelected(IntPtr id, Rectangle region) =>
			this.RunTracked(nameof(SpotlightViewRegionSelected), token => this.SpotlightViewRegionSelectedCore(id, region, token));

		private async Task SpotlightViewRegionSelectedCore(IntPtr id, Rectangle region, CancellationToken cancellationToken)
		{
			if (this._ignoreViewEvents)
			{
				return;
			}

			if (!this._spotlightViews.TryGetValue(id, out IThumbnailView view))
			{
				return;
			}

			Size clientSize = WindowGeometry.GetClientSize(id);
			region = SpotlightRegionCalculator.ClampToClient(region, clientSize);

			this._configuration.SetSpotlightRegion(view.Title, region);
			this.ResizeSpotlightToRegionAspect(view, region, clientSize);

			view.SourceRegion = region;

			this._configuration.SetSpotlightSize(view.Title, view.ThumbnailSize);
			this.SaveSpotlightConfiguration();

			await this._mediator.Publish(new SpotlightRegionUpdated(view.Title, region), cancellationToken);
		}

		private void ResizeSpotlightToRegionAspect(IThumbnailView view, Rectangle region, Size clientSize)
		{
			Size sourceSize = region.IsEmpty ? clientSize : region.Size;
			if (sourceSize.Width <= 0 || sourceSize.Height <= 0)
			{
				return;
			}

			Rectangle area = Screen.FromPoint(view.ThumbnailLocation).WorkingArea;
			Size minimumSize = new Size(160, 90);
			Size maximumSize = new Size(Math.Max(160, area.Width - 40), Math.Max(90, area.Height - 40));

			view.ThumbnailSize = SpotlightRegionCalculator.FitAspect(
				view.ThumbnailSize,
				new Rectangle(Point.Empty, sourceSize),
				minimumSize,
				maximumSize);
		}

		private void SpotlightViewResized(IntPtr id)
		{
			if (this._ignoreViewEvents)
			{
				return;
			}

			if (!this._spotlightViews.TryGetValue(id, out IThumbnailView view))
			{
				return;
			}

			this._configuration.SetSpotlightSize(view.Title, view.ThumbnailSize);
			this._spotlightLayoutDirty = true;

			view.Refresh(false);
		}

		private void SpotlightViewMoved(IntPtr id)
		{
			if (this._ignoreViewEvents)
			{
				return;
			}

			if (!this._spotlightViews.TryGetValue(id, out IThumbnailView view))
			{
				return;
			}

			this._configuration.SetSpotlightLocation(view.Title, view.ThumbnailLocation);
			this._spotlightLayoutDirty = true;

			view.Refresh(false);
		}

		private void SaveSpotlightConfigurationIfNeeded()
		{
			if (!this._spotlightLayoutDirty)
			{
				return;
			}

			this._spotlightLayoutDirty = false;
			this.SaveSpotlightConfiguration();
		}

		// Exceção que escapa de um async void derruba o processo sem log. Todo handler
		// async void do manager passa por aqui (issue #53 / auditoria TM-02).
		internal static async Task RunGuardedAsync(string operation, Func<Task> action)
		{
			try
			{
				await action();
			}
			catch (OperationCanceledException error)
			{
				// Cancelamento é parte do encerramento normal, não uma falha.
				System.Diagnostics.Trace.TraceInformation($"{operation}: operação cancelada ({error.Message}).");
			}
			catch (Exception error)
			{
				System.Diagnostics.Trace.TraceError($"{operation}: exceção assíncrona suprimida: {error}");
			}
		}

		// Registra o handler no rastreador e propaga o token de vida do manager para as
		// chamadas de mediator, de modo que o cancelamento seja cooperativo.
		private void RunTracked(string operation, Func<CancellationToken, Task> action)
		{
			CancellationToken cancellationToken = this._lifetimeCancellation?.Token ?? CancellationToken.None;

			this._asyncOperations.Run(() => RunGuardedAsync(operation, () => action(cancellationToken)));
		}

		private void SaveSpotlightConfiguration() =>
			this.RunTracked(nameof(SaveSpotlightConfiguration), token => this._mediator.Send(new SaveConfiguration(), token));

		public void Start()
		{
			this.EnsureThumbnailViewsThread(nameof(Start));
			// Um Start após um Stop anterior recomeça o ciclo de vida com um token novo.
			// O token antigo não é descartado aqui: operações ainda em voo podem tê-lo capturado.
			this._lifetimeCancellation = new CancellationTokenSource();

			this._thumbnailUpdateTimer.Start();

			this.RefreshThumbnails();
		}

		public async Task StopAsync(CancellationToken cancellationToken)
		{
			// Capturado antes do primeiro await: a varredura final precisa voltar para a
			// thread de UI, dona de _spotlightViews (issue #59). Sem contexto de
			// sincronização (testes ou host sem UI) não existe thread de UI para onde
			// despachar, então a varredura roda no thread atual.
			SynchronizationContext uiContext = SynchronizationContext.Current;

			this._thumbnailUpdateTimer.Stop();

			CancellationTokenSource lifetimeCancellation = this._lifetimeCancellation;
			this._lifetimeCancellation = null;
			lifetimeCancellation?.Cancel();

			this.CloseAllSpotlightViews();

			bool drained = false;
			try
			{
				// ConfigureAwait(false) mantém o shutdown fora da thread de UI: o fechamento
				// diferido do presenter aguarda esta tarefa sem travar o message loop.
				await this._asyncOperations.DrainAsync(cancellationToken).ConfigureAwait(false);
				drained = true;

				// Um tick que já estava em execução pode ter criado um spotlight depois da
				// primeira passada de fechamento. Fecha o que restar na thread de UI para não
				// deixar janela pendurada no encerramento (CloseSpotlightView não dispara
				// trabalho rastreado).
				await RunOnThreadAsync(uiContext, this.CloseAllSpotlightViews).ConfigureAwait(false);
			}
			finally
			{
				if (drained)
				{
					lifetimeCancellation?.Dispose();
				}
			}
		}

		private void CloseAllSpotlightViews()
		{
			foreach (IntPtr id in this._spotlightViews.Keys.ToList())
			{
				this.CloseSpotlightView(id);
			}
		}

		// Executa a ação no contexto informado. Se já estamos nele (ou se não há contexto),
		// executa inline. Usado para devolver à thread de UI o trabalho que precisa tocar
		// o estado confinado depois de um await com ConfigureAwait(false).
		private static async Task RunOnThreadAsync(SynchronizationContext context, Action action)
		{
			if (context == null || SynchronizationContext.Current == context)
			{
				action();
				return;
			}

			TaskCompletionSource completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
			context.Post(_ =>
			{
				try
				{
					action();
					completion.TrySetResult();
				}
				catch (Exception error)
				{
					completion.TrySetException(error);
				}
			}, null);

			await completion.Task.ConfigureAwait(false);
		}

		private void ThumbnailUpdateTimerTick(object sender, EventArgs e)
		{
			this.EnsureThumbnailViewsThread(nameof(ThumbnailUpdateTimerTick));
			this.UpdateThumbnailsList();
			this.RefreshThumbnails();
		}

		private void UpdateThumbnailsList() =>
			this.RunTracked(nameof(UpdateThumbnailsList), this.UpdateThumbnailsListCore);

		// O trabalho síncrono deste método (toda a manipulação de _thumbnailViews) acontece
		// antes do primeiro await. Mantenha essa ordem: a continuação do Publish não deve
		// voltar a tocar o dicionário.
		private async Task UpdateThumbnailsListCore(CancellationToken cancellationToken)
		{
			this.EnsureThumbnailViewsThread(nameof(UpdateThumbnailsListCore));
			this._processMonitor.GetUpdatedProcesses(out ICollection<IProcessInfo> addedProcesses, out ICollection<IProcessInfo> updatedProcesses, out ICollection<IProcessInfo> removedProcesses);

			List<string> viewsAdded = new List<string>();
			List<string> viewsRemoved = new List<string>();

			foreach (IProcessInfo process in addedProcesses)
			{
				// Usa o tamanho por cliente quando configurado (issue #138). Antes o
				// initialSize era calculado e descartado na chamada da fábrica.
				Size initialSize = this.GetConfiguredThumbnailSize(process.Title);

				IThumbnailView view = this._thumbnailViewFactory.Create(process.Handle, process.Title, initialSize);
				view.IsOverlayEnabled = this._configuration.ShowThumbnailOverlays;
				view.IsExcludedFromCycleGroup = false;
				view.SetFrames(this._configuration.ShowThumbnailFrames);
				// Max/Min size limitations should be set AFTER the frames are disabled
				// Otherwise thumbnail window will be unnecessary resized
				view.SetSizeLimitations(this._configuration.ThumbnailMinimumSize, this._configuration.ThumbnailMaximumSize);
				view.SetTopMost(this._configuration.ShowThumbnailsAlwaysOnTop);

				view.ThumbnailLocation = this.IsManageableThumbnail(view)
											? this._configuration.GetThumbnailLocation(view.Title, this._activeClient.Title, view.ThumbnailLocation)
											: this._configuration.LoginThumbnailLocation;

				this._thumbnailViews.Add(view.Id, view);

				view.ThumbnailResized = this.ThumbnailViewResized;
				view.ThumbnailMoved = this.ThumbnailViewMoved;
				view.ThumbnailFocused = this.ThumbnailViewFocused;
				view.ThumbnailLostFocus = this.ThumbnailViewLostFocus;
				view.ThumbnailActivated = this.ThumbnailActivated;
				view.ThumbnailDeactivated = this.ThumbnailDeactivated;

				view.ThumbnailToggleCycleGroup = this.ThumbnailToggleCycleGroup;

				view.RegisterHotkey(this._configuration.GetClientHotkey(view.Title));

				this.ApplyClientLayout(view);
				this.ApplyCaptionBar(view);
				this.EnsureSpotlightView(view.Id, view.Title);

				// TODO Add extension filter here later
				if (view.Title != ThumbnailManager.DEFAULT_CLIENT_TITLE)
				{
					viewsAdded.Add(view.Title);
				}
			}

			foreach (IProcessInfo process in updatedProcesses)
			{
				this._thumbnailViews.TryGetValue(process.Handle, out IThumbnailView view);

				if (view == null)
				{
					// Something went terribly wrong
					continue;
				}

				if (process.Title != view.Title) // update thumbnail title
				{
					viewsRemoved.Add(view.Title);
					this._configuration.RenameSpotlightClient(view.Title, process.Title);
					if (this._spotlightViews.TryGetValue(process.Handle, out IThumbnailView spotlightView))
					{
						spotlightView.Title = process.Title;
					}

					view.Title = process.Title;
					viewsAdded.Add(view.Title);

					view.RegisterHotkey(this._configuration.GetClientHotkey(process.Title));

					this.ApplyClientLayout(view);
					this.ApplyCaptionBar(view);
				}
			}

			foreach (IProcessInfo process in removedProcesses)
			{
				this.TryRemoveThumbnail(process, viewsRemoved);
			}

			if ((viewsAdded.Count > 0) || (viewsRemoved.Count > 0))
			{
				await this._mediator.Publish(new ThumbnailListUpdated(viewsAdded, viewsRemoved), cancellationToken);
			}
		}

		// Processes can disappear between a monitor snapshot and the next UI update.
		// An already-removed handle must not interrupt refresh of the remaining clients.
		internal bool TryRemoveThumbnail(IProcessInfo process, ICollection<string> viewsRemoved)
		{
			this.EnsureThumbnailViewsThread(nameof(TryRemoveThumbnail));
			if (!this._thumbnailViews.TryGetValue(process.Handle, out IThumbnailView view))
			{
				return false;
			}

			this._thumbnailViews.Remove(view.Id);
			this.CloseSpotlightView(process.Handle);
			if (view.Title != ThumbnailManager.DEFAULT_CLIENT_TITLE)
			{
				viewsRemoved.Add(view.Title);
			}

			view.UnregisterHotkey();

			view.ThumbnailResized = null;
			view.ThumbnailMoved = null;
			view.ThumbnailFocused = null;
			view.ThumbnailLostFocus = null;
			view.ThumbnailActivated = null;
			view.ThumbnailDeactivated = null;
			view.ThumbnailToggleCycleGroup = null;

			view.Close();
			return true;
		}

		private void RefreshThumbnails()
		{
			this.EnsureThumbnailViewsThread(nameof(RefreshThumbnails));
			// TODO Split this method
			IntPtr foregroundWindowHandle = this._windowManager.GetForegroundWindowHandle();

			// The foreground window can be NULL in certain circumstances, such as when a window is losing activation.
			// It is safer to just skip this refresh round than to do something while the system state is undefined
			if (foregroundWindowHandle == IntPtr.Zero)
			{
				return;
			}

			string foregroundWindowTitle = null;

			// Check if the foreground window handle is one of the known handles for client windows or their thumbnails
			bool isClientWindow = this.IsClientWindowActive(foregroundWindowHandle);
			bool isMainWindowActive = this.IsMainWindowActive(foregroundWindowHandle);

			if (foregroundWindowHandle == this._activeClient.Handle)
			{
				foregroundWindowTitle = this._activeClient.Title;
			}
			else if (this._thumbnailViews.TryGetValue(foregroundWindowHandle, out IThumbnailView foregroundView))
			{
				// This code will work only on Alt+Tab switch between clients
				foregroundWindowTitle = foregroundView.Title;
			}
			else if (!isClientWindow)
			{
				this._externalApplication = foregroundWindowHandle;
			}

			// No need to minimize EVE clients when switching out to non-EVE window (like thumbnail)
			if (!string.IsNullOrEmpty(foregroundWindowTitle))
			{
				this.SwitchActiveClient(foregroundWindowHandle, foregroundWindowTitle);
			}

			bool hideAllThumbnails = this._configuration.HideThumbnailsOnLostFocus && !(isClientWindow || isMainWindowActive);

			// Wait for some time before hiding all previews
			if (hideAllThumbnails)
			{
				this._hideThumbnailsDelay--;
				if (this._hideThumbnailsDelay > 0)
				{
					hideAllThumbnails = false; // Postpone the 'hide all' operation
				}
				else
				{
					this._hideThumbnailsDelay = 0; // Stop the counter
				}
			}
			else
			{
				this._hideThumbnailsDelay = this._configuration.HideThumbnailsDelay; // Reset the counter
			}

			this._refreshCycleCount++;

			bool forceRefresh;
			if (this._refreshCycleCount >= ThumbnailManager.FORCED_REFRESH_CYCLE_THRESHOLD)
			{
				this._refreshCycleCount = 0;
				forceRefresh = true;
			}
			else
			{
				forceRefresh = false;
			}

			this.DisableViewEvents();
			try
			{

				// Snap thumbnail
				// No need to update Thumbnails while one of them is highlighted
				if ((!this._isHoverEffectActive) && this.TryDequeueLocationChange(out var locationChange))
				{
					if ((locationChange.ActiveClient == this._activeClient.Title) && this._thumbnailViews.TryGetValue(locationChange.Handle, out var view))
					{
						this.SnapThumbnailView(view);

						this.RaiseThumbnailLocationUpdatedNotification(view.Title);
					}
					else
					{
						this.RaiseThumbnailLocationUpdatedNotification(locationChange.Title);
					}
				}

				// Hide, show, resize and move - update ZoomAnchor setting
				foreach (KeyValuePair<IntPtr, IThumbnailView> entry in this._thumbnailViews)
				{
					IThumbnailView view = entry.Value;
					// update ZoomAnchor regardless
					view.ClientZoomAnchor = this._configuration.GetZoomAnchor(view.Title, this._configuration.ThumbnailZoomAnchor);


					if (hideAllThumbnails || this._configuration.IsThumbnailDisabled(view.Title))
					{
						if (view.IsActive)
						{
							view.Hide();
						}
						continue;
					}

					if (this._configuration.HideActiveClientThumbnail && (view.Id == this._activeClient.Handle))
					{
						if (view.IsActive)
						{
							view.Hide();
						}
						continue;
					}

					if (this._configuration.HideLoginClientThumbnail && (view.Title == DEFAULT_CLIENT_TITLE ))
					{
						if (view.IsActive)
						{
							view.Hide();
						}
						continue;
					}

					// No need to update Thumbnails while one of them is highlighted
					if (!this._isHoverEffectActive)
					{
						// Do not even move thumbnails with default caption
						if (this.IsManageableThumbnail(view))
						{
							view.ThumbnailLocation = this._configuration.GetThumbnailLocation(view.Title, this._activeClient.Title, view.ThumbnailLocation);
							view.ThumbnailSize = this._configuration.GetThumbnailSize(view.Title, this._activeClient.Title, view.ThumbnailSize);
						}

						view.SetOpacity(this._configuration.ThumbnailOpacity);
						view.SetTopMost(this._configuration.ShowThumbnailsAlwaysOnTop);
					}

					view.IsOverlayEnabled = this._configuration.ShowThumbnailOverlays;

					view.SetHighlight(
						this._configuration.EnableActiveClientHighlight && (view.Id == this._activeClient.Handle),
						this._configuration.ActiveClientHighlightThickness);

					if (!view.IsActive)
					{
						view.Show();
					}
					else
					{
						view.Refresh(forceRefresh);
					}
				}

				this.RefreshSpotlightViews(forceRefresh);
			}
			finally
			{
				this.EnableViewEvents();
			}
		}

		public void UpdateSpotlights()
		{
			this.EnsureThumbnailViewsThread(nameof(UpdateSpotlights));
			this.DisableViewEvents();

			this.SyncSpotlightViews();
			this.RefreshSpotlightViews(false);
			this.SaveSpotlightConfigurationIfNeeded();

			this.EnableViewEvents();
		}

		public void ClearSpotlightRegion(string title)
		{
			if (string.IsNullOrEmpty(title))
			{
				return;
			}

			this._configuration.SetSpotlightRegion(title, Rectangle.Empty);

			IThumbnailView view = this._spotlightViews.Values.FirstOrDefault(x => x.Title == title);
			if (view != null)
			{
				Size clientSize = WindowGeometry.GetClientSize(view.Id);
				this.ResizeSpotlightToRegionAspect(view, Rectangle.Empty, clientSize);
				view.SourceRegion = Rectangle.Empty;
				this._configuration.SetSpotlightSize(title, view.ThumbnailSize);
			}

			this.SaveSpotlightConfiguration();
			this.PublishSpotlightRegionUpdated(title, Rectangle.Empty);
		}

		private void PublishSpotlightRegionUpdated(string title, Rectangle region) =>
			this.RunTracked(nameof(PublishSpotlightRegionUpdated), token => this._mediator.Publish(new SpotlightRegionUpdated(title, region), token));

		public void UpdateThumbnailsSize()
		{
			this.EnsureThumbnailViewsThread(nameof(UpdateThumbnailsSize));
			this.SetThumbnailsSize(this._configuration.ThumbnailSize);
		}
		public void UpdateCycleGroupIndicator()
		{
			this.EnsureThumbnailViewsThread(nameof(UpdateCycleGroupIndicator));
			this.SetCycleGroupIndicator(this._configuration.CycleGroupIndicatorAnchor);
		}

		private void SetCycleGroupIndicator(ZoomAnchor anchor)
		{
			this.DisableViewEvents();

			foreach (KeyValuePair<IntPtr, IThumbnailView> entry in this._thumbnailViews)
			{
				entry.Value.SetCycleGroupIndicator(entry.Value.IsExcludedFromCycleGroup, anchor);
				entry.Value.Refresh(false);
			}

			this.EnableViewEvents();
		}

		private void SetThumbnailsSize(Size size)
		{
			this.DisableViewEvents();

			foreach (KeyValuePair<IntPtr, IThumbnailView> entry in this._thumbnailViews)
			{
				entry.Value.ThumbnailSize = size;
				entry.Value.Refresh(false);
			}

			this.EnableViewEvents();
		}

		public void UpdateThumbnailFrames()
		{
			this.EnsureThumbnailViewsThread(nameof(UpdateThumbnailFrames));
			this.DisableViewEvents();

			foreach (KeyValuePair<IntPtr, IThumbnailView> entry in this._thumbnailViews)
			{
				entry.Value.SetFrames(this._configuration.ShowThumbnailFrames);
				ApplyCaptionBar(entry.Value);
				entry.Value.SetPreventPreviews();
			}

			this.EnableViewEvents();
		}

		private void EnableViewEvents()
		{
			this._ignoreViewEvents = false;
		}

		private void DisableViewEvents()
		{
			this._ignoreViewEvents = true;
		}

		private void SwitchActiveClient(IntPtr foregroundClientHandle, string foregroundClientTitle)
		{
			// Check if any actions are needed
			if (this._activeClient.Handle == foregroundClientHandle)
			{
				return;
			}

			// Minimize the currently active client if needed
			if (this._configuration.MinimizeInactiveClients && !this._configuration.IsPriorityClient(this._activeClient.Title))
			{
				this._windowManager.MinimizeWindow(this._activeClient.Handle, this._configuration.WindowsAnimationStyle, false);
#if LINUX
				this._windowManager.ActivateWindow(foregroundClientHandle, foregroundClientTitle);
#else
				this._windowManager.ActivateWindow(foregroundClientHandle, this._configuration.WindowsAnimationStyle);
#endif
			}

			this._activeClient = (foregroundClientHandle, foregroundClientTitle);
		}

		private void ThumbnailViewFocused(IntPtr id)
		{
			this.EnsureThumbnailViewsThread(nameof(ThumbnailViewFocused));
			if (this._isHoverEffectActive)
			{
				return;
			}

			this._isHoverEffectActive = true;

			IThumbnailView view = this._thumbnailViews[id];

			view.SetTopMost(true);
			view.SetOpacity(1.0);

			if (this._configuration.ThumbnailZoomEnabled && ! view.IsPreventPreviews() )
			{
				this.ThumbnailZoomIn(view);
			}
		}

		private void ThumbnailViewLostFocus(IntPtr id)
		{
			this.EnsureThumbnailViewsThread(nameof(ThumbnailViewLostFocus));
			if (!this._isHoverEffectActive)
			{
				return;
			}

			IThumbnailView view = this._thumbnailViews[id];

			if (this._configuration.ThumbnailZoomEnabled)
			{
				this.ThumbnailZoomOut(view);
			}

			view.SetOpacity(this._configuration.ThumbnailOpacity);

			this._isHoverEffectActive = false;
		}

		private void ThumbnailActivated(IntPtr id)
		{
			this.EnsureThumbnailViewsThread(nameof(ThumbnailActivated));
			if (!this._thumbnailViews.TryGetValue(id, out IThumbnailView view) || view.IsDisposed)
			{
				return;
			}

			var capturedId = view.Id;
			var capturedTitle = view.Title;
#if LINUX
			var capturedAnimation = capturedTitle;
#else
			var capturedAnimation = this._configuration.WindowsAnimationStyle;
#endif

			var uiScheduler = TaskScheduler.FromCurrentSynchronizationContext();
			Task.Run(() => this._windowManager.ActivateWindow(capturedId, capturedAnimation))
				.ContinueWith(activation =>
				{
					if (activation.IsFaulted)
					{
						var error = activation.Exception!.GetBaseException();
						System.Diagnostics.Trace.TraceError($"ThumbnailActivated: activation failed for {id}: {error.Message}");
						return;
					}

					if (activation.IsCanceled)
					{
						return;
					}

					try
					{
						// A continuação é despachada para o scheduler de UI capturado acima;
						// o guarda confirma a premissa antes de tocar o dicionário de views.
						this.EnsureThumbnailViewsThread(nameof(ThumbnailActivated));

						if (!this._thumbnailViews.TryGetValue(id, out IThumbnailView currentView)
							|| !ReferenceEquals(currentView, view) || currentView.IsDisposed)
						{
							return;
						}

						this.SwitchActiveClient(capturedId, capturedTitle);
						this.UpdateClientLayouts();
						this.RefreshThumbnails();
					}
					catch (Exception error)
					{
						System.Diagnostics.Trace.TraceError($"ThumbnailActivated: UI update failed for {id}: {error.Message}");
					}
				}, uiScheduler);
		}

		private void ThumbnailDeactivated(IntPtr id, bool switchOut)
		{
			this.EnsureThumbnailViewsThread(nameof(ThumbnailDeactivated));
			if (switchOut)
			{
#if LINUX
				this._windowManager.ActivateWindow(this._externalApplication, null);
#else
				this._windowManager.ActivateWindow(this._externalApplication, this._configuration.WindowsAnimationStyle);
#endif
			}
			else
			{
				if (!this._thumbnailViews.TryGetValue(id, out IThumbnailView view))
				{
					return;
				}

				this._windowManager.MinimizeWindow(view.Id, this._configuration.WindowsAnimationStyle, true);
				this.RefreshThumbnails();
			}
		}

		private void ThumbnailToggleCycleGroup(IntPtr id)
		{
			this.EnsureThumbnailViewsThread(nameof(ThumbnailToggleCycleGroup));
			var view = GetClientByPointer(id);
			if ( view != null )
			{
				view.IsExcludedFromCycleGroup = !view.IsExcludedFromCycleGroup;
				view.SetCycleGroupIndicator(view.IsExcludedFromCycleGroup, _configuration.CycleGroupIndicatorAnchor);

			}
			this.RefreshThumbnails();
		}


		private void ThumbnailViewResized(IntPtr id)
		{
			this.EnsureThumbnailViewsThread(nameof(ThumbnailViewResized));
			this.RunTracked(nameof(ThumbnailViewResized), token => this.ThumbnailViewResizedCore(id, token));
		}

		private async Task ThumbnailViewResizedCore(IntPtr id, CancellationToken cancellationToken)
		{
			if (this._ignoreViewEvents)
			{
				return;
			}

			if (!this._thumbnailViews.TryGetValue(id, out IThumbnailView view))
			{
				return;
			}

			// O resize da thumb de login não dita o tamanho das demais miniaturas
			// (issue #138). Ela apenas redesenha.
			if (view.Title == ThumbnailManager.DEFAULT_CLIENT_TITLE)
			{
				view.Refresh(false);
				return;
			}

			Size minimumSize = this._configuration.ThumbnailMinimumSize;
			Size maximumSize = this._configuration.ThumbnailMaximumSize;
			Size configuredSize = this.GetConfiguredThumbnailSize(view.Title);

			// A view só dispara ThumbnailResized no fim de um resize do usuário,
			// então aqui isUserResize é sempre true. Ainda assim a política confere
			// se o tamanho realmente mudou e aplica o clamp antes de propagar.
			if (!ThumbnailSizePolicy.ShouldPropagateResize(configuredSize, view.ThumbnailSize, minimumSize, maximumSize, isUserResize: true))
			{
				view.Refresh(false);
				return;
			}

			Size size = ThumbnailSizePolicy.ClampToLimits(view.ThumbnailSize, minimumSize, maximumSize);

			this.SetThumbnailsSize(size);

			view.Refresh(false);

			await this._mediator.Publish(new ThumbnailActiveSizeUpdated(size), cancellationToken);
		}

		private Size GetConfiguredThumbnailSize(string title)
		{
			Size? perClientSize = this._configuration.PerClientThumbnailSize.TryGetValue(title, out Size configuredSize)
				? configuredSize
				: (Size?)null;

			return ThumbnailSizePolicy.ResolveInitialThumbnailSize(this._configuration.ThumbnailSize, perClientSize, title);
		}

		private void ThumbnailViewMoved(IntPtr id)
		{
			this.EnsureThumbnailViewsThread(nameof(ThumbnailViewMoved));
			if (this._ignoreViewEvents)
			{
				return;
			}

			IThumbnailView view = this._thumbnailViews[id];
			view.Refresh(false);
			this.EnqueueLocationChange(view);
		}

		// Checks whether currently active window belongs to an EVE client or its thumbnail
		private bool IsClientWindowActive(IntPtr windowHandle)
		{
			if (windowHandle == IntPtr.Zero)
			{
				return false;
			}

			foreach (KeyValuePair<IntPtr, IThumbnailView> entry in this._thumbnailViews)
			{
				IThumbnailView view = entry.Value;

				if (view.IsKnownHandle(windowHandle))
				{
					return true;
				}
			}

			foreach (KeyValuePair<IntPtr, IThumbnailView> entry in this._spotlightViews)
			{
				if (entry.Value.IsKnownHandle(windowHandle))
				{
					return true;
				}
			}

			return false;
		}

		// Check whether the currently active window belongs to EVE-O-Preview itself
		private bool IsMainWindowActive(IntPtr windowHandle)
		{
			return (this._processMonitor.GetMainProcess().Handle == windowHandle);
		}

		private void ThumbnailZoomIn(IThumbnailView view)
		{
			this.DisableViewEvents();

			view.ZoomIn(ViewZoomAnchorConverter.Convert(view.ClientZoomAnchor), this._configuration.ThumbnailZoomFactor);
			view.Refresh(false);

			this.EnableViewEvents();
		}

		private void ThumbnailZoomOut(IThumbnailView view)
		{
			this.DisableViewEvents();

			view.ZoomOut();
			view.Refresh(false);

			this.EnableViewEvents();
		}

		private void SnapThumbnailView(IThumbnailView view)
		{
			// Check if this feature is enabled
			if (!this._configuration.EnableThumbnailSnap)
			{
				return;
			}

			// Only borderless thumbnails can be docked
			if (this._configuration.ShowThumbnailFrames)
			{
				return;
			}

			int width = this._configuration.ThumbnailSize.Width;
			int height = this._configuration.ThumbnailSize.Height;

			// TODO Extract method
			int baseX = view.ThumbnailLocation.X;
			int baseY = view.ThumbnailLocation.Y;

			Point[] viewPoints = { new Point(baseX, baseY), new Point(baseX + width, baseY), new Point(baseX, baseY + height), new Point(baseX + width, baseY + height) };

			// TODO Extract constants
			int thresholdX = Math.Max(20, width / 10);
			int thresholdY = Math.Max(20, height / 10);

			foreach (var entry in this._thumbnailViews)
			{
				IThumbnailView testView = entry.Value;

				if (view.Id == testView.Id)
				{
					continue;
				}

				int testX = testView.ThumbnailLocation.X;
				int testY = testView.ThumbnailLocation.Y;

				Point[] testPoints = { new Point(testX, testY), new Point(testX + width, testY), new Point(testX, testY + height), new Point(testX + width, testY + height) };

				var delta = ThumbnailManager.TestViewPoints(viewPoints, testPoints, thresholdX, thresholdY);

				if ((delta.X == 0) && (delta.Y == 0))
				{
					continue;
				}

				view.ThumbnailLocation = new Point(view.ThumbnailLocation.X + delta.X, view.ThumbnailLocation.Y + delta.Y);
				this._configuration.SetThumbnailLocation(view.Title, this._activeClient.Title, view.ThumbnailLocation);
				break;
			}
		}

		private static (int X, int Y) TestViewPoints(Point[] viewPoints, Point[] testPoints, int thresholdX, int thresholdY)
		{
			// Point combinations that we need to check
			// No need to check all 4x4 combinations
			(int ViewOffset, int TestOffset)[] testOffsets =
								{   ( 0, 3 ), ( 0, 2 ), ( 1, 2 ),
									( 0, 1 ), ( 0, 0 ), ( 1, 0 ),
									( 2, 1 ), ( 2, 0 ), ( 3, 0 )};

			foreach (var testOffset in testOffsets)
			{
				Point viewPoint = viewPoints[testOffset.ViewOffset];
				Point testPoint = testPoints[testOffset.TestOffset];

				int deltaX = testPoint.X - viewPoint.X;
				int deltaY = testPoint.Y - viewPoint.Y;

				if ((Math.Abs(deltaX) <= thresholdX) && (Math.Abs(deltaY) <= thresholdY))
				{
					return (deltaX, deltaY);
				}
			}

			return (0, 0);
		}
		private bool SetWindowStyle(IThumbnailView view, UInt32 styleToChange, bool remove)
		{
			IntPtr handle = view.Id;
			uint style = User32NativeMethods.GetWindowLong(handle, InteropConstants.GWL_STYLE);
			if (((style & styleToChange) == styleToChange) && remove == true)
			{
				style = style & ~styleToChange;
				User32NativeMethods.SetWindowLong(handle, InteropConstants.GWL_STYLE, style);
				return true;
			}
			if (((style & styleToChange) != styleToChange) && remove == false)
			{
				style = style | styleToChange;
				User32NativeMethods.SetWindowLong(handle, InteropConstants.GWL_STYLE, style);
				return true;
			}
			return false;
		}
		private void ApplyCaptionBar(IThumbnailView view)

		{
			if (view.Title == ThumbnailManager.DEFAULT_CLIENT_TITLE) return;
			IntPtr handle = view.Id;

			bool enable = this._configuration.HideCaptionOnClients;
			bool changed = false;
			changed = changed | SetWindowStyle(view, InteropConstants.WS_CAPTION, enable);
			changed = changed | SetWindowStyle(view, InteropConstants.WS_THICKFRAME, enable);
		}
		private void ApplyClientLayout(IThumbnailView view)
		{
			IntPtr clientHandle = view.Id;
			string clientTitle = view.Title;

			if (!this._configuration.EnableClientLayoutTracking)
			{
				return;
			}

			// No need to apply layout for not yet logged-in clients
			if (clientTitle == ThumbnailManager.DEFAULT_CLIENT_TITLE)
			{
				return;
			}

			ClientLayout clientLayout = this._configuration.GetClientLayout(clientTitle);

			if (clientLayout == null)
			{
				return;
			}

			if (clientLayout.IsMaximized)
			{
				this._windowManager.MaximizeWindow(clientHandle);
			}
			else
			{
				this._windowManager.MoveWindow(clientHandle, clientLayout.X, clientLayout.Y, clientLayout.Width, clientLayout.Height);
			}
		}

		private void UpdateClientLayouts()
		{
			if (!this._configuration.EnableClientLayoutTracking)
			{
				return;
			}

			foreach (KeyValuePair<IntPtr, IThumbnailView> entry in this._thumbnailViews)
			{
				IThumbnailView view = entry.Value;

				// No need to save layout for not yet logged-in clients
				if (view.Title == ThumbnailManager.DEFAULT_CLIENT_TITLE)
				{
					continue;
				}

				(int Left, int Top, int Right, int Bottom) position = this._windowManager.GetWindowPosition(view.Id);
				int width = Math.Abs(position.Right - position.Left);
				int height = Math.Abs(position.Bottom - position.Top);

				var isMaximized = this._windowManager.IsWindowMaximized(view.Id);

				if (!(isMaximized || this.IsValidWindowPosition(position.Left, position.Top, width, height)))
				{
					continue;
				}

				this._configuration.SetClientLayout(view.Title, new ClientLayout(position.Left, position.Top, width, height, isMaximized));
			}
		}

		private void EnqueueLocationChange(IThumbnailView view)
		{
			string activeClientTitle = this._activeClient.Title;
			// TODO ??
			this._configuration.SetThumbnailLocation(view.Title, activeClientTitle, view.ThumbnailLocation);

			lock (this._locationChangeNotificationSyncRoot)
			{
				if (this._enqueuedLocationChangeNotification.Handle == IntPtr.Zero)
				{
					this._enqueuedLocationChangeNotification = (view.Id, view.Title, activeClientTitle, view.ThumbnailLocation, ThumbnailManager.DEFAULT_LOCATION_CHANGE_NOTIFICATION_DELAY);
					return;
				}

				// Reset the delay and exit
				if ((this._enqueuedLocationChangeNotification.Handle == view.Id) &&
					(this._enqueuedLocationChangeNotification.ActiveClient == activeClientTitle))
				{
					this._enqueuedLocationChangeNotification.Delay = ThumbnailManager.DEFAULT_LOCATION_CHANGE_NOTIFICATION_DELAY;
					return;
				}

				this.RaiseThumbnailLocationUpdatedNotification(this._enqueuedLocationChangeNotification.Title);
				this._enqueuedLocationChangeNotification = (view.Id, view.Title, activeClientTitle, view.ThumbnailLocation, ThumbnailManager.DEFAULT_LOCATION_CHANGE_NOTIFICATION_DELAY);
			}
		}

		private bool TryDequeueLocationChange(out (IntPtr Handle, string Title, string ActiveClient, Point Location) change)
		{
			lock (this._locationChangeNotificationSyncRoot)
			{
				change = (IntPtr.Zero, null, null, Point.Empty);

				if (this._enqueuedLocationChangeNotification.Handle == IntPtr.Zero)
				{
					return false;
				}

				this._enqueuedLocationChangeNotification.Delay--;

				if (this._enqueuedLocationChangeNotification.Delay > 0)
				{
					return false;
				}

				change = (this._enqueuedLocationChangeNotification.Handle, this._enqueuedLocationChangeNotification.Title, this._enqueuedLocationChangeNotification.ActiveClient, this._enqueuedLocationChangeNotification.Location);
				this._enqueuedLocationChangeNotification = (IntPtr.Zero, null, null, Point.Empty, -1);

				return true;
			}
		}

		private void RaiseThumbnailLocationUpdatedNotification(string title) =>
			this.RunTracked(nameof(RaiseThumbnailLocationUpdatedNotification), token => this.RaiseThumbnailLocationUpdatedNotificationCore(title, token));

		private async Task RaiseThumbnailLocationUpdatedNotificationCore(string title, CancellationToken cancellationToken)
		{
			if (string.IsNullOrEmpty(title) || (title == ThumbnailManager.DEFAULT_CLIENT_TITLE))
			{
				return;
			}

			await this._mediator.Send(new SaveConfiguration(), cancellationToken);
		}

		// We shouldn't manage some thumbnails (like thumbnail of the EVE client sitting on the login screen)
		// TODO Move to a service (?)
		private bool IsManageableThumbnail(IThumbnailView view)
		{
			return view.Title != ThumbnailManager.DEFAULT_CLIENT_TITLE;
		}

		// Quick sanity check that the window is not minimized
		private bool IsValidWindowPosition(int left, int top, int width, int height)
		{
			return (left > ThumbnailManager.WINDOW_POSITION_THRESHOLD_LOW) && (left < ThumbnailManager.WINDOW_POSITION_THRESHOLD_HIGH)
					&& (top > ThumbnailManager.WINDOW_POSITION_THRESHOLD_LOW) && (top < ThumbnailManager.WINDOW_POSITION_THRESHOLD_HIGH)
					&& (width > ThumbnailManager.WINDOW_SIZE_THRESHOLD) && (height > ThumbnailManager.WINDOW_SIZE_THRESHOLD);
		}
	}
}
