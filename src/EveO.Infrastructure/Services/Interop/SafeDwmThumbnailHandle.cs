using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace EveOPreview.Services.Interop
{
	/// <summary>
	/// Handle de thumbnail registrada no DWM.
	///
	/// Antes: um <c>IntPtr</c> solto, liberado só quando o chamador lembrava de
	/// <c>Unregister()</c> — se o app fechasse a view por outro caminho, o registro
	/// ficava. Agora o handle é um <see cref="SafeHandle"/>: mesmo que o fluxo
	/// normal falhe, o finalizer devolve o registro ao DWM.
	/// </summary>
	internal sealed class SafeDwmThumbnailHandle : SafeHandleZeroOrMinusOneIsInvalid
	{
		public SafeDwmThumbnailHandle()
			: base(true)
		{
		}

		public SafeDwmThumbnailHandle(IntPtr handle)
			: base(true)
		{
			this.SetHandle(handle);
		}

		protected override bool ReleaseHandle()
		{
			try
			{
				DwmNativeMethods.DwmUnregisterThumbnail(this.handle);
				return true;
			}
			catch (Exception)
			{
				// DWM pode ter ido embora (troca de usuário, desligamento): nada a fazer,
				// e o finalizer não pode lançar.
				return false;
			}
		}
	}
}
