using EveOPreview.Services;
using EveOPreview.Configuration;
using EveOPreview.Properties;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace EveOPreview.View
{
	public partial class MainForm : Form, IMainFormView
	{
		private System.Windows.Forms.TreeView GroupsTree;

		public Action OpenConfigurationFileRequested { get; set; }

		public Action<int> EditCycleGroupRequested { get; set; }

		public Action<int> ClearCycleGroupRequested { get; set; }

		public Action CreateCycleGroupRequested { get; set; }

		/// <summary>Diálogo de edição de grupo. Devolve null quando cancelado.</summary>
		public CycleGroupEditModel EditCycleGroup(CycleGroupEditModel model)
		{
			return CycleGroupDialog.Edit(this, model);
		}

		public void ShowGroupMessage(string message)
		{
			MessageBox.Show(message, "Grupos", MessageBoxButtons.OK, MessageBoxIcon.Information);
		}

		/// <summary>
		/// Mostra os cinco grupos de ciclo (atalhos e clientes na ordem de ciclagem).
		/// Só leitura: editar continua sendo no arquivo, que o botão abre.
		/// </summary>
		public void SetCycleGroups(IReadOnlyList<CycleGroupSummary> groups)
		{
			if (this.GroupsTree == null)
			{
				return;
			}

			this.GroupsTree.BeginUpdate();
			try
			{
				this.GroupsTree.Nodes.Clear();

				foreach (CycleGroupSummary grupo in groups ?? new List<CycleGroupSummary>())
				{
					var node = new TreeNode(this.DescribeGroup(grupo)) { Tag = grupo.Number };

					if (grupo.Clients.Count == 0)
					{
						node.ForeColor = SystemColors.GrayText;
					}
					else
					{
						foreach (string cliente in grupo.Clients)
						{
							node.Nodes.Add(cliente);
						}

						node.Expand();
					}

					this.GroupsTree.Nodes.Add(node);
				}
			}
			finally
			{
				this.GroupsTree.EndUpdate();
			}
		}

		/// <summary>
		/// Uma linha por grupo, curta o bastante para a aba (327 px): sem colunas, sem
		/// rolagem horizontal. Os clientes ficam como filhos, na ordem de ciclagem.
		/// </summary>
		/// <summary>Grupo selecionado na árvore (o Tag carrega o número).</summary>
		private void RaiseGroupAction(Action<int> action)
		{
			if (action == null)
			{
				return;
			}

			if (this.GroupsTree?.SelectedNode?.Tag is int grupo)
			{
				action(grupo);
			}
		}

		private string DescribeGroup(CycleGroupSummary grupo)
		{
			if (grupo.ForwardHotkeys.Count == 0 && grupo.BackwardHotkeys.Count == 0)
			{
				return "grupo " + grupo.Number + " — sem atalho";
			}

			string avancar = grupo.ForwardHotkeys.Count == 0
				? "—"
				: string.Join(" / ", grupo.ForwardHotkeys);
			string voltar = grupo.BackwardHotkeys.Count == 0
				? "—"
				: string.Join(" / ", grupo.BackwardHotkeys);

			return "grupo " + grupo.Number + " — " + avancar + "  ⟲ " + voltar;
		}

		private void BuildCycleGroupsPanel()
		{
			this._groupsPanel = new Panel
			{
				Size = new Size(327, 243),
				BackColor = SystemColors.Control
			};

			this.GroupsTree = new System.Windows.Forms.TreeView
			{
				Dock = DockStyle.Fill,
				HideSelection = false,
				ShowLines = false,
				BorderStyle = BorderStyle.FixedSingle,
				ItemHeight = 20
			};

			var dica = new Label
			{
				Dock = DockStyle.Top,
				Height = 30,
				Text = "Somente leitura",
				TextAlign = ContentAlignment.MiddleLeft,
				AutoEllipsis = true
			};

			var rodape = new Panel
			{
				Dock = DockStyle.Bottom,
				Height = 40,
				Padding = new Padding(0, 6, 4, 6)
			};

			var editar = new System.Windows.Forms.Button
			{
				Text = "Editar",
				Width = 70,
				Dock = DockStyle.Right
			};
			editar.Click += delegate { this.RaiseGroupAction(this.EditCycleGroupRequested); };

			var limpar = new System.Windows.Forms.Button
			{
				Text = "Limpar",
				Width = 70,
				Dock = DockStyle.Right
			};
			limpar.Click += delegate { this.RaiseGroupAction(this.ClearCycleGroupRequested); };

			var novo = new System.Windows.Forms.Button
			{
				Text = "Novo",
				Width = 60,
				Dock = DockStyle.Right
			};
			novo.Click += delegate { this.CreateCycleGroupRequested?.Invoke(); };

			var abrirArquivo = new System.Windows.Forms.Button
			{
				Text = "Arquivo…",
				Width = 80,
				Dock = DockStyle.Right
			};
			abrirArquivo.Click += delegate { this.OpenConfigurationFileRequested?.Invoke(); };

			// Dock.Right empilha da direita para a esquerda: adicionar nesta ordem deixa
			// Arquivo | Novo | Limpar | Editar.
			rodape.Controls.Add(abrirArquivo);
			rodape.Controls.Add(novo);
			rodape.Controls.Add(limpar);
			rodape.Controls.Add(editar);

			this.GroupsTree.DoubleClick += delegate { this.RaiseGroupAction(this.EditCycleGroupRequested); };

			// Ordem de docking: Fill primeiro, bordas depois (o Fill consome o que sobra).
			this._groupsPanel.Controls.Add(this.GroupsTree);
			this._groupsPanel.Controls.Add(rodape);
			this._groupsPanel.Controls.Add(dica);

			this.SetCycleGroups(null);
		}

		/// <summary>
		/// Monta a navegação em blocos (Layout 3 sem tema): grade 4×2 no topo e um host
		/// único para as telas, no lugar do TabControl lateral.
		/// </summary>
		private void BuildLayout()
		{
			this.SuspendLayout();

			this.ContentTabControl.Visible = false;
			this.ClientSize = new Size(455, 388);

			this._contentHost = new Panel
			{
				Dock = DockStyle.Fill,
				Padding = new Padding(4)
			};

			TableLayoutPanel nav = new TableLayoutPanel
			{
				Dock = DockStyle.Top,
				Height = 128,
				ColumnCount = 4,
				RowCount = 2,
				Padding = new Padding(6),
				BackColor = SystemColors.Control
			};
			for (int coluna = 0; coluna < 4; coluna++)
			{
				nav.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
			}
			for (int linha = 0; linha < 2; linha++)
			{
				nav.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
			}

			var telas = new List<(string Titulo, string Glifo, Panel Conteudo)>
			{
				("General", "\uE713", (Panel)this.ContentTabControl.TabPages[0].Controls[0]),
				("Thumbnail", "\uE890", (Panel)this.ContentTabControl.TabPages[1].Controls[0]),
				("Zoom", "\uE71E", (Panel)this.ContentTabControl.TabPages[2].Controls[0]),
				("Overlay", "\uE7C3", (Panel)this.ContentTabControl.TabPages[3].Controls[0]),
				("Active Clients", "\uE716", (Panel)this.ContentTabControl.TabPages[4].Controls[0]),
				("Grupos", "\uE8FD", this._groupsPanel),
				("Spotlight", "\uE706", (Panel)this.ContentTabControl.TabPages[5].Controls[0]),
				("About", "\uE946", (Panel)this.ContentTabControl.TabPages[6].Controls[0])
			};

			for (int indice = 0; indice < telas.Count; indice++)
			{
				(string titulo, string glifo, Panel conteudo) = telas[indice];

				conteudo.Visible = false;
				conteudo.Location = new Point(4, 4);
				this._contentHost.Controls.Add(conteudo);
				this._layoutPages.Add(conteudo);

				System.Windows.Forms.Button botao = this.CreateNavButton(titulo, glifo);
				int destino = indice;
				botao.Click += delegate { this.ShowLayoutPage(destino); };
				nav.Controls.Add(botao, indice % 4, indice / 4);
				this._layoutButtons.Add(botao);
			}

			// Ordem de docking: o host (Fill) entra primeiro e a navegação depois; o
			// WinForms processa o docking do fim da coleção para o começo.
			this.Controls.Add(this._contentHost);
			this.Controls.Add(nav);

			this._contentHost.Resize += delegate { this.CenterLayoutPages(); };

			this.ShowLayoutPage(0);

			this.ResumeLayout();
		}

		private System.Windows.Forms.Button CreateNavButton(string titulo, string glifo)
		{
			System.Windows.Forms.Button botao = new System.Windows.Forms.Button
			{
				Dock = DockStyle.Fill,
				Margin = new Padding(3),
				FlatStyle = FlatStyle.Flat,
				Text = titulo,
				TextAlign = ContentAlignment.BottomCenter,
				Image = this.CreateGlyphImage(glifo),
				ImageAlign = ContentAlignment.TopCenter,
				TextImageRelation = TextImageRelation.ImageAboveText,
				Font = this._navFont,
				BackColor = SystemColors.Control,
				UseVisualStyleBackColor = false
			};
			botao.FlatAppearance.BorderColor = SystemColors.ControlDark;

			return botao;
		}

		private Image CreateGlyphImage(string glifo)
		{
			Bitmap bitmap = new Bitmap(20, 20);
			using (Graphics graphics = Graphics.FromImage(bitmap))
			using (Font fonte = new Font("Segoe MDL2 Assets", 11f, FontStyle.Regular, GraphicsUnit.Point))
			using (SolidBrush pincel = new SolidBrush(SystemColors.ControlText))
			{
				Size tamanho = TextRenderer.MeasureText(glifo, fonte);
				graphics.DrawString(glifo, fonte, pincel, (20 - tamanho.Width) / 2f, (20 - tamanho.Height) / 2f);
			}

			return bitmap;
		}

		private void ShowLayoutPage(int indice)
		{
			for (int i = 0; i < this._layoutPages.Count; i++)
			{
				bool selecionada = i == indice;
				this._layoutPages[i].Visible = selecionada;

				System.Windows.Forms.Button botao = this._layoutButtons[i];
				botao.BackColor = selecionada ? SystemColors.Window : SystemColors.Control;
				botao.Font = selecionada ? this._navFontSelected : this._navFont;
			}

			this.CenterLayoutPages();
		}

		private void CenterLayoutPages()
		{
			int largura = Math.Max(320, this._contentHost.ClientSize.Width - 8);

			foreach (Panel pagina in this._layoutPages)
			{
				if (pagina.Visible)
				{
					pagina.Location = new Point(4, 4);
					pagina.Width = largura;
				}
			}
		}
		#region Private fields
		private readonly ApplicationContext _context;
		private readonly Dictionary<ViewZoomAnchor, RadioButton> _zoomAnchorMap;
		private readonly Dictionary<ViewZoomAnchor, RadioButton> _overlayLabelMap;
		private readonly Dictionary<ViewZoomAnchor, RadioButton> _cycleGroupIndicatorMap;
		private ViewZoomAnchor _cachedThumbnailZoomAnchor;
		private ViewZoomAnchor _cachedOverlayLabelAnchor;
		private ViewZoomAnchor _cachedCycleGroupIndicatorAnchor;
		private bool _suppressEvents;
		private Size _minimumSize;
		private Size _maximumSize;
		private string _iconName;
		private readonly HashSet<string> _spotlightClients;
		private Rectangle _selectedSpotlightRegion;
		private Size _lastThumbnailSize;
		private readonly Dictionary<string, string> _clientLabels;
		private bool _suppressClientLabelEvents;
		private bool _thumbnailListTextClick;
		private readonly List<Panel> _layoutPages;
		private readonly List<System.Windows.Forms.Button> _layoutButtons;
		private readonly Font _navFont;
		private readonly Font _navFontSelected;
		private Panel _groupsPanel;
		private Panel _contentHost;
		#endregion

		#region Private constants
		private const string DEFAULT_CLIENT_TITLE = "EVE";
		#endregion

		public MainForm(ApplicationContext context)
		{
			this._context = context;
			this._zoomAnchorMap = new Dictionary<ViewZoomAnchor, RadioButton>();
			this._overlayLabelMap = new Dictionary<ViewZoomAnchor, RadioButton>();
			this._cycleGroupIndicatorMap = new Dictionary<ViewZoomAnchor, RadioButton>();
			this._cachedThumbnailZoomAnchor = ViewZoomAnchor.NW;
			this._suppressEvents = false;
			this._minimumSize = new Size(20, 20);
			this._maximumSize = new Size(20, 20);
			this._spotlightClients = new HashSet<string>();
			this._clientLabels = new Dictionary<string, string>();
			this._layoutPages = new List<Panel>();
			this._layoutButtons = new List<System.Windows.Forms.Button>();
			this._navFont = new Font(this.Font.FontFamily, 8.25f, FontStyle.Regular, GraphicsUnit.Point);
			this._navFontSelected = new Font(this.Font.FontFamily, 8.25f, FontStyle.Bold, GraphicsUnit.Point);

			InitializeComponent();

			this.ThumbnailsList.DisplayMember = "Title";

			this.InitZoomAnchorMap();
			this.InitOverlayLabelMap();
			this.InitCycleGroupIndicatorMap();

			this.AnimationStyleCombo.DataSource = Enum.GetValues(typeof(AnimationStyle));

			// A aba Grupos e construida em codigo (nao no Designer gerado) e a navegacao
			// em blocos substitui o TabControl: as telas viram paineis num host unico.
			this.BuildCycleGroupsPanel();
			this.BuildLayout();
		}

		public bool MinimizeToTray
		{
			get => this.MinimizeToTrayCheckBox.Checked;
			set => this.MinimizeToTrayCheckBox.Checked = value;
		}

		public string IconName
		{
			get => this._iconName;
			set
			{


				this._iconName = value;

				// Set Icon
				System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
				if (this._iconName == null || ((resources.GetObject(this._iconName))) == null)
				{
					this._iconName = "IconOriginal";
				}

				// pull icon from resources
				try
				{
					var iconBytes = (byte[])resources.GetObject(this._iconName);
					using (MemoryStream ms = new MemoryStream(iconBytes))
					{
						this.Icon = new Icon(ms);
						this.NotifyIcon.Icon = this.Icon;
					}
				}
				catch (Exception)
				{
					// Log ?
				}

				if (value != "")
				{
					this.ApplicationSettingsChanged?.Invoke();
				}
			}
		}

		public double ThumbnailOpacity
		{
			get => Math.Min(this.ThumbnailOpacityTrackBar.Value / 100.00, 1.00);
			set
			{
				int barValue = (int)(100.0 * value);
				if (barValue > 100)
				{
					barValue = 100;
				}
				else if (barValue < 10)
				{
					barValue = 10;
				}

				this.ThumbnailOpacityTrackBar.Value = barValue;
			}
		}

		public double SpotlightOpacity
		{
			get => Math.Min(this.SpotlightOpacityTrackBar.Value / 100.00, 1.00);
			set
			{
				int barValue = (int)(100.0 * value);
				if (barValue > 100)
				{
					barValue = 100;
				}
				else if (barValue < 20)
				{
					barValue = 20;
				}

				this.SpotlightOpacityTrackBar.Value = barValue;
			}
		}

		public bool EnableClientLayoutTracking
		{
			get => this.EnableClientLayoutTrackingCheckBox.Checked;
			set => this.EnableClientLayoutTrackingCheckBox.Checked = value;
		}

		public bool HideActiveClientThumbnail
		{
			get => this.HideActiveClientThumbnailCheckBox.Checked;
			set => this.HideActiveClientThumbnailCheckBox.Checked = value;
		}

		public bool MinimizeInactiveClients
		{
			get => this.MinimizeInactiveClientsCheckBox.Checked;
			set => this.MinimizeInactiveClientsCheckBox.Checked = value;
		}
		public bool HideCaptionOnClients
		{
			get => this.HideCaptionOnClientsCheckBox.Checked;
			set => this.HideCaptionOnClientsCheckBox.Checked = value;
		}
		public ViewAnimationStyle WindowsAnimationStyle
		{
			get => (ViewAnimationStyle)this.AnimationStyleCombo.SelectedItem;
			set => this.AnimationStyleCombo.SelectedIndex = (int)value;
		}

		public bool ShowThumbnailsAlwaysOnTop
		{
			get => this.ShowThumbnailsAlwaysOnTopCheckBox.Checked;
			set => this.ShowThumbnailsAlwaysOnTopCheckBox.Checked = value;
		}
		public bool PreventPreviews
		{
			get => this.PreventPreviewsCheckBox.Checked;
			set => this.PreventPreviewsCheckBox.Checked = value;
		}

		public bool HideThumbnailsOnLostFocus
		{
			get => this.HideThumbnailsOnLostFocusCheckBox.Checked;
			set => this.HideThumbnailsOnLostFocusCheckBox.Checked = value;
		}

		public bool EnablePerClientThumbnailLayouts
		{
			get => this.EnablePerClientThumbnailsLayoutsCheckBox.Checked;
			set => this.EnablePerClientThumbnailsLayoutsCheckBox.Checked = value;
		}

		public Size ThumbnailSize
		{
			get => new Size((int)this.ThumbnailsWidthNumericEdit.Value, (int)this.ThumbnailsHeightNumericEdit.Value);
			set
			{
				this.ThumbnailsWidthNumericEdit.Value = value.Width;
				this.ThumbnailsHeightNumericEdit.Value = value.Height;

				// Guarda o par efetivamente aplicado: ele é a referência de proporção
				// para a próxima edição do usuário (issue #140).
				this._lastThumbnailSize = new Size(
					(int)this.ThumbnailsWidthNumericEdit.Value,
					(int)this.ThumbnailsHeightNumericEdit.Value);
			}
		}

		public bool KeepAspectRatio
		{
			get => this.KeepAspectRatioCheckBox.Checked;
			set => this.KeepAspectRatioCheckBox.Checked = value;
		}

		public bool EnableThumbnailZoom
		{
			get => this.EnableThumbnailZoomCheckBox.Checked;
			set
			{
				this.EnableThumbnailZoomCheckBox.Checked = value;
				this.RefreshZoomSettings();
			}
		}

		public int ThumbnailZoomFactor
		{
			get => (int)this.ThumbnailZoomFactorNumericEdit.Value;
			set => this.ThumbnailZoomFactorNumericEdit.Value = value;
		}

		public ViewZoomAnchor ThumbnailZoomAnchor
		{
			get
			{
				if (this._zoomAnchorMap[this._cachedThumbnailZoomAnchor].Checked)
				{
					return this._cachedThumbnailZoomAnchor;
				}

				foreach (KeyValuePair<ViewZoomAnchor, RadioButton> valuePair in this._zoomAnchorMap)
				{
					if (!valuePair.Value.Checked)
					{
						continue;
					}

					this._cachedThumbnailZoomAnchor = valuePair.Key;
					return this._cachedThumbnailZoomAnchor;
				}

				// Default value
				return ViewZoomAnchor.NW;
			}
			set
			{
				this._cachedThumbnailZoomAnchor = value;
				this._zoomAnchorMap[this._cachedThumbnailZoomAnchor].Checked = true;
			}
		}

		public ViewZoomAnchor OverlayLabelAnchor
		{
			get
			{
				if (this._overlayLabelMap[this._cachedOverlayLabelAnchor].Checked)
				{
					return this._cachedOverlayLabelAnchor;
				}

				foreach (KeyValuePair<ViewZoomAnchor, RadioButton> valuePair in this._overlayLabelMap)
				{
					if (!valuePair.Value.Checked)
					{
						continue;
					}

					this._cachedOverlayLabelAnchor = valuePair.Key;
					return this._cachedOverlayLabelAnchor;
				}

				// Default Value
				return ViewZoomAnchor.NW;
			}
			set
			{
				this._cachedOverlayLabelAnchor = value;
				this._overlayLabelMap[this._cachedOverlayLabelAnchor].Checked = true;
			}
		}

		public ViewZoomAnchor CycleGroupIndicatorAnchor
		{
			get
			{
				if (this._cycleGroupIndicatorMap[this._cachedCycleGroupIndicatorAnchor].Checked)
				{
					return this._cachedCycleGroupIndicatorAnchor;
				}

				foreach (KeyValuePair<ViewZoomAnchor, RadioButton> valuePair in this._cycleGroupIndicatorMap)
				{
					if (!valuePair.Value.Checked)
					{
						continue;
					}

					this._cachedCycleGroupIndicatorAnchor = valuePair.Key;
					return this._cachedCycleGroupIndicatorAnchor;
				}

				// Default Value
				return ViewZoomAnchor.NW;
			}
			set
			{
				this._cachedCycleGroupIndicatorAnchor = value;
				this._cycleGroupIndicatorMap[this._cachedCycleGroupIndicatorAnchor].Checked = true;
			}
		}

		public bool ShowThumbnailOverlays
		{
			get => this.ShowThumbnailOverlaysCheckBox.Checked;
			set => this.ShowThumbnailOverlaysCheckBox.Checked = value;
		}

		public bool ShowThumbnailFrames
		{
			get => this.ShowThumbnailFramesCheckBox.Checked;
			set => this.ShowThumbnailFramesCheckBox.Checked = value;
		}
		public bool LockThumbnailLocation
		{
			get => this.LockThumbnailLocationCheckbox.Checked;
			set => this.LockThumbnailLocationCheckbox.Checked = value;
		}
		public bool ThumbnailSnapToGrid
		{
			get => this.ThumbnailSnapToGridCheckBox.Checked;
			set => this.ThumbnailSnapToGridCheckBox.Checked = value;
		}
		public int ThumbnailSnapToGridSizeX
		{
			get => (int)ThumbnailSnapToGridSizeXNumericEdit.Value;
			set => ThumbnailSnapToGridSizeXNumericEdit.Value = value;
		}
		public int ThumbnailSnapToGridSizeY
		{
			get => (int)ThumbnailSnapToGridSizeYNumericEdit.Value;
			set => ThumbnailSnapToGridSizeYNumericEdit.Value = value;
		}

		public bool EnableActiveClientHighlight
		{
			get => this.EnableActiveClientHighlightCheckBox.Checked;
			set => this.EnableActiveClientHighlightCheckBox.Checked = value;
		}

		public Color ActiveClientHighlightColor
		{
			get => this._activeClientHighlightColor;
			set
			{
				this._activeClientHighlightColor = value;
				this.ActiveClientHighlightColorButton.BackColor = value;
			}
		}
		private Color _activeClientHighlightColor;

		public Color PreventPreviewColor
		{
			get => this._preventPreviewColor;
			set
			{
				this._preventPreviewColor = value;
				this.PreventPreviewColorButton.BackColor = value;
			}
		}
		private Color _preventPreviewColor;

		public Color OverlayLabelColor
		{
			get => this._OverlayLabelColor;
			set
			{
				this._OverlayLabelColor = value;
				this.OverlayLabelColorButton.BackColor = value;
			}
		}
		private Color _OverlayLabelColor;

		public Font OverlayLabelFont
		{
			get => (Font)this._OverlayLabelFont;
			set
			{
				this._OverlayLabelFont = value;
				this.LabelOverlayLabelFont.Font = value;
			}
		}
		private Font _OverlayLabelFont;

		public new void Show()
		{
			// Registers the current instance as the application's Main Form
			this._context.MainForm = this;

			this._suppressEvents = true;
			this.FormActivated?.Invoke();
			this._suppressEvents = false;

			Application.Run(this._context);
		}

		public void SetThumbnailSizeLimitations(Size minimumSize, Size maximumSize)
		{
			this._minimumSize = minimumSize;
			this._maximumSize = maximumSize;
		}

		public void Minimize()
		{
			this.WindowState = FormWindowState.Minimized;
		}

		public void SetVersionInfo(string version)
		{
			this.VersionLabel.Text = version;
		}

		public void SetDocumentationUrl(string url)
		{
			this.DocumentationLink.Text = url;
		}

		public void AddThumbnails(IList<IThumbnailDescription> thumbnails)
		{
			this.ThumbnailsList.BeginUpdate();
			this.SpotlightClientsList.BeginUpdate();

			foreach (IThumbnailDescription view in thumbnails)
			{
				this.ThumbnailsList.SetItemChecked(this.ThumbnailsList.Items.Add(view), view.IsDisabled);
				this.AddSpotlightClientCandidate(view.Title);
			}

			this.SpotlightClientsList.EndUpdate();
			this.ThumbnailsList.EndUpdate();
		}

		public void RemoveThumbnails(IList<IThumbnailDescription> thumbnails)
		{
			this.ThumbnailsList.BeginUpdate();
			this.SpotlightClientsList.BeginUpdate();

			foreach (IThumbnailDescription view in thumbnails)
			{
				this.ThumbnailsList.Items.Remove(view);
				int index = this.SpotlightClientsList.Items.IndexOf(view.Title);
				if (index >= 0)
				{
					this.SpotlightClientsList.Items.RemoveAt(index);
				}
			}

			this.SpotlightClientsList.EndUpdate();
			this.ThumbnailsList.EndUpdate();
		}

		/// <summary>
		/// Labels customizadas por cliente. O campo abaixo da lista edita a selecionada;
		/// vazio significa "usar os 8 primeiros caracteres do nome" (issue #151).
		/// </summary>
		public void SetClientLabels(IReadOnlyDictionary<string, string> labels)
		{
			this._clientLabels.Clear();
			if (labels != null)
			{
				foreach (KeyValuePair<string, string> pair in labels)
				{
					this._clientLabels[pair.Key] = pair.Value;
				}
			}

			this.UpdateClientLabelEditor();
		}

		private void UpdateClientLabelEditor()
		{
			this._suppressClientLabelEvents = true;
			try
			{
				if (!(this.ThumbnailsList.SelectedItem is IThumbnailDescription selectedItem))
				{
					this.ClientLabelTextBox.Text = string.Empty;
					this.ClientLabelTextBox.PlaceholderText = string.Empty;
					this.ClientLabelTextBox.Enabled = false;
					return;
				}

				this.ClientLabelTextBox.Enabled = true;
				this.ClientLabelTextBox.Text = this._clientLabels.TryGetValue(selectedItem.Title, out string label)
					? label
					: string.Empty;

				// O placeholder mostra o que aparece quando o campo fica vazio.
				this.ClientLabelTextBox.PlaceholderText = OverlayLabel.FromTitle(selectedItem.Title);
			}
			finally
			{
				this._suppressClientLabelEvents = false;
			}
		}

		public IList<string> SpotlightClients => new List<string>(this._spotlightClients);

		public string SelectedSpotlightClient => this.SpotlightClientsList.SelectedItem as string;

		public void SetSpotlightClients(IEnumerable<string> clients)
		{
			bool wasSuppressed = this._suppressEvents;
			this._suppressEvents = true;

			this._spotlightClients.Clear();
			foreach (string client in clients)
			{
				this._spotlightClients.Add(client);
			}

			foreach (string title in this._spotlightClients)
			{
				this.AddSpotlightClientCandidate(title);
			}

			for (int i = 0; i < this.SpotlightClientsList.Items.Count; i++)
			{
				string title = this.SpotlightClientsList.Items[i] as string;
				bool isSpotlight = title != null && this._spotlightClients.Contains(title);
				this.SpotlightClientsList.SetItemChecked(i, isSpotlight);
			}

			this._suppressEvents = wasSuppressed;
			this.UpdateSpotlightRegionControls();
		}

		public void SetSpotlightHotkeyInfo(string hotkeyInfo)
		{
			string hotkey = hotkeyInfo?.Replace("Control+", "Ctrl+") ?? string.Empty;

			this.SpotlightHotkeyLabel.Text = string.IsNullOrEmpty(hotkey)
				? "Drag edges to resize · Shift+right-drag to crop"
				: $"{hotkey} toggle · drag edges · Shift+right-drag crop";
		}

		public void SetSpotlightRegionInfo(string title, Rectangle region)
		{
			if (!string.Equals(title, this.SelectedSpotlightClient))
			{
				return;
			}

			this._selectedSpotlightRegion = region;
			this.UpdateSpotlightRegionControls();
		}

		private void UpdateSpotlightRegionControls()
		{
			string title = this.SelectedSpotlightClient;
			bool isSpotlight = !string.IsNullOrEmpty(title) && this._spotlightClients.Contains(title);
			bool hasRegion = !this._selectedSpotlightRegion.IsEmpty;

			this.ClearSpotlightRegionButton.Enabled = isSpotlight && hasRegion;

			if (string.IsNullOrEmpty(title))
			{
				this.SpotlightRegionValueLabel.Text = "—";
			}
			else if (!isSpotlight)
			{
				this.SpotlightRegionValueLabel.Text = "Not a spotlight client";
			}
			else if (!hasRegion)
			{
				this.SpotlightRegionValueLabel.Text = "Full window";
			}
			else
			{
				this.SpotlightRegionValueLabel.Text =
					$"{this._selectedSpotlightRegion.X}, {this._selectedSpotlightRegion.Y}, " +
					$"{this._selectedSpotlightRegion.Width}, {this._selectedSpotlightRegion.Height}";
			}
		}

		private void AddSpotlightClientCandidate(string title)
		{
			if (string.IsNullOrEmpty(title) || title == MainForm.DEFAULT_CLIENT_TITLE)
			{
				return;
			}

			if (this.SpotlightClientsList.Items.IndexOf(title) < 0)
			{
				int index = this.SpotlightClientsList.Items.Add(title);
				this.SpotlightClientsList.SetItemChecked(index, this._spotlightClients.Contains(title));
			}
		}

		public void ShowConfigurationWarning(string details)
		{
			MessageBox.Show(
				"A configuração (EVE-O-Preview.json) foi ajustada ao abrir:"
				+ Environment.NewLine + Environment.NewLine + details
				+ Environment.NewLine + Environment.NewLine
				+ "Os valores fora da faixa foram corrigidos. O arquivo é regravado ao salvar.",
				"EVE-O Preview", MessageBoxButtons.OK, MessageBoxIcon.Warning);
		}

		public void RefreshZoomSettings()
		{
			bool enableControls = this.EnableThumbnailZoom;
			this.ThumbnailZoomFactorNumericEdit.Enabled = enableControls;
			this.ZoomAnchorPanel.Enabled = enableControls;
		}

		public Action ApplicationExitRequested { get; set; }

		public Action FormActivated { get; set; }

		public Action FormMinimized { get; set; }

		public Action<ViewCloseRequest> FormCloseRequested { get; set; }

		public Action ApplicationSettingsChanged { get; set; }

		public Action ThumbnailsSizeChanged { get; set; }

		public Action<string> ThumbnailStateChanged { get; set; }

		/// <summary>Usuário editou a label do cliente selecionado (título, texto).</summary>
		public Action<string, string> ClientLabelChanged { get; set; }

		public Action SpotlightClientsChanged { get; set; }

		public Action<string> SpotlightClientSelected { get; set; }

		public Action<string> ClearSpotlightRegionRequested { get; set; }

		public Action DocumentationLinkActivated { get; set; }

		#region UI events
		// A navegação em blocos (fase 2) substituiu o TabControl lateral; o desenho das
		// abas ficou no histórico. O controle segue no Designer apenas como fonte das
		// telas (cada página carrega um painel, reparentado em BuildLayout).
		private void ContentTabControl_DrawItem(object sender, DrawItemEventArgs e)
		{
		}

		private void OptionChanged_Handler(object sender, EventArgs e)
		{
			if (this._suppressEvents)
			{
				return;
			}

			this.ApplicationSettingsChanged?.Invoke();
		}

		private void ThumbnailSizeChanged_Handler(object sender, EventArgs e)
		{
			if (this._suppressEvents)
			{
				return;
			}

			Size previousSize = this._lastThumbnailSize;
			if (previousSize.Width <= 0 || previousSize.Height <= 0)
			{
				previousSize = this.ThumbnailSize;
			}

			bool changedHeight = sender == this.ThumbnailsHeightNumericEdit;
			int newValue = changedHeight
				? (int)this.ThumbnailsHeightNumericEdit.Value
				: (int)this.ThumbnailsWidthNumericEdit.Value;

			this._suppressEvents = true;

			Size thumbnailSize = this.KeepAspectRatio
				? ThumbnailSizePolicy.CalculateAspectRatioSize(
					previousSize,
					changedHeight ? ThumbnailSizePolicy.Dimension.Height : ThumbnailSizePolicy.Dimension.Width,
					newValue,
					this._minimumSize,
					this._maximumSize,
					keepAspectRatio: true)
				: new Size(
					Math.Min(Math.Max(changedHeight ? previousSize.Width : newValue, this._minimumSize.Width), this._maximumSize.Width),
					Math.Min(Math.Max(changedHeight ? newValue : previousSize.Height, this._minimumSize.Height), this._maximumSize.Height));

			this.ThumbnailSize = thumbnailSize;
			this._suppressEvents = false;

			this.ThumbnailsSizeChanged?.Invoke();
		}

		private void KeepAspectRatioCheckBox_CheckedChanged(object sender, EventArgs e)
		{
			if (this._suppressEvents)
			{
				return;
			}

			// Guarda a preferência e reaplica a proporção do par atual quando ligada.
			if (this.KeepAspectRatio)
			{
				Size current = this.ThumbnailSize;
				this._lastThumbnailSize = current;
			}

			this.ApplicationSettingsChanged?.Invoke();
		}

		private void ActiveClientHighlightColorButton_Click(object sender, EventArgs e)
		{
			using (ColorDialog dialog = new ColorDialog())
			{
				dialog.Color = this.ActiveClientHighlightColor;

				if (dialog.ShowDialog() != DialogResult.OK)
				{
					return;
				}

				this.ActiveClientHighlightColor = dialog.Color;
			}

			this.OptionChanged_Handler(sender, e);
		}

		private void OverlayLabelColorButton_Click(object sender, EventArgs e)
		{
			using (ColorDialog dialog = new ColorDialog())
			{
				dialog.Color = this.OverlayLabelColor;

				if (dialog.ShowDialog() != DialogResult.OK)
				{
					return;
				}
				this.OverlayLabelColor = dialog.Color;
			}

			this.OptionChanged_Handler(sender, e);
		}

		private void ThumbnailsList_ItemCheck_Handler(object sender, ItemCheckEventArgs e)
		{
			// A lista serve para duas coisas: selecionar o cliente (editar a label) e
			// marcar "nao mostrar". Clicar no nome so pode selecionar; o estado so muda
			// quando o clique foi na caixinha (issue #156).
			if (this._thumbnailListTextClick)
			{
				e.NewValue = e.CurrentValue;
				return;
			}

			if (!(this.ThumbnailsList.Items[e.Index] is IThumbnailDescription selectedItem))
			{
				return;
			}

			selectedItem.IsDisabled = (e.NewValue == CheckState.Checked);

			this.ThumbnailStateChanged?.Invoke(selectedItem.Title);
		}

		private void ThumbnailsList_MouseDown_Handler(object sender, MouseEventArgs e)
		{
			this._thumbnailListTextClick = false;

			if (e.Button != MouseButtons.Left)
			{
				return;
			}

			int index = this.ThumbnailsList.IndexFromPoint(e.Location);
			if (index < 0)
			{
				return;
			}

			Rectangle item = this.ThumbnailsList.GetItemRectangle(index);
			int checkBoxWidth = this.ThumbnailsList.LogicalToDeviceUnits(20);

			// Clique a esquerda da caixinha: so seleciona.
			this._thumbnailListTextClick = e.X > item.Left + checkBoxWidth;
		}

		private void ThumbnailsList_MouseUp_Handler(object sender, MouseEventArgs e)
		{
			this._thumbnailListTextClick = false;
		}

		private void ThumbnailsList_SelectedIndexChanged_Handler(object sender, EventArgs e)
		{
			this.UpdateClientLabelEditor();
		}

		private void ClientLabelTextBox_TextChanged_Handler(object sender, EventArgs e)
		{
			if (this._suppressClientLabelEvents)
			{
				return;
			}

			if (!(this.ThumbnailsList.SelectedItem is IThumbnailDescription selectedItem))
			{
				return;
			}

			string label = this.ClientLabelTextBox.Text;
			if (string.IsNullOrEmpty(label))
			{
				this._clientLabels.Remove(selectedItem.Title);
			}
			else
			{
				this._clientLabels[selectedItem.Title] = label;
			}

			this.ClientLabelChanged?.Invoke(selectedItem.Title, label);
		}

		private void SpotlightClientsList_ItemCheck_Handler(object sender, ItemCheckEventArgs e)
		{
			if (this._suppressEvents)
			{
				return;
			}

			if (!(this.SpotlightClientsList.Items[e.Index] is string title))
			{
				return;
			}

			if (e.NewValue == CheckState.Checked)
			{
				this._spotlightClients.Add(title);
			}
			else
			{
				this._spotlightClients.Remove(title);
			}

			this.SpotlightClientsList.SelectedIndex = e.Index;
			this.UpdateSpotlightRegionControls();
			this.SpotlightClientsChanged?.Invoke();
			this.SpotlightClientSelected?.Invoke(title);
		}

		private void SpotlightClientsList_SelectedIndexChanged(object sender, EventArgs e)
		{
			this._selectedSpotlightRegion = Rectangle.Empty;
			this.UpdateSpotlightRegionControls();
			this.SpotlightClientSelected?.Invoke(this.SelectedSpotlightClient);
		}

		private void ClearSpotlightRegionButton_Click(object sender, EventArgs e)
		{
			string title = this.SelectedSpotlightClient;
			if (!string.IsNullOrEmpty(title))
			{
				this.ClearSpotlightRegionRequested?.Invoke(title);
			}
		}

		private void DocumentationLinkClicked_Handler(object sender, LinkLabelLinkClickedEventArgs e)
		{
			this.DocumentationLinkActivated?.Invoke();
		}

		private void MainFormResize_Handler(object sender, EventArgs e)
		{
			if (this.WindowState != FormWindowState.Minimized)
			{
				return;
			}

			this.FormMinimized?.Invoke();
		}

		private void MainFormClosing_Handler(object sender, FormClosingEventArgs e)
		{
			ViewCloseRequest request = new ViewCloseRequest();

			this.FormCloseRequested?.Invoke(request);

			e.Cancel = !request.Allow;
		}

		private void RestoreMainForm_Handler(object sender, EventArgs e)
		{
			// This is form's GUI lifecycle event that is invariant to the Form data
			base.Show();
			this.WindowState = FormWindowState.Normal;
			this.BringToFront();
		}

		private void ExitMenuItemClick_Handler(object sender, EventArgs e)
		{
			this.ApplicationExitRequested?.Invoke();
		}
		#endregion

		private void InitZoomAnchorMap()
		{
			this._zoomAnchorMap[ViewZoomAnchor.NW] = this.ZoomAanchorNWRadioButton;
			this._zoomAnchorMap[ViewZoomAnchor.N] = this.ZoomAanchorNRadioButton;
			this._zoomAnchorMap[ViewZoomAnchor.NE] = this.ZoomAanchorNERadioButton;
			this._zoomAnchorMap[ViewZoomAnchor.W] = this.ZoomAanchorWRadioButton;
			this._zoomAnchorMap[ViewZoomAnchor.C] = this.ZoomAanchorCRadioButton;
			this._zoomAnchorMap[ViewZoomAnchor.E] = this.ZoomAanchorERadioButton;
			this._zoomAnchorMap[ViewZoomAnchor.SW] = this.ZoomAanchorSWRadioButton;
			this._zoomAnchorMap[ViewZoomAnchor.S] = this.ZoomAanchorSRadioButton;
			this._zoomAnchorMap[ViewZoomAnchor.SE] = this.ZoomAanchorSERadioButton;
		}
		private void InitOverlayLabelMap()
		{
			this._overlayLabelMap[ViewZoomAnchor.NW] = this.OverlayLabelNWRadioButton;
			this._overlayLabelMap[ViewZoomAnchor.N] = this.OverlayLabelNRadioButton;
			this._overlayLabelMap[ViewZoomAnchor.NE] = this.OverlayLabelNERadioButton;
			this._overlayLabelMap[ViewZoomAnchor.W] = this.OverlayLabelWRadioButton;
			this._overlayLabelMap[ViewZoomAnchor.C] = this.OverlayLabelCRadioButton;
			this._overlayLabelMap[ViewZoomAnchor.E] = this.OverlayLabelERadioButton;
			this._overlayLabelMap[ViewZoomAnchor.SW] = this.OverlayLabelSWRadioButton;
			this._overlayLabelMap[ViewZoomAnchor.S] = this.OverlayLabelSRadioButton;
			this._overlayLabelMap[ViewZoomAnchor.SE] = this.OverlayLabelSERadioButton;
		}
		private void InitCycleGroupIndicatorMap()
		{
			this._cycleGroupIndicatorMap[ViewZoomAnchor.NW] = this.CycleGroupIndicatorNWRadioButton;
			this._cycleGroupIndicatorMap[ViewZoomAnchor.N] = this.CycleGroupIndicatorNRadioButton;
			this._cycleGroupIndicatorMap[ViewZoomAnchor.NE] = this.CycleGroupIndicatorNERadioButton;
			this._cycleGroupIndicatorMap[ViewZoomAnchor.W] = this.CycleGroupIndicatorWRadioButton;
			this._cycleGroupIndicatorMap[ViewZoomAnchor.C] = this.CycleGroupIndicatorCRadioButton;
			this._cycleGroupIndicatorMap[ViewZoomAnchor.E] = this.CycleGroupIndicatorERadioButton;
			this._cycleGroupIndicatorMap[ViewZoomAnchor.SW] = this.CycleGroupIndicatorSWRadioButton;
			this._cycleGroupIndicatorMap[ViewZoomAnchor.S] = this.CycleGroupIndicatorSRadioButton;
			this._cycleGroupIndicatorMap[ViewZoomAnchor.SE] = this.CycleGroupIndicatorSERadioButton;
		}

		private void InitFormSize()
		{
			// O tamanho agora e fixo (455x400) e definido em BuildLayout: nao ha mais
			// coluna de abas para dimensionar.
			this.ClientSize = new Size(455, 400);
		}
		private void btnLabelFont_Click(object sender, EventArgs e)
		{
			FontDialog fontSelector = new FontDialog();
			fontSelector.Font = OverlayLabelFont;
			fontSelector.ShowColor = false;
			fontSelector.ShowApply = false;
			fontSelector.ShowHelp = false;
			if (fontSelector.ShowDialog() != DialogResult.Cancel)
			{
				OverlayLabelFont = fontSelector.Font;
				LabelOverlayLabelFont.Font = fontSelector.Font;
				this.OptionChanged_Handler(sender, e);
			}
		}

		private void PreventPreviewColorButton_Click(object sender, EventArgs e)
		{
			using (ColorDialog dialog = new ColorDialog())
			{
				dialog.Color = this.PreventPreviewColor;

				if (dialog.ShowDialog() != DialogResult.OK)
				{
					return;
				}

				this.PreventPreviewColor = dialog.Color;
			}

			this.OptionChanged_Handler(sender, e);

		}
	}
}