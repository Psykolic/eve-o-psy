using System;
using System.Drawing;

namespace EveOPreview.Services
{
	/// <summary>
	/// Captura estática de um cliente como <see cref="Image"/> (GDI+).
	///
	/// Fica no Infrastructure (e não no <see cref="IWindowManager"/> do Core) porque
	/// <c>System.Drawing.Image</c> exige <c>System.Drawing.Common</c>, que é Windows-only
	/// e o Core não pode referenciar (issue #162, decisão 5.2 do plano de camadas).
	/// </summary>
	public interface IStaticThumbnailProvider
	{
		Image GetStaticThumbnail(IntPtr source);
	}
}
