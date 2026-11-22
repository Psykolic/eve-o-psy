using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using EveOPreview.Configuration;
using EveOPreview.Services;
using EveOPreview.Services.Interop;
using EveOPreview.UI.Hotkeys;

namespace EveOPreview.View
{
	public abstract partial class ThumbnailView : Form, IThumbnailView
	{
		#region Private constants
		private const double OPACITY_THRESHOLD = 0.9;
		private const double OPACITY_EPSILON = 0.1;
		private const int RESIZE_BORDER_SIZE = 8;
		#endregion

		#region Private fields
		private readonly ThumbnailOverlay _overlay;

		// Part of the logic (namely current size / position management)
		// was moved to the view due to the performance reasons
		private bool _isOverlayVisible;
		private bool _isTopMost;
		private bool _isHighlightEnabled;
		private bool _isHighlightRequested;
		private int _highlightWidth;

		private bool _isLocationChanged;
		private bool _isSizeChanged;

		private bool _isCustomMouseModeActive;

		private bool _isRegionSelectionActive;
		private Point _regionSelectionStart;
		private Rectangle _regionSelectionRectangle;
		private Rectangle _sourceRegion;

		private bool _isSpotlight;
		private bool _isResizing;
		private SpotlightResizeEdge _resizeEdge;
		private Point _resizeStartScreen;
		private Rectangle _resizeStartBounds;

		private double _opacity;

		// Tamanho no início do resize do usuário: só o tamanho final dele pode ser
		// propagado para as demais miniaturas e para a configuração (issue #138).
		private Size _userResizeStartSize;
		private Size _customResizeStartSize;

		private DateTime _suppressResizeEventsTimestamp;
		private Size _baseZoomSize;
		private Point _baseZoomLocation;
		private Point _baseMousePosition;
		private Size _baseZoomMaximumSize;

		private HotkeyHandler _hotkeyHandler;
		// Fonte do overlay é neutra (string) na config; aqui convertemos e mantemos
		// um único Font por view (issue #162), evitando alocação de handle GDI a cada uso.
		private string _overlayLabelFontValue;
		private Font _overlayLabelFont;

		private IThumbnailConfiguration _config;
		private Lazy<Color> _myBorderColor;
		private Lazy<Color> _preventPreviewColor;
		private Lazy<bool> _preventPreviews;
		private IThumbnailManager _thumbnailManager;

		// Lifecycle tracking: distinct from IsActive (visibility).
		// IsDisposed = true means the view has been closed and should not be used.
		// IsActive = false means the view is hidden but still valid (e.g., minimized, not on top).
		private bool _isDisposed;
		#endregion

		protected ThumbnailView(IWindowManager windowManager, IThumbnailConfiguration config, IThumbnailManager thumbnailManager)
		{
			this._config = config;
			this.SuppressResizeEvent();

			this.WindowManager = windowManager;

			this.IsActive = false;

			this.IsOverlayEnabled = false;
			this._isOverlayVisible = false;
			this.IsExcludedFromCycleGroup = false;

			this._isTopMost = false;
			this._isHighlightEnabled = false;
			this._isHighlightRequested = false;

			this._isLocationChanged = true;
			this._isSizeChanged = true;

			this._isCustomMouseModeActive = false;

			this._opacity = 0.1;

			InitializeComponent();

			this._overlay = new ThumbnailOverlay(this,
				this.MouseEnter_Handler,
				this.MouseLeave_Handler,
				this.MouseDown_Handler,
				this.MouseUp_Handler,
				this.MouseMove_Handler
				);

			SetDefaultBorderColor();
			SetPreventPreviews();
			this._overlay.EnableFakePreview(this.IsPreventPreviews(), false, 0, SystemColors.Control);
			this._thumbnailManager = thumbnailManager;
		}

		public IWindowManager WindowManager { get; }

		public IntPtr Id { get; set; }

		public string Title
		{
			get => this.Text;
			set
			{
				this.Text = value;
				this._overlay.SetOverlayLabel(OverlayLabel.Resolve(value, this._config?.GetClientLabel(value)));
				this._overlay.SetPropertiesOverlayLabel(this.GetOverlayLabelFont(), _config.OverlayLabelColor, _config.OverlayLabelAnchor);
				SetDefaultBorderColor();
				SetPreventPreviews();
				this._overlay.EnableFakePreview(this.IsPreventPreviews(), false, 0, SystemColors.Control);
				this._overlay.SetCycleGroupIndicator(this.IsExcludedFromCycleGroup , _config.CycleGroupIndicatorAnchor);
			}
		}

