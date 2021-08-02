/*
Copyright (c) 2017, Lars Brubaker, John Lewin
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
using System.Threading;
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

namespace MatterHackers.MatterControl.DesignTools
{
	public interface IImageProvider
	{
		ImageBuffer Image { get; }
	}

	[HideMeterialAndColor]
	public class ImageObject3D : AssetObject3D, IImageProvider, IObject3DControlsProvider
	{
		private const double DefaultSizeMm = 60;

		private string _assetPath;

		private ImageBuffer _image;

		private bool _invert;

		public ImageObject3D()
		{
			Name = "Image".Localize();
		}

		public override bool CanFlatten => false;

		[GoogleSearch]
		public string ImageSearch { get; set; } = "";


		[DisplayName("")]
		[JsonIgnore]
		[ImageDisplay(Margin = new int[] { 30, 3, 30, 3 }, MaxXSize = 400, Stretch = true)]
		public ImageBuffer Image
		{
			get
			{
				if (_image == null)
				{
					// set a temp image so we don't have any problems with threading
					var image = this.LoadImage();

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

					_image = image;

					// send the invalidate on image change
					Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Image));
					Invalidate(InvalidateType.DisplayValues);
				}

				return _image;
			}

			set
			{
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
					_image = null;
					var _ = Image;

					Invalidate(InvalidateType.Image);
				}
			}
		}


		[DisplayName("Open new image")]
		[Description("Open")]
		public override string AssetPath
		{
			get => _assetPath;
			set
			{
				if (_assetPath != value)
				{
					_assetPath = value;
					_image = null;

					InitMesh(this.Image);

					this.Invalidate(InvalidateType.DisplayValues);
				}
			}
		}

		public void AddObject3DControls(Object3DControlsLayer object3DControlsLayer)
		{
			object3DControlsLayer.AddControls(ControlTypes.Standard2D);
		}

		private ImageBuffer LoadImage()
		{
			// TODO: Consider non-awful alternatives
			var resetEvent = new AutoResetEvent(false);

			ImageBuffer imageBuffer = null;

			this.LoadAsset(CancellationToken.None, null).ContinueWith((streamTask) =>
			{
				Stream assetStream = null;
				try
				{
					assetStream = streamTask.Result;
					if (assetStream != null)
					{
						imageBuffer = ImageIO.LoadImage(assetStream);
					}
				}
				catch { }

				assetStream?.Dispose();

				resetEvent.Set();
			});

			// Wait up to 30 seconds for a given image asset
			resetEvent.WaitOne(30 * 1000);

			return imageBuffer;
		}

		public override Mesh Mesh
		{
			get
			{
				if (!string.IsNullOrWhiteSpace(this.AssetPath)
					// TODO: Remove this hack needed to work around Persistable = false
					&& (base.Mesh == null || base.Mesh.FaceTextures.Count <= 0))
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

			return base.Rebuild();
		}

		private Mesh InitMesh(ImageBuffer imageBuffer)
		{
			if (!string.IsNullOrWhiteSpace(this.AssetPath))
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
			}

			return null;
		}

		public void AddEditorExtra(GuiWidget imageWidget, ThemeConfig theme, Action updateEditorImage)
		{
			imageWidget.Click += (s, e) =>
			{
				if (e.Button == MouseButtons.Right)
				{
					var popupMenu = new PopupMenu(theme);

					var pasteMenu = popupMenu.CreateMenuItem("Paste".Localize());
					pasteMenu.Click += (s2, e2) =>
					{
						var activeImage = Clipboard.Instance.GetImage();

						// Persist
						string filePath = ApplicationDataStorage.Instance.GetNewLibraryFilePath(".png");
						ImageIO.SaveImageData(
							filePath,
							activeImage);

						this.AssetPath = filePath;
						this.Mesh = null;

						updateEditorImage();

						this.Invalidate(InvalidateType.Image);
					};

					pasteMenu.Enabled = Clipboard.Instance.ContainsImage;

					var copyMenu = popupMenu.CreateMenuItem("Copy".Localize());
					copyMenu.Click += (s2, e2) =>
					{
						Clipboard.Instance.SetImage(this.Image);
					};

					popupMenu.ShowMenu(imageWidget, e);
				}
			};
		}
	}
}