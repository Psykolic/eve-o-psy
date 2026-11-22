using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Newtonsoft.Json;

namespace EveOPreview.Configuration.Implementation
{
	sealed class ThumbnailConfiguration : IThumbnailConfiguration
	{
		#region Private constants
		// Version 2 lowered the minimum thumbnail size from 192x108 to 32x18
		public const int CURRENT_CONFIG_VERSION = 2;

		private static readonly Size LEGACY_THUMBNAIL_MINIMUM_SIZE = new Size(192, 108);

		public static readonly Size DEFAULT_THUMBNAIL_MINIMUM_SIZE = new Size(32, 18);
		#endregion

		#region Private fields
		private bool _enablePerClientThumbnailLayouts;
		private bool _enableClientLayoutTracking;
		#endregion

		public ThumbnailConfiguration()
		{
			this.ConfigVersion = ThumbnailConfiguration.CURRENT_CONFIG_VERSION;

			this.CycleGroup1ForwardHotkeys = new List<string> { "F14", "Control+F14" };
			this.CycleGroup1BackwardHotkeys = new List<string> { "F13", "Control+F13" };
			this.CycleGroup1ClientsOrder = new Dictionary<string, int>
			{
				{ "EVE - Example DPS Toon 1", 1 },
				{ "EVE - Example DPS Toon 2", 2 },
				{ "EVE - Example DPS Toon 3", 3 }
			};

			this.CycleGroup2ForwardHotkeys = new List<string> { "F16", "Control+F16" };
			this.CycleGroup2BackwardHotkeys = new List<string> { "F15", "Control+F15" };
			this.CycleGroup2ClientsOrder = new Dictionary<string, int>
			{
				{ "EVE - Example Logi Toon 1", 1 },
				{ "EVE - Example Scout Toon 2", 2 },
				{ "EVE - Example Tackle Toon 3", 3 }
			};

			this.CycleGroup3ForwardHotkeys = new List<string> { "" };
			this.CycleGroup3BackwardHotkeys = new List<string> { "" };
			this.CycleGroup3ClientsOrder = new Dictionary<string, int>
			{
				{ "EVE - cycle group 3", 1 },
			};
			this.CycleGroup4ForwardHotkeys = new List<string> { "" };
			this.CycleGroup4BackwardHotkeys = new List<string> { "" };
			this.CycleGroup4ClientsOrder = new Dictionary<string, int>
			{
				{ "EVE - cycle group 4", 1 },
			};
			this.CycleGroup5ForwardHotkeys = new List<string> { "" };
			this.CycleGroup5BackwardHotkeys = new List<string> { "" };
			this.CycleGroup5ClientsOrder = new Dictionary<string, int>
			{
				{ "EVE - cycle group 5", 1 },
			};

			this.PerClientActiveClientHighlightColor = new Dictionary<string, Color>
			{
				{"EVE - Example Toon 1", Color.Red},
				{"EVE - Example Toon 2", Color.Green}
			};
			this.PerClientPreventPreviewColor = new Dictionary<string, Color>
			{
				{"EVE - Example Toon 1", Color.Red},
				{"EVE - Example Toon 2", Color.Green}
			};
			this.PerClientPreventPreviews = new Dictionary<string, bool>
			{
				{"EVE - Example Toon 1", false},
				{"EVE - Example Toon 2", true}
			};

			this.PerClientThumbnailSize = new Dictionary<string, Size>
			{
				{"EVE - Example Toon 1", new Size(200, 200)},
				{"EVE - Example Toon 2", new Size(200, 200)}
			};

			this.PerClientZoomAnchor = new Dictionary<string, ZoomAnchor>
			{
				{"EVE - Example Toon 1", ZoomAnchor.N },
				{"EVE - Example Toon 2", ZoomAnchor.S}
			};

			this.PerClientLayout = new Dictionary<string, Dictionary<string, Point>>();
			this.FlatLayout = new Dictionary<string, Point>();
			this.ClientLayout = new Dictionary<string, ClientLayout>();
			this.ClientHotkey = new Dictionary<string, string>();
			this.PerClientLabel = new Dictionary<string, string>();
			this.MinimizeAllClientsHotkeys = new List<string> { "Control+F22" };
			this.DisableThumbnail = new Dictionary<string, bool>();
			this.PriorityClients = new List<string>();

			this.ExecutablesToPreview = new List<string> { "exefile" };

			this.MinimizeToTray = false;
			this.ThumbnailRefreshPeriod = 500;
			// Fase 2.2 (ADR-006): refresh event-driven, com tick lento de reconciliacao
			// para cobrir eventos que nao chegam (UIPI/execucao elevada).
			this.EventDrivenRefresh = true;
			this.ReconciliationPeriod = 5000;
			this.ThumbnailResizeTimeoutPeriod = 500;

#if LINUX
			this.EnableWineCompatibilityMode = true;
#else
			this.EnableWineCompatibilityMode = false;
#endif

			this.ThumbnailOpacity = 0.5;

			this.SpotlightClients = new List<string>();
			this.ToggleSpotlightHotkeys = new List<string> { "Control+F12" };
			this.SpotlightSize = new Size(640, 360);
			this.SpotlightOpacity = 1.0;
			this.SpotlightAlwaysOnTop = true;
			this.PerClientSpotlightSize = new Dictionary<string, Size>();
			this.SpotlightLayout = new Dictionary<string, Point>();
			this.PerClientSpotlightRegion = new Dictionary<string, Rectangle>();

			this.EnableClientLayoutTracking = false;
			this.HideActiveClientThumbnail = false;
			this.HideLoginClientThumbnail = false;
			this.MinimizeInactiveClients = false;
			this.HideCaptionOnClients = false;
			this.WindowsAnimationStyle = AnimationStyle.NoAnimation;
			this.ShowThumbnailsAlwaysOnTop = true;
			this.EnablePerClientThumbnailLayouts = false;

			this.HideThumbnailsOnLostFocus = false;
			this.PreventPreviews = false;
			this.HideThumbnailsDelay = 2; // 2 thumbnails refresh cycles (1.0 sec)

			this.ThumbnailSize = new Size(384, 216);
			this.ThumbnailMinimumSize = ThumbnailConfiguration.DEFAULT_THUMBNAIL_MINIMUM_SIZE;
			this.ThumbnailMaximumSize = new Size(960, 540);
			this.ThumbnailKeepAspectRatio = true;

			this.EnableThumbnailSnap = true;

			this.ThumbnailZoomEnabled = false;
			this.ThumbnailZoomFactor = 2;
			this.ThumbnailZoomAnchor = ZoomAnchor.NW;
			this.OverlayLabelAnchor = ZoomAnchor.NW;
			this.CycleGroupIndicatorAnchor = ZoomAnchor.NW;

			this.ShowThumbnailOverlays = true;
			this.ShowThumbnailFrames = false;
			this.LockThumbnailLocation = false;

			this.ThumbnailSnapToGrid = true;
			this.ThumbnailSnapToGridSizeX = 100;
			this.ThumbnailSnapToGridSizeY = 50;

            this.EnableActiveClientHighlight = false;
			this.ActiveClientHighlightColor = Color.GreenYellow;
			this.PreventPreviewColor = Color.Purple;
			this.ActiveClientHighlightThickness = 3;

			this.OverlayLabelColor = Color.Orange;
			// Fonte neutra (issue #162): o executável define o padrão da plataforma e converte.
			this.OverlayLabelFont = string.Empty;

			this.IconName = "";

			this.LoginThumbnailLocation = new Point(5, 5);
		}


