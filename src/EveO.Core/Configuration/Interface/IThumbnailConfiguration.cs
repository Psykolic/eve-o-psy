using System.Collections.Generic;
using System.Drawing;

namespace EveOPreview.Configuration
{
	public interface IThumbnailConfiguration
	{
		List<string> CycleGroup1ForwardHotkeys { get; set; }
		List<string> CycleGroup1BackwardHotkeys { get; set; }
		Dictionary<string, int> CycleGroup1ClientsOrder { get; set; }

		List<string> CycleGroup2ForwardHotkeys { get; set; }
		List<string> CycleGroup2BackwardHotkeys { get; set; }
		Dictionary<string, int> CycleGroup2ClientsOrder { get; set; }

		List<string> CycleGroup3ForwardHotkeys { get; set; }
		List<string> CycleGroup3BackwardHotkeys { get; set; }
		Dictionary<string, int> CycleGroup3ClientsOrder { get; set; }

		List<string> CycleGroup4ForwardHotkeys { get; set; }
		List<string> CycleGroup4BackwardHotkeys { get; set; }
		Dictionary<string, int> CycleGroup4ClientsOrder { get; set; }

		List<string> CycleGroup5ForwardHotkeys { get; set; }
		List<string> CycleGroup5BackwardHotkeys { get; set; }
		Dictionary<string, int> CycleGroup5ClientsOrder { get; set; }

		Dictionary<string, Color> PerClientActiveClientHighlightColor { get; set; }
		Dictionary<string, Color> PerClientPreventPreviewColor { get; set; }
		Dictionary<string, bool> PerClientPreventPreviews { get; set; }
		Dictionary<string, Size> PerClientThumbnailSize { get; set; }
		Dictionary<string, bool> CycleGroupExclusions { get; set; }

		/// <summary>Label customizada por cliente; vazio usa os 8 primeiros caracteres do nome.</summary>
		Dictionary<string, string> PerClientLabel { get; set; }
		string GetClientLabel(string currentClient);
		void SetClientLabel(string currentClient, string label);
		void RenameClientLabel(string oldTitle, string newTitle);

		bool MinimizeToTray { get; set; }

		/// <summary>Tema escuro da janela de configuração (fase 3 do layout).</summary>
		bool DarkTheme { get; set; }
		int ThumbnailRefreshPeriod { get; set; }
		bool EventDrivenRefresh { get; set; }
		int ReconciliationPeriod { get; set; }
		int ThumbnailResizeTimeoutPeriod { get; set; }
		bool EnableWineCompatibilityMode { get; set; }

		double ThumbnailOpacity { get; set; }

		List<string> SpotlightClients { get; set; }
		Size SpotlightSize { get; set; }
		double SpotlightOpacity { get; set; }
		bool SpotlightAlwaysOnTop { get; set; }
		List<string> ToggleSpotlightHotkeys { get; set; }

		bool EnableClientLayoutTracking { get; set; }
		bool HideActiveClientThumbnail { get; set; }
		bool HideLoginClientThumbnail { get; set; }
		bool MinimizeInactiveClients { get; set; }
		bool HideCaptionOnClients { get; set; }
		AnimationStyle WindowsAnimationStyle { get; set; }
		bool ShowThumbnailsAlwaysOnTop { get; set; }
		bool EnablePerClientThumbnailLayouts { get; set; }

		bool PreventPreviews { get; set; }
		bool HideThumbnailsOnLostFocus { get; set; }
		int HideThumbnailsDelay { get; set; }

		Size ThumbnailSize { get; set; }
		Size ThumbnailMinimumSize { get; set; }
		Size ThumbnailMaximumSize { get; set; }
		bool ThumbnailKeepAspectRatio { get; set; }

		bool EnableThumbnailSnap { get; set; }

		bool ThumbnailZoomEnabled { get; set; }
		int ThumbnailZoomFactor { get; set; }
		ZoomAnchor ThumbnailZoomAnchor { get; set; }
		ZoomAnchor OverlayLabelAnchor { get; set; }
		ZoomAnchor CycleGroupIndicatorAnchor { get; set; }

		bool ShowThumbnailOverlays { get; set; }
		bool ShowThumbnailFrames { get; set; }
		bool LockThumbnailLocation { get; set; }
		bool ThumbnailSnapToGrid {  get; set; }
		int ThumbnailSnapToGridSizeX { get; set; }
		int ThumbnailSnapToGridSizeY { get; set; }

		bool EnableActiveClientHighlight { get; set; }
		Color ActiveClientHighlightColor { get; set; }
		Color PreventPreviewColor { get; set; }
		int ActiveClientHighlightThickness { get; set; }
		Color OverlayLabelColor { get; set; }

		/// <summary>
		/// Fonte do rótulo de overlay na forma textual do <c>FontConverter</c>
		/// (ex.: "Microsoft Sans Serif, 10pt, style=Bold"). O Core não referencia
		/// <c>System.Drawing.Common</c>, então a conversão para <c>Font</c> fica no executável.
		/// </summary>
		string OverlayLabelFont { get; set; }

		string IconName { get; set; }
		List<string> MinimizeAllClientsHotkeys { get; set; }

		Point LoginThumbnailLocation { get; set; }

		Point GetThumbnailLocation(string currentClient, string activeClient, Point defaultLocation);
		Size GetThumbnailSize(string currentClient, string activeClient, Size defaultSize);
		Size GetSpotlightSize(string currentClient, Size defaultSize);
		void SetSpotlightSize(string currentClient, Size size);
		Point GetSpotlightLocation(string currentClient, Point defaultLocation);
		void SetSpotlightLocation(string currentClient, Point location);
		Rectangle GetSpotlightRegion(string currentClient);
		void SetSpotlightRegion(string currentClient, Rectangle region);
		void RenameSpotlightClient(string oldTitle, string newTitle);
		ZoomAnchor GetZoomAnchor(string currentClient, ZoomAnchor defaultZoomAnchor);
		void SetThumbnailLocation(string currentClient, string activeClient, Point location);

		ClientLayout GetClientLayout(string currentClient);
		void SetClientLayout(string currentClient, ClientLayout layout);

		/// <summary>
		/// Todos os nomes de cliente que a configuração conhece (grupos, layout por cliente
		/// e layout achatado). Usado pelo editor de grupos para oferecer escolhas.
		/// </summary>
		IReadOnlyCollection<string> GetKnownClients();

		HotkeyBinding GetClientHotkey(string currentClient);
		void SetClientHotkey(string currentClient, HotkeyBinding hotkey);
		bool IsPriorityClient(string currentClient);
		bool IsExecutableToPreview(string processName);

		bool IsThumbnailDisabled(string currentClient);
		void ToggleThumbnail(string currentClient, bool isDisabled);

		void ApplyRestrictions();
	}
}