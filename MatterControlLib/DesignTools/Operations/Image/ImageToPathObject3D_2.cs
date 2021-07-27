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
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using ClipperLib;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Image.ThresholdFunctions;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MarchingSquares;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.VectorMath;
using Newtonsoft.Json;
using Polygon = System.Collections.Generic.List<ClipperLib.IntPoint>;
using Polygons = System.Collections.Generic.List<System.Collections.Generic.List<ClipperLib.IntPoint>>;

namespace MatterHackers.MatterControl.DesignTools
{
	[HideMeterialAndColor]
	public class ImageToPathObject3D_2 : Object3D, IImageProvider, IPathObject, ISelectedEditorDraw, IObject3DControlsProvider
	{
		private ThresholdFunctions _featureDetector = ThresholdFunctions.Intensity;

		public ImageToPathObject3D_2()
		{
			Name = "Image to Path".Localize();
		}

		public enum ThresholdFunctions
		{
			Transparency,
			Colors,
			Intensity,
		}

		/// <summary>
		/// This is the image after it has been processed into an alpha image
		/// </summary>
		[JsonIgnore]
		[ImageDisplay(Margin = new int[] { 30, 3, 30, 3 }, MaxXSize = 400, Stretch = true)]
		public ImageBuffer Image
		{
			get
			{
				var imageObject = (ImageObject3D)Children.Where(i => i is ImageObject3D).FirstOrDefault();

				return imageObject.Image;
			}

			set
			{
			}
		}


		[EnumDisplay(Mode = EnumDisplayAttribute.PresentationMode.Tabs)]
		public ThresholdFunctions FeatureDetector
		{
			get
			{
				return _featureDetector;
			}

			set
			{
				if (value != _featureDetector)
				{
					_featureDetector = value;
					IntensityHistogram.Recalculate(SourceImage);
				}
			}
		}

		[JsonIgnore]
		private ImageBuffer SourceImage => ((IImageProvider)this.Descendants().Where(i => i is IImageProvider).FirstOrDefault())?.Image;

		public Historgram IntensityHistogram { get; set; } = new Historgram();

		public IVertexSource VertexSource { get; set; } = new VertexStorage();

		public void AddObject3DControls(Object3DControlsLayer object3DControlsLayer)
		{
			object3DControlsLayer.AddControls(ControlTypes.Standard2D);
		}

		public void DrawEditor(Object3DControlsLayer layer, List<Object3DView> transparentMeshes, DrawEventArgs e)
		{
			this.DrawPath();
		}

		public override bool CanFlatten => true;

		public override void Flatten(UndoBuffer undoBuffer)
		{
			this.FlattenToPathObject(undoBuffer);
		}

		public void GenerateMarchingSquaresAndLines(Action<double, string> progressReporter, ImageBuffer image, IThresholdFunction thresholdFunction)
		{
			if (image != null)
			{
				// Regenerate outline
				var marchingSquaresData = new MarchingSquaresByte(
					image,
					thresholdFunction.ZeroColor,
					thresholdFunction.Threshold,
					0);

				progressReporter?.Invoke(0, "Creating Outline");

				marchingSquaresData.CreateLineSegments();
				progressReporter?.Invoke(.1, null);

				int pixelsToIntPointsScale = 1000;
				var lineLoops = marchingSquaresData.CreateLineLoops(pixelsToIntPointsScale);

				progressReporter?.Invoke(.15, null);

				var min = new IntPoint(-10, -10);
				var max = new IntPoint(10 + image.Width * pixelsToIntPointsScale, 10 + image.Height * pixelsToIntPointsScale);

				var boundingPoly = new Polygon();
				boundingPoly.Add(min);
				boundingPoly.Add(new IntPoint(min.X, max.Y));
				boundingPoly.Add(max);
				boundingPoly.Add(new IntPoint(max.X, min.Y));

				// now clip the polygons to get the inside and outside polys
				var clipper = new Clipper();
				clipper.AddPaths(lineLoops, PolyType.ptSubject, true);
				clipper.AddPath(boundingPoly, PolyType.ptClip, true);

				var polygonShape = new Polygons();
				progressReporter?.Invoke(.3, null);

				clipper.Execute(ClipType.ctIntersection, polygonShape);

				progressReporter?.Invoke(.55, null);

				polygonShape = Clipper.CleanPolygons(polygonShape, 100);

				progressReporter?.Invoke(.75, null);

				VertexStorage rawVectorShape = polygonShape.PolygonToPathStorage();

				var aabb = this.VisibleMeshes().FirstOrDefault().GetAxisAlignedBoundingBox();
				var xScale = aabb.XSize / image.Width;

				var affine = Affine.NewScaling(1.0 / pixelsToIntPointsScale * xScale);
				affine *= Affine.NewTranslation(-aabb.XSize / 2, -aabb.YSize / 2);

				rawVectorShape.transform(affine);
				this.VertexSource = rawVectorShape;

				progressReporter?.Invoke(1, null);
			}
		}

