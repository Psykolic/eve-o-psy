using System.Threading;
using System.Threading.Tasks;
using EveOPreview.Configuration;
using EveOPreview.Mediator.Messages;
using EveOPreview.Mediator.Messages.Configuration;
using MediatR;

namespace EveOPreview.Mediator.Handlers.Configuration
{
	sealed class SaveConfigurationHandler : IRequestHandler<SaveConfiguration>
	{
		private readonly IConfigurationStorage _storage;
		private readonly IMediator _mediator;

		public SaveConfigurationHandler(IConfigurationStorage storage, IMediator mediator)
		{
			this._storage = storage;
			this._mediator = mediator;
		}

		public async Task<Unit> Handle(SaveConfiguration message, CancellationToken cancellationToken)
		{
			this._storage.Save();

			// Aguarda os handlers de hot-reload: exceção precisa aparecer no fluxo de
			// salvamento, não como tarefa não observada (que derrubaria o app).
			await this._mediator.Publish(new ConfigurationChangedNotification(), cancellationToken);

			return Unit.Value;
		}
	}
}