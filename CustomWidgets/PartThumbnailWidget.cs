/*
Copyright (c) 2014, Lars Brubaker
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

using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.RayTracer;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl
{
	public class PartThumbnailWidget : ClickWidget
	{
		const int tooBigAndroid = 50000000;
		const int tooBigDesktop = 250000000;

		const int renderOrthoAndroid = 20000000;
		const int renderOrthoDesktop = 100000000;

		// all the color stuff
		new public double BorderWidth = 0;

		public RGBA_Bytes FillColor = new RGBA_Bytes(255, 255, 255);

		public RGBA_Bytes HoverBackgroundColor = new RGBA_Bytes(0, 0, 0, 50);

		protected double borderRadius = 0;

		//Don't delete this - required for OnDraw
		protected RGBA_Bytes HoverBorderColor = new RGBA_Bytes();

		private static bool processingThumbnail = false;

		private static string partExtension = ".png";

		private ImageBuffer buildingThumbnailImage = new Agg.Image.ImageBuffer();

		private RGBA_Bytes normalBackgroundColor = ActiveTheme.Instance.PrimaryAccentColor;

		private ImageBuffer noThumbnailImage = new Agg.Image.ImageBuffer();

		private PartPreviewMainWindow partPreviewWindow;

		private PrintItemWrapper printItem;

		private bool thumbNailHasBeenCreated = false;

		private ImageBuffer thumbnailImage = new Agg.Image.ImageBuffer();

		public PartThumbnailWidget(PrintItemWrapper item, string noThumbnailFileName, string buildingThumbnailFileName, ImageSizes size)
		{
			this.PrintItem = item;

			EnsureCorrectPartExtension();

			// Set Display Attributes
			this.Margin = new BorderDouble(0);
			this.Padding = new BorderDouble(5);
			Size = size;
			switch (size)
			{
				case ImageSizes.Size50x50:
					this.Width = 50 * TextWidget.GlobalPointSizeScaleRatio;
					this.Height = 50 * TextWidget.GlobalPointSizeScaleRatio;
					break;

				case ImageSizes.Size115x115:
					this.Width = 115 * TextWidget.GlobalPointSizeScaleRatio;
					this.Height = 115 * TextWidget.GlobalPointSizeScaleRatio;
					break;

				default:
					throw new NotImplementedException();
			}
			this.MinimumSize = new Vector2(this.Width, this.Height);

			this.BackgroundColor = normalBackgroundColor;
			this.Cursor = Cursors.Hand;

			// set background images
			if (noThumbnailImage.Width == 0)
			{
				StaticData.Instance.LoadIcon(noThumbnailFileName, noThumbnailImage);
				StaticData.Instance.LoadIcon(buildingThumbnailFileName, buildingThumbnailImage);
			}
			this.thumbnailImage = new ImageBuffer(buildingThumbnailImage);

			// Add Handlers
			this.Click += new EventHandler(OnMouseClick);
			this.MouseEnterBounds += new EventHandler(onEnter);
			this.MouseLeaveBounds += new EventHandler(onExit);
			ActiveTheme.Instance.ThemeChanged.RegisterEvent(ThemeChanged, ref unregisterEvents);
		}

		private event EventHandler unregisterEvents;

		public enum ImageSizes { Size50x50, Size115x115 };

		private enum RenderType { NONE, ORTHOGROPHIC, PERSPECTIVE, RAY_TRACE };

		public PrintItemWrapper PrintItem
		{
			get { return printItem; }
			set
			{
				if (printItem != null)
				{
					printItem.FileHasChanged.UnregisterEvent(item_FileHasChanged, ref unregisterEvents);
				}
				printItem = value;
				thumbNailHasBeenCreated = false;
				if (printItem != null)
				{
					printItem.FileHasChanged.RegisterEvent(item_FileHasChanged, ref unregisterEvents);
				}
			}
		}

		public ImageSizes Size { get; set; }

		private Point2D bigRenderSize
		{
			get
			{
				switch (GetRenderType(printItem.FileLocation))
				{
					case RenderType.RAY_TRACE:
						return new Point2D(115, 115);

					case RenderType.PERSPECTIVE:
					case RenderType.ORTHOGROPHIC:
						return new Point2D(460, 460);

					default:
						return new Point2D(460, 460);
				}
			}
		}

		public static void CleanUpCacheData()
		{
			//string pngFileName = GetFilenameForSize(stlHashCode, ref size);
			// delete everything that is a tga (we now save pngs).
		}

		public static string GetImageFilenameForItem(PrintItemWrapper item)
		{
			return GetFilenameForSize(item.FileHashCode.ToString(), new Point2D(460, 460));
		}

		public override void OnClosed(EventArgs e)
		{
			if (unregisterEvents != null)
			{
				unregisterEvents(this, null);
			}
			if (printItem != null)
			{
				printItem.FileHasChanged.UnregisterEvent(item_FileHasChanged, ref unregisterEvents);
			}
			base.OnClosed(e);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			//Trigger thumbnail generation if neeeded
			if (!thumbNailHasBeenCreated && !processingThumbnail)
			{
				Task.Run(() => LoadOrCreateThumbnail());
			}

			if (this.FirstWidgetUnderMouse)
			{
				RoundedRect rectBorder = new RoundedRect(this.LocalBounds, 0);
				//graphics2D.Render(rectBorder, this.HoverBackgroundColor);
			}
			graphics2D.Render(thumbnailImage, Width / 2 - thumbnailImage.Width / 2, Height / 2 - thumbnailImage.Height / 2);
			base.OnDraw(graphics2D);

			if (HoverBorderColor.Alpha0To255 > 0)
			{
				RectangleDouble Bounds = LocalBounds;
				RoundedRect borderRect = new RoundedRect(this.LocalBounds, this.borderRadius);
				Stroke strokeRect = new Stroke(borderRect, BorderWidth);
				graphics2D.Render(strokeRect, HoverBorderColor);
			}
		}

		public void ThemeChanged(object sender, EventArgs e)
		{
			//Set background color to new theme
			this.normalBackgroundColor = ActiveTheme.Instance.PrimaryAccentColor;
			this.BackgroundColor = ActiveTheme.Instance.PrimaryAccentColor;

			//Regenerate thumbnails
			// The thumbnail color is currently white and does not change with this change.
			// If we eventually change the thumbnail color with the theme we will need to change this.
			//this.thumbNailHasBeenRequested = false;
			this.Invalidate();
		}

		private static ImageBuffer BuildImageFromMeshGroups(List<MeshGroup> loadedMeshGroups, string stlHashCode, Point2D size)
		{
			if (loadedMeshGroups != null
				&& loadedMeshGroups.Count > 0
				&& loadedMeshGroups[0].Meshes != null
				&& loadedMeshGroups[0].Meshes[0] != null)
			{
				ImageBuffer tempImage = new ImageBuffer(size.x, size.y, 32, new BlenderBGRA());
				Graphics2D partGraphics2D = tempImage.NewGraphics2D();
				partGraphics2D.Clear(new RGBA_Bytes());

				AxisAlignedBoundingBox aabb = loadedMeshGroups[0].GetAxisAlignedBoundingBox();
				for (int meshGroupIndex = 1; meshGroupIndex < loadedMeshGroups.Count; meshGroupIndex++)
				{
					aabb = AxisAlignedBoundingBox.Union(aabb, loadedMeshGroups[meshGroupIndex].GetAxisAlignedBoundingBox());
				}
				double maxSize = Math.Max(aabb.XSize, aabb.YSize);
				double scale = size.x / (maxSize * 1.2);
				RectangleDouble bounds2D = new RectangleDouble(aabb.minXYZ.x, aabb.minXYZ.y, aabb.maxXYZ.x, aabb.maxXYZ.y);
				foreach (MeshGroup meshGroup in loadedMeshGroups)
				{
					foreach (Mesh loadedMesh in meshGroup.Meshes)
					{
						PolygonMesh.Rendering.OrthographicZProjection.DrawTo(partGraphics2D, loadedMesh,
							new Vector2((size.x / scale - bounds2D.Width) / 2 - bounds2D.Left,
								(size.y / scale - bounds2D.Height) / 2 - bounds2D.Bottom),
							scale, RGBA_Bytes.White);
					}
				}

				if (File.Exists("RunUnitTests.txt"))
				{
					foreach (Mesh loadedMesh in loadedMeshGroups[0].Meshes)
					{
						List<MeshEdge> nonManifoldEdges = loadedMesh.GetNonManifoldEdges();
						if (nonManifoldEdges.Count > 0)
						{
							partGraphics2D.Circle(size.x / 4, size.x / 4, size.x / 8, RGBA_Bytes.Red);
						}
					}
				}

				// and give it back
				return tempImage;
			}

			return null;
		}

		private static void CreateImage(PartThumbnailWidget thumbnailWidget, double Width, double Height)
		{
			thumbnailWidget.thumbnailImage = new ImageBuffer((int)Width, (int)Height, 32, new BlenderBGRA());
		}

		private static void EnsureCorrectPartExtension()
		{
			if (OsInformation.OperatingSystem == OSType.Mac
				|| OsInformation.OperatingSystem == OSType.Android
				|| OsInformation.OperatingSystem == OSType.X11)
			{
				partExtension = ".tga";
			}
		}

		private static string GetFilenameForSize(string stlHashCode, Point2D size)
		{
			EnsureCorrectPartExtension();

			string folderToSaveThumbnailsTo = ThumbnailPath();
			string imageFileName = Path.Combine(folderToSaveThumbnailsTo, Path.ChangeExtension("{0}_{1}x{2}".FormatWith(stlHashCode, size.x, size.y), partExtension));
			return imageFileName;
		}

		private static string GetImageFileName(string stlHashCode, Point2D size)
		{
			string imageFileName = GetFilenameForSize(stlHashCode, size);
			string folderToSavePrintsTo = Path.GetDirectoryName(imageFileName);

			if (!Directory.Exists(folderToSavePrintsTo))
			{
				Directory.CreateDirectory(folderToSavePrintsTo);
			}

			return imageFileName;
		}

		private static RenderType GetRenderType(string fileLocation)
		{
			if (UserSettings.Instance.get("ThumbnailRenderingMode") == "raytraced")
			{
				if (Is32Bit())
				{
					long estimatedMemoryUse = 0;
					if (File.Exists(fileLocation))
					{
						estimatedMemoryUse = MeshFileIo.GetEstimatedMemoryUse(fileLocation);

						if (OsInformation.OperatingSystem == OSType.Android)
						{
							if (estimatedMemoryUse > renderOrthoAndroid)
							{
								return RenderType.ORTHOGROPHIC;
							}
						}
						else
						{
							if (estimatedMemoryUse > renderOrthoDesktop)
							{
								return RenderType.ORTHOGROPHIC;
							}
						}
					}
				}

				return RenderType.RAY_TRACE;
			}

			return RenderType.ORTHOGROPHIC;
		}

		private static ImageBuffer LoadImageFromDisk(PartThumbnailWidget thumbnailWidget, string stlHashCode, Point2D size)
		{
			ImageBuffer tempImage = new ImageBuffer(size.x, size.y, 32, new BlenderBGRA());
			string imageFileName = GetFilenameForSize(stlHashCode, size);

			if (File.Exists(imageFileName))
			{
				if (partExtension == ".png")
				{
					if (ImageIO.LoadImageData(imageFileName, tempImage))
					{
						return tempImage;
					}
				}
				else
				{
					if (ImageTgaIO.LoadImageData(imageFileName, tempImage))
					{
						return tempImage;
					}
				}
			}

			return null;
		}

		private static string ThumbnailPath()
		{
			string applicationUserDataPath = ApplicationDataStorage.Instance.ApplicationUserDataPath;
			string folderToSaveThumbnailsTo = Path.Combine(applicationUserDataPath, "data", "temp", "thumbnails");
			return folderToSaveThumbnailsTo;
		}

		private void CreateThumbnail(PartThumbnailWidget thumbnailWidget)
		{
			if (thumbnailWidget != null)
			{
				string stlHashCode = thumbnailWidget.PrintItem.FileHashCode.ToString();

				ImageBuffer bigRender = new ImageBuffer();
				if (!File.Exists(thumbnailWidget.PrintItem.FileLocation))
				{
					return;
				}

				List<MeshGroup> loadedMeshGroups = MeshFileIo.Load(thumbnailWidget.PrintItem.FileLocation);

				RenderType renderType = GetRenderType(thumbnailWidget.PrintItem.FileLocation);

				switch (renderType)
				{
					case RenderType.RAY_TRACE:
						{
							ThumbnailTracer tracer = new ThumbnailTracer(loadedMeshGroups, bigRenderSize.x, bigRenderSize.y);
							tracer.DoTrace();

							bigRender = tracer.destImage;
						}
						break;

					case RenderType.PERSPECTIVE:
						{
							ThumbnailTracer tracer = new ThumbnailTracer(loadedMeshGroups, bigRenderSize.x, bigRenderSize.y);
							thumbnailWidget.thumbnailImage = new ImageBuffer(thumbnailWidget.buildingThumbnailImage);
							thumbnailWidget.thumbnailImage.NewGraphics2D().Clear(new RGBA_Bytes(255, 255, 255, 0));

							bigRender = new ImageBuffer(bigRenderSize.x, bigRenderSize.y, 32, new BlenderBGRA());

							foreach (MeshGroup meshGroup in loadedMeshGroups)
							{
								double minZ = double.MaxValue;
								double maxZ = double.MinValue;
								foreach (Mesh loadedMesh in meshGroup.Meshes)
								{
									tracer.GetMinMaxZ(loadedMesh, ref minZ, ref maxZ);
								}

								foreach (Mesh loadedMesh in meshGroup.Meshes)
								{
									tracer.DrawTo(bigRender.NewGraphics2D(), loadedMesh, RGBA_Bytes.White, minZ, maxZ);
								}
							}

							if (bigRender == null)
							{
								bigRender = new ImageBuffer(thumbnailWidget.noThumbnailImage);
							}
						}
						break;

					case RenderType.NONE:
					case RenderType.ORTHOGROPHIC:

						thumbnailWidget.thumbnailImage = new ImageBuffer(thumbnailWidget.buildingThumbnailImage);
						thumbnailWidget.thumbnailImage.NewGraphics2D().Clear(new RGBA_Bytes(255, 255, 255, 0));
						bigRender = BuildImageFromMeshGroups(loadedMeshGroups, stlHashCode, bigRenderSize);
						if (bigRender == null)
						{
							bigRender = new ImageBuffer(thumbnailWidget.noThumbnailImage);
						}
						break;
				}

				// and save it to disk
				string imageFileName = GetImageFileName(stlHashCode, bigRenderSize);

				if (partExtension == ".png")
				{
					ImageIO.SaveImageData(imageFileName, bigRender);
				}
				else
				{
					ImageTgaIO.SaveImageData(imageFileName, bigRender);
				}

				ImageBuffer unScaledImage = new ImageBuffer(bigRender.Width, bigRender.Height, 32, new BlenderBGRA());
				unScaledImage.NewGraphics2D().Render(bigRender, 0, 0);
				// If the source image (the one we downloaded) is more than twice as big as our dest image.
				while (unScaledImage.Width > Width * 2)
				{
					// The image sampler we use is a 2x2 filter so we need to scale by a max of 1/2 if we want to get good results.
					// So we scale as many times as we need to to get the Image to be the right size.
					// If this were going to be a non-uniform scale we could do the x and y separatly to get better results.
					ImageBuffer halfImage = new ImageBuffer(unScaledImage.Width / 2, unScaledImage.Height / 2, 32, new BlenderBGRA());
					halfImage.NewGraphics2D().Render(unScaledImage, 0, 0, 0, halfImage.Width / (double)unScaledImage.Width, halfImage.Height / (double)unScaledImage.Height);
					unScaledImage = halfImage;
				}

				thumbnailWidget.thumbnailImage = new ImageBuffer((int)Width, (int)Height, 32, new BlenderBGRA());
				thumbnailWidget.thumbnailImage.NewGraphics2D().Clear(new RGBA_Bytes(255, 255, 255, 0));
				thumbnailWidget.thumbnailImage.NewGraphics2D().Render(unScaledImage, 0, 0, 0, (double)thumbnailWidget.thumbnailImage.Width / unScaledImage.Width, (double)thumbnailWidget.thumbnailImage.Height / unScaledImage.Height);

				UiThread.RunOnIdle(thumbnailWidget.EnsureImageUpdated);
			}
		}

		private void DoOnMouseClick()
		{
			if (printItem != null)
			{
				string pathAndFile = printItem.FileLocation;
				if (File.Exists(pathAndFile))
				{
					bool shiftKeyDown = Keyboard.IsKeyDown(Keys.ShiftKey);
					if (shiftKeyDown)
					{
						OpenPartPreviewWindow(View3DWidget.AutoRotate.Disabled);
					}
					else
					{
						OpenPartPreviewWindow(View3DWidget.AutoRotate.Enabled);
					}
				}
				else
				{
					QueueRowItem.ShowCantFindFileMessage(printItem);
				}
			}
		}

		private void EnsureImageUpdated()
		{
			thumbnailImage.MarkImageChanged();
			Invalidate();
		}

		private void item_FileHasChanged(object sender, EventArgs e)
		{
			thumbNailHasBeenCreated = false;
			Invalidate();
		}
		private void onEnter(object sender, EventArgs e)
		{
			HoverBorderColor = new RGBA_Bytes(255, 255, 255);
			this.Invalidate();
		}

		private void onExit(object sender, EventArgs e)
		{
			HoverBorderColor = new RGBA_Bytes();
			this.Invalidate();
		}

		private void OnMouseClick(object sender, EventArgs e)
		{
			UiThread.RunOnIdle(DoOnMouseClick);
		}

		private void OpenPartPreviewWindow(View3DWidget.AutoRotate autoRotate)
		{
			if (partPreviewWindow == null)
			{
				partPreviewWindow = new PartPreviewMainWindow(this.PrintItem, autoRotate);
				partPreviewWindow.Closed += (object sender, EventArgs e) =>
				{
					this.partPreviewWindow = null;
				};
			}
			else
			{
				partPreviewWindow.BringToFront();
			}
		}

		private bool SetImageFast()
		{
			if (this.printItem == null)
			{
				this.thumbnailImage = new ImageBuffer(this.noThumbnailImage);
				this.Invalidate();
				return true;
			}

			if (this.PrintItem.FileLocation == QueueData.SdCardFileName)
			{
				switch (this.Size)
				{
					case ImageSizes.Size115x115:
						{
							StaticData.Instance.LoadIcon(Path.ChangeExtension("icon_sd_card_115x115", partExtension), this.thumbnailImage);
						}
						break;

					case ImageSizes.Size50x50:
						{
							StaticData.Instance.LoadIcon(Path.ChangeExtension("icon_sd_card_50x50", partExtension), this.thumbnailImage);
						}
						break;

					default:
						throw new NotImplementedException();
				}
				this.thumbnailImage.SetRecieveBlender(new BlenderPreMultBGRA());
				Graphics2D graphics = this.thumbnailImage.NewGraphics2D();
				Ellipse outline = new Ellipse(new Vector2(Width / 2.0, Height / 2.0), Width / 2 - Width / 12);
				graphics.Render(new Stroke(outline, Width / 12), RGBA_Bytes.White);

				UiThread.RunOnIdle(this.EnsureImageUpdated);
				return true;
			}
			else if (Path.GetExtension(this.PrintItem.FileLocation).ToUpper() == ".GCODE")
			{
				CreateImage(this, Width, Height);
				this.thumbnailImage.SetRecieveBlender(new BlenderPreMultBGRA());
				Graphics2D graphics = this.thumbnailImage.NewGraphics2D();
				Vector2 center = new Vector2(Width / 2.0, Height / 2.0);
				Ellipse outline = new Ellipse(center, Width / 2 - Width / 12);
				graphics.Render(new Stroke(outline, Width / 12), RGBA_Bytes.White);
				graphics.DrawString("GCode", center.x, center.y, 8 * Width / 50, Agg.Font.Justification.Center, Agg.Font.Baseline.BoundsCenter, color: RGBA_Bytes.White);

				UiThread.RunOnIdle(this.EnsureImageUpdated);
				return true;
			}
			else if (!File.Exists(this.PrintItem.FileLocation))
			{
				CreateImage(this, Width, Height);
				this.thumbnailImage.SetRecieveBlender(new BlenderPreMultBGRA());
				Graphics2D graphics = this.thumbnailImage.NewGraphics2D();
				Vector2 center = new Vector2(Width / 2.0, Height / 2.0);
				graphics.DrawString("Missing", center.x, center.y, 8 * Width / 50, Agg.Font.Justification.Center, Agg.Font.Baseline.BoundsCenter, color: RGBA_Bytes.White);

				UiThread.RunOnIdle(this.EnsureImageUpdated);
				return true;
			}
			else if (MeshIsTooBigToLoad(this.PrintItem.FileLocation))
			{
				CreateImage(this, Width, Height);
				this.thumbnailImage.SetRecieveBlender(new BlenderPreMultBGRA());
				Graphics2D graphics = this.thumbnailImage.NewGraphics2D();
				Vector2 center = new Vector2(Width / 2.0, Height / 2.0);
				double yOffset = 8 * Width / 50 * TextWidget.GlobalPointSizeScaleRatio * 1.5;
				graphics.DrawString("Too Big\nto\nRender", center.x, center.y + yOffset, 8 * Width / 50, Agg.Font.Justification.Center, Agg.Font.Baseline.BoundsCenter, color: RGBA_Bytes.White);

				UiThread.RunOnIdle(this.EnsureImageUpdated);
				return true;
				//GetRenderType(thumbnailWidget.PrintItem.FileLocation);
			}

			string stlHashCode = this.PrintItem.FileHashCode.ToString();

			ImageBuffer bigRender = LoadImageFromDisk(this, stlHashCode, bigRenderSize);
			if (bigRender == null)
			{
				this.thumbnailImage = new ImageBuffer(buildingThumbnailImage);
				return false;
			}

			ImageBuffer unScaledImage = new ImageBuffer(bigRender.Width, bigRender.Height, 32, new BlenderBGRA());
			unScaledImage.NewGraphics2D().Render(bigRender, 0, 0);
			// If the source image (the one we downloaded) is more than twice as big as our dest image.
			while (unScaledImage.Width > Width * 2)
			{
				// The image sampler we use is a 2x2 filter so we need to scale by a max of 1/2 if we want to get good results.
				// So we scale as many times as we need to to get the Image to be the right size.
				// If this were going to be a non-uniform scale we could do the x and y separatly to get better results.
				ImageBuffer halfImage = new ImageBuffer(unScaledImage.Width / 2, unScaledImage.Height / 2, 32, new BlenderBGRA());
				halfImage.NewGraphics2D().Render(unScaledImage, 0, 0, 0, halfImage.Width / (double)unScaledImage.Width, halfImage.Height / (double)unScaledImage.Height);
				unScaledImage = halfImage;
			}

			this.thumbnailImage = new ImageBuffer((int)Width, (int)Height, 32, new BlenderBGRA());
			this.thumbnailImage.NewGraphics2D().Clear(new RGBA_Bytes(255, 255, 255, 0));
			this.thumbnailImage.NewGraphics2D().Render(unScaledImage, 0, 0, 0, (double)this.thumbnailImage.Width / unScaledImage.Width, (double)this.thumbnailImage.Height / unScaledImage.Height);

			UiThread.RunOnIdle(this.EnsureImageUpdated);

			return true;
		}

		private static bool Is32Bit()
		{
			if (IntPtr.Size == 4)
			{
				return true;
			}

			return false;
		}

		private bool MeshIsTooBigToLoad(string fileLocation)
		{
			if (Is32Bit())
			{
				long estimatedMemoryUse = 0;
				if (File.Exists(fileLocation))
				{
					estimatedMemoryUse = MeshFileIo.GetEstimatedMemoryUse(fileLocation);

					if (OsInformation.OperatingSystem == OSType.Android)
					{
						if (estimatedMemoryUse > tooBigAndroid)
						{
							return true;
						}
					}
					else
					{
						if (estimatedMemoryUse > tooBigDesktop)
						{
							return true;
						}
					}
				}
			}

			return false;
		}

		private void LoadOrCreateThumbnail()
		{
			using (TimedLock.Lock(this, "TryLoad"))
			{
				if (!thumbNailHasBeenCreated)
				{
					if (SetImageFast())
					{
						thumbNailHasBeenCreated = true;
					}
					else
					{
						if (!processingThumbnail)
						{
							thumbNailHasBeenCreated = true;
							processingThumbnail = true;
							CreateThumbnail(this);
							processingThumbnail = false;
						}
					}
				}
			}
		}
	}
}