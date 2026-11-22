using System.Collections.Generic;
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

		/// <summary>
		/// Libera as hotkeys globais dos grupos enquanto uma janela está capturando teclas.
		/// Sem isso, pressionar uma tecla já registrada dispara a ciclagem no meio da edição.
		/// </summary>
		void SuspendCycleGroupHotkeys();

		/// <summary>Títulos das thumbnails vivas — os clientes abertos agora.</summary>
		IReadOnlyList<string> GetClientTitles();

		IThumbnailView GetClientByTitle(string title);
		IThumbnailView GetClientByPointer(System.IntPtr ptr);
		IThumbnailView GetActiveClient();
	}
}
