using System;
using System.Drawing;

namespace EveOPreview.Services
{
	/// <summary>
	/// Thumbnail registrada no DWM. <see cref="IDisposable"/> garante que o registro
	/// é devolvido mesmo quando o fluxo normal de fechamento falha (item 1.3).
	/// </summary>
	public interface IDwmThumbnail : IDisposable
	{
		void Register(IntPtr destination, IntPtr source);
		void Unregister();

		void Move(int left, int top, int right, int bottom);
		void SetSourceRegion(Rectangle region);
		void Update();
	}
}