		public override async void OnInvalidate(InvalidateArgs invalidateArgs)
		{
			if (invalidateArgs.InvalidateType.HasFlag(InvalidateType.Image)
				&& invalidateArgs.Source != this
				&& !RebuildLocked)
			{
				await Rebuild();
			}
			else if ((invalidateArgs.InvalidateType.HasFlag(InvalidateType.Properties) && invalidateArgs.Source == this))
			{
				await Rebuild();
			}
			else if (SheetObject3D.NeedsRebuild(this, invalidateArgs))
			{
				await Rebuild();
			}

			base.OnInvalidate(invalidateArgs);
		}

		public override Task Rebuild()
		{
			this.DebugDepth("Rebuild");

			var rebuildLock = RebuildLock();
			// now create a long running task to process the image
			return ApplicationController.Instance.Tasks.Execute(
				"Calculate Path".Localize(),
				null,
				(reporter, cancellationToken) =>
				{
					var progressStatus = new ProgressStatus();
					var thresholdFunction = new AlphaThresholdFunction(0, 1);					
					this.GenerateMarchingSquaresAndLines(
						(progress0to1, status) =>
						{
							progressStatus.Progress0To1 = progress0to1;
							progressStatus.Status = status;
							reporter.Report(progressStatus);
						},
						Image,
						thresholdFunction);

					UiThread.RunOnIdle(() =>
					{
						rebuildLock.Dispose();
						Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Path));
					});

					return Task.CompletedTask;
				});
		}
	}

	public class Historgram
	{
		private ImageBuffer _histogramRawCache = new ImageBuffer(256, 100);
		private ThemeConfig theme;

		public double RangeStart { get; set; } = .1;

		public double RangeEnd { get; set; } = 1;

		private Color GetRGBA(byte[] buffer, int offset)
		{
			return new Color(buffer[offset + 2], buffer[offset + 1], buffer[offset + 0], buffer[offset + 3]);
		}

		public void Recalculate(ImageBuffer image)
		{
			if (_histogramRawCache == null)
			{
				_histogramRawCache = new ImageBuffer(256, 100);
				if (image != null)
				{
					var counts = new int[_histogramRawCache.Width];
					var function =  new MapOnMaxIntensity(RangeStart, RangeEnd);

					byte[] buffer = image.GetBuffer();
					int strideInBytes = image.StrideInBytes();
					for (int y = 0; y < image.Height; y++)
					{
						int imageBufferOffset = image.GetBufferOffsetY(y);
						int thresholdBufferOffset = y * image.Width;

						for (int x = 0; x < image.Width; x++)
						{
							int imageBufferOffsetWithX = imageBufferOffset + x * 4;
							var color = GetRGBA(buffer, imageBufferOffsetWithX);
							counts[(int)(function.Transform(color) * (_histogramRawCache.Width - 1))]++;
						}
					}

					double max = counts.Select((value, index) => new { value, index })
						.OrderByDescending(vi => vi.value)
						.First().value;
					var graphics2D2 = _histogramRawCache.NewGraphics2D();
					graphics2D2.Clear(Color.White);
					for (int i = 0; i < 256; i++)
					{
						graphics2D2.Line(i, 0, i, Easing.Exponential.Out(counts[i] / max) * _histogramRawCache.Height, Color.Black);
					}
				}
			}
		}

		public GuiWidget NewEditWidget(ThemeConfig theme)
		{
			this.theme = theme;
			var historgramWidget = new GuiWidget()
			{
				HAnchor = HAnchor.Stretch,
				Height = 60 * GuiWidget.DeviceScale,
				Margin = 5,
				BackgroundColor = theme.SlightShade
			};

			var handleWidth = 10 * GuiWidget.DeviceScale;
			var historgramBackground = new GuiWidget()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
				Margin = new BorderDouble(handleWidth, 0)
			};

			historgramBackground.AfterDraw += HistorgramBackground_AfterDraw;
			historgramWidget.AddChild(historgramBackground);

			var leftHandle = new ImageWidget((int)(handleWidth), (int)historgramWidget.Height);
			leftHandle.Position = new Vector2(RangeStart * _histogramRawCache.Width, 0);
			var image = leftHandle.Image;
			var leftGraphics = image.NewGraphics2D();
			leftGraphics.Line(image.Width, 0, image.Width, image.Height, theme.TextColor);
			leftGraphics.FillRectangle(0, image.Height / 4, image.Width, image.Height / 4 * 3, theme.TextColor);
			historgramWidget.AddChild(leftHandle);

			bool leftDown = false;
			var leftX = 0.0;
			leftHandle.MouseDown += (s, e) =>
			{
				if (e.Button == MouseButtons.Left)
				{
					leftDown = true;
					leftX = e.Position.X;
				}
			};
			leftHandle.MouseMove += (s, e) =>
			{
				if (leftDown)
				{
					var offset = e.Position.X - leftX;
					RangeStart += offset / _histogramRawCache.Width;
					RangeStart = Math.Max(0, Math.Min(RangeStart, RangeEnd));
					leftHandle.Position = new Vector2(RangeStart * _histogramRawCache.Width, 0);
				}
			};
			leftHandle.MouseUp += (s, e) =>
			{
				leftDown = false;
			};

			var rightHandle = new ImageWidget((int)(handleWidth), (int)historgramWidget.Height);
			rightHandle.Position = new Vector2(RangeEnd * _histogramRawCache.Width + handleWidth, 0);
			image = rightHandle.Image;
			var rightGraphics = image.NewGraphics2D();
			rightGraphics.Line(0, 0, 0, image.Height, theme.TextColor);
			rightGraphics.FillRectangle(0, image.Height / 4, image.Width, image.Height / 4 * 3, theme.TextColor);
			historgramWidget.AddChild(rightHandle);

			bool rightDown = false;
			var rightX = 0.0;
			rightHandle.MouseDown += (s, e) =>
			{
				if (e.Button == MouseButtons.Left)
				{
					rightDown = true;
					rightX = e.Position.X;
				}
			};
			rightHandle.MouseMove += (s, e) =>
			{
				if (rightDown)
				{
					var offset = e.Position.X - rightX;
					RangeEnd += offset / _histogramRawCache.Width;
					RangeEnd = Math.Min(1, Math.Max(RangeStart, RangeEnd));
					rightHandle.Position = new Vector2(RangeEnd * _histogramRawCache.Width + handleWidth, 0);
				}
			};
			rightHandle.MouseUp += (s, e) =>
			{
				rightDown = false;
			};

			return historgramWidget;
		}

		private void HistorgramBackground_AfterDraw(object sender, DrawEventArgs e)
		{
			var rangeStart = RangeStart;
			var rangeEnd = RangeEnd;
			var graphics2D = e.Graphics2D;
			graphics2D.Render(_histogramRawCache, 0, 0);
			var background = _histogramRawCache;
			graphics2D.FillRectangle(rangeStart * background.Width, 0, rangeEnd * background.Width, background.Height, theme.PrimaryAccentColor.WithAlpha(60));
		}

		public void ProcessOutputImage()
		{
		}
	}
}