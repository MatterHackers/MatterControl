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
using MatterHackers.DataConverters3D;
using MatterHackers.ImageProcessing;
using MatterHackers.MatterControl.Library;
using MatterHackers.Agg.Font;

namespace MatterHackers.MatterControl
{
	public class PartThumbnailWidget : ClickWidget
	{
		private static readonly bool Is32Bit = IntPtr.Size == 4;
		private static readonly int MaxFileSize = (OsInformation.OperatingSystem == OSType.Android) ? tooBigAndroid : tooBigDesktop;
		private static readonly Point2D BigRenderSize = new Point2D(460, 460);

		internal static readonly string ThumbnailsPath = Path.Combine(ApplicationDataStorage.ApplicationUserDataPath, "data", "temp", "thumbnails");

		object locker = new object();
		// all the color stuff
		new public double BorderWidth = 0;

		public event EventHandler<StringEventArgs> DoneRendering;
		public ImageSizes Size { get; set; }
		public enum ImageSizes { Size50x50, Size115x115 };
		public RGBA_Bytes FillColor = new RGBA_Bytes(255, 255, 255);
		public RGBA_Bytes HoverBackgroundColor = new RGBA_Bytes(0, 0, 0, 50);
		protected double borderRadius = 0;

		//Don't delete this - required for OnDraw
		protected RGBA_Bytes HoverBorderColor = new RGBA_Bytes();

		private const int renderOrthoAndroid = 20000000;
		private const int renderOrthoDesktop = 100000000;
		private const int tooBigAndroid = 50000000;
		private const int tooBigDesktop = 250000000;
		
		private static bool processingThumbnail = false;
		private ImageBuffer buildingThumbnailImage = new ImageBuffer();

		// TODO: temporarily work around exception when trying to gen thumbnails for new file type
		private bool supportsThumbnails = true;

		private RGBA_Bytes normalBackgroundColor = ActiveTheme.Instance.PrimaryAccentColor;

		private ImageBuffer noThumbnailImage = new ImageBuffer();

		private PartPreviewMainWindow partPreviewWindow;

		private PrintItemWrapper printItemWrapper;

		private bool thumbNailHasBeenCreated = false;

		private ImageBuffer thumbnailImage = new ImageBuffer();

		private EventHandler unregisterEvents;

		private enum RenderType { NONE, ORTHOGROPHIC, PERSPECTIVE, RAY_TRACE };

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
		}

		public override void OnClick(MouseEventArgs mouseEvent)
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
						// TODO: What's the analogy? Can we drop PartThumbnailWidget and ignore this?
						//QueueRowItem.ShowCantFindFileMessage(printItemWrapper);
					}
				}
			});

			base.OnClick(mouseEvent);
		}

		public PrintItemWrapper ItemWrapper
		{
			get { return printItemWrapper; }
			set
			{
				printItemWrapper = value;

				supportsThumbnails = false;

				thumbNailHasBeenCreated = false;
				if (ItemWrapper != null)
				{
					supportsThumbnails = 
						!string.IsNullOrEmpty(printItemWrapper.FileLocation) 
						&& ( File.Exists(printItemWrapper.FileLocation) || printItemWrapper.FileLocation == QueueData.SdCardFileName)
						&& Path.GetExtension(printItemWrapper.FileLocation).ToLower() != ".mcp";
				}
			}
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			if(ItemWrapper == null)
			{
				return;
			}

			//Trigger thumbnail generation if neeeded
			string stlHashCode = this.ItemWrapper.FileHashCode.ToString();
			if (!thumbNailHasBeenCreated && !processingThumbnail && supportsThumbnails)
			{
				/*
				// Attempt to load the thumbnail. If not loaded, schedule its creation via a new Task to CreateThumbnail
				ApplicationController.Instance.LoadItemThumbnail(
				{
					thumbNailHasBeenCreated = true;
					OnDoneRendering();
				}
				else
				{
					// CreateThumbnail
					Task.Run(() =>
					{
						lock (locker)
						{
							if (!thumbNailHasBeenCreated && !processingThumbnail)
							{ 
								thumbNailHasBeenCreated = true;
								processingThumbnail = true;
								this.thumbnailImage = ThumbnailEngine.Generate(new Object3D(), "", GetRenderType(""), BigRenderSize.x, BigRenderSize.y);
								processingThumbnail = false;
							}
						}
					});
				} */
			}

			graphics2D.Render(thumbnailImage, Width / 2 - thumbnailImage.Width / 2, Height / 2 - thumbnailImage.Height / 2);
			base.OnDraw(graphics2D);

			// Draw hover border
			if (HoverBorderColor.Alpha0To255 > 0)
			{
				graphics2D.Render(
					new Stroke(
						new RoundedRect(this.LocalBounds, this.borderRadius),
						BorderWidth), 
					HoverBorderColor);
			}
		}

		public static string GetImageFileName(PrintItemWrapper item)
		{
			return GetImageFileName(item.FileHashCode.ToString());
		}

		private static string GetImageFileName(string stlHashCode)
		{
			string imageFileName = Path.Combine(ThumbnailsPath, "{0}_{1}x{2}.png".FormatWith(stlHashCode, BigRenderSize.x, BigRenderSize.y));

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
				if (Is32Bit)
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

		public static ImageBuffer LoadImageFromDisk(string stlHashCode)
		{
			try
			{
				string imageFileName = GetImageFileName(stlHashCode);
				if (File.Exists(imageFileName))
				{
					var tempImage = new ImageBuffer(BigRenderSize.x, BigRenderSize.y);
					if (ImageIO.LoadImageData(imageFileName, tempImage))
					{
						return tempImage;
					}
				}
			}
			catch { }

			return null;
		}

		private void OnDoneRendering()
		{
			if (ItemWrapper != null)
			{
				string stlHashCode = this.ItemWrapper.FileHashCode.ToString();
				string imageFileName = GetImageFileName(stlHashCode);

				DoneRendering?.Invoke(this, new StringEventArgs(imageFileName));
			}
		}

		private void EnsureImageUpdated()
		{
			thumbnailImage.MarkImageChanged();
			Invalidate();
		}

		private static bool MeshIsTooBigToLoad(string fileLocation)
		{
			if (Is32Bit && File.Exists(fileLocation))
			{
				// Mesh is too big if the estimated size is greater than Max
				return MeshFileIo.GetEstimatedMemoryUse(fileLocation) > MaxFileSize;
			}

			return false;
		}

		public override void OnMouseEnterBounds(MouseEventArgs mouseEvent)
		{
			HoverBorderColor = new RGBA_Bytes(255, 255, 255);
			this.Invalidate();

			base.OnMouseEnterBounds(mouseEvent);
		}

		public override void OnMouseLeaveBounds(MouseEventArgs mouseEvent)
		{
			HoverBorderColor = new RGBA_Bytes();
			this.Invalidate();

			base.OnMouseLeaveBounds(mouseEvent);
		}

		private void OpenPartPreviewWindow(View3DWidget.AutoRotate autoRotate)
		{
			if (partPreviewWindow == null)
			{
				partPreviewWindow = new PartPreviewMainWindow(this.ItemWrapper, autoRotate);
				partPreviewWindow.Closed += (s, e) =>
				{
					this.partPreviewWindow = null;
				};
			}
			else
			{
				partPreviewWindow.BringToFront();
			}
		}
	}
}