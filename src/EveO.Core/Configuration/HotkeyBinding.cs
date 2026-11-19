using System;

namespace EveOPreview.Configuration
{
	/// <summary>
	/// Representação neutra de um atalho de teclado (issue #162).
	///
	/// O Core não pode referenciar <c>System.Windows.Forms.Keys</c> (o tipo arrasta o WinForms,
	/// proibido em <c>EveO.Core</c>). Por isso o atalho é mantido na forma textual canônica
	/// usada pelo armazenamento de configuração (ex.: "Control+F14"). A conversão para
	/// <c>Keys</c> depende do <c>KeysConverter</c> do WinForms e por isso vive no executável
	/// (<c>HotkeyConverter</c>), preservando exatamente o comportamento anterior.
	/// </summary>
	public readonly struct HotkeyBinding : IEquatable<HotkeyBinding>
	{
		private readonly string _value;

		public HotkeyBinding(string value)
		{
			this._value = value ?? string.Empty;
		}

		/// <summary>Um atalho não atribuído (equivale ao antigo <c>Keys.None</c>).</summary>
		public static HotkeyBinding None => new HotkeyBinding(string.Empty);

		/// <summary>Forma textual do atalho (ex.: "Control+F14"). Nunca nulo.</summary>
		public string Value => this._value ?? string.Empty;

		public bool IsEmpty => string.IsNullOrWhiteSpace(this.Value);

		public bool Equals(HotkeyBinding other) => string.Equals(this.Value, other.Value, StringComparison.Ordinal);

		public override bool Equals(object obj) => obj is HotkeyBinding other && this.Equals(other);

		public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(this.Value);

		public static bool operator ==(HotkeyBinding left, HotkeyBinding right) => left.Equals(right);

		public static bool operator !=(HotkeyBinding left, HotkeyBinding right) => !left.Equals(right);

		public override string ToString() => this.Value;
	}
}
