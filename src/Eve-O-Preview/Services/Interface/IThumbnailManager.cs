using EveOPreview.View;

namespace EveOPreview.Services
{
	public interface IThumbnailManager
	{
		void Start();
		void Stop();

		void UpdateCycleGroupIndicator();
		void UpdateThumbnailsSize();
		void UpdateThumbnailFrames();
		void UpdateSpotlights();
		void ClearSpotlightRegion(string title);

		IThumbnailView GetClientByTitle(string title);
		IThumbnailView GetClientByPointer(System.IntPtr ptr);
		IThumbnailView GetActiveClient();
	}
}