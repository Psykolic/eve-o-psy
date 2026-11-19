using System.Threading;
using System.Threading.Tasks;
using EveOPreview.Mediator.Messages;
using EveOPreview.Services;
using MediatR;

namespace EveOPreview.Mediator.Handlers.Thumbnails
{
	sealed class ClearSpotlightRegionHandler : IRequestHandler<ClearSpotlightRegion>
	{
		private readonly IThumbnailManager _manager;

		public ClearSpotlightRegionHandler(IThumbnailManager manager)
		{
			this._manager = manager;
		}

		public Task<Unit> Handle(ClearSpotlightRegion message, CancellationToken cancellationToken)
		{
			this._manager.ClearSpotlightRegion(message.Title);

			return Unit.Task;
		}
	}
}