		[JsonProperty("ConfigVersion")]
		public int ConfigVersion { get; set; }

		[JsonIgnore]
		public Dictionary<string, bool> CycleGroupExclusions { get; set; }

		[JsonProperty("CycleGroup1ForwardHotkeys")]
		public List<string> CycleGroup1ForwardHotkeys { get; set; }

		[JsonProperty("CycleGroup1BackwardHotkeys")]
		public List<string> CycleGroup1BackwardHotkeys { get; set; }

		[JsonProperty("CycleGroup1ClientsOrder")]
		public Dictionary<string, int> CycleGroup1ClientsOrder { get; set; }

		[JsonProperty("CycleGroup2ForwardHotkeys")]
		public List<string> CycleGroup2ForwardHotkeys { get; set; }

		[JsonProperty("CycleGroup2BackwardHotkeys")]
		public List<string> CycleGroup2BackwardHotkeys { get; set; }

		[JsonProperty("CycleGroup2ClientsOrder")]
		public Dictionary<string, int> CycleGroup2ClientsOrder { get; set; }

		[JsonProperty("CycleGroup3ForwardHotkeys")]
		public List<string> CycleGroup3ForwardHotkeys { get; set; }

		[JsonProperty("CycleGroup3BackwardHotkeys")]
		public List<string> CycleGroup3BackwardHotkeys { get; set; }

