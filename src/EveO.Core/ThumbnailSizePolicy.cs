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

		/// <summary>
		/// Eixo alterado pelo usuário no painel de configuração.
		/// </summary>
		public enum Dimension
		{
			Width,
			Height
		}

		/// <summary>
		/// Recalcula o par largura/altura mantendo a proporção vigente (issue #140).
		/// A proporção vem do par anterior; o eixo alterado é limitado aos limites
		/// configurados e o outro eixo acompanha, também respeitando os limites.
		/// </summary>
		public static Size CalculateAspectRatioSize(
			Size previousSize,
			Dimension changedDimension,
			int newValue,
			Size minimumSize,
			Size maximumSize,
			bool keepAspectRatio)
		{
			newValue = ClampDimension(newValue, 1, 100000);

			if (!keepAspectRatio || previousSize.Width <= 0 || previousSize.Height <= 0)
			{
				return changedDimension == Dimension.Width
					? new Size(newValue, previousSize.Height)
					: new Size(previousSize.Width, newValue);
			}

			double ratio = (double)previousSize.Width / previousSize.Height;

			if (changedDimension == Dimension.Width)
			{
				int width = ClampDimension(newValue, minimumSize.Width, maximumSize.Width);
				int height = (int)Math.Round(width / ratio, MidpointRounding.AwayFromZero);

				if (height < minimumSize.Height)
				{
					height = minimumSize.Height;
					width = (int)Math.Round(height * ratio, MidpointRounding.AwayFromZero);
				}
				else if (height > maximumSize.Height)
				{
					height = maximumSize.Height;
					width = (int)Math.Round(height * ratio, MidpointRounding.AwayFromZero);
				}

				return new Size(ClampDimension(width, minimumSize.Width, maximumSize.Width), height);
			}

			int altura = ClampDimension(newValue, minimumSize.Height, maximumSize.Height);
			int largura = (int)Math.Round(altura * ratio, MidpointRounding.AwayFromZero);

			if (largura < minimumSize.Width)
			{
				largura = minimumSize.Width;
				altura = (int)Math.Round(largura / ratio, MidpointRounding.AwayFromZero);
			}
			else if (largura > maximumSize.Width)
			{
				largura = maximumSize.Width;
				altura = (int)Math.Round(largura / ratio, MidpointRounding.AwayFromZero);
			}

			return new Size(largura, ClampDimension(altura, minimumSize.Height, maximumSize.Height));
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
