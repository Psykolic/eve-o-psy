using System.Threading;
using System.Threading.Tasks;
using EveOPreview.Mediator.Messages;
using EveOPreview.Services;
using MediatR;

namespace EveOPreview.Mediator.Handlers.Services
{
	sealed class StartStopServiceHandler : IRequestHandler<StartService>, IRequestHandler<StopService>
	{
		private readonly IThumbnailManager _manager;

		public StartStopServiceHandler(IThumbnailManager manager)
		{
			this._manager = manager;
		}

		public Task<Unit> Handle(StartService message, CancellationToken cancellationToken)
		{
			this._manager.Start();

			return Unit.Task;
		}

		public async Task<Unit> Handle(StopService message, CancellationToken cancellationToken)
		{
			await this._manager.StopAsync(cancellationToken);
			return Unit.Value;
		}
	}
}
