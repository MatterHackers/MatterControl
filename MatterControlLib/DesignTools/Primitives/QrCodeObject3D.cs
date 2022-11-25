/*
Copyright (c) 2022 Lars Brubaker, John Lewin
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.ImageProcessing;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.PolygonMesh;
using Newtonsoft.Json;
using QRCoder;

namespace MatterHackers.MatterControl.DesignTools
{
    [HideMeterialAndColor]
	public class QrCodeObject3D : Object3D, IImageProvider, IObject3DControlsProvider, IPropertyGridModifier
	{
		private const double DefaultSizeMm = 60;

		private ImageBuffer _image;

		private bool _invert;

		public QrCodeObject3D()
		{
			Name = "QR Code".Localize();
		}

		public static async Task<QrCodeObject3D> Create()
		{
			var item = new QrCodeObject3D();
			await item.Rebuild();
			return item;
		}

		public override bool CanApply => false;

        public enum QrCodeTypes
		{
            Text,
            WiFi
		}

        [EnumDisplay(Mode = EnumDisplayAttribute.PresentationMode.Buttons)]
        public QrCodeTypes OutputOption { get; set; } = QrCodeTypes.Text;


		// WIFI:S:<SSID>;T:<WEP|WPA|blank>;P:<PASSWORD>;H:<true|false|blank>;;
		[Description("The name of the WiFi network")]
        public StringOrExpression SSID { get; set; } = "";

        [Description("The password of the WiFi network")]
        public StringOrExpression Password { get; set; } = "";

        public enum SecurityTypes
		{
            WEP,
            WPA,
            None
        }

		[EnumDisplay(Mode = EnumDisplayAttribute.PresentationMode.Buttons)]
		public SecurityTypes Security { get; set; } = SecurityTypes.WPA;

        public StringOrExpression Text { get; set; } = "https://www.matterhackers.com";

        [DisplayName("")]
		[JsonIgnore]
		[ImageDisplay(Margin = new int[] { 9, 3, 9, 3 }, MaxXSize = 400, Stretch = true)]
		public ImageBuffer Image
		{
			get
			{
				if (_image == null)
                {
                    RebuildImage();

                    // send the invalidate on image change
                    this.CancelAllParentBuilding();
                    Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Image));
                    Invalidate(InvalidateType.DisplayValues);
                }

                return _image;
			}

			set
			{
			}
		}

        private void RebuildImage()
        {
            // set a temp image so we don't have any problems with threading
            var image = this.BuildImage();

            if (image != null)
            {
                if (this.Invert)
                {
                    image = InvertLightness.DoInvertLightness(image);
                }
            }
            else // bad load
            {
                image = new ImageBuffer(200, 100);
                var graphics2D = image.NewGraphics2D();
                graphics2D.Clear(Color.White);
                graphics2D.DrawString("Image Missing".Localize(), image.Width / 2, image.Height / 2, 20, Agg.Font.Justification.Center, Agg.Font.Baseline.BoundsCenter);
            }

            // we don't want to invalidate on the mesh change
            using (RebuildLock())
            {
                base.Mesh = this.InitMesh(image) ?? PlatonicSolids.CreateCube(100, 100, 0.2);
            }

			if (_image == null)
			{
				_image = image;
			}
            else
            {
				_image.CopyFrom(image);
            }
        }

        public bool Invert
		{
			get => _invert;
			set
			{
				if (_invert != value)
				{
					_invert = value;
					RebuildImage();
					Invalidate(InvalidateType.Image);
				}
			}
		}

		public override async void OnInvalidate(InvalidateArgs invalidateArgs)
		{
			if ((invalidateArgs.InvalidateType.HasFlag(InvalidateType.Properties) && invalidateArgs.Source == this))
			{
				RebuildImage();
				await Rebuild();
			}
			else if (SheetObject3D.NeedsRebuild(this, invalidateArgs))
			{
				RebuildImage();
				await Rebuild();
			}
			else
			{
				base.OnInvalidate(invalidateArgs);
			}
		}

		public static string GetFileOrAsset(string file)
		{
			if (!File.Exists(file))
			{
				var path = Path.Combine(ApplicationDataStorage.Instance.LibraryAssetsPath, file);
				if (File.Exists(path))
				{
					return path;
				}

				// can't find a real file
				return null;
			}

			return file;
		}


		public static bool FilesAreEqual(string first, string second)
		{
			if (string.IsNullOrEmpty(first)
				|| string.IsNullOrEmpty(second))
			{
				return false;
			}

			var diskFirst = GetFileOrAsset(first);
			var diskSecond = GetFileOrAsset(second);
			if (File.Exists(diskFirst) && File.Exists(diskSecond))
			{
				return FilesAreEqual(new FileInfo(diskFirst), new FileInfo(diskSecond));
			}

			return false;
		}

		public static bool FilesAreEqual(FileInfo first, FileInfo second)
		{
			if (first.Length != second.Length)
			{
				return false;
			}

			if (string.Equals(first.FullName, second.FullName, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}

			int readSize = 1 << 16;
			int numReads = (int)Math.Ceiling((double)first.Length / readSize);

			using (var firstFs = first.OpenRead())
			{
				using (var secondFs = second.OpenRead())
				{
					byte[] one = new byte[readSize];
					byte[] two = new byte[readSize];

					for (int i = 0; i < numReads; i++)
					{
						firstFs.Read(one, 0, readSize);
						secondFs.Read(two, 0, readSize);

						for (int j = 0; j < readSize; j++)
						{
							if (one[j] != two[j])
							{
								return false;
							}
						}
					}
				}
			}

			return true;
		}
		public void AddObject3DControls(Object3DControlsLayer object3DControlsLayer)
		{
			object3DControlsLayer.AddControls(ControlTypes.Standard2D);
		}

		private ImageBuffer BuildImage()
		{
			var text = Text.Value(this);

			if (OutputOption == QrCodeTypes.WiFi)
			{
				var ssid = SSID.Value(this).Replace(":", "\\:");
				var security = "";
				switch(Security)
				{
					case SecurityTypes.WPA:
						security = "WPA";
						break;
					case SecurityTypes.WEP:
						security = "WEP";
						break;
				}
				var password = Password.Value(this).Replace(":", "\\:");

				text = $"WIFI:S:{ssid};T:{security};P:{password};H:;;";
			}

            QRCodeGenerator qrGenerator = new QRCodeGenerator();
			QRCodeData qrCodeData = qrGenerator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
			QRCode qrCode = new QRCode(qrCodeData);
            System.Drawing.Bitmap qrCodeImage = qrCode.GetGraphic(16);

			var destImage = new ImageBuffer();
			ConvertBitmapToImage(destImage, qrCodeImage);
			return destImage;
		}

		public bool ConvertBitmapToImage(ImageBuffer destImage, System.Drawing.Bitmap bitmap)
		{
			if (bitmap != null)
			{
				switch (bitmap.PixelFormat)
				{
					case System.Drawing.Imaging.PixelFormat.Format32bppArgb:
						{
							destImage.Allocate(bitmap.Width, bitmap.Height, bitmap.Width * 4, 32);
							if (destImage.GetRecieveBlender() == null)
							{
								destImage.SetRecieveBlender(new BlenderBGRA());
							}

                            System.Drawing.Imaging.BitmapData bitmapData = bitmap.LockBits(
								new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height), System.Drawing.Imaging.ImageLockMode.ReadWrite, bitmap.PixelFormat);
							int sourceIndex = 0;
							int destIndex = 0;
							unsafe
							{
								byte[] destBuffer = destImage.GetBuffer(out int offset);
								byte* pSourceBuffer = (byte*)bitmapData.Scan0;
								for (int y = 0; y < destImage.Height; y++)
								{
									destIndex = destImage.GetBufferOffsetXY(0, destImage.Height - 1 - y);
									for (int x = 0; x < destImage.Width; x++)
									{
#if true
										destBuffer[destIndex++] = pSourceBuffer[sourceIndex++];
										destBuffer[destIndex++] = pSourceBuffer[sourceIndex++];
										destBuffer[destIndex++] = pSourceBuffer[sourceIndex++];
										destBuffer[destIndex++] = pSourceBuffer[sourceIndex++];
#else
                                            Color notPreMultiplied = new Color(pSourceBuffer[sourceIndex + 0], pSourceBuffer[sourceIndex + 1], pSourceBuffer[sourceIndex + 2], pSourceBuffer[sourceIndex + 3]);
                                            sourceIndex += 4;
                                            Color preMultiplied = notPreMultiplied.ToColorF().premultiply().ToColor();
                                            destBuffer[destIndex++] = preMultiplied.blue;
                                            destBuffer[destIndex++] = preMultiplied.green;
                                            destBuffer[destIndex++] = preMultiplied.red;
                                            destBuffer[destIndex++] = preMultiplied.alpha;
#endif
									}
								}
							}

							bitmap.UnlockBits(bitmapData);

							return true;
						}

					case System.Drawing.Imaging.PixelFormat.Format24bppRgb:
						{
							destImage.Allocate(bitmap.Width, bitmap.Height, bitmap.Width * 4, 32);
							if (destImage.GetRecieveBlender() == null)
							{
								destImage.SetRecieveBlender(new BlenderBGRA());
							}

                            System.Drawing.Imaging.BitmapData bitmapData = bitmap.LockBits(new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height), System.Drawing.Imaging.ImageLockMode.ReadWrite, bitmap.PixelFormat);
							int sourceIndex = 0;
							int destIndex = 0;
							unsafe
							{
								byte[] destBuffer = destImage.GetBuffer(out int offset);
								byte* pSourceBuffer = (byte*)bitmapData.Scan0;
								for (int y = 0; y < destImage.Height; y++)
								{
									sourceIndex = y * bitmapData.Stride;
									destIndex = destImage.GetBufferOffsetXY(0, destImage.Height - 1 - y);
									for (int x = 0; x < destImage.Width; x++)
									{
										destBuffer[destIndex++] = pSourceBuffer[sourceIndex++];
										destBuffer[destIndex++] = pSourceBuffer[sourceIndex++];
										destBuffer[destIndex++] = pSourceBuffer[sourceIndex++];
										destBuffer[destIndex++] = 255;
									}
								}
							}

							bitmap.UnlockBits(bitmapData);
							return true;
						}

					default:
						// let this code fall through and return false
						break;
				}
			}

			return false;
		}

		public override Mesh Mesh
		{
			get
			{
				if (base.Mesh == null || base.Mesh.FaceTextures.Count <= 0)
				{
					using (this.RebuildLock())
					{
						// TODO: Revise fallback mesh
						base.Mesh = this.InitMesh(this.Image) ?? PlatonicSolids.CreateCube(100, 100, 0.2);
					}
				}

				return base.Mesh;
			}
		}

		public double ScaleMmPerPixels { get; private set; }

		public override Task Rebuild()
		{
			InitMesh(this.Image);

			UiThread.RunOnIdle(() => Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Image)));

			return base.Rebuild();
		}

		private Mesh InitMesh(ImageBuffer imageBuffer)
		{
			if (imageBuffer != null)
			{
				ScaleMmPerPixels = Math.Min(DefaultSizeMm / imageBuffer.Width, DefaultSizeMm / imageBuffer.Height);

				// Create texture mesh
				double width = ScaleMmPerPixels * imageBuffer.Width;
				double height = ScaleMmPerPixels * imageBuffer.Height;

				Mesh textureMesh = PlatonicSolids.CreateCube(width, height, 0.2);
				textureMesh.PlaceTextureOnFaces(0, imageBuffer);

				return textureMesh;
			}

			return null;
		}

		public static void ModifyImageObjectEditorWidget(ImageObject3D imageObject, GuiWidget widget, ThemeConfig theme, Action requestWidgetUpdate)
		{
			widget.Click += (s, e) =>
			{
				if (e.Button == MouseButtons.Left)
				{
					ShowOpenDialog(imageObject);
				}

				if (e.Button == MouseButtons.Right)
				{
					var popupMenu = new PopupMenu(theme);

					var openMenu = popupMenu.CreateMenuItem("Open".Localize());
					openMenu.Click += (s2, e2) =>
					{
						popupMenu.Close();
						ShowOpenDialog(imageObject);
					};

					popupMenu.CreateSeparator();

					var copyMenu = popupMenu.CreateMenuItem("Copy".Localize());
					copyMenu.Click += (s2, e2) =>
					{
						Clipboard.Instance.SetImage(imageObject.Image);
					};

					var pasteMenu = popupMenu.CreateMenuItem("Paste".Localize());
					pasteMenu.Click += (s2, e2) =>
					{
						var activeImage = Clipboard.Instance.GetImage();

						// Persist
						string filePath = ApplicationDataStorage.Instance.GetNewLibraryFilePath(".png");
						ImageIO.SaveImageData(
							filePath,
							activeImage);

						imageObject.AssetPath = filePath;
						imageObject.Mesh = null;

						requestWidgetUpdate();

						imageObject.Invalidate(InvalidateType.Image);
					};

					pasteMenu.Enabled = Clipboard.Instance.ContainsImage;

					popupMenu.ShowMenu(widget, e);
				}
			};
		}

		public static void ShowOpenDialog(IAssetObject assetObject)
		{
			UiThread.RunOnIdle(() =>
			{
				// we do this using to make sure that the stream is closed before we try and insert the Picture
				AggContext.FileDialogs.OpenFileDialog(
					new OpenFileDialogParams(
						"Select an image file|*.jpg;*.png;*.bmp;*.gif;*.pdf",
						multiSelect: false,
						title: "Add Image".Localize()),
						(openParams) =>
						{
							if (!File.Exists(openParams.FileName))
							{
								return;
							}

							assetObject.AssetPath = openParams.FileName;
						});
			});
		}

        public void UpdateControls(PublicPropertyChange change)
        {
            change.SetRowVisible(nameof(Text), () => OutputOption == QrCodeTypes.Text);
            change.SetRowVisible(nameof(SSID), () => OutputOption == QrCodeTypes.WiFi);
            change.SetRowVisible(nameof(Password), () => OutputOption == QrCodeTypes.WiFi);
            change.SetRowVisible(nameof(Security), () => OutputOption == QrCodeTypes.WiFi);
        }
    }
}