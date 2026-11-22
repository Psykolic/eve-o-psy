using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using EveOPreview.Configuration;
using EveOPreview.UI.Hotkeys;

namespace EveOPreview.View
{
	/// <summary>
	/// Edição de um grupo de ciclo: uma tecla para avançar, uma para retroceder e a
	/// escolha/ordem dos clientes.
	///
	/// Só existe porque configurar grupo dependia de editar o JSON — e quem não acha o
	/// arquivo não configura nada.
	/// </summary>
	internal sealed class CycleGroupDialog : Form
	{
		private readonly CycleGroupEditModel _model;

		private TextBox _forwardBox;
		private TextBox _backwardBox;
		private CheckedListBox _clientsList;
		private Label _extraLabel;

		public CycleGroupDialog(CycleGroupEditModel model)
		{
			this._model = model;

			this.Text = "Grupo " + model.Number;
			this.FormBorderStyle = FormBorderStyle.FixedDialog;
			this.StartPosition = FormStartPosition.CenterParent;
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.ShowInTaskbar = false;

			this.BuildLayout();
			this.LoadFromModel();
		}

		/// <summary>Devolve o modelo preenchido ou <c>null</c> quando cancelado.</summary>
		public static CycleGroupEditModel Edit(IWin32Window parent, CycleGroupEditModel model)
		{
			using (var dialog = new CycleGroupDialog(model))
			{
				return dialog.ShowDialog(parent) == DialogResult.OK ? dialog._model : null;
			}
		}

		private void BuildLayout()
		{
			int y = 12;

			this.AddLabel("Avançar (uma tecla):", ref y);
			this._forwardBox = this.AddHotkeyBox(ref y);

			this.AddLabel("Retroceder (uma tecla):", ref y);
			this._backwardBox = this.AddHotkeyBox(ref y);

			this.AddLabel("Clientes (● = aberto agora; marque e use ▲▼ para ordenar):", ref y);

			this._clientsList = new CheckedListBox
			{
				Left = 12,
				Top = y,
				Width = 330,
				Height = 170,
				CheckOnClick = true,
				IntegralHeight = false
			};
			this.Controls.Add(this._clientsList);

			var up = new Button { Text = "▲", Left = 350, Top = y, Width = 50, Height = 30 };
			var down = new Button { Text = "▼", Left = 350, Top = y + 36, Width = 50, Height = 30 };
			up.Click += delegate { this.MoveSelected(-1); };
			down.Click += delegate { this.MoveSelected(1); };
			this.Controls.Add(up);
			this.Controls.Add(down);
			y += 178;

			this._extraLabel = new Label
			{
				Left = 12,
				Top = y,
				Width = 390,
				Height = 32,
				ForeColor = SystemColors.GrayText,
				AutoEllipsis = true
			};
			this.Controls.Add(this._extraLabel);
			y += 36;

			var ok = new Button { Text = "OK", Left = 236, Top = y, Width = 80, DialogResult = DialogResult.OK };
			var cancel = new Button { Text = "Cancelar", Left = 322, Top = y, Width = 80, DialogResult = DialogResult.Cancel };
			this.Controls.Add(ok);
			this.Controls.Add(cancel);
			this.AcceptButton = ok;
			this.CancelButton = cancel;

			this.ClientSize = new Size(414, y + 42);
		}

		private void AddLabel(string text, ref int y)
		{
			this.Controls.Add(new Label { Text = text, Left = 12, Top = y, Width = 390, Height = 18 });
			y += 20;
		}

		private TextBox AddHotkeyBox(ref int y)
		{
			var box = new TextBox
			{
				Left = 12,
				Top = y,
				Width = 200,
				ReadOnly = true,
				ShortcutsEnabled = false
			};
			box.KeyDown += this.CaptureHotkey;
			this.Controls.Add(box);
			y += 32;
			return box;
		}

		/// <summary>
		/// Captura a combinação no mesmo formato do arquivo ("F14", "Control+F14").
		/// Backspace/Delete limpa.
		/// </summary>
		private void CaptureHotkey(object sender, KeyEventArgs e)
		{
			e.SuppressKeyPress = true;
			e.Handled = true;

			var box = (TextBox)sender;

			if (e.KeyCode == Keys.Back || e.KeyCode == Keys.Delete)
			{
				box.Text = string.Empty;
				return;
			}

			// Só modificador pressionado não é atalho.
			if (e.KeyCode == Keys.ControlKey || e.KeyCode == Keys.ShiftKey || e.KeyCode == Keys.Menu)
			{
				return;
			}

			string texto = HotkeyConverter.ToInvariantString(e.KeyCode | e.Modifiers);
			box.Text = texto.Replace("Ctrl+", "Control+").Replace("Alt+", "Alt+");
		}

		private void MoveSelected(int delta)
		{
			int atual = this._clientsList.SelectedIndex;
			int destino = atual + delta;

			if (atual < 0 || destino < 0 || destino >= this._clientsList.Items.Count)
			{
				return;
			}

			bool marcado = this._clientsList.GetItemChecked(atual);
			object item = this._clientsList.Items[atual];

			this._clientsList.Items.RemoveAt(atual);
			this._clientsList.Items.Insert(destino, item);
			this._clientsList.SetItemChecked(destino, marcado);
			this._clientsList.SelectedIndex = destino;
		}

		private void LoadFromModel()
		{
			this._forwardBox.Text = this._model.Forward;
			this._backwardBox.Text = this._model.Backward;

			foreach (CycleGroupClient cliente in this._model.Clients)
			{
				int indice = this._clientsList.Items.Add(cliente);
				this._clientsList.SetItemChecked(indice, this._model.SelectedClients
					.Contains(cliente.Title, StringComparer.OrdinalIgnoreCase));
			}

			var extras = this._model.ExtraForward.Concat(this._model.ExtraBackward).ToList();
			if (extras.Count > 0)
			{
				this._extraLabel.Text = "Também mantém: " + string.Join(", ", extras);
			}
		}

		protected override void OnFormClosing(FormClosingEventArgs e)
		{
			if (this.DialogResult == DialogResult.OK)
			{
				this._model.Forward = this._forwardBox.Text.Trim();
				this._model.Backward = this._backwardBox.Text.Trim();

				this._model.SelectedClients.Clear();
				foreach (object item in this._clientsList.CheckedItems)
				{
					if (item is CycleGroupClient cliente)
					{
						this._model.SelectedClients.Add(cliente.Title);
					}
				}
			}

			base.OnFormClosing(e);
		}
	}
}
