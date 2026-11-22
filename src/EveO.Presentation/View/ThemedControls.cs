using System;
using System.Drawing;
using System.Windows.Forms;

namespace EveOPreview.View
{
	/// <summary>
	/// CheckBox desenhado com a paleta do tema. O glifo nativo do Windows é branco e
	/// briga com o fundo escuro; aqui a caixa usa as cores do tema.
	/// </summary>
	internal sealed class ThemedCheckBox : CheckBox
	{
		public ThemedCheckBox()
		{
			this.SetStyle(
				ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw,
				true);
		}

		public override Size GetPreferredSize(Size proposedSize)
		{
			Size texto = TextRenderer.MeasureText(this.Text, this.Font);

			return new Size(texto.Width + 22, Math.Max(19, texto.Height));
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			Graphics grafico = e.Graphics;
			Color fundo = this.Parent != null ? this.Parent.BackColor : Theme.SurfaceColor;
			Color tinta = this.Enabled ? this.ForeColor : Theme.BoxDisabledText;

			using (SolidBrush pincel = new SolidBrush(fundo))
			{
				grafico.FillRectangle(pincel, this.ClientRectangle);
			}

			const int lado = 13;
			Rectangle caixa = new Rectangle(0, (this.ClientSize.Height - lado) / 2, lado, lado);

			using (SolidBrush preenchimento = new SolidBrush(this.Enabled ? Theme.BoxFill : fundo))
			{
				grafico.FillRectangle(preenchimento, caixa);
			}

			using (Pen borda = new Pen(this.Enabled ? Theme.BoxBorder : Theme.BoxDisabledText))
			{
				grafico.DrawRectangle(borda, caixa);
			}

			if (this.CheckState == CheckState.Checked)
			{
				using (Pen marca = new Pen(Theme.CheckMark, 2f))
				{
					grafico.DrawLines(marca, new[]
					{
						new Point(caixa.Left + 3, caixa.Top + 7),
						new Point(caixa.Left + 6, caixa.Top + 10),
						new Point(caixa.Left + 11, caixa.Top + 3)
					});
				}
			}
			else if (this.CheckState == CheckState.Indeterminate)
			{
				using (Pen traco = new Pen(Theme.CheckMark, 2f))
				{
					grafico.DrawLine(traco, caixa.Left + 3, caixa.Top + 6, caixa.Right - 4, caixa.Top + 6);
				}
			}

			Rectangle areaTexto = new Rectangle(caixa.Right + 4, 0, this.ClientSize.Width - caixa.Right - 4, this.ClientSize.Height);
			TextRenderer.DrawText(
				grafico,
				this.Text,
				this.Font,
				areaTexto,
				tinta,
				TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);

			if (this.Focused)
			{
				ControlPaint.DrawFocusRectangle(grafico, areaTexto, tinta, fundo);
			}
		}
	}

	/// <summary>RadioButton com o mesmo desenho do tema (círculo + ponto).</summary>
	internal sealed class ThemedRadioButton : RadioButton
	{
		public ThemedRadioButton()
		{
			this.SetStyle(
				ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw,
				true);
		}

		public override Size GetPreferredSize(Size proposedSize)
		{
			Size texto = TextRenderer.MeasureText(this.Text, this.Font);

			return new Size(texto.Width + 22, Math.Max(19, texto.Height));
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			Graphics grafico = e.Graphics;
			Color fundo = this.Parent != null ? this.Parent.BackColor : Theme.SurfaceColor;
			Color tinta = this.Enabled ? this.ForeColor : Theme.BoxDisabledText;

			using (SolidBrush pincel = new SolidBrush(fundo))
			{
				grafico.FillRectangle(pincel, this.ClientRectangle);
			}

			const int lado = 13;
			Rectangle circulo = new Rectangle(0, (this.ClientSize.Height - lado) / 2, lado, lado);

			using (SolidBrush preenchimento = new SolidBrush(this.Enabled ? Theme.BoxFill : fundo))
			{
				grafico.FillEllipse(preenchimento, circulo);
			}

			using (Pen borda = new Pen(this.Enabled ? Theme.BoxBorder : Theme.BoxDisabledText))
			{
				grafico.DrawEllipse(borda, circulo);
			}

			if (this.Checked)
			{
				using (SolidBrush ponto = new SolidBrush(Theme.CheckMark))
				{
					grafico.FillEllipse(ponto, circulo.Left + 4, circulo.Top + 4, 5, 5);
				}
			}

			Rectangle areaTexto = new Rectangle(circulo.Right + 4, 0, this.ClientSize.Width - circulo.Right - 4, this.ClientSize.Height);
			TextRenderer.DrawText(
				grafico,
				this.Text,
				this.Font,
				areaTexto,
				tinta,
				TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);

			if (this.Focused)
			{
				ControlPaint.DrawFocusRectangle(grafico, areaTexto, tinta, fundo);
			}
		}
	}
}