		[JsonProperty("CycleGroup3ClientsOrder")]
		public Dictionary<string, int> CycleGroup3ClientsOrder { get; set; }

		[JsonProperty("CycleGroup4ForwardHotkeys")]
		public List<string> CycleGroup4ForwardHotkeys { get; set; }

		[JsonProperty("CycleGroup4BackwardHotkeys")]
		public List<string> CycleGroup4BackwardHotkeys { get; set; }

		[JsonProperty("CycleGroup4ClientsOrder")]
		public Dictionary<string, int> CycleGroup4ClientsOrder { get; set; }

		[JsonProperty("CycleGroup5ForwardHotkeys")]
		public List<string> CycleGroup5ForwardHotkeys { get; set; }

		[JsonProperty("CycleGroup5BackwardHotkeys")]
		public List<string> CycleGroup5BackwardHotkeys { get; set; }

		[JsonProperty("CycleGroup5ClientsOrder")]
		public Dictionary<string, int> CycleGroup5ClientsOrder { get; set; }

		[JsonProperty("PerClientPreventPreviewColor")]
		public Dictionary<string, Color> PerClientPreventPreviewColor { get; set; }

		[JsonProperty("PerClientActiveClientHighlightColor")]
		public Dictionary<string, Color> PerClientActiveClientHighlightColor { get; set; }

		[JsonProperty("PerClientPreventPreviews")]
		public Dictionary<string, bool> PerClientPreventPreviews { get; set; }

		[JsonProperty("PerClientThumbnailSize")]
		public Dictionary<string, Size> PerClientThumbnailSize { get; set; }

		[JsonProperty("PerClientLabel")]
		public Dictionary<string, string> PerClientLabel { get; set; }

		[JsonProperty("PerClientZoomAnchor")]
		public Dictionary<string, ZoomAnchor> PerClientZoomAnchor{ get; set; }
		public bool MinimizeToTray { get; set; }
		public int ThumbnailRefreshPeriod { get; set; }

		/// <summary>
		/// Usa hooks de evento (janelas/processos) em vez de polling para atualizar as
		/// thumbnails. Quando desligado, o comportamento antigo volta: o timer de
		/// <see cref="ThumbnailRefreshPeriod"/> faz todo o trabalho (rollback da Fase 2.2).
		/// </summary>
		[JsonProperty("EventDrivenRefresh")]
		public bool EventDrivenRefresh { get; set; }

		/// <summary>
		/// Intervalo do tick de reconciliacao quando <see cref="EventDrivenRefresh"/> esta ligado.
		/// Nao e divida tecnica: hooks nao chegam quando o cliente roda elevado e o app nao (UIPI).
		/// </summary>
		[JsonProperty("ReconciliationPeriod")]
		public int ReconciliationPeriod { get; set; }

		public int ThumbnailResizeTimeoutPeriod { get; set; }

		[JsonProperty("WineCompatibilityMode")]
		public bool EnableWineCompatibilityMode { get; set; }

		[JsonProperty("ThumbnailsOpacity")]
		public double ThumbnailOpacity { get; set; }

		[JsonProperty("SpotlightClients")]
		public List<string> SpotlightClients { get; set; }
		[JsonProperty("ToggleSpotlightHotkeys")]
		public List<string> ToggleSpotlightHotkeys { get; set; }
		public Size SpotlightSize { get; set; }
		public double SpotlightOpacity { get; set; }
		public bool SpotlightAlwaysOnTop { get; set; }

		public bool EnableClientLayoutTracking
		{
			get => this._enableClientLayoutTracking;
			set
			{
				if (!value)
				{
					this.ClientLayout.Clear();
				}

				this._enableClientLayoutTracking = value;
			}
		}

		public bool HideActiveClientThumbnail { get; set; }
		public bool HideLoginClientThumbnail { get; set; }
		public bool MinimizeInactiveClients { get; set; }
		public bool HideCaptionOnClients { get; set; }
		public AnimationStyle WindowsAnimationStyle { get; set; }
		public bool ShowThumbnailsAlwaysOnTop { get; set; }

		public bool EnablePerClientThumbnailLayouts
		{
			get => this._enablePerClientThumbnailLayouts;
			set
			{
				if (!value)
				{
					this.PerClientLayout.Clear();
				}

				this._enablePerClientThumbnailLayouts = value;
			}
		}

