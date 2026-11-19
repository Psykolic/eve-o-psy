using System.Threading;
using System.Threading.Tasks;
using EveOPreview.Services;
using EveOPreview.Mediator.Messages.Configuration;
using MediatR;

namespace EveOPreview.Mediator.Handlers.Configuration
{
	sealed class ConfigurationChangedHandler : INotificationHandler<ConfigurationChangedNotification>
	{
		private readonly IThumbnailManager _manager;

		public ConfigurationChangedHandler(IThumbnailManager manager)
		{
			this._manager = manager;
		}

		public Task Handle(ConfigurationChangedNotification notification, CancellationToken cancellationToken)
		{
			// Hot-reload ThumbnailRefreshPeriod: restart the timer with the new interval
			this._manager.RefreshTimerInterval();

			// Hot-reload ZoomFactor: update zoom factor on all visible thumbnails
			this._manager.RefreshZoomFactor();

			return Task.CompletedTask;
		}
	}
}