		public bool IsActive { get; set; }

		/// <summary>Resize em andamento iniciado pelo usuário (arrasto de borda/quina).</summary>
		public bool IsUserResizing => this._isResizing;

		public bool IsOverlayEnabled { get; set; }
		public bool IsExcludedFromCycleGroup { get; set; }
		public bool IsSpotlight
		{
			get => this._isSpotlight;
			set
			{
				this._isSpotlight = value;
				this._overlay.SetResizeGripVisible(value);
			}
		}
		public ZoomAnchor ClientZoomAnchor { get; set; }

		public Point ThumbnailLocation
		{
			get => this.Location;
			set
			{
				this.StartPosition = FormStartPosition.Manual;
				this.Location = value;
			}
		}

		public Size ThumbnailSize
		{
			get => this.ClientSize;
			set => this.ClientSize = value;
		}

		public Rectangle SourceRegion
		{
			get => this._sourceRegion;
			set
			{
				if (this._sourceRegion == value)
				{
					return;
				}

				this._sourceRegion = value;
				this.OnSourceRegionChanged();
			}
		}

		protected virtual void OnSourceRegionChanged()
		{
		}

		public Action<IntPtr> ThumbnailResized { get; set; }

		public Action<IntPtr> ThumbnailMoved { get; set; }

		public Action<IntPtr> ThumbnailFocused { get; set; }

		public Action<IntPtr> ThumbnailLostFocus { get; set; }

		public Action<IntPtr> ThumbnailActivated { get; set; }

		public Action<IntPtr, bool> ThumbnailDeactivated { get; set; }
		public Action<IntPtr> ThumbnailToggleCycleGroup { get; set; }
		public Action<IntPtr, Rectangle> ThumbnailRegionSelected { get; set; }

		private bool WindowMoved = false;

		public void SetDefaultBorderColor()
		{
			this._myBorderColor = new Lazy<Color>(() =>
			{
				if (this._config.PerClientActiveClientHighlightColor.Any(x => x.Key == this.Title))
				{
					return this._config.PerClientActiveClientHighlightColor[Title];
				}
				else
				{
					return _config.ActiveClientHighlightColor;
				}
			});
		}

		public bool IsPreventPreviews()
		{
			return !this.IsSpotlight && this._preventPreviews.Value;
		}
		public void SetPreventPreviews()
		{
			this._preventPreviews = new Lazy<bool>(() =>
			{
				if (this._config.PerClientPreventPreviews.Any(x => x.Key == this.Title))
				{
					return this._config.PerClientPreventPreviews[Title];
				}
				else
				{
					return _config.PreventPreviews;
				}
			});

			this._preventPreviewColor = new Lazy<Color>(() =>
			{
				if (this._config.PerClientPreventPreviewColor.Any(x => x.Key == this.Title))
				{
					return this._config.PerClientPreventPreviewColor[Title];
				}
				else
				{
					return _config.PreventPreviewColor;
				}
			});
		}

		public new void Show()
		{
			this.SuppressResizeEvent();

			base.Show();

			this._isLocationChanged = true;
			this._isSizeChanged = true;
			this._isOverlayVisible = false;

			this.Refresh(true);

			this.IsActive = true;
		}

		public new void Hide()
		{
			this.SuppressResizeEvent();

			this.IsActive = false;

			this._isOverlayVisible = false;
			this._overlay.Hide();
			base.Hide();
		}

		public new bool IsDisposed => _isDisposed || base.IsDisposed || this.Disposing;

		public new virtual void Close()
		{
			this.SuppressResizeEvent();

			this.IsActive = false;
			this._isDisposed = true;
			this._overlayLabelFont?.Dispose();
			this._overlayLabelFont = null;
			this._overlay.Close();
			base.Close();
		}

		// This method is used to determine if the provided Handle is related to client or its thumbnail
		public bool IsKnownHandle(IntPtr handle)
		{
			return (this.Id == handle) || (this.Handle == handle) || (this._overlay.Handle == handle);
		}

