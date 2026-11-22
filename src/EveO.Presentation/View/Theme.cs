using System.Drawing;
using System.Windows.Forms;

namespace EveOPreview.View
{
	/// <summary>
	/// Paleta e aplicação do tema escuro (fase 3 do estudo de layout).
	///
	/// O WinForms não tem tema: cada controle precisa receber cor. A paleta vem do mockup
	/// do mantenedor (azul-marinho + acento mint). O tema claro continua sendo o do sistema.
	/// </summary>
	internal static class Theme
	{
		public static readonly Color Surface = Color.FromArgb(17, 30, 49);
		public static readonly Color Alt = Color.FromArgb(21, 38, 60);
		public static readonly Color Input = Color.FromArgb(14, 25, 42);
		public static readonly Color Line = Color.FromArgb(51, 73, 98);
		public static readonly Color Ink = Color.FromArgb(234, 244, 255);
		public static readonly Color Muted = Color.FromArgb(173, 192, 212);
		public static readonly Color Accent = Color.FromArgb(115, 235, 200);
		public static readonly Color Selected = Color.FromArgb(25, 61, 65);

		/// <summary>Estado atual; os controles com desenho próprio consultam isto.</summary>
		public static bool Dark { get; private set; }

		public static Color SurfaceColor => Dark ? Surface : SystemColors.Control;
		public static Color BoxBorder => Dark ? Line : SystemColors.ControlDark;
		public static Color BoxFill => Dark ? Input : SystemColors.Window;
		public static Color BoxText => Dark ? Ink : SystemColors.ControlText;
		public static Color BoxDisabledText => Dark ? Muted : SystemColors.GrayText;
		public static Color CheckMark => Dark ? Accent : SystemColors.ControlText;

		/// <summary>Aplica a paleta em toda a árvore de controles.</summary>
		public static void Apply(Control raiz, bool escuro)
		{
			Dark = escuro;			raiz.BackColor = escuro ? Surface : SystemColors.Control;
			raiz.ForeColor = escuro ? Ink : SystemColors.ControlText;

			foreach (Control filho in raiz.Controls)
			{
				AplicarControle(filho, escuro);
				Apply(filho, escuro);
			}
		}

		/// <summary>Desenha o item do combo (fechado e aberto) com a paleta do tema.</summary>
		private static void DesenharItemCombo(object remetente, DrawItemEventArgs e)
		{
			ComboBox combo = (ComboBox)remetente;
			Color fundo = Dark ? Input : SystemColors.Window;
			Color tinta = Dark ? Ink : SystemColors.ControlText;

			if ((e.State & DrawItemState.Selected) != 0 && Dark)
			{
				fundo = Selected;
			}

			using (SolidBrush pincel = new SolidBrush(fundo))
			{
				e.Graphics.FillRectangle(pincel, e.Bounds);
			}

			string texto = e.Index >= 0 && e.Index < combo.Items.Count
				? combo.GetItemText(combo.Items[e.Index])
				: combo.Text;

			TextRenderer.DrawText(
				e.Graphics,
				texto,
				combo.Font,
				e.Bounds,
				tinta,
				TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
		}

		private static void AplicarControle(Control controle, bool escuro)
		{
			Color superficie = escuro ? Surface : SystemColors.Control;
			Color tinta = escuro ? Ink : SystemColors.ControlText;
			Color entrada = escuro ? Input : SystemColors.Window;

			switch (controle)
			{
				case LinkLabel link:
					link.BackColor = Color.Transparent;
					link.LinkColor = escuro ? Accent : SystemColors.HotTrack;
					link.ActiveLinkColor = escuro ? Color.White : Color.Red;
					link.VisitedLinkColor = escuro ? Accent : Color.Purple;
					break;

				case Label rotulo:
					rotulo.BackColor = Color.Transparent;
					rotulo.ForeColor = tinta;
					break;

				case CheckBox caixa:
					caixa.BackColor = Color.Transparent;
					caixa.ForeColor = tinta;
					caixa.FlatStyle = escuro ? FlatStyle.Flat : FlatStyle.Standard;
					break;

				case RadioButton botao:
					botao.BackColor = Color.Transparent;
					botao.ForeColor = tinta;
					botao.FlatStyle = escuro ? FlatStyle.Flat : FlatStyle.Standard;
					break;

				case Button botao:
					botao.BackColor = escuro ? Alt : SystemColors.Control;
					botao.ForeColor = tinta;
					botao.FlatStyle = FlatStyle.Flat;
					botao.FlatAppearance.BorderColor = escuro ? Line : SystemColors.ControlDark;
					break;

				case TextBox texto:
					texto.BackColor = entrada;
					texto.ForeColor = tinta;
					texto.BorderStyle = BorderStyle.FixedSingle;
					break;

				case NumericUpDown numero:
					numero.BackColor = entrada;
					numero.ForeColor = tinta;
					numero.BorderStyle = BorderStyle.FixedSingle;
					break;

				case ComboBox combo:
					combo.BackColor = entrada;
					combo.ForeColor = tinta;
					combo.FlatStyle = escuro ? FlatStyle.Flat : FlatStyle.Standard;
					combo.DrawMode = DrawMode.OwnerDrawFixed;
					combo.DrawItem -= DesenharItemCombo;
					combo.DrawItem += DesenharItemCombo;

					// O campo fechado do DropDownList é desenhado uma vez só; sem um
					// repaint explícito ele fica com as cores anteriores ao tema.
					combo.Refresh();
					break;

				case CheckedListBox lista:
					lista.BackColor = entrada;
					lista.ForeColor = tinta;
					lista.BorderStyle = BorderStyle.FixedSingle;
					break;

				case ListBox lista:
					lista.BackColor = entrada;
					lista.ForeColor = tinta;
					break;

				case TreeView arvore:
					arvore.BackColor = entrada;
					arvore.ForeColor = tinta;
					arvore.LineColor = escuro ? Line : SystemColors.ControlDark;
					break;

				case Panel painel:
					painel.BackColor = superficie;
					break;

				case PictureBox imagem:
					imagem.BackColor = superficie;
					break;
			}
		}
	}
}