		public bool PreventPreviews { get; set; }
		public bool HideThumbnailsOnLostFocus { get; set; }
		public int HideThumbnailsDelay { get; set; }

		public Size ThumbnailSize { get; set; }
		public Size ThumbnailMaximumSize { get; set; }
		public Size ThumbnailMinimumSize { get; set; }
		public bool ThumbnailKeepAspectRatio { get; set; }

		public bool EnableThumbnailSnap { get; set; }

		[JsonProperty("EnableThumbnailZoom")]
		public bool ThumbnailZoomEnabled { get; set; }
		public int ThumbnailZoomFactor { get; set; }
		public ZoomAnchor ThumbnailZoomAnchor { get; set; }
		public ZoomAnchor OverlayLabelAnchor { get; set; }
		public ZoomAnchor CycleGroupIndicatorAnchor { get; set; }

		public bool ShowThumbnailOverlays { get; set; }
		public bool ShowThumbnailFrames { get; set; }
		public bool LockThumbnailLocation { get; set; }
		public bool ThumbnailSnapToGrid { get; set; }
		public int ThumbnailSnapToGridSizeX {  get; set; }
		public int ThumbnailSnapToGridSizeY { get; set; }

		public bool EnableActiveClientHighlight { get; set; }

		public Color ActiveClientHighlightColor { get; set; }
		public Color PreventPreviewColor { get; set; }
		public Color OverlayLabelColor { get; set; }

		[JsonProperty]
		[JsonConverter(typeof(EveOPreview.Configuration.Serialization.OverlayLabelFontJsonConverter))]
		public string OverlayLabelFont { get; set; }
		public string IconName { get; set; }

		public int ActiveClientHighlightThickness { get; set; }

		[JsonProperty("LoginThumbnailLocation")]
		public Point LoginThumbnailLocation { get; set; }

		[JsonProperty]
		private Dictionary<string, Dictionary<string, Point>> PerClientLayout { get; set; }
		[JsonProperty]
		private Dictionary<string, Size> PerClientSpotlightSize { get; set; }
		[JsonProperty]
		private Dictionary<string, Point> SpotlightLayout { get; set; }
		[JsonProperty]
		private Dictionary<string, Rectangle> PerClientSpotlightRegion { get; set; }
		[JsonProperty]
		private Dictionary<string, Point> FlatLayout { get; set; }
		[JsonProperty]
		private Dictionary<string, ClientLayout> ClientLayout { get; set; }
		[JsonProperty]
		private Dictionary<string, string> ClientHotkey { get; set; }
		[JsonProperty]
		public List<string> MinimizeAllClientsHotkeys { get; set; }
		[JsonProperty]
		private Dictionary<string, bool> DisableThumbnail { get; set; }
		[JsonProperty]
		private List<string> PriorityClients { get; set; }
		[JsonProperty]
		private List<string> ExecutablesToPreview { get; set; }

		public Point GetThumbnailLocation(string currentClient, string activeClient, Point defaultLocation)
		{
			Point location;

			// What this code does:
			// If Per-Client layouts are enabled
			//    and client name is known
			//    and there is a separate thumbnails layout for this client
			//    and this layout contains an entry for the current client
			// then return that entry
			// otherwise try to get client layout from the flat all-clients layout
			// If there is no layout too then use the default one
			if (this.EnablePerClientThumbnailLayouts && !string.IsNullOrEmpty(activeClient))
			{
				Dictionary<string, Point> layoutSource;
				if (this.PerClientLayout.TryGetValue(activeClient, out layoutSource) && layoutSource.TryGetValue(currentClient, out location))
				{
					return location;
				}
			}

			return this.FlatLayout.TryGetValue(currentClient, out location) ? location : defaultLocation;
		}

		public Size GetThumbnailSize(string currentClient, string activeClient, Size defaultSize)
		{
			Size sizeOfThumbnail;
			return this.PerClientThumbnailSize.TryGetValue(currentClient, out sizeOfThumbnail) ? sizeOfThumbnail : defaultSize;
		}

		public Size GetSpotlightSize(string currentClient, Size defaultSize)
		{
			return this.PerClientSpotlightSize.TryGetValue(currentClient, out Size size) ? size : defaultSize;
		}

		public void SetSpotlightSize(string currentClient, Size size)
		{
			this.PerClientSpotlightSize[currentClient] = size;
		}

