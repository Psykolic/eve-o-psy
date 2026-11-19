using System;
using System.Drawing;
using System.Windows.Forms;
using EveOPreview.Configuration;
using EveOPreview.Services;

namespace EveOPreview.View
{
	sealed class StaticThumbnailView : ThumbnailView
	{
		#region Private fields
		private readonly PictureBox _thumbnail;
		private readonly IStaticThumbnailProvider _staticThumbnailProvider;
		private IThumbnailConfiguration _config;
		private int _backgroundCaptureTicks;
		#endregion

		// A captura de cliente não ativo custa 30-60 ms por causa do retry com PrintWindow
		// (issue #50). Meio segundo de defasagem numa miniatura parada é imperceptível,
		// então o cliente em segundo plano é capturado a cada 2º refresh forçado.
		internal const int BackgroundCaptureInterval = 2;

		internal static bool NextBackgroundCapture(ref int tick)
		{
			var capture = (tick % BackgroundCaptureInterval) == 0;
			tick++;
			return capture;
		}

		public StaticThumbnailView(IWindowManager windowManager, IStaticThumbnailProvider staticThumbnailProvider, IThumbnailConfiguration config, IThumbnailManager thumbnailManager)
			: base(windowManager, config, thumbnailManager)
		{
			this._thumbnail = new StaticThumbnailImage
			{
				TabStop = false,
				SizeMode = PictureBoxSizeMode.StretchImage,
				Location = new Point(0, 0),
				Size = new Size(this.ClientSize.Width, this.ClientSize.Height)
			};
			this.Controls.Add(this._thumbnail);
			this._staticThumbnailProvider = staticThumbnailProvider;
			this._config = config;
		}

		protected override void RefreshThumbnail(bool forceRefresh)
		{
			if (!forceRefresh || this.IsPreventPreviews())
			{
				return;
			}

			// Cliente em primeiro plano mantém 1 Hz; os demais caem para 0.5 Hz.
			if (this.WindowManager.GetForegroundWindowHandle() != this.Id)
			{
				if (!NextBackgroundCapture(ref this._backgroundCaptureTicks))
				{
					return;
				}
			}
			else
			{
				// Voltar ao primeiro plano retoma a captura imediatamente.
				this._backgroundCaptureTicks = 0;
			}

			var thumbnail = this._staticThumbnailProvider.GetStaticThumbnail(this.Id);
			if (thumbnail != null)
			{
				var displayImage = this.CropThumbnail(thumbnail);
				var oldImage = this._thumbnail.Image;
				this._thumbnail.Image = displayImage;
				oldImage?.Dispose();
			}
		}

		private Image CropThumbnail(Image image)
		{
			Rectangle region = this.SourceRegion;
			if (region.IsEmpty)
			{
				return image;
			}

			region.Intersect(new Rectangle(Point.Empty, image.Size));
			if (region.Width < 1 || region.Height < 1)
			{
				return image;
			}

			Bitmap cropped = new Bitmap(region.Width, region.Height);
			using (Graphics graphics = Graphics.FromImage(cropped))
			{
				graphics.DrawImage(
					image,
					new Rectangle(0, 0, region.Width, region.Height),
					region,
					GraphicsUnit.Pixel);
			}

			image.Dispose();

			return cropped;
		}

		protected override void OnSourceRegionChanged()
		{
			this.Refresh(true);
		}

		protected override void ResizeThumbnail(int baseWidth, int baseHeight, int highlightWidthTop, int highlightWidthRight, int highlightWidthBottom, int highlightWidthLeft)
		{
			var left = 0 + highlightWidthLeft;
			var top = 0 + highlightWidthTop;
			if (this.IsLocationUpdateRequired(this._thumbnail.Location, left, top))
			{
				this._thumbnail.Location = new Point(left, top);
			}

			var width = baseWidth - highlightWidthLeft - highlightWidthRight;
			var height = baseHeight - highlightWidthTop - highlightWidthBottom;
			if (this.IsSizeUpdateRequired(this._thumbnail.Size, width, height))
			{
				this._thumbnail.Size = new Size(width, height);
			}
		}

		private bool IsLocationUpdateRequired(Point currentLocation, int left, int top)
		{
			return (currentLocation.X != left) || (currentLocation.Y != top);
		}

		private bool IsSizeUpdateRequired(Size currentSize, int width, int height)
		{
			return (currentSize.Width != width) || (currentSize.Height != height);
		}

		public override void Close()
		{
			this._thumbnail.Image?.Dispose();
			this._thumbnail.Image = null;

			base.Close();
		}
	}
}