		public void SetSizeLimitations(Size minimumSize, Size maximumSize)
		{
			this.MinimumSize = minimumSize;
			this.MaximumSize = maximumSize;
		}

		public void SetOpacity(double opacity)
		{
			if (opacity >= OPACITY_THRESHOLD)
			{
				opacity = 1.0;
			}

			if (Math.Abs(opacity - this._opacity) < OPACITY_EPSILON)
			{
				return;
			}

			try
			{
				this.Opacity = opacity;

				// Overlay opacity settings
				// Of the thumbnail's opacity is almost full then set the overlay's one to
				// full. Otherwise set it to half of the thumbnail opacity
				// Opacity value is stored even if the overlay is not displayed atm
				this._overlay.Opacity = opacity > 0.8 ? 1.0 : 1.0 - (1.0 - opacity) / 2;

				this._opacity = opacity;
			}
			catch (Win32Exception)
			{
				// Something went wrong in WinForms internals
				// Opacity will be updated in the next cycle
			}
		}

		public void SetFrames(bool enable)
		{
			FormBorderStyle style = enable ? FormBorderStyle.SizableToolWindow : FormBorderStyle.None;

			// No need to change the borders style if it is ALREADY correct
			if (this.FormBorderStyle == style)
			{
				return;
			}

			this.SuppressResizeEvent();

			this.FormBorderStyle = style;
		}
		public void SetOverlayLabel()
		{
			this._overlay.SetOverlayLabel(OverlayLabel.Resolve(this.Title, this._config.GetClientLabel(this.Title)));
		}
		public void SetCycleGroupIndicator(bool displayCycleGroup, ZoomAnchor anchor)
		{
			this._overlay.SetCycleGroupIndicator(displayCycleGroup, anchor);
		}

		public void SetTopMost(bool enableTopmost)
		{
			if (this._isTopMost == enableTopmost)
			{
				return;
			}

			this._overlay.TopMost = enableTopmost;
			this.TopMost = enableTopmost;

			this._isTopMost = enableTopmost;
		}

		public void SetHighlight()
		{
			SetHighlight(_config.EnableActiveClientHighlight, _config.ActiveClientHighlightThickness);
		}

		public void SetHighlight(bool enabled, int width)
		{
			if (this._isHighlightRequested == enabled)
			{
				return;
			}

			if (enabled)
			{
				this._isHighlightRequested = true;
				this._highlightWidth = width;
				this.BackColor = _myBorderColor.Value;
			}
			else
			{
				this._isHighlightRequested = false;
				this.BackColor = Color.Black;
			}

			this._isSizeChanged = true;
		}

		public void ClearBorder()
		{
			this.SetHighlight(false, 0);
			this.Refresh(true);
		}

		public void ZoomIn(ViewZoomAnchor anchor, int zoomFactor)
		{
			int oldWidth = this._baseZoomSize.Width;
			int oldHeight = this._baseZoomSize.Height;

			int locationX = this.Location.X;
			int locationY = this.Location.Y;

			int clientSizeWidth = this.ClientSize.Width;
			int clientSizeHeight = this.ClientSize.Height;
			int newWidth = (zoomFactor * clientSizeWidth) + (this.Size.Width - clientSizeWidth);
			int newHeight = (zoomFactor * clientSizeHeight) + (this.Size.Height - clientSizeHeight);

			// First change size, THEN move the window
			// Otherwise there is a chance to fail in a loop
			// Zoom required -> Moved the windows 1st -> Focus is lost -> Window is moved back -> Focus is back on -> Zoom required -> ...
			this.MaximumSize = new Size(0, 0);
			this.Size = new Size(newWidth, newHeight);

			switch (anchor)
			{
				case ViewZoomAnchor.NW:
					break;
				case ViewZoomAnchor.N:
					this.Location = new Point(locationX - newWidth / 2 + oldWidth / 2, locationY);
					break;
				case ViewZoomAnchor.NE:
					this.Location = new Point(locationX - newWidth + oldWidth, locationY);
					break;

				case ViewZoomAnchor.W:
					this.Location = new Point(locationX, locationY - newHeight / 2 + oldHeight / 2);
					break;
				case ViewZoomAnchor.C:
					this.Location = new Point(locationX - newWidth / 2 + oldWidth / 2, locationY - newHeight / 2 + oldHeight / 2);
					break;
				case ViewZoomAnchor.E:
					this.Location = new Point(locationX - newWidth + oldWidth, locationY - newHeight / 2 + oldHeight / 2);
					break;

				case ViewZoomAnchor.SW:
					this.Location = new Point(locationX, locationY - newHeight + this._baseZoomSize.Height);
					break;
				case ViewZoomAnchor.S:
					this.Location = new Point(locationX - newWidth / 2 + oldWidth / 2, locationY - newHeight + oldHeight);
					break;
				case ViewZoomAnchor.SE:
					this.Location = new Point(locationX - newWidth + oldWidth, locationY - newHeight + oldHeight);
					break;
			}
		}

