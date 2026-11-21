using System;
using System.Drawing;
using System.Runtime.InteropServices;
using EveOPreview.Services.Interop;

namespace EveOPreview.Services.Implementation
{
	class DwmThumbnail : IDwmThumbnail
	{
		#region Private fields
		private readonly IWindowManager _windowManager;
		private SafeDwmThumbnailHandle _handle;
		private DWM_THUMBNAIL_PROPERTIES _properties;
		#endregion

		public DwmThumbnail(IWindowManager windowManager)
		{
			this._windowManager = windowManager;
			this._handle = new SafeDwmThumbnailHandle();
		}

		public void Register(IntPtr destination, IntPtr source)
		{
			this._properties = new DWM_THUMBNAIL_PROPERTIES();
			this._properties.dwFlags = DWM_TNP_CONSTANTS.DWM_TNP_VISIBLE
									  + DWM_TNP_CONSTANTS.DWM_TNP_OPACITY
									  + DWM_TNP_CONSTANTS.DWM_TNP_RECTDESTINATION
									  + DWM_TNP_CONSTANTS.DWM_TNP_SOURCECLIENTAREAONLY;
			this._properties.opacity = 255;
			this._properties.fVisible = true;
			this._properties.fSourceClientAreaOnly = true;

			if (!this._windowManager.IsCompositionEnabled)
			{
				return;
			}

			// Registro novo sempre substitui o anterior: descartar aqui evita acumular
			// duas inscrições no DWM quando a view é reciclada.
			this.ReplaceHandle(new SafeDwmThumbnailHandle());

			try
			{
				this.ReplaceHandle(new SafeDwmThumbnailHandle(
					DwmNativeMethods.DwmRegisterThumbnail(destination, source)));
			}
			catch (ArgumentException)
			{
				// This exception is raised if the source client is already closed
				// Can happen on a really slow CPU's that the window is still being
				// listed in the process list yet it already cannot be used as
				// a thumbnail source
			}
			catch (COMException)
			{
				// This exception is raised if DWM is suddenly not available
				// (f.e. when switching between Windows user accounts)
			}
		}

		public void Unregister()
		{
			this.ReplaceHandle(new SafeDwmThumbnailHandle());
		}

		public void Move(int left, int top, int right, int bottom)
		{
			this._properties.rcDestination = new RECT(left, top, right, bottom);
		}

		public void SetSourceRegion(Rectangle region)
		{
			if (region.Width <= 0 || region.Height <= 0)
			{
				return;
			}

			this._properties.rcSource = new RECT(region.Left, region.Top, region.Right, region.Bottom);
			this._properties.dwFlags |= DWM_TNP_CONSTANTS.DWM_TNP_RECTSOURCE;
		}

		public void Update()
		{
			if ((!this._windowManager.IsCompositionEnabled) || this._handle.IsInvalid)
			{
				return;
			}

			try
			{
				DwmNativeMethods.DwmUpdateThumbnailProperties(this._handle.DangerousGetHandle(), this._properties);
			}
			catch (ArgumentException)
			{
				// This exception will be thrown if the EVE client disappears while this method is running
			}
			catch (COMException)
			{
				// This exception is raised when DWM is not available for some reason
			}
		}

		/// <summary>Devolve o registro ao DWM. Idempotente.</summary>
		public void Dispose() => this.Unregister();

		private void ReplaceHandle(SafeDwmThumbnailHandle handle)
		{
			SafeDwmThumbnailHandle anterior = this._handle;
			this._handle = handle;

			// Só descarta depois de trocar: se o registro novo falhar, o antigo já saiu.
			anterior?.Dispose();
		}
	}
}
