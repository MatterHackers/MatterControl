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
using MatterHackers.MatterControl.PrintLibrary;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.RayTracer;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using MatterHackers.Localizations;
using MatterHackers.Agg.ImageProcessing;

namespace MatterHackers.MatterControl
{
	public class PartThumbnailWidget : ClickWidget
	{
		object locker = new object();
		// all the color stuff
		new public double BorderWidth = 0;

		public RGBA_Bytes FillColor = new RGBA_Bytes(255, 255, 255);
		public RGBA_Bytes HoverBackgroundColor = new RGBA_Bytes(0, 0, 0, 50);
		protected double borderRadius = 0;

		//Don't delete this - required for OnDraw
		protected RGBA_Bytes HoverBorderColor = new RGBA_Bytes();

		private const int renderOrthoAndroid = 20000000;
		private const int renderOrthoDesktop = 100000000;
		private const int tooBigAndroid = 50000000;
		private const int tooBigDesktop = 250000000;
		private static string partExtension = ".png";
		private static bool processingThumbnail = false;
		private ImageBuffer buildingThumbnailImage = new Agg.Image.ImageBuffer();

		private RGBA_Bytes normalBackgroundColor = ActiveTheme.Instance.PrimaryAccentColor;

		private ImageBuffer noThumbnailImage = new Agg.Image.ImageBuffer();

		private PartPreviewMainWindow partPreviewWindow;

		private PrintItemWrapper printItemWrapper;

		private bool thumbNailHasBeenCreated = false;

		private ImageBuffer thumbnailImage = new Agg.Image.ImageBuffer();

		public PartThumbnailWidget(PrintItemWrapper item, string noThumbnailFileName, string buildingThumbnailFileName, ImageSizes size)
		{
			ToolTipText = "Click to show in 3D View".Localize();
			this.ItemWrapper = item;

			// Set Display Attributes
			this.Margin = new BorderDouble(0);
			this.Padding = new BorderDouble(5);
			Size = size;
			switch (size)
			{
				case ImageSizes.Size50x50:
					this.Width = 50 * GuiWidget.DeviceScale;
					this.Height = 50 * GuiWidget.DeviceScale;
					break;

				case ImageSizes.Size115x115:
					this.Width = 115 * GuiWidget.DeviceScale;
					this.Height = 115 * GuiWidget.DeviceScale;
					break;

				default:
					throw new NotImplementedException();
			}
			this.MinimumSize = new Vector2(this.Width, this.Height);

			this.BackgroundColor = normalBackgroundColor;
			this.Cursor = Cursors.Hand;
			this.ToolTipText = "Click to show in 3D View".Localize();

			// set background images
			if (noThumbnailImage.Width == 0)
			{
				StaticData.Instance.LoadIcon(noThumbnailFileName, noThumbnailImage);
				noThumbnailImage.InvertLightness();
				StaticData.Instance.LoadIcon(buildingThumbnailFileName, buildingThumbnailImage);
				buildingThumbnailImage.InvertLightness();
			}
			this.thumbnailImage = new ImageBuffer(buildingThumbnailImage);

			// Add Handlers
			this.Click += DoOnMouseClick;
			this.MouseEnterBounds += onEnter;
			this.MouseLeaveBounds += onExit;
			ActiveTheme.ThemeChanged.RegisterEvent(ThemeChanged, ref unregisterEvents);
		}

		private void DoOnMouseClick(object sender, EventArgs e)
		{
			UiThread.RunOnIdle(() =>
			{
				if (printItemWrapper != null)
				{
					string pathAndFile = printItemWrapper.FileLocation;
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
						QueueRowItem.ShowCantFindFileMessage(printItemWrapper);
					}
				}
			});
		}
		
		public event EventHandler<StringEventArgs> DoneRendering;

		private event EventHandler unregisterEvents;

		public enum ImageSizes { Size50x50, Size115x115 };

		private enum RenderType { NONE, ORTHOGROPHIC, PERSPECTIVE, RAY_TRACE };