		public void ZoomOut()
		{
			this.RestoreWindowSizeAndLocation();
		}

		public void RegisterHotkey(Keys hotkey)
		{
			if (this._hotkeyHandler != null)
			{
				this.UnregisterHotkey();
			}

			if (hotkey == Keys.None)
			{
				return;
			}

			this._hotkeyHandler = new HotkeyHandler(this.Handle, hotkey);
			this._hotkeyHandler.Pressed += HotkeyPressed_Handler;
			this._hotkeyHandler.Register();
		}

		private Font GetOverlayLabelFont()
		{
			if (!string.Equals(this._overlayLabelFontValue, this._config.OverlayLabelFont, StringComparison.Ordinal))
			{
				Font resolved = OverlayLabelFontConverter.FromInvariantString(this._config.OverlayLabelFont);
				this._overlayLabelFont?.Dispose();
				this._overlayLabelFont = resolved;
				this._overlayLabelFontValue = this._config.OverlayLabelFont;
			}

			return this._overlayLabelFont;
		}

		public void UnregisterHotkey()
		{
			if (this._hotkeyHandler == null)
			{
				return;
			}

			this._hotkeyHandler.Unregister();
			this._hotkeyHandler.Pressed -= HotkeyPressed_Handler;
			this._hotkeyHandler.Dispose();
			this._hotkeyHandler = null;
		}

		public void Refresh(bool forceRefresh)
		{
			this.RefreshThumbnail(forceRefresh);
			this.HighlightThumbnail(forceRefresh || this._isSizeChanged);
			this.RefreshOverlay(forceRefresh || this._isSizeChanged || this._isLocationChanged);

			this._isSizeChanged = false;
		}

		protected abstract void RefreshThumbnail(bool forceRefresh);

		protected abstract void ResizeThumbnail(int baseWidth, int baseHeight, int highlightWidthTop, int highlightWidthRight, int highlightWidthBottom, int highlightWidthLeft);

		private void HighlightThumbnail(bool forceRefresh)
		{
			if (!forceRefresh && (this._isHighlightRequested == this._isHighlightEnabled))
			{
				// Nothing to do here
				return;
			}

			this._isHighlightEnabled = this._isHighlightRequested;

			int baseWidth = this.ClientSize.Width;
			int baseHeight = this.ClientSize.Height;

			if (!this._isHighlightRequested)
			{
				//No highlighting enabled, so no math required
				this.ResizeThumbnail(baseWidth, baseHeight, 0, 0, 0, 0);
				this._overlay.EnableFakePreview(this.IsPreventPreviews(),false, 0, this._preventPreviewColor.Value);
				return;
			}

			double baseAspectRatio = ((double)baseWidth) / baseHeight;

			int actualHeight = baseHeight - 2 * this._highlightWidth;
			double desiredWidth = actualHeight * baseAspectRatio;
			int actualWidth = (int)Math.Round(desiredWidth, MidpointRounding.AwayFromZero);
			int highlightWidthLeft = (baseWidth - actualWidth) / 2;
			int highlightWidthRight = baseWidth - actualWidth - highlightWidthLeft;

			this._overlay.EnableFakePreview(this.IsPreventPreviews(), true, this._highlightWidth, this._preventPreviewColor.Value);
			this.ResizeThumbnail(this.ClientSize.Width, this.ClientSize.Height, this._highlightWidth, highlightWidthRight, this._highlightWidth, highlightWidthLeft);
		}

