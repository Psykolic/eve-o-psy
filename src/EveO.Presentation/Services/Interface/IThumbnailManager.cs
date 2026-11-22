using System.Threading;
using System.Threading.Tasks;
using EveOPreview.View;

namespace EveOPreview.Services
{
	public interface IThumbnailManager
	{
		void Start();
		Task StopAsync(CancellationToken cancellationToken);

		void UpdateCycleGroupIndicator();
		void UpdateThumbnailsSize();
		void UpdateThumbnailFrames();
		void UpdateSpotlights();
		void ClearSpotlightRegion(string title);

		/// <summary>
		/// Restart the thumbnail update timer with the current <see cref="ThumbnailConfiguration.ThumbnailRefreshPeriod"/>.
		/// Hot-reload applied without requiring an application restart.
		/// </summary>
		void RefreshTimerInterval();

		/// <summary>
		 /// Apply the current <see cref="ThumbnailConfiguration.ThumbnailZoomFactor"/> to all visible thumbnails.
		/// Hot-reload applied without requiring an application restart.
		/// </summary>
		void RefreshZoomFactor();

		/// <summary>
		/// Re-registra as hotkeys dos grupos de ciclo a partir da configuração atual.
		/// Aplicado sem reiniciar — editar atalho não deve exigir fechar o aplicativo.
		/// </summary>
		void RefreshCycleGroupHotkeys();

		IThumbnailView GetClientByTitle(string title);
		IThumbnailView GetClientByPointer(System.IntPtr ptr);
		IThumbnailView GetActiveClient();
	}
}
