using System;
using System.Drawing;
using System.Windows.Forms;

namespace EveOPreview.View
{
	/// <summary>
	/// ComboBox com o campo fechado desenhado pela paleta do tema.
	///
	/// Em DropDownList o WinForms/Windows pinta o campo com a cor do sistema depois do
	/// owner-draw (o our DrawItem cuida só dos itens da lista). Aqui o campo é repintado
	/// depois do WM_PAINT nativo, com a seta desenhada à mão.
	/// </summary>
	internal sealed class ThemedComboBox : ComboBox
	{
		private const int WM_PAINT = 0x000F;

		protected override void WndProc(ref Message m)
		{
			base.WndProc(ref m);

			if (m.Msg != ThemedComboBox.WM_PAINT || this.DropDownStyle != ComboBoxStyle.DropDownList || !Theme.Dark)
			{
				return;
			}

			using (Graphics grafico = Graphics.FromHwnd(this.Handle))
			{
				Rectangle area = new Rectangle(1, 1, Math.Max(1, this.Width - 2), Math.Max(1, this.Height - 2));

				using (SolidBrush fundo = new SolidBrush(Theme.Input))
				{
					grafico.FillRectangle(fundo, area);
				}

				string texto = this.GetItemText(this.SelectedItem);
				Rectangle areaTexto = new Rectangle(area.Left + 4, area.Top, Math.Max(1, area.Width - 22), area.Height);
				TextRenderer.DrawText(
					grafico,
					texto,
					this.Font,
					areaTexto,
					Theme.Ink,
					TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

				int setaX = area.Right - 13;
				int setaY = area.Top + (area.Height / 2) - 1;
				using (Pen seta = new Pen(Theme.Muted, 1.6f))
				{
					grafico.DrawLine(seta, setaX, setaY, setaX + 4, setaY + 3);
					grafico.DrawLine(seta, setaX + 4, setaY + 3, setaX + 8, setaY);
				}
			}
		}
	}
}
