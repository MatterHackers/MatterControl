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
using System.ComponentModel;
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
using Newtonsoft.Json;
using Polygon = System.Collections.Generic.List<ClipperLib.IntPoint>;
using Polygons = System.Collections.Generic.List<System.Collections.Generic.List<ClipperLib.IntPoint>>;

namespace MatterHackers.MatterControl.DesignTools
{
	[HideMeterialAndColor]
	public class ImageToPathObject3D_2 : Object3D, IImageProvider, IPathObject, ISelectedEditorDraw, IObject3DControlsProvider, IPropertyGridModifier, IEditorWidgetModifier
	{
		public ImageToPathObject3D_2()
		{
			Name = "Image to Path".Localize();
		}

		public enum AnalysisTypes
		{
			Transparency,
			Colors,
			Intensity,
		}

		private ImageBuffer alphaImage;

		private ImageBuffer _image;
		/// <summary>
		/// This is the image after it has been processed into an alpha image
		/// </summary>
		[DisplayName("")]
		[JsonIgnore]
		[ImageDisplay(Margin = new int[] { 30, 3, 30, 3 }, MaxXSize = 400, Stretch = true)]
		public ImageBuffer Image
		{
			get
			{
				if (_image == null
					&& SourceImage != null)
				{
					_image = new ImageBuffer(SourceImage);
					alphaImage = new ImageBuffer(SourceImage);
					Histogram.BuildHistogramFromImage(SourceImage, AnalysisType);
					Histogram.RangeChanged += (s, e) =>
					{
						Histogram.RebuildAlphaImage(SourceImage, alphaImage, _image, AnalysisType);
					};

					Histogram.EditComplete += (s, e) =>
					{
						this.Invalidate(InvalidateType.Properties);
					};

					switch (AnalysisType)
					{
						case AnalysisTypes.Intensity:
						case AnalysisTypes.Colors:
							Histogram.RebuildAlphaImage(SourceImage, alphaImage, _image, AnalysisType);
							break;

						case AnalysisTypes.Transparency:
							_image.CopyFrom(SourceImage);
							break;
					}
				}

				return _image;
			}

			set
			{
			}
		}


		private AnalysisTypes _featureDetector = AnalysisTypes.Intensity;
		[EnumDisplay(Mode = EnumDisplayAttribute.PresentationMode.Tabs)]
		public AnalysisTypes AnalysisType
		{
			get
			{
				return _featureDetector;
			}

			set
			{
				if (_featureDetector != value)
				{
					_featureDetector = value;
					var sourceImage = SourceImage;
					if (sourceImage != null)
					{
						switch (AnalysisType)
						{
							case AnalysisTypes.Intensity:
							case AnalysisTypes.Colors:
								Histogram.BuildHistogramFromImage(sourceImage, AnalysisType);
								Histogram.RebuildAlphaImage(sourceImage, alphaImage, Image, AnalysisType);
								break;

							case AnalysisTypes.Transparency:
								Image?.CopyFrom(sourceImage);
								break;
						}
					}
				}
			}
		}

		[DisplayName("")]
		[ReadOnly(true)]
		public string TransparencyMessage { get; set; } = "Your image is processed as is with no modifications. Transparent pixels are ignored, only opaque pixels are considered in feature detection.";


		[DisplayName("")]
		[JsonIgnore]
		private ImageBuffer SourceImage => ((IImageProvider)this.Descendants().Where(i => i is IImageProvider).FirstOrDefault())?.Image;

		[DisplayName("Select Range")]
		public Histogram Histogram { get; set; } = new Histogram();

		[Slider(0, 10, 1)]
		[Description("The minimum area each loop needs to be for inclusion")]
		public double MinSurfaceArea {get; set; } = 1;

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

		public void GenerateMarchingSquaresAndLines(Action<double, string> progressReporter, ImageBuffer image, IThresholdFunction thresholdFunction, int minimumSurfaceArea)
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

				if (minimumSurfaceArea > 0)
				{
					for(int i=lineLoops.Count - 1; i >=0; i--)
					{
						var area = Math.Abs(Clipper.Area(lineLoops[i]));
						if (area < minimumSurfaceArea)
						{
							lineLoops.RemoveAt(i);
						}
					}
				}

				progressReporter?.Invoke(.15, null);

				var min = new IntPoint(-10, -10);
				var max = new IntPoint(10 + image.Width * pixelsToIntPointsScale, 10 + image.Height * pixelsToIntPointsScale);

				var boundingPoly = new Polygon
				{
					min,
					new IntPoint(min.X, max.Y),
					max,
					new IntPoint(max.X, min.Y)
				};

				// now clip the polygons to get the inside and outside polys
				var clipper = new Clipper();
				clipper.AddPaths(lineLoops, PolyType.ptSubject, true);
				clipper.AddPath(boundingPoly, PolyType.ptClip, true);

				var polygonShapes = new Polygons();
				progressReporter?.Invoke(.3, null);

				clipper.Execute(ClipType.ctIntersection, polygonShapes);

				progressReporter?.Invoke(.55, null);

				polygonShapes = Clipper.CleanPolygons(polygonShapes, 100);

				progressReporter?.Invoke(.75, null);

				VertexStorage rawVectorShape = polygonShapes.PolygonToPathStorage();

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
				if (AnalysisType != AnalysisTypes.Transparency)
				{
					Histogram.BuildHistogramFromImage(SourceImage, AnalysisType);
					var _ = Image; // call this to make sure it is built
					Histogram.RebuildAlphaImage(SourceImage, alphaImage, Image, AnalysisType);
				}
				else
				{
					Image?.CopyFrom(SourceImage);
				}
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
					switch (AnalysisType)
					{
						case AnalysisTypes.Transparency:
							this.GenerateMarchingSquaresAndLines(
								(progress0to1, status) =>
								{
									progressStatus.Progress0To1 = progress0to1;
									progressStatus.Status = status;
									reporter.Report(progressStatus);
								},
								SourceImage,
								new AlphaFunction(),
								(int)(MinSurfaceArea * 1000));
							break;

						case AnalysisTypes.Colors:
						case AnalysisTypes.Intensity:
							this.GenerateMarchingSquaresAndLines(
								(progress0to1, status) =>
								{
									progressStatus.Progress0To1 = progress0to1;
									progressStatus.Status = status;
									reporter.Report(progressStatus);
								},
								alphaImage,
								new AlphaFunction(),
								(int)(Math.Pow(MinSurfaceArea * 1000, 2)));
							break;
					}

					UiThread.RunOnIdle(() =>
					{
						rebuildLock.Dispose();
						Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Path));
					});

					return Task.CompletedTask;
				});
		}

		public void ModifyEditorWidget(GuiWidget widget, ThemeConfig theme, Action requestWidgetUpdate)
		{
			var child = this.Children.First();
			if (child is ImageObject3D imageObject)
			{
				ImageObject3D.ModifyImageObjectEditorWidget(imageObject, widget, theme, requestWidgetUpdate);
			}
		}

		public void UpdateControls(PublicPropertyChange change)
		{
			change.SetRowVisible(nameof(Histogram), () => AnalysisType != AnalysisTypes.Transparency);
			change.SetRowVisible(nameof(MinSurfaceArea), () => AnalysisType != AnalysisTypes.Transparency);
			change.SetRowVisible(nameof(TransparencyMessage), () => AnalysisType == AnalysisTypes.Transparency);
		}
	}
}