		public PrintItemWrapper ItemWrapper
		{
			get { return printItemWrapper; }
			set
			{
				if (ItemWrapper != null)
				{
					PrintItemWrapper.FileHasChanged.UnregisterEvent(item_FileHasChanged, ref unregisterEvents);
				}
				
				printItemWrapper = value;
				
				thumbNailHasBeenCreated = false;
				if (ItemWrapper != null)
				{
					PrintItemWrapper.FileHasChanged.RegisterEvent(item_FileHasChanged, ref unregisterEvents);
				}
			}
		}

		public ImageSizes Size { get; set; }

		static private Point2D BigRenderSize
		{
			get
			{
				return new Point2D(460, 460);
			}
		}

		public static string GetImageFileName(PrintItemWrapper item)
		{
			return GetImageFileName(item.FileHashCode.ToString());
		}

		public override void OnClosed(EventArgs e)
		{
			if (unregisterEvents != null)
			{
				unregisterEvents(this, null);
			}

			base.OnClosed(e);
		}

		private void LoadOrCreateThumbnail()
		{
			lock(locker)
			{
				if (!thumbNailHasBeenCreated)
				{
					if (!processingThumbnail)
					{
						thumbNailHasBeenCreated = true;
						processingThumbnail = true;
						CreateThumbnail();
						processingThumbnail = false;
					}
				}
			}
		}
		
		public override void OnDraw(Graphics2D graphics2D)
		{
			//Trigger thumbnail generation if neeeded
			if (!thumbNailHasBeenCreated && !processingThumbnail)
			{
				if (SetImageFast())
				{
					thumbNailHasBeenCreated = true;
					OnDoneRendering();
				}
				else
				{
					Task.Run(() => LoadOrCreateThumbnail());
				}
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
				ImageBuffer tempImage = new ImageBuffer(size.x, size.y);
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

				tempImage.SetRecieveBlender(new BlenderPreMultBGRA());
				AllWhite.DoAllWhite(tempImage);

				// and give it back
				return tempImage;
			}

			return null;
		}

		private static void CreateImage(PartThumbnailWidget thumbnailWidget, double Width, double Height)
		{
			thumbnailWidget.thumbnailImage = new ImageBuffer((int)Width, (int)Height);
		}

		private static string GetImageFileName(string stlHashCode)
		{
			string folderToSaveThumbnailsTo = ThumbnailPath();
			string imageFileName = Path.Combine(folderToSaveThumbnailsTo, Path.ChangeExtension("{0}_{1}x{2}".FormatWith(stlHashCode, BigRenderSize.x, BigRenderSize.y), partExtension));

			string folderToSavePrintsTo = Path.GetDirectoryName(imageFileName);

			if (!Directory.Exists(folderToSavePrintsTo))
			{
				Directory.CreateDirectory(folderToSavePrintsTo);
			}

			return imageFileName;
		}