		public Point GetSpotlightLocation(string currentClient, Point defaultLocation)
		{
			return this.SpotlightLayout.TryGetValue(currentClient, out Point location) ? location : defaultLocation;
		}

		public void SetSpotlightLocation(string currentClient, Point location)
		{
			this.SpotlightLayout[currentClient] = location;
		}

		public Rectangle GetSpotlightRegion(string currentClient)
		{
			return this.PerClientSpotlightRegion.TryGetValue(currentClient, out Rectangle region) ? region : Rectangle.Empty;
		}

		public void SetSpotlightRegion(string currentClient, Rectangle region)
		{
			if (region.Width <= 0 || region.Height <= 0)
			{
				this.PerClientSpotlightRegion.Remove(currentClient);
				return;
			}

			this.PerClientSpotlightRegion[currentClient] = region;
		}

		public void RenameSpotlightClient(string oldTitle, string newTitle)
		{
			if (string.IsNullOrEmpty(oldTitle) || string.IsNullOrEmpty(newTitle) || oldTitle == newTitle)
			{
				return;
			}

			int index = this.SpotlightClients.IndexOf(oldTitle);
			if (index >= 0)
			{
				this.SpotlightClients[index] = newTitle;
			}

			if (this.PerClientSpotlightSize.TryGetValue(oldTitle, out Size size))
			{
				this.PerClientSpotlightSize.Remove(oldTitle);
				this.PerClientSpotlightSize[newTitle] = size;
			}

			if (this.SpotlightLayout.TryGetValue(oldTitle, out Point location))
			{
				this.SpotlightLayout.Remove(oldTitle);
				this.SpotlightLayout[newTitle] = location;
			}

			if (this.PerClientSpotlightRegion.TryGetValue(oldTitle, out Rectangle region))
			{
				this.PerClientSpotlightRegion.Remove(oldTitle);
				this.PerClientSpotlightRegion[newTitle] = region;
			}
		}
		public ZoomAnchor GetZoomAnchor(string currentClient, ZoomAnchor defaultZoomAnchor)
		{
			ZoomAnchor zoomAnchor;
			return this.PerClientZoomAnchor.TryGetValue(currentClient, out zoomAnchor) ? zoomAnchor : defaultZoomAnchor;
		}

		public void SetThumbnailLocation(string currentClient, string activeClient, Point location)
		{
			Dictionary<string, Point> layoutSource;

			if (this.EnablePerClientThumbnailLayouts)
			{
				if (string.IsNullOrEmpty(activeClient))
				{
					return;
				}

				if (!this.PerClientLayout.TryGetValue(activeClient, out layoutSource))
				{
					layoutSource = new Dictionary<string, Point>();
					this.PerClientLayout[activeClient] = layoutSource;
				}
			}
			else
			{
				layoutSource = this.FlatLayout;
			}

			layoutSource[currentClient] = location;
		}

		public ClientLayout GetClientLayout(string currentClient)
		{
			ClientLayout layout;
			this.ClientLayout.TryGetValue(currentClient, out layout);

			return layout;
		}

		public IReadOnlyCollection<string> GetKnownClients()
		{
			var nomes = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

			ThumbnailConfiguration.AddNames(nomes, this.FlatLayout?.Keys);
			ThumbnailConfiguration.AddNames(nomes, this.ClientLayout?.Keys);
			ThumbnailConfiguration.AddNames(nomes, this.CycleGroup1ClientsOrder?.Keys);
			ThumbnailConfiguration.AddNames(nomes, this.CycleGroup2ClientsOrder?.Keys);
			ThumbnailConfiguration.AddNames(nomes, this.CycleGroup3ClientsOrder?.Keys);
			ThumbnailConfiguration.AddNames(nomes, this.CycleGroup4ClientsOrder?.Keys);
			ThumbnailConfiguration.AddNames(nomes, this.CycleGroup5ClientsOrder?.Keys);

			return nomes.ToList();
		}

		private static void AddNames(ISet<string> nomes, IEnumerable<string> chaves)
		{
			if (chaves == null)
			{
				return;
			}

			foreach (string chave in chaves)
			{
				if (!string.IsNullOrWhiteSpace(chave))
				{
					nomes.Add(chave.Trim());
				}
			}
		}

		public void SetClientLayout(string currentClient, ClientLayout layout)
		{
			this.ClientLayout[currentClient] = layout;
		}

