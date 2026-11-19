using System.Windows.Forms;

namespace EveOPreview.UI.Hotkeys
{
	/// <summary>
	/// Conversão entre a forma textual do atalho (neutra, usada pelo Core — ver
	/// <c>EveOPreview.Configuration.HotkeyBinding</c>) e <see cref="Keys"/>.
	///
	/// Fica no executável porque <c>KeysConverter</c> depende do WinForms, que o Core não
	/// pode referenciar (issue #162). O comportamento é idêntico ao antigo, que vivia em
	/// <c>ThumbnailConfiguration.StringToKey</c>/<c>GetClientHotkey</c>.
	/// </summary>
	internal static class HotkeyConverter
	{
		public static Keys ToKeys(string hotkey)
		{
			object rawValue = (new KeysConverter()).ConvertFromInvariantString(hotkey);
			return rawValue != null ? (Keys)rawValue : Keys.None;
		}

		public static string ToInvariantString(Keys hotkey)
		{
			return (new KeysConverter()).ConvertToInvariantString(hotkey);
		}
	}
}
