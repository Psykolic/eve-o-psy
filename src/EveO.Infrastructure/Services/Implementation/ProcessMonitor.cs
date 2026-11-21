using EveOPreview.Configuration;
using EveOPreview.Services.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace EveOPreview.Services.Implementation
{
	sealed class ProcessMonitor : IProcessMonitor
	{
		#region Private constants
		private const string DEFAULT_PROCESS_NAME = "ExeFile";
		private const string CURRENT_PROCESS_NAME = "EVE-O-Preview";
		#endregion

		#region Private fields
		private readonly ProcessSetDiff _processDiff;
		private readonly IDictionary<IntPtr, int> _processIds;
		private IProcessInfo _currentProcessInfo;
		private readonly IThumbnailConfiguration _configuration;
		#endregion

		public ProcessMonitor(IThumbnailConfiguration configuration)
		{
			this._processDiff = new ProcessSetDiff(ProcessSetDiff.DefaultRemovalMissThreshold);
			this._processIds = new Dictionary<IntPtr, int>(512);
			this._configuration = configuration;

			// This field cannot be initialized properly in constructor
			// At the moment this code is executed the main application window is not yet initialized
			this._currentProcessInfo = new ProcessInfo(IntPtr.Zero, "");
		}

		private bool IsMonitoredProcess(string processName)
		{
			// This is a possible extension point
			return _configuration.IsExecutableToPreview(processName);
		}

		private IProcessInfo GetCurrentProcessInfo()
		{
			var currentProcess = Process.GetCurrentProcess();
			return new ProcessInfo(currentProcess.MainWindowHandle, currentProcess.MainWindowTitle);
		}

		public IProcessInfo GetMainProcess()
		{
			if (this._currentProcessInfo.Handle == IntPtr.Zero)
			{
				var processInfo = this.GetCurrentProcessInfo();

				// Are we initialized yet?
				if (processInfo.Title != "")
				{
					this._currentProcessInfo = processInfo;
				}
			}

			return this._currentProcessInfo;
		}

		/// <summary>
		/// Confirma que o handle/processo ainda existe. A ausência temporária do
		/// MainWindowHandle (cliente sem responder, minimizado ou na tela de login)
		/// não pode ser tratada como fechamento da janela (issue #139).
		/// </summary>
		private bool ShouldKeepKnownHandle(IntPtr handle)
		{
			if (User32NativeMethods.IsWindow(handle))
			{
				return true;
			}

			if (!this._processIds.TryGetValue(handle, out int processId))
			{
				return false;
			}

			try
			{
				using (Process process = Process.GetProcessById(processId))
				{
					if (process.HasExited)
					{
						return false;
					}

					IntPtr currentMainWindowHandle = process.MainWindowHandle;

					// Processo vivo e ainda sem janela principal: a ausência é transitória.
					// Se ele já expôs outra janela, este handle ficou obsoleto.
					return currentMainWindowHandle == IntPtr.Zero || currentMainWindowHandle == handle;
				}
			}
			catch (ArgumentException)
			{
				// O processo encerrou entre a leitura do handle e esta verificação.
				return false;
			}
			catch (InvalidOperationException)
			{
				return false;
			}
		}

		public void GetUpdatedProcesses(out ICollection<IProcessInfo> addedProcesses, out ICollection<IProcessInfo> updatedProcesses, out ICollection<IProcessInfo> removedProcesses)
		{
			addedProcesses = new List<IProcessInfo>(16);
			updatedProcesses = new List<IProcessInfo>(16);
			removedProcesses = new List<IProcessInfo>(16);

			IList<ProcessSetEntry> observedProcesses = new List<ProcessSetEntry>(16);
			foreach (Process process in Process.GetProcesses())
			{
				string processName = process.ProcessName;

				if (!this.IsMonitoredProcess(processName))
				{
					continue;
				}

				IntPtr mainWindowHandle = process.MainWindowHandle;
				if (mainWindowHandle == IntPtr.Zero)
				{
					continue; // No need to monitor non-visual processes right now
				}

				string mainWindowTitle = process.MainWindowTitle.Replace("—", "-");
				observedProcesses.Add(new ProcessSetEntry(mainWindowHandle, mainWindowTitle));
				this._processIds[mainWindowHandle] = process.Id;
			}

			ProcessSetDiffResult diff = this._processDiff.Update(observedProcesses, this.ShouldKeepKnownHandle);

			foreach (ProcessSetEntry process in diff.Added)
			{
				addedProcesses.Add(new ProcessInfo(process.Handle, process.Title));
			}

			foreach (ProcessSetEntry process in diff.Updated)
			{
				updatedProcesses.Add(new ProcessInfo(process.Handle, process.Title));
			}

			foreach (ProcessSetEntry process in diff.Removed)
			{
				this._processIds.Remove(process.Handle);

				Trace.TraceInformation(
					"ProcessMonitor: removendo janela 0x{0:X} ('{1}') após {2} ciclos sem MainWindowHandle; motivo: processo/handle não existe mais.",
					process.Handle.ToInt64(),
					process.Title,
					process.Misses);

				removedProcesses.Add(new ProcessInfo(process.Handle, process.Title));
			}
		}
	}
}
