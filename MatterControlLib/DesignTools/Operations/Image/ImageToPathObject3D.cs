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

using ClipperLib;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MarchingSquares;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.DesignTools
{
	using MatterHackers.Agg.Image.ThresholdFunctions;
	using MatterHackers.Agg.UI;
	using MatterHackers.MatterControl.PartPreviewWindow;
	using MatterHackers.RenderOpenGl.OpenGl;
	using MatterHackers.VectorMath;
	using System.ComponentModel.DataAnnotations;
	using Polygon = List<IntPoint>;
	using Polygons = List<List<IntPoint>>;

	public class ImageToPathObject3D : Object3D, IPathObject, IEditorDraw
	{
		private ThresholdFunctions _featureDetector = ThresholdFunctions.Silhouette;

		private ImageBuffer _histogramRawCache = null;
		private ImageBuffer _histogramDisplayCache = null;

		public ImageToPathObject3D()
		{
			Name = "Image to Path".Localize();
		}

		public enum ThresholdFunctions { Silhouette, Intensity, Alpha, Hue }

		[EnumRename("Alpha", "Transparency")]
		public ThresholdFunctions FeatureDetector
		{
			get { return _featureDetector; }
			set
			{
				if (value != _featureDetector)
				{
					_histogramRawCache = null;
					_featureDetector = value;
					var recreate = this.Histogram;
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
			if (_histogramRawCache != null)
			{
				var graphics2D = _histogramDisplayCache.NewGraphics2D();
				graphics2D.Clear(Color.Transparent);
				_histogramDisplayCache.CopyFrom(_histogramRawCache);
				graphics2D.FillRectangle(0, 0, RangeStart * _histogramDisplayCache.Width, _histogramDisplayCache.Height, new Color(Color.Red, 100));
				graphics2D.FillRectangle(RangeEnd * _histogramDisplayCache.Width, 0, 255, _histogramDisplayCache.Height, new Color(Color.Red, 100));
				graphics2D.Line(RangeStart * _histogramDisplayCache.Width, 0, RangeStart * _histogramDisplayCache.Width, _histogramDisplayCache.Height, new Color(Color.LightGray, 200));
				graphics2D.Line(RangeEnd * _histogramDisplayCache.Width, 0, RangeEnd * _histogramDisplayCache.Width, _histogramDisplayCache.Height, new Color(Color.LightGray, 200));
			}
		}

		[JsonIgnore]
		private ImageBuffer Image => this.Descendants<ImageObject3D>().FirstOrDefault()?.Image;

		[Range(0, 1, ErrorMessage = "Value for {0} must be between {1} and {2}.")]
		public double RangeStart { get; set; } = .1;
		[Range(0, 1, ErrorMessage = "Value for {0} must be between {1} and {2}.")]
		public double RangeEnd { get; set; } = 1;

		public IVertexSource VertexSource { get; set; } = new VertexStorage();

		private IThresholdFunction ThresholdFunction
		{
			get
			{
				switch (FeatureDetector)
				{
					case ThresholdFunctions.Silhouette:
						return new SilhouetteThresholdFunction(RangeStart, RangeEnd);

					case ThresholdFunctions.Intensity:
						return new MapOnMaxIntensity(RangeStart, RangeEnd);

					case ThresholdFunctions.Alpha:
						return new AlphaThresholdFunction(RangeStart, RangeEnd);

					case ThresholdFunctions.Hue:
						return new HueThresholdFunction(RangeStart, RangeEnd);
				}

				return new MapOnMaxIntensity(RangeStart, RangeEnd);
			}
		}

		public static void DrawPath(IObject3D item)
		{
			if (item is IPathObject pathObject)
			{
				if (pathObject.VertexSource == null)
				{
					return;
				}

				bool first = true;
				var lastPosition = Vector2.Zero;
				var aabb = item.VisibleMeshes().FirstOrDefault().GetAxisAlignedBoundingBox();
				var firstMove = Vector2.Zero;
				foreach (var vertex in pathObject.VertexSource.Vertices())
				{
					var position = vertex.position;
					if (first)
					{
						GL.PushMatrix();
						GL.PushAttrib(AttribMask.EnableBit);
						GL.MultMatrix(item.WorldMatrix().GetAsFloatArray());

						GL.Disable(EnableCap.Texture2D);
						GL.Disable(EnableCap.Blend);

						GL.Begin(BeginMode.Lines);
						GL.Color4(255, 0, 0, 255);
					}

					if (vertex.IsMoveTo)
					{
						firstMove = position;
					}
					else if (vertex.IsLineTo)
					{
						GL.Vertex3(lastPosition.X, lastPosition.Y, aabb.MaxXYZ.Z + 0.002);
						GL.Vertex3(position.X, position.Y, aabb.MaxXYZ.Z + 0.002);
					}
					else if (vertex.IsClose)
					{
						GL.Vertex3(firstMove.X, firstMove.Y, aabb.MaxXYZ.Z + 0.002);
						GL.Vertex3(lastPosition.X, lastPosition.Y, aabb.MaxXYZ.Z + 0.002);
					}

					lastPosition = position;
					first = false;
				}

				// if we drew anything
				if (!first)
				{
					GL.End();
					GL.PopAttrib();
					GL.PopMatrix();
				}
			}
		}

		public void DrawEditor(InteractionLayer layer, List<Object3DView> transparentMeshes, DrawEventArgs e, ref bool suppressNormalDraw)
		{
			ImageToPathObject3D.DrawPath(this);
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

		public override async void OnInvalidate(InvalidateArgs invalidateType)
		{
			if (invalidateType.InvalidateType.HasFlag(InvalidateType.Image)
				&& invalidateType.Source != this
				&& !RebuildLocked)
			{
				await Rebuild();
			}
			else if (invalidateType.InvalidateType.HasFlag(InvalidateType.Properties)
				&& invalidateType.Source == this)
			{
				UpdateHistogramDisplay();
				await Rebuild();
			}

			base.OnInvalidate(invalidateType);
		}

		private Color GetRGBA(byte[] buffer, int offset)
		{
			return new Color(buffer[offset + 2], buffer[offset + 1], buffer[offset + 0], buffer[offset + 3]);
		}

		public override Task Rebuild()
		{
			this.DebugDepth("Rebuild");

			bool propertyUpdated = false;
			var minSeparation = .01;
			if (RangeStart < 0
				|| RangeStart > 1
				|| RangeEnd < 0
				|| RangeEnd > 1
				|| RangeStart > RangeEnd - minSeparation)
			{
				RangeStart = Math.Max(0, Math.Min(1 - minSeparation, RangeStart));
				RangeEnd = Math.Max(0, Math.Min(1, RangeEnd));
				if (RangeStart > RangeEnd - minSeparation)
				{
					// values are overlapped or too close together
					if (RangeEnd < 1 - minSeparation)
					{
						// move the end up whenever possible
						RangeEnd = RangeStart + minSeparation;
					}
					else
					{
						// move the end to the end and the start up
						RangeEnd = 1;
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

					rebuildLock.Dispose();
					Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Path));
					return Task.CompletedTask;
				});
		}
	}
}