		public HotkeyBinding GetClientHotkey(string currentClient)
		{
			string hotkey;
			if (this.ClientHotkey.TryGetValue(currentClient, out hotkey))
			{
				return new HotkeyBinding(hotkey);
			}

			return HotkeyBinding.None;
		}

		public void SetClientHotkey(string currentClient, HotkeyBinding hotkey)
		{
			this.ClientHotkey[currentClient] = hotkey.Value;
		}

		/// <summary>Label customizada do cliente; vazio significa "usar o nome".</summary>
		public string GetClientLabel(string currentClient)
		{
			if (string.IsNullOrEmpty(currentClient) || this.PerClientLabel == null)
			{
				return string.Empty;
			}

			return this.PerClientLabel.TryGetValue(currentClient, out string label) ? label : string.Empty;
		}

		/// <summary>Grava a label ja normalizada; texto em branco remove a entrada.</summary>
		public void SetClientLabel(string currentClient, string label)
		{
			if (string.IsNullOrEmpty(currentClient))
			{
				return;
			}

			this.PerClientLabel = this.PerClientLabel ?? new Dictionary<string, string>();

			string sanitized = OverlayLabel.Sanitize(label);
			if (sanitized.Length == 0)
			{
				this.PerClientLabel.Remove(currentClient);
				return;
			}

			this.PerClientLabel[currentClient] = sanitized;
		}

		/// <summary>Move a label quando o cliente troca de nome (troca de personagem).</summary>
		public void RenameClientLabel(string oldTitle, string newTitle)
		{
			if (string.IsNullOrEmpty(oldTitle) || string.IsNullOrEmpty(newTitle) || oldTitle == newTitle)
			{
				return;
			}

			if (this.PerClientLabel == null || !this.PerClientLabel.TryGetValue(oldTitle, out string label))
			{
				return;
			}

			this.PerClientLabel.Remove(oldTitle);
			this.PerClientLabel[newTitle] = label;
		}

		public bool IsPriorityClient(string currentClient)
		{
			return this.PriorityClients.Contains(currentClient);
		}
		public bool IsExecutableToPreview(string processName)
		{
			return this.ExecutablesToPreview.Any(s => s.Equals(processName, StringComparison.OrdinalIgnoreCase));
		}

		public bool IsThumbnailDisabled(string currentClient)
		{
			return this.DisableThumbnail.TryGetValue(currentClient, out bool isDisabled) && isDisabled;
		}

		public void ToggleThumbnail(string currentClient, bool isDisabled)
		{
			this.DisableThumbnail[currentClient] = isDisabled;
		}

