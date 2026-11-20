using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using EveOPreview.Configuration;
using EveOPreview.Refresh;

namespace EveOPreview.Services.Implementation
{
	/// <summary>
	/// Publica mudanças de janela no <see cref="IClientChangeSignal"/> usando
	/// <c>SetWinEventHook</c> (Fase 2.2, ADR-006).
	///
	/// Medido no spike: 180 s com 7 clientes reais geraram 123 eventos e 97 refreshes
	/// coalescidos, a 0,33% de um core — contra 1,42% da varredura por polling a 2 Hz.
	///
	/// O hook usa <c>WINEVENT_OUTOFCONTEXT</c>, então os callbacks chegam na thread que
	/// instalou o hook; por isso ele vive em uma thread própria com message pump.
	/// </summary>
	sealed class WindowEventSource : IWindowEventSource
	{
		private const uint EVENT_OBJECT_CREATE = 0x8000;
		private const uint EVENT_OBJECT_DESTROY = 0x8001;
		private const uint EVENT_OBJECT_SHOW = 0x8002;
		private const uint EVENT_OBJECT_HIDE = 0x8003;
		private const uint EVENT_OBJECT_NAMECHANGE = 0x800C;
		private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
		private const int OBJID_WINDOW = 0;
		private const uint WM_QUIT = 0x0012;
		private const uint PM_NOREMOVE = 0x0000;
		private const int THREAD_JOIN_TIMEOUT_MS = 2000;

		private const int GWL_EXSTYLE = -20;
		private const long WS_EX_TOOLWINDOW = 0x00000080;
		private const uint GW_OWNER = 4;

		private readonly IClientChangeSignal _signal;
		private readonly IThumbnailConfiguration _configuration;

		// Cache pid -> processo monitorado. Os callbacks chegam todos na thread do hook,
		// então não há concorrência aqui.
		private readonly Dictionary<uint, bool> _monitoredPids;

		// Precisa ficar em campo: o GC pode coletar o delegate e o hook passa a chamar
		// memória liberada (crash silencioso e difícil de diagnosticar).
		private WinEventProc _callback;

		private Thread _thread;
		private uint _threadId;
		private IntPtr _hook;
		private volatile bool _isRunning;

		public WindowEventSource(IClientChangeSignal signal, IThumbnailConfiguration configuration)
		{
			this._signal = signal;
			this._configuration = configuration;
			this._monitoredPids = new Dictionary<uint, bool>(64);
		}

		public bool IsRunning => this._isRunning;

		public void Start()
		{
			if (this._isRunning)
			{
				return;
			}

			this._isRunning = true;

			this._thread = new Thread(this.RunHookThread)
			{
				IsBackground = true,
				Name = "eveo-window-events"
			};
			this._thread.SetApartmentState(ApartmentState.STA);
			this._thread.Start();
		}

		public void Stop()
		{
			if (!this._isRunning)
			{
				return;
			}

			this._isRunning = false;

			// A thread só publica o próprio Id depois de subir; postar antes disso falha
			// silenciosamente (fila de mensagens ainda não existe) e o Join esperaria o
			// timeout inteiro.
			uint threadId = this.WaitForHookThreadId();
			if (threadId != 0)
			{
				PostThreadMessage(threadId, WindowEventSource.WM_QUIT, IntPtr.Zero, IntPtr.Zero);
			}

			this._thread?.Join(WindowEventSource.THREAD_JOIN_TIMEOUT_MS);
			this._thread = null;
		}

		private uint WaitForHookThreadId()
		{
			const int stepMs = 10;
			const int maxWaitMs = 500;

			for (int waited = 0; waited < maxWaitMs; waited += stepMs)
			{
				uint threadId = this._threadId;
				if (threadId != 0)
				{
					return threadId;
				}

				Thread.Sleep(stepMs);
			}

			return 0;
		}

		private void RunHookThread()
		{
			// Força a criação da fila de mensagens antes de publicar o Id: sem isso,
			// PostThreadMessage(WM_QUIT) pode chegar antes da fila existir.
			WindowEventSource.PeekMessage(out _, IntPtr.Zero, 0, 0, WindowEventSource.PM_NOREMOVE);

			this._threadId = WindowEventSource.GetCurrentThreadId();
			this._callback = this.OnWinEvent;

			// CREATE..NAMECHANGE cobre janela criada/destruida/mostrada/escondida/renomeada.
			this._hook = WindowEventSource.SetWinEventHook(
				WindowEventSource.EVENT_OBJECT_CREATE,
				WindowEventSource.EVENT_OBJECT_NAMECHANGE,
				IntPtr.Zero,
				this._callback,
				0,
				0,
				WindowEventSource.WINEVENT_OUTOFCONTEXT);

			try
			{
				while (WindowEventSource.GetMessage(out MSG message, IntPtr.Zero, 0, 0) > 0)
				{
					WindowEventSource.TranslateMessage(ref message);
					WindowEventSource.DispatchMessage(ref message);
				}
			}
			finally
			{
				if (this._hook != IntPtr.Zero)
				{
					WindowEventSource.UnhookWinEvent(this._hook);
					this._hook = IntPtr.Zero;
				}

				this._callback = null;
				this._threadId = 0;
			}
		}

