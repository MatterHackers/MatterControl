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
	public class ImageToPathObject3D_2 : Object3D, IPathObject, ISelectedEditorDraw, IObject3DControlsProvider
	{
		private ThresholdFunctions _featureDetector = ThresholdFunctions.Intensity;

		private ImageBuffer _histogramRawCache = null;
		private ImageBuffer _histogramDisplayCache = null;

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

		[JsonIgnore]
		public ImageBuffer ImageWithAlpha
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
					_histogramRawCache = null;
					_featureDetector = value;
					// make sure we create it
					var _ = this.Histogram;
				}
			}
		}

		[JsonIgnore]
		public ImageBuffer Histogram
		{
			get
			{
				if (_histogramRawCache == null)
				{
					_histogramRawCache = new ImageBuffer(256, 100);
					var image = Image;
					if (image != null)
					{
						var counts = new int[_histogramRawCache.Width];
						var function = ThresholdFunction;

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

				if (_histogramDisplayCache == null)
				{
					_histogramDisplayCache = new ImageBuffer(_histogramRawCache);
				}

				UpdateHistogramDisplay();

				return _histogramDisplayCache;
			}

			set
			{
			}
		}

		private void UpdateHistogramDisplay()
		{
			if (_histogramRawCache != null
				&& _histogramDisplayCache != null)
			{
				var graphics2D = _histogramDisplayCache.NewGraphics2D();
				graphics2D.Clear(Color.Transparent);
				_histogramDisplayCache.CopyFrom(_histogramRawCache);
				var rangeStart = RangeStart.Value(this);
				var rangeEnd = RangeEnd.Value(this);
				graphics2D.FillRectangle(0, 0, rangeStart * _histogramDisplayCache.Width, _histogramDisplayCache.Height, new Color(Color.Red, 100));
				graphics2D.FillRectangle(rangeEnd * _histogramDisplayCache.Width, 0, 255, _histogramDisplayCache.Height, new Color(Color.Red, 100));
				graphics2D.Line(rangeStart * _histogramDisplayCache.Width, 0, rangeStart * _histogramDisplayCache.Width, _histogramDisplayCache.Height, new Color(Color.LightGray, 200));
				graphics2D.Line(rangeEnd * _histogramDisplayCache.Width, 0, rangeEnd * _histogramDisplayCache.Width, _histogramDisplayCache.Height, new Color(Color.LightGray, 200));
			}
		}

		[JsonIgnore]
		private ImageBuffer Image => this.Descendants<ImageObject3D>().FirstOrDefault()?.Image;

		[Range(0, 1, ErrorMessage = "Value for {0} must be between {1} and {2}.")]
		public DoubleOrExpression RangeStart { get; set; } = .1;

		[Range(0, 1, ErrorMessage = "Value for {0} must be between {1} and {2}.")]
		public DoubleOrExpression RangeEnd { get; set; } = 1;

		public IVertexSource VertexSource { get; set; } = new VertexStorage();

		private IThresholdFunction ThresholdFunction
		{
			get
			{
				switch (FeatureDetector)
				{
					case ThresholdFunctions.Intensity:
						return new MapOnMaxIntensity(RangeStart.Value(this), RangeEnd.Value(this));

					case ThresholdFunctions.Transparency:
						return new AlphaThresholdFunction(RangeStart.Value(this), RangeEnd.Value(this));

					case ThresholdFunctions.Colors:
						return new HueThresholdFunction(RangeStart.Value(this), RangeEnd.Value(this));
				}

				return new MapOnMaxIntensity(RangeStart.Value(this), RangeEnd.Value(this));
			}
		}

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

		private Color GetRGBA(byte[] buffer, int offset)
		{
			return new Color(buffer[offset + 2], buffer[offset + 1], buffer[offset + 0], buffer[offset + 3]);
		}

		public override Task Rebuild()
		{
			this.DebugDepth("Rebuild");

			UpdateHistogramDisplay();
			bool propertyUpdated = false;
			var minSeparation = .01;
			var rangeStart = RangeStart.Value(this);
			var rangeEnd = RangeEnd.Value(this);
			if (rangeStart < 0
				|| rangeStart > 1
				|| rangeEnd < 0
				|| rangeEnd > 1
				|| rangeStart > rangeEnd - minSeparation)
			{
				rangeStart = Math.Max(0, Math.Min(1 - minSeparation, rangeStart));
				rangeEnd = Math.Max(0, Math.Min(1, rangeEnd));
				if (rangeStart > rangeEnd - minSeparation)
				{
					// values are overlapped or too close together
					if (rangeEnd < 1 - minSeparation)
					{
						// move the end up whenever possible
						rangeEnd = rangeStart + minSeparation;
					}
					else
					{
						// move the end to the end and the start up
						rangeEnd = 1;
						RangeStart = 1 - minSeparation;
					}
				}

				propertyUpdated = true;
			}

			var rebuildLock = RebuildLock();
			// now create a long running task to process the image
			return ApplicationController.Instance.Tasks.Execute(
				"Calculate Path".Localize(),
				null,
				(reporter, cancellationToken) =>
				{
					var progressStatus = new ProgressStatus();
					this.GenerateMarchingSquaresAndLines(
						(progress0to1, status) =>
						{
							progressStatus.Progress0To1 = progress0to1;
							progressStatus.Status = status;
							reporter.Report(progressStatus);
						},
						Image,
						ThresholdFunction);

					if (propertyUpdated)
					{
						UpdateHistogramDisplay();
						this.Invalidate(InvalidateType.Properties);
					}

					UiThread.RunOnIdle(() =>
					{
						rebuildLock.Dispose();
						Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Path));
					});

					return Task.CompletedTask;
				});
		}
	}
}