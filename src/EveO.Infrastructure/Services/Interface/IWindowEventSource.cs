using System;

namespace EveOPreview.Services
{
	/// <summary>
	/// Fonte de eventos de janela (hook do Windows) que alimenta o refresh das thumbnails
	/// sem polling. Introduzida na Fase 2.2 (ADR-006).
	/// </summary>
	public interface IWindowEventSource : IDisposable
	{
		bool IsRunning { get; }

		/// <summary>Instala o hook em uma thread dedicada com message pump.</summary>
		void Start();

		/// <summary>Encerra a thread do hook. Idempotente.</summary>
		void Stop();
	}
}