		private void OnWinEvent(IntPtr hook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
		{
			// Só janelas top-level interessam: filhos (controles) gerariam ruído sem
			// nenhuma informação útil para o refresh das thumbnails.
			if (hwnd == IntPtr.Zero || idObject != WindowEventSource.OBJID_WINDOW)
			{
				return;
			}

			// O range CREATE..NAMECHANGE inclui LOCATIONCHANGE, que dispara dezenas de
			// vezes por segundo enquanto uma janela é arrastada. Só os eventos que mudam
			// o conjunto de clientes (ou o título) importam aqui.
			if (eventType != WindowEventSource.EVENT_OBJECT_CREATE
				&& eventType != WindowEventSource.EVENT_OBJECT_DESTROY
				&& eventType != WindowEventSource.EVENT_OBJECT_SHOW
				&& eventType != WindowEventSource.EVENT_OBJECT_HIDE
				&& eventType != WindowEventSource.EVENT_OBJECT_NAMECHANGE)
			{
				return;
			}

			// Menus, tooltips, popups e as próprias overlays do EVE-O (tool windows do
			// próprio app) não mudam o conjunto de clientes. Sem este filtro o app reage
			// ao seu próprio desenho — laço de refreshes e CPU alta em desktop ocupado.
			if ((WindowEventSource.GetWindowLongPtr(hwnd, WindowEventSource.GWL_EXSTYLE).ToInt64() & WindowEventSource.WS_EX_TOOLWINDOW) != 0)
			{
				return;
			}

			if (WindowEventSource.GetWindow(hwnd, WindowEventSource.GW_OWNER) != IntPtr.Zero)
			{
				return;
			}

			// Só processos configurados em ExecutablesToPreview interessam.
			if (!this.IsMonitoredWindow(hwnd))
			{
				return;
			}

			// O sinal é coalescido (canal de 1 item, DropOldest): publicar sem debounce
			// aqui é intencional, o consumidor recebe no máximo um refresh pendente.
			this._signal.Publish();
		}

		private bool IsMonitoredWindow(IntPtr hwnd)
		{
			WindowEventSource.GetWindowThreadProcessId(hwnd, out uint processId);
			if (processId == 0)
			{
				return false;
			}

			if (this._monitoredPids.TryGetValue(processId, out bool cached))
			{
				return cached;
			}

			bool monitored;
			try
			{
				using (Process process = Process.GetProcessById((int)processId))
				{
					// O cache vale para a vida do processo: o nome não muda.
					monitored = this._configuration.IsExecutableToPreview(process.ProcessName);
				}
			}
			catch (ArgumentException)
			{
				// Processo encerrou entre o evento e a consulta.
				monitored = false;
			}
			catch (InvalidOperationException)
			{
				monitored = false;
			}

			this._monitoredPids[processId] = monitored;
			return monitored;
		}

		public void Dispose()
		{
			this.Stop();
		}

		#region Interop

		private delegate void WinEventProc(IntPtr hook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint thread, uint time);

		[StructLayout(LayoutKind.Sequential)]
		private struct MSG
		{
			public IntPtr Hwnd;
			public uint Message;
			public IntPtr WParam;
			public IntPtr LParam;
			public uint Time;
			public int PointX;
			public int PointY;
		}

		[DllImport("user32.dll", SetLastError = true)]
		private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
			WinEventProc callback, uint idProcess, uint idThread, uint dwFlags);

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool UnhookWinEvent(IntPtr hook);

		[DllImport("user32.dll")]
		private static extern int GetMessage(out MSG message, IntPtr hwnd, uint messageFilterMin, uint messageFilterMax);

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool TranslateMessage(ref MSG message);

		[DllImport("user32.dll")]
		private static extern IntPtr DispatchMessage(ref MSG message);

		[DllImport("user32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool PostThreadMessage(uint threadId, uint message, IntPtr wParam, IntPtr lParam);

		[DllImport("kernel32.dll")]
		private static extern uint GetCurrentThreadId();

		[DllImport("user32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool PeekMessage(out MSG message, IntPtr hwnd, uint filterMin, uint filterMax, uint removeMsg);

		[DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
		private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);

		[DllImport("user32.dll")]
		private static extern IntPtr GetWindow(IntPtr hwnd, uint command);

		[DllImport("user32.dll")]
		private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

		#endregion
	}
}
