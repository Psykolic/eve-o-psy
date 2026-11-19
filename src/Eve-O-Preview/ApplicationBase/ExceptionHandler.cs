using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using EveOPreview.Services;
using EveOPreview.Services.Interop;

namespace EveOPreview
{
	// A really very primitive exception handler stuff here
	// No IoC, no fancy DI containers - just a plain exception stacktrace dump
	// If this code is called then something was gone really bad
	// so even the DI infrastructure might be dead already.
	// So this dumb and non elegant approach is used
	sealed class ExceptionHandler
	{
		private const string EXCEPTION_DUMP_FILE_PATTERN = "EVE-O-Preview-*.log";
		private const string CONFIGURATION_FILE_NAME = "EVE-O-Preview.json";
		private const int EXCEPTION_DUMP_FILES_TO_KEEP = 5;
		private const uint GR_GDIOBJECTS = 0;
		private const uint GR_USEROBJECTS = 1;

		private const string EXCEPTION_MESSAGE = "EVE-O-Preview has encountered a problem and needs to close. Additional information has been saved in the crash log file.";

		private static readonly DateTime ProcessStartUtc = DateTime.UtcNow;

		public void SetupExceptionHandlers()
		{
			if (System.Diagnostics.Debugger.IsAttached)
			{
				return;
			}

			Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
			Application.ThreadException += delegate (Object sender, ThreadExceptionEventArgs e)
			{
				this.ExceptionEventHandler(e.Exception);
			};

			AppDomain.CurrentDomain.UnhandledException += delegate (Object sender, UnhandledExceptionEventArgs e)
			{
				this.ExceptionEventHandler(e.ExceptionObject as Exception);
			};
		}

		private void ExceptionEventHandler(Exception exception)
		{
			string logPath = null;

			try
			{
				logPath = this.WriteCrashLog(exception);
				ExceptionHandler.PruneOldCrashLogs(Path.GetDirectoryName(logPath));
			}
			catch
			{
				// We are in unstable state now so even this operation might fail
				// Still we actually don't care anymore - anyway the application has been cashed
			}

			try
			{
				string message = ExceptionHandler.EXCEPTION_MESSAGE;
				if (!string.IsNullOrEmpty(logPath))
				{
					message += Environment.NewLine + Environment.NewLine + "Crash log: " + logPath;
				}

				MessageBox.Show(message, @"EVE-O-Preview", MessageBoxButtons.OK, MessageBoxIcon.Error);
			}
			catch
			{
				// Nao ha mais nada seguro a fazer alem de encerrar.
			}

			System.Environment.Exit(1);
		}

		private string WriteCrashLog(Exception exception)
		{
			string directory = ExceptionHandler.GetLogDirectory();
			List<string> existing = ExceptionHandler.EnumerateCrashLogs(directory);

			string path = CrashLogNaming.NextPath(directory, DateTime.Now, existing);

			File.WriteAllText(path, this.BuildCrashDump(exception), new UTF8Encoding(false));

			return path;
		}

		private static void PruneOldCrashLogs(string directory)
		{
			if (string.IsNullOrEmpty(directory))
			{
				return;
			}

			List<string> logs = ExceptionHandler.EnumerateCrashLogs(directory);
			IReadOnlyList<string> kept = CrashLogNaming.PruneKeepingNewest(logs, EXCEPTION_DUMP_FILES_TO_KEEP);
			HashSet<string> keptPaths = new HashSet<string>(kept, StringComparer.OrdinalIgnoreCase);

			foreach (string path in logs)
			{
				if (keptPaths.Contains(path))
				{
					continue;
				}

				try
				{
					File.Delete(path);
				}
				catch
				{
					// Se um log antigo nao puder ser removido, o crash atual ja esta salvo.
				}
			}
		}

		private static string GetLogDirectory()
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

		private static List<string> EnumerateCrashLogs(string directory)
		{
			try
			{
				return Directory
					.EnumerateFiles(directory, EXCEPTION_DUMP_FILE_PATTERN, SearchOption.TopDirectoryOnly)
					.ToList();
			}
			catch
			{
				return new List<string>();
			}
		}

