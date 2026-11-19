using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace EveOPreview.Services
{
	/// <summary>
	/// Nomeacao e retencao dos crash logs. Fica no nucleo puro para poder ser
	/// testado no Linux, onde o handler WinForms nao roda.
	/// </summary>
	public static class CrashLogNaming
	{
		public const string FilePrefix = "EVE-O-Preview-";
		public const string FileExtension = ".log";
		public const int DefaultFilesToKeep = 5;

		private const string TimestampFormat = "yyyyMMdd-HHmmss";
		private const int TimestampLength = 15;

		private static readonly char[] WindowsInvalidFileNameChars =
		{
			'<', '>', ':', '"', '/', '\\', '|', '?', '*'
		};

		private static readonly string[] WindowsReservedNames =
		{
			"CON", "PRN", "AUX", "NUL",
			"COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
			"LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
		};

		/// <summary>
		/// Retorna um caminho ainda nao usado para o crash log do instante informado.
		/// Se o diretorio nao existir ele e criado; se nao puder ser criado, o caminho
		/// retornado continua sintaticamente valido para quem for tentar gravar.
		/// Dois crashes no mesmo segundo recebem sufixos distintos (-1, -2, ...).
		/// </summary>
		public static string NextPath(string directory, DateTime timestamp, IEnumerable<string> existing)
		{
			string safeDirectory = EnsureDirectory(directory);
			string baseName = FilePrefix + timestamp.ToString(TimestampFormat, CultureInfo.InvariantCulture);

			HashSet<string> taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			if (existing != null)
			{
				foreach (string path in existing)
				{
					AddExistingName(taken, path);
				}
			}

			// Inclui o que ja esta no disco para nao sobrescrever nem quando o chamador
			// passou uma lista desatualizada.
			try
			{
				foreach (string path in Directory.EnumerateFiles(safeDirectory, FilePrefix + "*" + FileExtension))
				{
					AddExistingName(taken, path);
				}
			}
			catch
			{
				// Sem acesso ao diretorio: segue apenas com a lista fornecida.
			}

			string candidate = baseName + FileExtension;
			if (!taken.Contains(candidate))
			{
				return Path.Combine(safeDirectory, candidate);
			}

			for (int suffix = 1; ; suffix++)
			{
				candidate = baseName + "-" + suffix.ToString(CultureInfo.InvariantCulture) + FileExtension;
				if (!taken.Contains(candidate))
				{
					return Path.Combine(safeDirectory, candidate);
				}
			}
		}

		/// <summary>
		/// Recebe caminhos de crash logs e devolve os <paramref name="keep"/> mais recentes,
		/// do mais novo para o mais antigo. A funcao nao apaga nada: o chamador compara
		/// o resultado com a lista original e remove o restante.
		/// </summary>
		public static IReadOnlyList<string> PruneKeepingNewest(IEnumerable<string> paths, int keep)
		{
			if (paths == null || keep <= 0)
			{
				return Array.Empty<string>();
			}

			return paths
				.Where(path => !string.IsNullOrEmpty(path))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.Select(path => new { Path = path, Order = BuildOrderKey(path) })
				.OrderByDescending(entry => entry.Order, StringComparer.Ordinal)
				.ThenByDescending(entry => entry.Path, StringComparer.OrdinalIgnoreCase)
				.Take(keep)
				.Select(entry => entry.Path)
				.ToList();
		}

		private static void AddExistingName(HashSet<string> taken, string path)
		{
			if (string.IsNullOrEmpty(path))
			{
				return;
			}

			string name = Path.GetFileName(path);
			if (!string.IsNullOrEmpty(name))
			{
				taken.Add(name);
			}
		}

		private static string EnsureDirectory(string directory)
		{
			if (string.IsNullOrWhiteSpace(directory))
			{
				directory = GetCurrentDirectory();
			}

			string safeDirectory = SanitizePath(directory);
			if (string.IsNullOrWhiteSpace(safeDirectory))
			{
				safeDirectory = GetCurrentDirectory();
			}

			try
			{
				Directory.CreateDirectory(safeDirectory);
			}
			catch
			{
				// O handler de crash nao pode falhar por causa do diretorio.
			}

			return safeDirectory;
		}

		private static string GetCurrentDirectory()
		{
			try
			{
				return Directory.GetCurrentDirectory();
			}
			catch
			{
				return AppContext.BaseDirectory;
			}
		}

		/// <summary>
		/// Gera uma chave ordenavel a partir do nome do arquivo. O sufixo numerico
		/// desempata crashes gravados no mesmo segundo, e arquivos fora do padrao
		/// ficam no fim (sao os primeiros candidatos a remocao).
		/// </summary>
		private static string BuildOrderKey(string path)
		{
			string name = Path.GetFileName(path);
			if (string.IsNullOrEmpty(name)
				|| !name.StartsWith(FilePrefix, StringComparison.OrdinalIgnoreCase)
				|| !name.EndsWith(FileExtension, StringComparison.OrdinalIgnoreCase))
			{
				return string.Empty;
			}

			string timestampAndSuffix = name.Substring(
				FilePrefix.Length,
				name.Length - FilePrefix.Length - FileExtension.Length);

			int suffix = 0;
			if (timestampAndSuffix.Length > TimestampLength && timestampAndSuffix[TimestampLength] == '-')
			{
				string suffixText = timestampAndSuffix.Substring(TimestampLength + 1);
				if (!int.TryParse(suffixText, NumberStyles.None, CultureInfo.InvariantCulture, out suffix))
				{
					return string.Empty;
				}

				timestampAndSuffix = timestampAndSuffix.Substring(0, TimestampLength);
			}

			if (timestampAndSuffix.Length != TimestampLength)
			{
				return string.Empty;
			}

			if (!DateTime.TryParseExact(
				timestampAndSuffix,
				TimestampFormat,
				CultureInfo.InvariantCulture,
				DateTimeStyles.None,
				out DateTime timestamp))
			{
				return string.Empty;
			}

			return timestamp.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)
				+ "-"
				+ suffix.ToString("D9", CultureInfo.InvariantCulture);
		}

		private static string SanitizePath(string path)
		{
			try
			{
				char separator = Path.DirectorySeparatorChar;
				string normalized = path
					.Replace('\\', separator)
					.Replace('/', separator);

				string prefix = string.Empty;
				int index = 0;

				if (normalized.Length >= 2 && normalized[0] == separator && normalized[1] == separator)
				{
					// UNC (\\server\share) ou raiz dupla.
					prefix = new string(separator, 2);
					index = 2;
				}
				else if (normalized.Length >= 2 && char.IsLetter(normalized[0]) && normalized[1] == ':')
				{
					// Letra de unidade: preserva "C:" para nao quebrar o caminho absoluto.
					prefix = normalized.Substring(0, 2);
					index = 2;
					if (index < normalized.Length && normalized[index] == separator)
					{
						prefix += separator;
						index++;
					}
				}
				else if (normalized.Length >= 1 && normalized[0] == separator)
				{
					prefix = separator.ToString();
					index = 1;
				}

				string[] segments = normalized
					.Substring(index)
					.Split(new[] { separator }, StringSplitOptions.RemoveEmptyEntries);

				string sanitized = string.Join(
					separator.ToString(),
					segments.Select(SanitizeSegment));

				return prefix + sanitized;
			}
			catch
			{
				// Caminho hostil (por exemplo com caracteres de controle): devolve algo
				// gravavel em vez de propagar a excecao.
				return GetCurrentDirectory();
			}
		}

		private static string SanitizeSegment(string segment)
		{
			if (segment == "." || segment == "..")
			{
				return segment;
			}

			StringBuilder builder = new StringBuilder(segment.Length);
			foreach (char character in segment)
			{
				if (character < 32 || Array.IndexOf(WindowsInvalidFileNameChars, character) >= 0)
				{
					builder.Append('_');
				}
				else
				{
					builder.Append(character);
				}
			}

			string sanitized = builder.ToString().TrimEnd(' ', '.');

			if (sanitized.Length == 0)
			{
				return "_";
			}

			string nameWithoutExtension = Path.GetFileNameWithoutExtension(sanitized);
			if (WindowsReservedNames.Any(name => string.Equals(name, nameWithoutExtension, StringComparison.OrdinalIgnoreCase)))
			{
				sanitized = "_" + sanitized;
			}

			return sanitized;
		}
	}
}