		/// <summary>
		/// Applies restrictions to different parameters of the config
		/// </summary>
		public void ApplyRestrictions()
		{
			this.MigrateLegacySettings();

#if LINUX
			this.ThumbnailRefreshPeriod = ThumbnailConfiguration.ApplyRestrictions(this.ThumbnailRefreshPeriod, ConfigurationLimits.RefreshPeriodMin, ConfigurationLimits.RefreshPeriodMax);
#else
			this.ThumbnailRefreshPeriod = ThumbnailConfiguration.ApplyRestrictions(this.ThumbnailRefreshPeriod, ConfigurationLimits.RefreshPeriodMin, ConfigurationLimits.RefreshPeriodMax);
#endif
			// Nao mexe em EventDrivenRefresh: um valor booleano nao precisa de clamp.
			this.ReconciliationPeriod = ThumbnailConfiguration.ApplyRestrictions(this.ReconciliationPeriod, ConfigurationLimits.ReconciliationPeriodMin, ConfigurationLimits.ReconciliationPeriodMax);
			this.ThumbnailResizeTimeoutPeriod = ThumbnailConfiguration.ApplyRestrictions(this.ThumbnailResizeTimeoutPeriod, ConfigurationLimits.ResizeTimeoutMin, ConfigurationLimits.ResizeTimeoutMax);
			this.ThumbnailMinimumSize = new Size(
				ThumbnailConfiguration.ApplyRestrictions(this.ThumbnailMinimumSize.Width, ConfigurationLimits.MinimumWidth, ConfigurationLimits.MaximumWidth),
				ThumbnailConfiguration.ApplyRestrictions(this.ThumbnailMinimumSize.Height, ConfigurationLimits.MinimumHeight, ConfigurationLimits.MaximumHeight));
			this.ThumbnailSize = new Size(ThumbnailConfiguration.ApplyRestrictions(this.ThumbnailSize.Width, this.ThumbnailMinimumSize.Width, this.ThumbnailMaximumSize.Width),
				ThumbnailConfiguration.ApplyRestrictions(this.ThumbnailSize.Height, this.ThumbnailMinimumSize.Height, this.ThumbnailMaximumSize.Height));
			this.ThumbnailOpacity = ThumbnailConfiguration.ApplyRestrictions((int)(this.ThumbnailOpacity * 100.00), ConfigurationLimits.OpacityMinPercent, ConfigurationLimits.OpacityMaxPercent) / 100.00;
			this.ThumbnailZoomFactor = ThumbnailConfiguration.ApplyRestrictions(this.ThumbnailZoomFactor, ConfigurationLimits.ZoomFactorMin, ConfigurationLimits.ZoomFactorMax);
			this.ActiveClientHighlightThickness = ThumbnailConfiguration.ApplyRestrictions(this.ActiveClientHighlightThickness, ConfigurationLimits.HighlightThicknessMin, ConfigurationLimits.HighlightThicknessMax);

			this.SpotlightClients = this.SpotlightClients ?? new List<string>();
			this.ToggleSpotlightHotkeys = this.ToggleSpotlightHotkeys ?? new List<string>();
			this.PerClientSpotlightSize = this.PerClientSpotlightSize ?? new Dictionary<string, Size>();
			this.SpotlightLayout = this.SpotlightLayout ?? new Dictionary<string, Point>();
			this.PerClientSpotlightRegion = this.PerClientSpotlightRegion ?? new Dictionary<string, Rectangle>();

			// Label: arquivo editado a mao nao fura o limite de 8 nem deixa chave vazia.
			this.PerClientLabel = this.PerClientLabel ?? new Dictionary<string, string>();
			foreach (string client in this.PerClientLabel.Keys.ToList())
			{
				string label = OverlayLabel.Sanitize(this.PerClientLabel[client]);
				if (label.Length == 0)
				{
					this.PerClientLabel.Remove(client);
					continue;
				}

				this.PerClientLabel[client] = label;
			}

			foreach (string client in this.PerClientSpotlightRegion.Keys.ToList())
			{
				Rectangle region = this.PerClientSpotlightRegion[client];
				if (region.Width < 4 || region.Height < 4)
				{
					this.PerClientSpotlightRegion.Remove(client);
					continue;
				}

				this.PerClientSpotlightRegion[client] = new Rectangle(
					Math.Max(0, region.Left),
					Math.Max(0, region.Top),
					region.Width,
					region.Height);
			}
			this.SpotlightSize = new Size(
				ThumbnailConfiguration.ApplyRestrictions(this.SpotlightSize.Width, ConfigurationLimits.SpotlightMinWidth, ConfigurationLimits.MaximumWidth),
				ThumbnailConfiguration.ApplyRestrictions(this.SpotlightSize.Height, ConfigurationLimits.SpotlightMinHeight, ConfigurationLimits.MaximumHeight));
			this.SpotlightOpacity = ThumbnailConfiguration.ApplyRestrictions((int)(this.SpotlightOpacity * 100.00), ConfigurationLimits.OpacityMinPercent, ConfigurationLimits.OpacityMaxPercent) / 100.00;
		}

		/// <summary>
		/// Upgrades settings written by previous versions of the application.
		/// </summary>
		private void MigrateLegacySettings()
		{
			if (this.ConfigVersion >= ThumbnailConfiguration.CURRENT_CONFIG_VERSION)
			{
				return;
			}

			// Version 1 stored the old 192x108 floor, which prevented users from
			// shrinking the thumbnails. Keep a custom floor if the user changed it,
			// but replace the old default with the new one.
			if (this.ThumbnailMinimumSize == ThumbnailConfiguration.LEGACY_THUMBNAIL_MINIMUM_SIZE)
			{
				this.ThumbnailMinimumSize = ThumbnailConfiguration.DEFAULT_THUMBNAIL_MINIMUM_SIZE;
			}

			this.ConfigVersion = ThumbnailConfiguration.CURRENT_CONFIG_VERSION;
		}

		private static int ApplyRestrictions(int value, int minimum, int maximum)
		{
			if (value <= minimum)
			{
				return minimum;
			}

			if (value >= maximum)
			{
				return maximum;
			}

			return value;
		}
	}
}