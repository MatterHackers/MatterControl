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
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Image.ThresholdFunctions;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.DesignTools
{
	public class Histogram
	{
		private ImageBuffer _histogramRawCache = new ImageBuffer(256, 100);

		public double RangeStart { get; set; } = 0;

		public double RangeEnd { get; set; } = .9;

		private Color GetRGBA(byte[] buffer, int offset)
		{
			return new Color(buffer[offset + 2], buffer[offset + 1], buffer[offset + 0], buffer[offset + 3]);
		}

		public event EventHandler RangeChanged;
		
		public event EventHandler EditComplete;

		public void RebuildAlphaImage(ImageBuffer sourceImage, ImageBuffer alphaImage, ImageToPathObject3D_2.AnalysisTypes analysisType)
		{
			if (analysisType == ImageToPathObject3D_2.AnalysisTypes.Colors)
			{
				RebuildColorToAlphaImage(sourceImage, alphaImage);
			}
			else
			{
				RebuildIntensityToAlphaImage(sourceImage, alphaImage);
			}
		}

		private void RebuildIntensityToAlphaImage(ImageBuffer sourceImage, ImageBuffer alphaImage)
		{
			if (sourceImage == null)
			{
				return;
			}

			// build the alpha image
			if (alphaImage == null)
			{
				alphaImage = new ImageBuffer(sourceImage.Width, sourceImage.Height);
			}
			else if (alphaImage.Width != sourceImage.Width
				|| alphaImage.Height != sourceImage.Height)
			{
				alphaImage.Allocate(sourceImage.Width, sourceImage.Height, sourceImage.BitDepth, sourceImage.GetRecieveBlender());
			}

			var startInt = (int)(RangeStart * 255);
			var endInt = (int)(RangeEnd * 255);
			var rangeInt = (int)Math.Max(1, (RangeEnd - RangeStart) * 255);

			byte GetAlphaFromIntensity(byte r, byte g, byte b)
			{
				// return (color.Red0To1 * 0.2989) + (color.Green0To1 * 0.1140) + (color.Blue0To1 * 0.5870);
				var alpha = (r * 76 + g * 29 + b * 150) / 255;
				if (alpha < startInt)
				{
					return 0;
				}
				else if (alpha > endInt)
				{
					return 0;
				}
				else
				{
					var s1 = 255 - Math.Min(255, ((alpha - startInt) * 255 / rangeInt));

					return (byte)s1;
				}
			}

			byte[] sourceBuffer = sourceImage.GetBuffer();
			byte[] destBuffer = alphaImage.GetBuffer();
			Parallel.For(0, sourceImage.Height, (y) =>
			{
				int imageOffset = sourceImage.GetBufferOffsetY(y);

				for (int x = 0; x < sourceImage.Width; x++)
				{
					int imageBufferOffsetWithX = imageOffset + x * 4;
					var b = sourceBuffer[imageBufferOffsetWithX + 0];
					var g = sourceBuffer[imageBufferOffsetWithX + 1];
					var r = sourceBuffer[imageBufferOffsetWithX + 2];
					destBuffer[imageBufferOffsetWithX + 0] = b;
					destBuffer[imageBufferOffsetWithX + 1] = g;
					destBuffer[imageBufferOffsetWithX + 2] = r;
					destBuffer[imageBufferOffsetWithX + 3] = GetAlphaFromIntensity(r, g, b);
				}
			});

			alphaImage.MarkImageChanged();
		}

		private static (float hue, float saturation) GetHue(byte bR, byte bG, byte bB)
		{
			var r = bR / 255.0f;
			var g = bG / 255.0f;
			var b = bB / 255.0f;
			var maxRGB = Math.Max(r, Math.Max(g, b));
			var minRGB = Math.Min(r, Math.Min(g, b));
			var deltaMaxToMin = maxRGB - minRGB;
			float r2, g2, b2;

			var lightness0To1 = (minRGB + maxRGB) / 2.0;
			if (lightness0To1 <= 0.0)
			{
				return (0, 0);
			}

			var saturation0To1 = deltaMaxToMin;
			if (saturation0To1 > 0.0)
			{
				saturation0To1 /= (lightness0To1 <= 0.5f) ? (maxRGB + minRGB) : (2.0f - maxRGB - minRGB);
			}
			else
			{
				return (0, 0);
			}

			r2 = (maxRGB - r) / deltaMaxToMin;
			g2 = (maxRGB - g) / deltaMaxToMin;
			b2 = (maxRGB - b) / deltaMaxToMin;
			var hue0To1 = 0.0f;
			if (r == maxRGB)
			{
				if (g == minRGB)
				{
					hue0To1 = 5.0f + b2;
				}
				else
				{
					hue0To1 = 1.0f - g2;
				}
			}
			else if (g == maxRGB)
			{
				if (b == minRGB)
				{
					hue0To1 = 1.0f + r2;
				}
				else
				{
					hue0To1 = 3.0f - b2;
				}
			}
			else
			{
				if (r == minRGB)
				{
					hue0To1 = 3.0f + g2;
				}
				else
				{
					hue0To1 = 5.0f - r2;
				}
			}

			hue0To1 /= 6.0f;

			return (hue0To1, saturation0To1);
		}

		private static byte GetAlphaFromHue(byte r, byte g, byte b, double start, double end)
		{
			var hs = GetHue(r, g, b);
			if (hs.saturation > .6 && hs.hue <= end && hs.hue > start)
			{
				return 255;
			}

			return 0;
		}

		private void RebuildColorToAlphaImage(ImageBuffer sourceImage, ImageBuffer alphaImage)
		{
			if (sourceImage == null)
			{
				return;
			}

			// build the alpha image
			if (alphaImage == null)
			{
				alphaImage = new ImageBuffer(sourceImage.Width, sourceImage.Height);
			}
			else if (alphaImage.Width != sourceImage.Width
				|| alphaImage.Height != sourceImage.Height)
			{
				alphaImage.Allocate(sourceImage.Width, sourceImage.Height, sourceImage.BitDepth, sourceImage.GetRecieveBlender());
			}

			byte[] sourceBuffer = sourceImage.GetBuffer();
			byte[] destBuffer = alphaImage.GetBuffer();
			//Parallel.For(0, sourceImage.Height, (y) =>
			for(int y = 0; y < sourceImage.Height; y++)
			{
				int imageOffset = sourceImage.GetBufferOffsetY(y);

				for (int x = 0; x < sourceImage.Width; x++)
				{
					int imageBufferOffsetWithX = imageOffset + x * 4;
					var b = sourceBuffer[imageBufferOffsetWithX + 0];
					var g = sourceBuffer[imageBufferOffsetWithX + 1];
					var r = sourceBuffer[imageBufferOffsetWithX + 2];
					destBuffer[imageBufferOffsetWithX + 0] = b;
					destBuffer[imageBufferOffsetWithX + 1] = g;
					destBuffer[imageBufferOffsetWithX + 2] = r;
					destBuffer[imageBufferOffsetWithX + 3] = GetAlphaFromHue(r, g, b, RangeStart, RangeEnd);
				}
			}
			//});

			alphaImage.MarkImageChanged();
		}

		class QuickHue : IThresholdFunction
		{
			public Color ZeroColor => throw new NotImplementedException();

			public double Threshold(Color color)
			{
				throw new NotImplementedException();
			}

			public double Transform(Color color)
			{
				return GetHue(color.red, color.green, color.blue).hue;
			}
		}

		public void BuildHistogramFromImage(ImageBuffer image, ImageToPathObject3D_2.AnalysisTypes analysisType)
		{
			// build the histogram cache
			var height = (int)(100 * GuiWidget.DeviceScale);
			_histogramRawCache = new ImageBuffer(256, height);
			var counts = new int[_histogramRawCache.Width];
			IThresholdFunction function = new MapOnMaxIntensity(0, 1);
			var bottom = 0;
			if (analysisType == ImageToPathObject3D_2.AnalysisTypes.Colors)
			{
				function = new QuickHue();
				bottom = (int)(10 * GuiWidget.DeviceScale);
			}

			byte[] buffer = image.GetBuffer();
			for (int y = 0; y < image.Height; y++)
			{
				int imageBufferOffset = image.GetBufferOffsetY(y);

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
			var graphics = _histogramRawCache.NewGraphics2D();
			var theme = ApplicationController.Instance.Theme;
			graphics.Clear(theme.SlightShade);

			var graphShape = new VertexStorage();
			var graphHeight = height - bottom;
			graphShape.MoveTo(0, bottom);
			for (int i = 0; i < 256; i++)
			{
				graphShape.LineTo(i, bottom + counts[i] * graphHeight / max );
			}
			graphShape.LineTo(256, bottom);
			graphShape.LineTo(0, bottom);
			graphics.Render(graphShape, 0, 0, theme.TextColor);

			for(int i=0; i<256; i++)
			{
				var hue = ColorF.FromHSL(i / 255.0, 1, .49).ToColor();
				graphics.Line(i, 0, i, bottom, hue);
			}
		}

		public GuiWidget NewEditWidget(ThemeConfig theme)
		{
			var histogramWidget = new GuiWidget()
			{
				HAnchor = HAnchor.Stretch,
				Height = 60 * GuiWidget.DeviceScale,
				Margin = 5,
			};

			var handleWidth = 10 * GuiWidget.DeviceScale;
			var histogramBackground = new GuiWidget()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
				Margin = new BorderDouble(handleWidth, 0)
			};

			histogramBackground.AfterDraw += (s, e) =>
			{
				var rangeStart = RangeStart;
				var rangeEnd = RangeEnd;
				var graphics2D = e.Graphics2D;
				graphics2D.Render(_histogramRawCache, 0, 0);
				var background = _histogramRawCache;
				graphics2D.FillRectangle(rangeStart * background.Width, 0, rangeEnd * background.Width, background.Height, theme.PrimaryAccentColor.WithAlpha(60));
			};

			histogramWidget.AddChild(histogramBackground);

			void RenderHandle(Graphics2D g, double s, double e)
			{
				var w = g.Width;
				var h = g.Height;
				g.Line(w * e, 0, w * e, h, theme.TextColor);
				var leftEdge = new VertexStorage();
				leftEdge.MoveTo(w * e, h * .80);
				leftEdge.curve3(w * e, h * .70, w * .5, h * .70);
				leftEdge.curve3(w * s, h * .60);
				leftEdge.LineTo(w * s, h * .40);
				leftEdge.curve3(w * s, h * .30, w * .5, h * .30);
				leftEdge.curve3(w * e, h * .20);
				g.Render(new FlattenCurves(leftEdge), theme.PrimaryAccentColor);
				g.Line(w * .35, h * .6, w * .35, h * .4, theme.BackgroundColor);
				g.Line(w * .65, h * .6, w * .65, h * .4, theme.BackgroundColor);
			}

			var leftHandle = new ImageWidget((int)(handleWidth), (int)histogramWidget.Height);
			leftHandle.Position = new Vector2(RangeStart * _histogramRawCache.Width, 0);
			var image = leftHandle.Image;
			RenderHandle(image.NewGraphics2D(), 0, 1);
			histogramWidget.AddChild(leftHandle);

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
					var newStart = RangeStart + offset / _histogramRawCache.Width;
					newStart = agg_basics.Clamp(newStart, 0, RangeEnd);
					if (RangeStart != newStart)
					{
						RangeStart = newStart;
						leftHandle.Position = new Vector2(RangeStart * _histogramRawCache.Width, 0);
						RangeChanged?.Invoke(this, null);
					}
				}
			};
			leftHandle.MouseUp += (s, e) =>
			{
				if (leftDown)
				{
					leftDown = false;
					EditComplete?.Invoke(this, null);
				}
			};

			var rightHandle = new ImageWidget((int)(handleWidth), (int)histogramWidget.Height);
			rightHandle.Position = new Vector2(RangeEnd * _histogramRawCache.Width + handleWidth, 0);
			image = rightHandle.Image;
			RenderHandle(image.NewGraphics2D(), 1, 0);
			histogramWidget.AddChild(rightHandle);

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
					var newEnd = RangeEnd + offset / _histogramRawCache.Width;
					newEnd = agg_basics.Clamp(newEnd, RangeStart, 1);
					if (RangeEnd != newEnd)
					{
						RangeEnd = newEnd;
						rightHandle.Position = new Vector2(RangeEnd * _histogramRawCache.Width + handleWidth, 0);
						RangeChanged?.Invoke(this, null);
					}
				}
			};
			rightHandle.MouseUp += (s, e) =>
			{
				if (rightDown)
				{
					rightDown = false;
					EditComplete?.Invoke(this, null);
				}
			};

			// grabing the center
			bool centerDown = false;
			var centerX = 0.0;
			histogramBackground.MouseDown += (s, e) =>
			{
				if (e.Button == MouseButtons.Left)
				{
					centerDown = true;
					centerX = e.Position.X;
				}
			};

			histogramBackground.MouseMove += (s, e) =>
			{
				if (centerDown)
				{
					var offset = e.Position.X - centerX;
					var newEnd = RangeEnd + offset / _histogramRawCache.Width;
					newEnd = agg_basics.Clamp(newEnd, RangeStart, 1);

					var newStart = RangeStart + offset / _histogramRawCache.Width;
					newStart = agg_basics.Clamp(newStart, 0, newEnd);

					if (RangeStart != newStart
						&& RangeEnd != newEnd)
					{
						RangeStart = newStart;
						RangeEnd = newEnd;
						leftHandle.Position = new Vector2(RangeStart * _histogramRawCache.Width, 0);
						rightHandle.Position = new Vector2(RangeEnd * _histogramRawCache.Width + handleWidth, 0);
						RangeChanged?.Invoke(this, null);
						histogramBackground.Invalidate();
					}
				}
			};

			histogramBackground.MouseUp += (s, e) =>
			{
				if (centerDown)
				{
					centerDown = false;
					EditComplete?.Invoke(this, null);
				}
			};

			return histogramWidget;
		}
	}
}