		private string BuildCrashDump(Exception exception)
		{
			StringBuilder builder = new StringBuilder();

			builder.AppendLine("EVE-O Preview - crash log");
			builder.AppendLine("=========================");
			builder.AppendLine("Gerado (UTC): " + ExceptionHandler.FormatUtc(DateTime.UtcNow));
			builder.AppendLine("Versao do aplicativo: " + ExceptionHandler.GetApplicationVersion());
			builder.AppendLine("Runtime .NET: " + Environment.Version);
			builder.AppendLine("Arquitetura: " + RuntimeInformation.ProcessArchitecture);
			builder.AppendLine("Build LINUX: " + (ExceptionHandler.IsLinuxBuild ? "sim" : "nao"));
			builder.AppendLine("Sistema: " + RuntimeInformation.OSDescription);
			builder.AppendLine("PID: " + Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
			builder.AppendLine("Thread: " + Environment.CurrentManagedThreadId.ToString(CultureInfo.InvariantCulture));
			builder.AppendLine("Iniciado (UTC): " + ExceptionHandler.FormatUtc(ProcessStartUtc));
			builder.AppendLine("Uptime: " + ExceptionHandler.FormatUptime(DateTime.UtcNow - ProcessStartUtc));
			builder.AppendLine("Memoria de trabalho: " + ExceptionHandler.FormatWorkingSet());
			ExceptionHandler.AppendGuiResourceCounts(builder);
			ExceptionHandler.AppendConfigInfo(builder);
			ExceptionHandler.AppendLastOperations(builder);

			builder.AppendLine();
			builder.AppendLine("Excecao:");
			builder.AppendLine(exception == null ? "(nao disponivel)" : exception.ToString());

			return builder.ToString();
		}

		private static bool IsLinuxBuild
		{
			get
			{
#if LINUX
				return true;
#else
				return false;
#endif
			}
		}

		private static string GetApplicationVersion()
		{
			Version version = Assembly.GetEntryAssembly()?.GetName().Version;

			if (version == null)
			{
				version = typeof(ExceptionHandler).Assembly.GetName().Version;
			}

			return version == null ? "desconhecida" : version.ToString();
		}

		private static string FormatUtc(DateTime timestamp)
		{
			return timestamp.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
		}

		private static string FormatUptime(TimeSpan uptime)
		{
			return string.Format(
				CultureInfo.InvariantCulture,
				"{0}d {1:00}:{2:00}:{3:00}",
				(int)uptime.TotalDays,
				uptime.Hours,
				uptime.Minutes,
				uptime.Seconds);
		}

		private static string FormatWorkingSet()
		{
			long bytes = Environment.WorkingSet;
			double megabytes = bytes / (1024d * 1024d);

			return string.Format(
				CultureInfo.InvariantCulture,
				"{0:F1} MB ({1} bytes)",
				megabytes,
				bytes);
		}

		private static void AppendGuiResourceCounts(StringBuilder builder)
		{
			try
			{
				using (Process process = Process.GetCurrentProcess())
				{
					uint gdi = User32NativeMethods.GetGuiResources(process.Handle, GR_GDIOBJECTS);
					uint user = User32NativeMethods.GetGuiResources(process.Handle, GR_USEROBJECTS);

					builder.AppendLine("Handles GDI: " + gdi.ToString(CultureInfo.InvariantCulture));
					builder.AppendLine("Handles USER: " + user.ToString(CultureInfo.InvariantCulture));
				}
			}
			catch (Exception exception)
			{
				builder.AppendLine("Handles GDI/USER: indisponivel (" + exception.GetType().Name + ")");
			}
		}

		private static void AppendConfigInfo(StringBuilder builder)
		{
			string path = ExceptionHandler.GetConfigPath();

			builder.AppendLine("Config: " + path);

			try
			{
				FileInfo info = new FileInfo(path);
				if (!info.Exists)
				{
					builder.AppendLine("Config: ausente");
					return;
				}

				builder.AppendLine("Config tamanho: " + info.Length.ToString(CultureInfo.InvariantCulture) + " bytes");

				using (FileStream stream = File.OpenRead(path))
				{
					byte[] hash = SHA256.HashData(stream);
					builder.AppendLine("Config SHA-256: " + Convert.ToHexString(hash));
				}
			}
			catch (Exception exception)
			{
				builder.AppendLine("Config: falha ao inspecionar (" + exception.GetType().Name + ": " + exception.Message + ")");
			}
		}

		private static string GetConfigPath()
		{
			string[] candidates =
			{
				Path.Combine(ExceptionHandler.GetLogDirectory(), CONFIGURATION_FILE_NAME),
				Path.Combine(AppContext.BaseDirectory, CONFIGURATION_FILE_NAME)
			};

			foreach (string candidate in candidates)
			{
				try
				{
					if (File.Exists(candidate))
					{
						return candidate;
					}
				}
				catch
				{
					// Caminho inacessivel: tenta o proximo.
				}
			}

			return candidates[0];
		}

		private static void AppendLastOperations(StringBuilder builder)
		{
			builder.AppendLine();
			builder.AppendLine("Ultima operacao conhecida (mais antiga primeiro):");

			IReadOnlyList<OperationHistoryEntry> operations = AppOperationLog.Snapshot();
			if (operations.Count == 0)
			{
				builder.AppendLine("  (nenhuma operacao registrada)");
				return;
			}

			foreach (OperationHistoryEntry operation in operations)
			{
				builder.AppendLine(
					"  "
					+ ExceptionHandler.FormatUtc(operation.TimestampUtc)
					+ " "
					+ operation.Operation);
			}
		}
	}
}
