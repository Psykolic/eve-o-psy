using MediatR;

namespace EveOPreview.Mediator.Messages.Configuration
{
	/// <summary>
	/// Notification published when configuration changes are saved.
	/// Handlers can react immediately without polling.
	/// </summary>
	sealed class ConfigurationChangedNotification : INotification
	{
	}
}