		private void RefreshOverlay(bool forceRefresh)
		{
			if (this._isOverlayVisible && !forceRefresh)
			{
				// No need to update anything. Everything is already set up
				return;
			}

			// Only show overlay if enabled AND thumbnail is active/visible.
			this._overlay.EnableOverlayLabel(this.IsOverlayEnabled && this.Visible);

			if (!this._isOverlayVisible && ((this.IsOverlayEnabled && this.Visible) || this.IsPreventPreviews() ) && (this.IsSpotlight || !_config.IsThumbnailDisabled(this.Title)) )
			{
				// One-time action to show the Overlay before it is set up
				// Otherwise its position won't be set
				this._overlay.Show();
				this._isOverlayVisible = true;
			}

			Size overlaySize = this.ClientSize;
			Point overlayLocation = this.Location;

			int borderWidth = (this.Size.Width - this.ClientSize.Width) / 2;
			overlayLocation.X += borderWidth;
			overlayLocation.Y += (this.Size.Height - this.ClientSize.Height) - borderWidth;

			this._isLocationChanged = false;
			this._overlay.Size = overlaySize;

			this._overlay.SetPropertiesOverlayLabel(this.GetOverlayLabelFont(), _config.OverlayLabelColor, _config.OverlayLabelAnchor);

			this._overlay.Location = overlayLocation;
			this._overlay.Refresh();
		}

		private void SuppressResizeEvent()
		{
			// Workaround for WinForms issue with the Resize event being fired with inconsistent ClientSize value
			// Any Resize events fired before this timestamp will be ignored
			this._suppressResizeEventsTimestamp = DateTime.UtcNow.AddMilliseconds(_config.ThumbnailResizeTimeoutPeriod);
		}

		#region GUI events
		protected override CreateParams CreateParams
		{
			get
			{
				var Params = base.CreateParams;
				Params.ExStyle |= (int)InteropConstants.WS_EX_TOOLWINDOW;
				return Params;
			}
		}

		private void Move_Handler(object sender, EventArgs e)
		{
			this._isLocationChanged = true;
			this.ThumbnailMoved?.Invoke(this.Id);
		}

		private void Resize_Handler(object sender, EventArgs e)
		{
			if (DateTime.UtcNow < this._suppressResizeEventsTimestamp)
			{
				return;
			}

			// Apenas marca o redesenho. Resizes programáticos (SetFrames, limites,
			// restauração de layout, zoom, DPI) não podem propagar tamanho; a
			// propagação só acontece no fim de um resize do usuário (issue #138).
			this._isSizeChanged = true;
		}

		private void ResizeBegin_Handler(object sender, EventArgs e)
		{
			// WM_ENTERSIZEMOVE: começo do arrasto de borda (ou de movimento).
			this._userResizeStartSize = this.ClientSize;
		}

		private void ResizeEnd_Handler(object sender, EventArgs e)
		{
			// WM_EXITSIZEMOVE: o arrasto terminou, então o tamanho agora é o final.
			if (this.ClientSize == this._userResizeStartSize)
			{
				// Foi apenas um movimento de janela, não um redimensionamento.
				return;
			}

			this.NotifyUserResize();
		}

		private void NotifyUserResize()
		{
			if (DateTime.UtcNow < this._suppressResizeEventsTimestamp)
			{
				return;
			}

			this._isSizeChanged = true;
			this.ThumbnailResized?.Invoke(this.Id);
		}

		private void MouseEnter_Handler(object sender, EventArgs e)
		{
			this.ExitCustomMouseMode();
			this.SaveWindowSizeAndLocation();

			this.ThumbnailFocused?.Invoke(this.Id);
		}

		private void MouseLeave_Handler(object sender, EventArgs e)
		{
			if (this._isRegionSelectionActive)
			{
				return;
			}

			if (!this._isResizing)
			{
				this.SetMouseCursor(Cursors.Hand);
			}

			this.ThumbnailLostFocus?.Invoke(this.Id);
		}

