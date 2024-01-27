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
using Matter_CAD_Lib.DesignTools.Interfaces;
using Matter_CAD_Lib.DesignTools._Object3D;
using MatterControlLib.DesignTools.Operations.Path;
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
    public class ImageToPathObject3D_2 : PathObject3DAbstract, IImageProvider, IObject3DControlsProvider, IPropertyGridModifier, IEditorWidgetModifier
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
		[ImageDisplay(Margin = new int[] { 9, 3, 9, 3 }, MaxXSize = 400, Stretch = true)]
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

        public override bool MeshIsSolidObject => false;

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
					RebuildFromImageData();
				}
			}
		}

		private void RebuildFromImageData()
		{
			var sourceImage = SourceImage;
			if (sourceImage != null)
			{
				switch (AnalysisType)
				{
					case AnalysisTypes.Intensity:
					case AnalysisTypes.Colors:
						if (alphaImage == null)
						{
							alphaImage = new ImageBuffer(SourceImage);
						}
						Histogram.BuildHistogramFromImage(sourceImage, AnalysisType);
						Histogram.RebuildAlphaImage(sourceImage, alphaImage, Image, AnalysisType);
						break;

					case AnalysisTypes.Transparency:
						Image?.CopyFrom(sourceImage);
						break;
				}
			}
		}

        [JsonIgnore]
        [DisplayName("")]
		[ReadOnly(true)]
		public string TransparencyMessage { get; set; } = "Your image is processed as is with no modifications. Transparent pixels are ignored, only opaque pixels are considered in feature detection.";


		[DisplayName("")]
		[JsonIgnore]
		private ImageBuffer SourceImage => ((IImageProvider)this.Descendants().Where(i => i is IImageProvider).FirstOrDefault())?.Image;

		[DisplayName("Select Range")]
		public Histogram Histogram { get; set; } = new Histogram();

		[Slider(0, 150, Easing.EaseType.Quadratic)]
		[Description("The minimum area each loop needs to be for inclusion")]
		[MaxDecimalPlaces(2)]
		public double MinSurfaceArea {get; set; } = 1;

		public void AddObject3DControls(Object3DControlsLayer object3DControlsLayer)
		{
			object3DControlsLayer.AddControls(ControlTypes.Standard2D);
		}

		public override bool CanApply => true;

		[HideFromEditor]
		public int NumLineLoops { get; set; }

		public override void Apply(UndoBuffer undoBuffer)
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

				NumLineLoops = lineLoops.Count;

				if (NumLineLoops > 1 && MinSurfaceArea > 0)
				{
					var minimumSurfaceArea = Math.Pow(MinSurfaceArea * 1000, 2);

					for (int i=lineLoops.Count - 1; i >=0; i--)
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

				rawVectorShape.Transform(affine);
				this.VertexStorage = rawVectorShape;

				progressReporter?.Invoke(1, null);
			}
		}

		private bool ColorDetected(ImageBuffer sourceImage, out double hueDetected)
		{
			byte[] sourceBuffer = sourceImage.GetBuffer();
			var min = new Vector3(double.MaxValue, double.MaxValue, double.MaxValue);
			var max = new Vector3(double.MinValue, double.MinValue, double.MinValue);

			var hueCount = new int[10];
			var colorPixels = 0;

			for(int y = 0; y < sourceImage.Height; y++)
			{
				int imageOffset = sourceImage.GetBufferOffsetY(y);
				for (int x = 0; x < sourceImage.Width; x++)
				{
					int offset = imageOffset + x * 4;
					var b = sourceBuffer[offset + 0];
					var g = sourceBuffer[offset + 1];
					var r = sourceBuffer[offset + 2];

					var color = new ColorF(r / 255.0, g / 255.0, b / 255.0);
					color.GetHSL(out double hue, out double saturation, out double lightness);

					min = Vector3.ComponentMin(min, new Vector3(hue, saturation, lightness));
					max = Vector3.ComponentMax(max, new Vector3(hue, saturation, lightness));

					if (saturation > .4 && lightness > .1 && lightness < .9)
					{
						hueCount[(int)(hue * 9)]++;
						colorPixels++;
					}
				}
			}


			if (colorPixels / (double)(sourceImage.Width * sourceImage.Height) > .1)
			{
				var indexAtMax = hueCount.ToList().IndexOf(hueCount.Max());
				hueDetected = indexAtMax / 10.0;
				return true;
			}

			hueDetected = 0;
			return false;
		}

		public override async void OnInvalidate(InvalidateArgs invalidateArgs)
		{
			if ((invalidateArgs.InvalidateType.HasFlag(InvalidateType.Image)
				|| invalidateArgs.InvalidateType.HasFlag(InvalidateType.Children))
                && invalidateArgs.Source != this
				&& !RebuildLocked)
			{
				// try to pick the best processing mode
				if (SourceImage.HasTransparency)
				{
					AnalysisType = AnalysisTypes.Transparency;
					Histogram.RangeStart = 0;
					Histogram.RangeEnd = .9;
				}
				else if (ColorDetected(SourceImage, out double hue))
				{
					AnalysisType = AnalysisTypes.Colors;
					Histogram.RangeStart = Math.Max(0, hue - .2);
					Histogram.RangeEnd = Math.Min(1, hue + .2);
				}
				else
				{
					AnalysisType = AnalysisTypes.Intensity;
					Histogram.RangeStart = 0;
					Histogram.RangeEnd = .9;
				}

				CopyNewImageData();
				await Rebuild();

				this.ReloadEditorPanel();
			}
			else if ((invalidateArgs.InvalidateType.HasFlag(InvalidateType.Properties) && invalidateArgs.Source == this))
			{
				await Rebuild();
			}
			else if (Expressions.NeedRebuild(this, invalidateArgs))
			{
                CopyNewImageData();
                await Rebuild();
			}

			base.OnInvalidate(invalidateArgs);
		}

		private void CopyNewImageData()
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
					switch (AnalysisType)
					{
						case AnalysisTypes.Transparency:
							this.GenerateMarchingSquaresAndLines(
								(progress0to1, status) =>
								{
									reporter?.Invoke(progress0to1, status);
								},
								SourceImage,
								new AlphaFunction());
							break;

						case AnalysisTypes.Colors:
						case AnalysisTypes.Intensity:
							this.GenerateMarchingSquaresAndLines(
								(progress0to1, status) =>
								{
                                    reporter?.Invoke(progress0to1, status);
                                },
								alphaImage,
								new AlphaFunction());
							break;
					}

					UiThread.RunOnIdle(() =>
					{
						rebuildLock.Dispose();
						Invalidate(InvalidateType.DisplayValues);
						this.DoRebuildComplete();
						Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Path));
					});

					return Task.CompletedTask;
				});
		}

		public void ModifyEditorWidget(GuiWidget widget, ThemeConfig theme, UndoBuffer undoBuffer, Action requestWidgetUpdate)
		{
			var child = this.Children.First();
			if (child is ImageObject3D imageObject)
			{
				ImageObject3D.ModifyImageObjectEditorWidget(imageObject, widget, theme, undoBuffer, requestWidgetUpdate);
			}
		}

		public void UpdateControls(PublicPropertyChange change)
		{
			change.SetRowVisible(nameof(Histogram), () => AnalysisType != AnalysisTypes.Transparency);
			change.SetRowVisible(nameof(MinSurfaceArea), () => AnalysisType != AnalysisTypes.Transparency);
			change.SetRowVisible(nameof(TransparencyMessage), () => AnalysisType == AnalysisTypes.Transparency);
			change.SetRowVisible(nameof(MinSurfaceArea), () => NumLineLoops > 1);
		}
	}
}