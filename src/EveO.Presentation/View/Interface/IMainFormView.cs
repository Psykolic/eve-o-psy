using System;
using System.Collections.Generic;
using System.Drawing;

namespace EveOPreview.View
{
	/// <summary>
	/// Main view interface
	/// Presenter uses it to access GUI properties
	/// </summary>
	public interface IMainFormView : IView
	{
		bool MinimizeToTray { get; set; }

		double ThumbnailOpacity { get; set; }
		double SpotlightOpacity { get; set; }
		IList<string> SpotlightClients { get; }
		string SelectedSpotlightClient { get; }

		bool EnableClientLayoutTracking { get; set; }
		bool HideActiveClientThumbnail { get; set; }
		bool MinimizeInactiveClients { get; set; }
		bool HideCaptionOnClients { get; set; }
		ViewAnimationStyle WindowsAnimationStyle { get; set; }
        bool ShowThumbnailsAlwaysOnTop { get; set; }
		bool PreventPreviews { get; set; }
		bool HideThumbnailsOnLostFocus { get; set; }
		bool EnablePerClientThumbnailLayouts { get; set; }

		Size ThumbnailSize { get; set; }
		bool KeepAspectRatio { get; set; }

		bool EnableThumbnailZoom { get; set; }
		int ThumbnailZoomFactor { get; set; }
		ViewZoomAnchor ThumbnailZoomAnchor { get; set; }
		ViewZoomAnchor OverlayLabelAnchor { get; set; }
		ViewZoomAnchor CycleGroupIndicatorAnchor { get; set; }

		bool ShowThumbnailOverlays { get; set; }
		bool ShowThumbnailFrames { get; set; }

		bool LockThumbnailLocation { get; set; }
		bool ThumbnailSnapToGrid { get; set; }
		int ThumbnailSnapToGridSizeX { get; set; }
		int ThumbnailSnapToGridSizeY { get; set; }

		bool EnableActiveClientHighlight { get; set; }
		Color ActiveClientHighlightColor { get; set; }
		Color PreventPreviewColor { get; set; }
		Color OverlayLabelColor { get; set; }
		Font OverlayLabelFont { get; set; }

		string IconName { get; set; }

		void SetDocumentationUrl(string url);
		void SetVersionInfo(string version);
		void SetThumbnailSizeLimitations(Size minimumSize, Size maximumSize);

		void Minimize();

		void AddThumbnails(IList<IThumbnailDescription> thumbnails);
		void RemoveThumbnails(IList<IThumbnailDescription> thumbnails);
		void SetSpotlightClients(IEnumerable<string> clients);
		void SetSpotlightHotkeyInfo(string hotkeyInfo);
		void SetSpotlightRegionInfo(string title, Rectangle region);
		void RefreshZoomSettings();

		/// <summary>Avisa que a configuração foi corrigida no carregamento.</summary>
		void ShowConfigurationWarning(string details);

		Action ApplicationExitRequested { get; set; }
		Action FormActivated { get; set; }
		Action FormMinimized { get; set; }
		Action<ViewCloseRequest> FormCloseRequested { get; set; }
		Action ApplicationSettingsChanged { get; set; }
		Action ThumbnailsSizeChanged { get; set; }
		Action<string> ThumbnailStateChanged { get; set; }
		Action SpotlightClientsChanged { get; set; }
		Action<string> SpotlightClientSelected { get; set; }
		Action<string> ClearSpotlightRegionRequested { get; set; }
		Action DocumentationLinkActivated { get; set; }
	}
}