		private void MouseDown_Handler(object sender, MouseEventArgs e)
		{
			Keys modifiers = Control.ModifierKeys;

			if (this.IsSpotlight && e.Button == MouseButtons.Left && (modifiers == Keys.None || modifiers == Keys.Shift))
			{
				SpotlightResizeEdge edge = this.GetResizeEdge(this.PointToClient(Control.MousePosition));
				if (edge != SpotlightResizeEdge.None)
				{
					this.StartResize(edge);
					return;
				}
			}

			this.MouseDownEventHandler(e.Button, modifiers);
		}

		private void MouseMove_Handler(object sender, MouseEventArgs e)
		{
			if (this._isRegionSelectionActive)
			{
				this.UpdateRegionSelection();
				return;
			}

			if (this._isResizing)
			{
				this.UpdateResize();
				return;
			}

			if (this._isCustomMouseModeActive)
			{
				this.ProcessCustomMouseMode(e.Button.HasFlag(MouseButtons.Left), e.Button.HasFlag(MouseButtons.Right));
				return;
			}

			if (this.IsSpotlight)
			{
				this.UpdateResizeCursor();
			}
		}

		private void MouseUp_Handler(object sender, MouseEventArgs e)
		{
			if (this._isResizing && e.Button == MouseButtons.Left)
			{
				this.EndResize();
				return;
			}

			if (this._isRegionSelectionActive && e.Button == MouseButtons.Right)
			{
				this.CompleteRegionSelection();
				return;
			}

			if (e.Button == MouseButtons.Right)
			{
				this.ExitCustomMouseMode();

				// Snap to Grid on release of mouse (if moved)
				if (_config.ThumbnailSnapToGrid && this.WindowMoved)
				{
					var x = (int)Math.Round((double)this.Location.X / (double)_config.ThumbnailSnapToGridSizeX) * _config.ThumbnailSnapToGridSizeX;
                    var y = (int)Math.Round((double)this.Location.Y / (double)_config.ThumbnailSnapToGridSizeY) * _config.ThumbnailSnapToGridSizeY;
					this.Location = new Point(x, y);
					this._baseZoomLocation = this.Location;

					this.WindowMoved = false;

                }
			}
		}

		private void HotkeyPressed_Handler(object sender, HandledEventArgs e)
		{
			this.SetHighlight();
			this.ThumbnailActivated?.Invoke(this.Id);

			e.Handled = true;
		}
		#endregion

		#region Custom Mouse mode
		// This pair of methods saves/restores certain window properties
		// Methods are used to remove the 'Zoom' effect (if any) when the
		// custom resize/move mode is activated
		// Methods are kept on this level because moving to the presenter
		// the code that responds to the mouse events like movement
		// seems like a huge overkill
		private void SaveWindowSizeAndLocation()
		{
			this._baseZoomSize = this.Size;
			this._baseZoomLocation = this.Location;
			this._baseZoomMaximumSize = this.MaximumSize;
		}

		private void RestoreWindowSizeAndLocation()
		{
			this.Size = this._baseZoomSize;
			this.MaximumSize = this._baseZoomMaximumSize;
			this.Location = this._baseZoomLocation;
		}

		private void EnterCustomMouseMode()
		{
			this.RestoreWindowSizeAndLocation();

			this._isCustomMouseModeActive = true;
			this._baseMousePosition = Control.MousePosition;
			this._customResizeStartSize = this.Size;
		}

		private void ProcessCustomMouseMode(bool leftButton, bool rightButton)
		{
			Point mousePosition = Control.MousePosition;
			int offsetX = mousePosition.X - this._baseMousePosition.X;
			int offsetY = mousePosition.Y - this._baseMousePosition.Y;
			this._baseMousePosition = mousePosition;

			if (!_config.LockThumbnailLocation)
			{
                // Left + Right buttons trigger thumbnail resize
                // Right button only trigger thumbnail movement
                if (leftButton && rightButton)
                {
                    Size newSize = new Size(this.Size.Width + offsetX, this.Size.Height + offsetY);
                    if (newSize != this.Size)
                    {
                        this.Size = newSize;
                        this._baseZoomSize = newSize;

                        // Resize feito pelo usuário (botões esquerdo+direito) deve notificar
                        // com o tamanho final, caso contrário o resize é tratado como
                        // programático e não propaga (issue #138).
                        this.NotifyUserResize();
                    }
                }
                else
                {
                    this.Location = new Point(this.Location.X + offsetX, this.Location.Y + offsetY);
                    this._baseZoomLocation = this.Location;
					this.WindowMoved = true;
                }
            }
		}

