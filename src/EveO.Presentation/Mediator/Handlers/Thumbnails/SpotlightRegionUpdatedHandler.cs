using System.Threading;
using System.Threading.Tasks;
using EveOPreview.Mediator.Messages;
using EveOPreview.Presenters;
using MediatR;

namespace EveOPreview.Mediator.Handlers.Thumbnails
{
	sealed class SpotlightRegionUpdatedHandler : INotificationHandler<SpotlightRegionUpdated>
	{
		private readonly IMainFormPresenter _presenter;

		public SpotlightRegionUpdatedHandler(MainFormPresenter presenter)
		{
			this._presenter = presenter;
		}

		public Task Handle(SpotlightRegionUpdated notification, CancellationToken cancellationToken)
		{
			this._presenter.UpdateSpotlightRegionInfo(notification.Title, notification.Region);

			return Task.CompletedTask;
		}
	}
}