		private static RenderType GetRenderType(string fileLocation)
		{
			if (UserSettings.Instance.get(UserSettingsKey.ThumbnailRenderingMode) == "raytraced")
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

		private static bool Is32Bit()
		{
			if (IntPtr.Size == 4)
			{
				return true;
			}

			return false;
		}

		private static ImageBuffer LoadImageFromDisk(PartThumbnailWidget thumbnailWidget, string stlHashCode)
		{
			ImageBuffer tempImage = new ImageBuffer(BigRenderSize.x, BigRenderSize.y);
			string imageFileName = GetImageFileName(stlHashCode);

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

		public static string ThumbnailPath()
		{
			string applicationUserDataPath = ApplicationDataStorage.ApplicationUserDataPath;
			string folderToSaveThumbnailsTo = Path.Combine(applicationUserDataPath, "data", "temp", "thumbnails");
			return folderToSaveThumbnailsTo;
		}

		private void CreateThumbnail()
		{
			string stlHashCode = this.ItemWrapper.FileHashCode.ToString();

			ImageBuffer bigRender = new ImageBuffer();
			if (!File.Exists(this.ItemWrapper.FileLocation))
			{
				return;
			}

			List<MeshGroup> loadedMeshGroups = MeshFileIo.Load(this.ItemWrapper.FileLocation);

			RenderType renderType = GetRenderType(this.ItemWrapper.FileLocation);

			switch (renderType)
			{
				case RenderType.RAY_TRACE:
					{
						ThumbnailTracer tracer = new ThumbnailTracer(loadedMeshGroups, BigRenderSize.x, BigRenderSize.y);
						tracer.DoTrace();

						bigRender = tracer.destImage;
					}
					break;

				case RenderType.PERSPECTIVE:
					{
						ThumbnailTracer tracer = new ThumbnailTracer(loadedMeshGroups, BigRenderSize.x, BigRenderSize.y);
						this.thumbnailImage = new ImageBuffer(this.buildingThumbnailImage);
						this.thumbnailImage.NewGraphics2D().Clear(new RGBA_Bytes(255, 255, 255, 0));

						bigRender = new ImageBuffer(BigRenderSize.x, BigRenderSize.y);

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
							bigRender = new ImageBuffer(this.noThumbnailImage);
						}
					}
					break;

				case RenderType.NONE:
				case RenderType.ORTHOGROPHIC:

					this.thumbnailImage = new ImageBuffer(this.buildingThumbnailImage);
					this.thumbnailImage.NewGraphics2D().Clear(new RGBA_Bytes(255, 255, 255, 0));
					bigRender = BuildImageFromMeshGroups(loadedMeshGroups, stlHashCode, BigRenderSize);
					if (bigRender == null)
					{
						bigRender = new ImageBuffer(this.noThumbnailImage);
					}
					break;
			}

			// and save it to disk
			string imageFileName = GetImageFileName(stlHashCode);

			if (partExtension == ".png")
			{
				ImageIO.SaveImageData(imageFileName, bigRender);
			}
			else
			{
				ImageTgaIO.SaveImageData(imageFileName, bigRender);
			}

			bigRender.SetRecieveBlender(new BlenderPreMultBGRA());

			this.thumbnailImage = ImageBuffer.CreateScaledImage(bigRender, (int)Width, (int)Height);

			UiThread.RunOnIdle(this.EnsureImageUpdated);

			OnDoneRendering();
		}

		private void OnDoneRendering()
		{
			if (ItemWrapper != null)
			{
				string stlHashCode = this.ItemWrapper.FileHashCode.ToString();
				string imageFileName = GetImageFileName(stlHashCode);

				if (DoneRendering != null)
				{
					DoneRendering(this, new StringEventArgs(imageFileName));
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
			PrintItemWrapper senderItem = sender as PrintItemWrapper;
			if (senderItem != null
				&& senderItem.FileLocation == printItemWrapper.FileLocation)
			{
				thumbNailHasBeenCreated = false;
				Invalidate();
			}
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

		private void OpenPartPreviewWindow(View3DWidget.AutoRotate autoRotate)
		{
			if (partPreviewWindow == null)
			{
				partPreviewWindow = new PartPreviewMainWindow(this.ItemWrapper, autoRotate);
				partPreviewWindow.Name = "Part Preview Window Thumbnail";
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
			if (this.ItemWrapper == null)
			{
				this.thumbnailImage = new ImageBuffer(this.noThumbnailImage);
				this.Invalidate();
				return true;
			}

			if (this.ItemWrapper.FileLocation == QueueData.SdCardFileName)
			{
				switch (this.Size)
				{
					case ImageSizes.Size115x115:
						{
							this.thumbnailImage = StaticData.Instance.LoadIcon(Path.ChangeExtension("icon_sd_card_115x115", partExtension)).InvertLightness();
						}
						break;

					case ImageSizes.Size50x50:
						{
							this.thumbnailImage = StaticData.Instance.LoadIcon(Path.ChangeExtension("icon_sd_card_50x50", partExtension)).InvertLightness();
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
			else if (Path.GetExtension(this.ItemWrapper.FileLocation).ToUpper() == ".GCODE")
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
			else if (!File.Exists(this.ItemWrapper.FileLocation))
			{
				CreateImage(this, Width, Height);
				this.thumbnailImage.SetRecieveBlender(new BlenderPreMultBGRA());
				Graphics2D graphics = this.thumbnailImage.NewGraphics2D();
				Vector2 center = new Vector2(Width / 2.0, Height / 2.0);
				graphics.DrawString("Missing", center.x, center.y, 8 * Width / 50, Agg.Font.Justification.Center, Agg.Font.Baseline.BoundsCenter, color: RGBA_Bytes.White);

				UiThread.RunOnIdle(this.EnsureImageUpdated);
				return true;
			}
			else if (MeshIsTooBigToLoad(this.ItemWrapper.FileLocation))
			{
				CreateImage(this, Width, Height);
				this.thumbnailImage.SetRecieveBlender(new BlenderPreMultBGRA());
				Graphics2D graphics = this.thumbnailImage.NewGraphics2D();
				Vector2 center = new Vector2(Width / 2.0, Height / 2.0);
				double yOffset = 8 * Width / 50 * GuiWidget.DeviceScale * 2;
				graphics.DrawString("Reduce\nPolygons\nto\nRender", center.x, center.y + yOffset, 8 * Width / 50, Agg.Font.Justification.Center, Agg.Font.Baseline.BoundsCenter, color: RGBA_Bytes.White);

				UiThread.RunOnIdle(this.EnsureImageUpdated);
				return true;
			}

			string stlHashCode = this.ItemWrapper.FileHashCode.ToString();

			if (stlHashCode != "0")
			{
				ImageBuffer bigRender = LoadImageFromDisk(this, stlHashCode);
				if (bigRender == null)
				{
					this.thumbnailImage = new ImageBuffer(buildingThumbnailImage);
					return false;
				}

				bigRender.SetRecieveBlender(new BlenderPreMultBGRA());

				this.thumbnailImage = ImageBuffer.CreateScaledImage(bigRender, (int)Width, (int)Height);

				UiThread.RunOnIdle(this.EnsureImageUpdated);

				return true;
			}

			return false;
		}
	}

	public class AllWhite
	{
		public static void DoAllWhite(ImageBuffer sourceImageAndDest)
		{
			DoAllWhite(sourceImageAndDest, sourceImageAndDest);
		}

		public static void DoAllWhite(ImageBuffer result, ImageBuffer imageA)
		{
			if (imageA.BitDepth != result.BitDepth)
			{
				throw new NotImplementedException("All the images have to be the same bit depth.");
			}
			if (imageA.Width != result.Width || imageA.Height != result.Height)
			{
				throw new Exception("All images must be the same size.");
			}

			switch (imageA.BitDepth)
			{
				case 32:
					{
						int height = imageA.Height;
						int width = imageA.Width;
						byte[] resultBuffer = result.GetBuffer();
						byte[] imageABuffer = imageA.GetBuffer();
						for (int y = 0; y < height; y++)
						{
							int offsetA = imageA.GetBufferOffsetY(y);
							int offsetResult = result.GetBufferOffsetY(y);

							for (int x = 0; x < width; x++)
							{
								int alpha = imageABuffer[offsetA+3];
								if (alpha > 0)
								{
									resultBuffer[offsetResult++] = (byte)255; offsetA++;
									resultBuffer[offsetResult++] = (byte)255; offsetA++;
									resultBuffer[offsetResult++] = (byte)255; offsetA++;
									resultBuffer[offsetResult++] = (byte)alpha; offsetA++;
								}
								else
								{
									resultBuffer[offsetResult++] = (byte)0; offsetA++;
									resultBuffer[offsetResult++] = (byte)0; offsetA++;
									resultBuffer[offsetResult++] = (byte)0; offsetA++;
									resultBuffer[offsetResult++] = (byte)0; offsetA++;
								}
							}
						}
						result.SetRecieveBlender(new BlenderPreMultBGRA());
					}
					break;

				default:
					throw new NotImplementedException();
			}
		}
	}
}