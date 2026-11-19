using System.Threading;
using System.Threading.Tasks;
using EveOPreview.Mediator.Messages;
using EveOPreview.Services;
using MediatR;

namespace EveOPreview.Mediator.Handlers.Thumbnails
{
	sealed class SpotlightSettingsUpdatedHandler : INotificationHandler<SpotlightSettingsUpdated>
	{
		private readonly IThumbnailManager _manager;

		public SpotlightSettingsUpdatedHandler(IThumbnailManager manager)
		{
			this._manager = manager;
		}

		public Task Handle(SpotlightSettingsUpdated notification, CancellationToken cancellationToken)
		{
			this._manager.UpdateSpotlights();

			return Task.CompletedTask;
		}
	}
}
