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
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MarchingSquares;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.DesignTools
{
	using MatterHackers.Agg.UI;
	using MatterHackers.RenderOpenGl.OpenGl;
	using MatterHackers.VectorMath;
	using Polygon = List<IntPoint>;
	using Polygons = List<List<IntPoint>>;

	public class ImageToPath : Object3D, IPublicPropertyObject, IPathObject, IEditorDraw
	{
		private static ImageBuffer generatingThumbnailIcon = AggContext.StaticData.LoadIcon(Path.Combine("building_thumbnail_40x40.png"));

		public ImageToPath()
		{
			Name = "Path".Localize();
		}

		public enum ThresholdFunctions { Intensity, Alpha, Hue }

		public override bool CanRemove => true;

		public ThresholdFunctions FeatureDetector { get; set; } = ThresholdFunctions.Intensity;
		public int EndThreshold { get; internal set; } = 255;
		public int StartThreshold { get; internal set; } = 120;

		[JsonIgnore]
		public IVertexSource VertexSource { get; set; } = new VertexStorage();

		[JsonIgnore]
		private ImageBuffer Image
		{
			get
			{
				var item = this.Descendants().Where((d) => d is ImageObject3D).FirstOrDefault();
				if (item is ImageObject3D imageItem)
				{
					return imageItem.Image;
				}

				return null;
			}
		}

		private IThresholdFunction ThresholdFunction
		{
			get
			{
				switch (FeatureDetector)
				{
					case ThresholdFunctions.Intensity:
						return new MapOnMaxIntensity(StartThreshold, EndThreshold);

					case ThresholdFunctions.Alpha:
						return new AlphaThresholdFunction(StartThreshold, EndThreshold);

					case ThresholdFunctions.Hue:
						break;
				}

				return new MapOnMaxIntensity(StartThreshold, EndThreshold);
			}
		}

		public void DrawEditor(object sender, DrawEventArgs e)
		{
			ImageToPath.DrawPath(this);
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
				Vector2 lastPosition = Vector2.Zero;
				var aabb = item.VisibleMeshes().FirstOrDefault().GetAxisAlignedBoundingBox();
				foreach (var vertex in pathObject.VertexSource.Vertices())
				{
					var position = vertex.position;
					if (first)
					{
						first = false;
						GL.PushMatrix();
						GL.PushAttrib(AttribMask.EnableBit);
						GL.MultMatrix(item.WorldMatrix().GetAsFloatArray());

						GL.Disable(EnableCap.Texture2D);
						GL.Disable(EnableCap.Blend);

						GL.Begin(BeginMode.Lines);
						GL.Color4(255, 0, 0, 255);
					}

					if (vertex.IsLineTo)
					{
						GL.Vertex3(lastPosition.X, lastPosition.Y, aabb.maxXYZ.Z + 0.002);
						GL.Vertex3(position.X, position.Y, aabb.maxXYZ.Z + 0.002);
					}

					lastPosition = position;
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

		public void GenerateMarchingSquaresAndLines(Action<double, string> progressReporter, ImageBuffer image, IThresholdFunction thresholdFunction)
		{
			if (image != null)
			{
				// Regenerate outline
				var marchingSquaresData = new MarchingSquaresByte(
					image,
					thresholdFunction.Threshold0To1,
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

				this.VertexSource = new VertexSourceApplyTransform(rawVectorShape, affine);

				progressReporter?.Invoke(1, null);

				Invalidate(new InvalidateArgs(this, InvalidateType.Path));
			}
		}

		public override void OnInvalidate(InvalidateArgs invalidateType)
		{
			if (invalidateType.InvalidateType == InvalidateType.Image
				&& invalidateType.Source != this
				&& !RebuildSuspended)
			{
				Rebuild(null);
			}
			else
			{
				base.OnInvalidate(invalidateType);
			}
		}

		public override void Rebuild(UndoBuffer undoBuffer)
		{
			SuspendRebuild();

			// Make a fast simple path
			this.GenerateMarchingSquaresAndLines(null, generatingThumbnailIcon, new MapOnMaxIntensity());

			// now create a long running task to process the image
			ApplicationController.Instance.Tasks.Execute(
				"Extrude Image".Localize(),
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

					return Task.CompletedTask;
				});

			ResumeRebuild();

			Invalidate(new InvalidateArgs(this, InvalidateType.Path));
			base.Rebuild(undoBuffer);
		}
	}
}