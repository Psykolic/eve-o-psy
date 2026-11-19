using System.Drawing;
using MediatR;

namespace EveOPreview.Mediator.Messages
{
	sealed class SpotlightRegionUpdated : INotification
	{
		public SpotlightRegionUpdated(string title, Rectangle region)
		{
			this.Title = title;
			this.Region = region;
		}

		public string Title { get; }
		public Rectangle Region { get; }
	}
}
