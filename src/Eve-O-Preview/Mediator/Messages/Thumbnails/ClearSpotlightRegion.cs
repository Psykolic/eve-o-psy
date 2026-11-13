using MediatR;

namespace EveOPreview.Mediator.Messages
{
	sealed class ClearSpotlightRegion : IRequest
	{
		public ClearSpotlightRegion(string title)
		{
			this.Title = title;
		}

		public string Title { get; }
	}
}