		private void ExitCustomMouseMode()
		{
			if (!this._isCustomMouseModeActive)
			{
				return;
			}

			this._isCustomMouseModeActive = false;

			// O modo customizado também permite redimensionar (botões esquerdo+direito);
			// propaga só no fim da interação (issue #138).
			if (this.Size != this._customResizeStartSize)
			{
				this.NotifyUserResize();
			}
		}
		#endregion

		#region Region selection
		private void StartRegionSelection()
		{
			this._isRegionSelectionActive = true;
			this._regionSelectionStart = this.PointToClient(Control.MousePosition);
			this._regionSelectionRectangle = Rectangle.Empty;

			this.Cursor = Cursors.Cross;
			this._overlay.SetSelectionMode(true);
			this._overlay.SetSelectionRectangle(Rectangle.Empty);
		}

		private void UpdateRegionSelection()
		{
			Point current = this.PointToClient(Control.MousePosition);
			Rectangle selection = Rectangle.FromLTRB(
				Math.Min(this._regionSelectionStart.X, current.X),
				Math.Min(this._regionSelectionStart.Y, current.Y),
				Math.Max(this._regionSelectionStart.X, current.X),
				Math.Max(this._regionSelectionStart.Y, current.Y));

			selection.Intersect(new Rectangle(Point.Empty, this.ClientSize));

			this._regionSelectionRectangle = selection;
			this._overlay.SetSelectionRectangle(selection);
		}

		private void CompleteRegionSelection()
		{
			this._isRegionSelectionActive = false;
			this.Cursor = Cursors.Default;
			this._overlay.SetSelectionMode(false);
			this._overlay.SetSelectionRectangle(Rectangle.Empty);

			Rectangle selection = this._regionSelectionRectangle;
			this._regionSelectionRectangle = Rectangle.Empty;

			// A simple Shift+right-click (no meaningful drag) resets the crop.
			if (selection.Width < 6 || selection.Height < 6 || this.ClientSize.Width <= 0 || this.ClientSize.Height <= 0)
			{
				this.ThumbnailRegionSelected?.Invoke(this.Id, Rectangle.Empty);
				return;
			}

			Size clientSize = WindowGeometry.GetClientSize(this.Id);
			Rectangle displayedSource = this.SourceRegion;
			if (displayedSource.IsEmpty)
			{
				if (clientSize.Width <= 0 || clientSize.Height <= 0)
				{
					return;
				}

				displayedSource = new Rectangle(Point.Empty, clientSize);
			}

			Rectangle region = SpotlightRegionCalculator.MapSelection(selection, displayedSource, this.ClientSize);
			this.ThumbnailRegionSelected?.Invoke(this.Id, region);
		}
		#endregion

		#region Window resize
		private void StartResize(SpotlightResizeEdge edge)
		{
			this._isResizing = true;
			this._resizeEdge = edge;
			this._resizeStartScreen = Control.MousePosition;
			this._resizeStartBounds = this.Bounds;

			this.SetMouseCursor(ThumbnailView.GetResizeCursor(edge));
		}

		private void UpdateResize()
		{
			if (this._resizeEdge == SpotlightResizeEdge.None)
			{
				return;
			}

			Point mouse = Control.MousePosition;
			int deltaX = mouse.X - this._resizeStartScreen.X;
			int deltaY = mouse.Y - this._resizeStartScreen.Y;

			bool freeResize = Control.ModifierKeys.HasFlag(Keys.Shift);

			this.Bounds = SpotlightResizeCalculator.Calculate(
				this._resizeStartBounds,
				this._resizeEdge,
				deltaX,
				deltaY,
				this.GetSourceAspect(),
				freeResize,
				new Size(160, 90));
		}

		private double GetSourceAspect()
		{
			Size sourceSize = this.SourceRegion.IsEmpty
				? WindowGeometry.GetClientSize(this.Id)
				: this.SourceRegion.Size;

			return sourceSize.Width > 0 && sourceSize.Height > 0
				? (double)sourceSize.Width / sourceSize.Height
				: 0;
		}

