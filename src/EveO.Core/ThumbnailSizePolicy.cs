using System;
using System.Drawing;

namespace EveOPreview.Services
{
	/// <summary>
	/// Regras puras de tamanho das miniaturas (issue #138).
	/// Ficam em EveO.Core para serem testáveis no Linux, sem WinForms.
	/// </summary>
	public static class ThumbnailSizePolicy
	{
		/// <summary>
		/// Título da janela do cliente que ainda está na tela de login.
		/// </summary>
		public const string DefaultClientTitle = "EVE";

		/// <summary>
		/// Resolve o tamanho inicial de uma miniatura recém-criada.
		/// O tamanho por cliente só vale para clientes já logados; a thumb de login
		/// usa sempre o tamanho global configurado.
		/// </summary>
		public static Size ResolveInitialThumbnailSize(Size configurationSize, Size? perClientSize, string title)
		{
			if (!perClientSize.HasValue || string.IsNullOrEmpty(title) || title == DefaultClientTitle)
			{
				return configurationSize;
			}

			return perClientSize.Value;
		}

		/// <summary>
		/// Aplica os limites configurados a um tamanho proposto.
		/// </summary>
		public static Size ClampToLimits(Size size, Size minimumSize, Size maximumSize)
		{
			return new Size(
				ClampDimension(size.Width, minimumSize.Width, maximumSize.Width),
				ClampDimension(size.Height, minimumSize.Height, maximumSize.Height));
		}

		/// <summary>
		/// Indica se um resize deve ser propagado para as demais miniaturas e para a
		/// configuração. Só resizes do usuário contam e o tamanho proposto é comparado
		/// já com os limites aplicados, para que um clamp não gere propagação inútil.
		/// </summary>
		public static bool ShouldPropagateResize(Size currentSize, Size proposedSize, Size minimumSize, Size maximumSize, bool isUserResize)
		{
			if (!isUserResize)
			{
				return false;
			}

			return ClampToLimits(proposedSize, minimumSize, maximumSize) != currentSize;
		}

		private static int ClampDimension(int value, int minimum, int maximum)
		{
			if (maximum < minimum)
			{
				maximum = minimum;
			}

			return Math.Min(Math.Max(value, minimum), maximum);
		}
	}
}