		private void EndResize()
		{
			this._isResizing = false;
			this._resizeEdge = SpotlightResizeEdge.None;
			this.UpdateResizeCursor();

			// O redimensionamento manual do spotlight também é interação do usuário:
			// propaga só no fim do arrasto (issue #138).
			if (this.Size != this._resizeStartBounds.Size)
			{
				this.NotifyUserResize();
			}
		}

		private void UpdateResizeCursor()
		{
			// Travar a posição não deve impedir o redimensionamento: mover continua
			// bloqueado em ProcessCustomMouseMode, mas a borda segue arrastável.
			SpotlightResizeEdge edge = this.GetResizeEdge(this.PointToClient(Control.MousePosition));

			this.SetMouseCursor(edge == SpotlightResizeEdge.None ? Cursors.Hand : ThumbnailView.GetResizeCursor(edge));
		}

		private SpotlightResizeEdge GetResizeEdge(Point point)
		{
			int width = this.ClientSize.Width;
			int height = this.ClientSize.Height;

			bool left = point.X >= 0 && point.X <= ThumbnailView.RESIZE_BORDER_SIZE;
			bool right = point.X >= width - ThumbnailView.RESIZE_BORDER_SIZE && point.X <= width;
			bool top = point.Y >= 0 && point.Y <= ThumbnailView.RESIZE_BORDER_SIZE;
			bool bottom = point.Y >= height - ThumbnailView.RESIZE_BORDER_SIZE && point.Y <= height;

			if (left && top) return SpotlightResizeEdge.TopLeft;
			if (right && top) return SpotlightResizeEdge.TopRight;
			if (left && bottom) return SpotlightResizeEdge.BottomLeft;
			if (right && bottom) return SpotlightResizeEdge.BottomRight;
			if (left) return SpotlightResizeEdge.Left;
			if (right) return SpotlightResizeEdge.Right;
			if (top) return SpotlightResizeEdge.Top;
			if (bottom) return SpotlightResizeEdge.Bottom;

			return SpotlightResizeEdge.None;
		}

		private static Cursor GetResizeCursor(SpotlightResizeEdge edge)
		{
			switch (edge)
			{
				case SpotlightResizeEdge.Left:
				case SpotlightResizeEdge.Right:
					return Cursors.SizeWE;
				case SpotlightResizeEdge.Top:
				case SpotlightResizeEdge.Bottom:
					return Cursors.SizeNS;
				case SpotlightResizeEdge.TopLeft:
				case SpotlightResizeEdge.BottomRight:
					return Cursors.SizeNWSE;
				case SpotlightResizeEdge.TopRight:
				case SpotlightResizeEdge.BottomLeft:
					return Cursors.SizeNESW;
				default:
					return Cursors.Hand;
			}
		}

		private void SetMouseCursor(Cursor cursor)
		{
			if (this.Cursor != cursor)
			{
				this.Cursor = cursor;
			}

			this._overlay.SetMouseCursor(cursor);
		}
		#endregion

		#region Custom GUI events
		protected virtual void MouseDownEventHandler(MouseButtons mouseButtons, Keys modifierKeys)
		{
			switch (mouseButtons)
			{
				case MouseButtons.Left when modifierKeys == Keys.Control:
					this.ThumbnailDeactivated?.Invoke(this.Id, false);
					break;
				case MouseButtons.Left when modifierKeys == Keys.Shift:
					this.ThumbnailToggleCycleGroup?.Invoke(this.Id);
					break;
				case MouseButtons.Left when modifierKeys == (Keys.Control | Keys.Shift):
					this.ThumbnailDeactivated?.Invoke(this.Id, true);
					break;
				case MouseButtons.Left:
					var oldWindow = this._thumbnailManager.GetActiveClient();
					this.ThumbnailActivated?.Invoke(this.Id);
					this.SetHighlight();
					this.Refresh(true);

					oldWindow?.ClearBorder();
					break;
				case MouseButtons.Right when this.IsSpotlight && modifierKeys.HasFlag(Keys.Shift):
					this.StartRegionSelection();
					break;
				case MouseButtons.Right:
				case MouseButtons.Left | MouseButtons.Right:
					this.EnterCustomMouseMode();
					break;
			}
		}
		#endregion
	}
}
