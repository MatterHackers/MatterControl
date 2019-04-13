/*
Copyright (c) 2019, Lars Brubaker, John Lewin
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
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.ConfigurationPage.PrintLeveling
{
	public class ProbePositionsWidget : GuiWidget
	{
		private List<Vector2> probePoints;
		private List<ProbePosition> probePositions;
		private PrinterConfig printer;
		private ThemeConfig theme;
		private Color opaqueMinimumAccent;
		private Color opaqueAccent;
		private RectangleDouble bedBounds;

		//private Vector2 bedSize;
		private bool circularBed;
		private Color extraLightColor;
		private Color lightColor;
		private double scalingFactor;
		private LevelingFunctions currentLevelingFunctions = null;

		public ProbePositionsWidget(PrinterConfig printer, List<Vector2> probePoints, List<ProbePosition> probePositions, ThemeConfig theme)
		{
			this.probePoints = probePoints;
			this.probePositions = probePositions;
			this.printer = printer;
			this.VAnchor = VAnchor.Absolute;
			this.theme = theme;

			extraLightColor = theme.BackgroundColor.Blend(theme.TextColor, 0.1);
			lightColor = theme.BackgroundColor.Blend(theme.TextColor, 0.2);
			opaqueMinimumAccent = theme.ResolveColor(theme.BackgroundColor, theme.AccentMimimalOverlay);
			opaqueAccent = theme.ResolveColor(theme.BackgroundColor, theme.AccentMimimalOverlay.WithAlpha(140));

			bedBounds = printer.Settings.BedBounds;
			circularBed = printer.Settings.GetValue<BedShape>(SettingsKey.bed_shape) == BedShape.Circular;
		}

		public int ActiveProbeIndex { get; set; }

		public bool RenderProbePath { get; set; } = true;

		public bool RenderLevelingData { get; set; }

		public bool SimplePoints { get; set; }

		public override void OnBoundsChanged(EventArgs e)
		{
			if (this.Height > this.Width)
			{
				scalingFactor = this.Width / bedBounds.Width;
			}
			else
			{
				scalingFactor = this.Height / bedBounds.Height;
			}

			base.OnBoundsChanged(e);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			var inverseScale = 1 / scalingFactor;

			var offset = Vector2.Zero;

			//graphics2D.PushTransform();

			// Reset to zero
			var existing = graphics2D.GetTransform();
			existing.translation(out double x, out double y);
			offset.X += x;
			offset.Y += y;

			// Center
			if (this.Width > this.Height)
			{
				offset.X += (this.Width / 2) - (bedBounds.Width * scalingFactor / 2);
			}
			else
			{
				offset.Y += (this.Height / 2) - (bedBounds.Height * scalingFactor / 2);
			}

			// Offset considering bed bounds
			offset.X -= bedBounds.Left * scalingFactor;
			offset.Y -= bedBounds.Bottom * scalingFactor;

			// Apply transform
			graphics2D.SetTransform(Affine.NewScaling(scalingFactor) * Affine.NewTranslation(offset));

			// Draw the bed
			this.RenderBed(graphics2D);

			// Build hotend path
			if (this.RenderProbePath)
			{
				this.RenderProbingPath(graphics2D);
			}

			if (this.RenderLevelingData)
			{
				if (currentLevelingFunctions == null)
				{
					PrintLevelingData levelingData = printer.Settings.Helpers.PrintLevelingData;
					currentLevelingFunctions = new LevelingFunctions(printer, levelingData);
				}

				var levelingTriangles = new VertexStorage();

				foreach (var region in currentLevelingFunctions.Regions)
				{
					levelingTriangles.MoveTo(region.V0.X, region.V0.Y);

					levelingTriangles.LineTo(region.V1.X, region.V1.Y);
					levelingTriangles.LineTo(region.V2.X, region.V2.Y);
					levelingTriangles.LineTo(region.V0.X, region.V0.Y);
				}

				graphics2D.Render(
					new Stroke(levelingTriangles),
					opaqueMinimumAccent);
			}

			// Render probe points
			int i = 0;
			foreach (var position in probePoints)
			{
				var center = new Vector2(position.X, position.Y);

				var circleColor = lightColor;

				if (this.SimplePoints)
				{
					graphics2D.Render(
						new Ellipse(center, 4 * inverseScale),
						opaqueMinimumAccent);
				}
				else
				{
					if (i < this.ActiveProbeIndex)
					{
						circleColor = opaqueMinimumAccent;
					}
					else if (i == this.ActiveProbeIndex)
					{
						circleColor = opaqueAccent;
					}

					graphics2D.Render(
						new Ellipse(center, 8 * inverseScale),
						circleColor);

					graphics2D.DrawString(
						$"{1 + i++}",
						center.X,
						center.Y,
						justification: Agg.Font.Justification.Center,
						baseline: Agg.Font.Baseline.BoundsCenter,
						pointSize: theme.FontSize7 * inverseScale,
						color: theme.TextColor);
				}
			}

			//graphics2D.PopTransform();

			base.OnDraw(graphics2D);
		}

		private void RenderProbingPath(Graphics2D graphics2D)
		{
			var firstPosition = probePoints.First();

			var path = new VertexStorage();
			path.MoveTo(firstPosition.X, firstPosition.Y);

			foreach (var position in probePoints)
			{
				var center = new Vector2(position.X, position.Y);
				path.LineTo(center);
			}

			// Render total path before probe points
			graphics2D.Render(
				new Stroke(path),
				theme.AccentMimimalOverlay);
		}

		private void RenderBed(Graphics2D graphics2D)
		{
			if (circularBed)
			{
				var radius = bedBounds.Width / 2;

				var lineCount = bedBounds.Width / 2 / 10;
				var steps = radius / lineCount;

				var bedShape = new Ellipse(Vector2.Zero, radius);
				graphics2D.Render(bedShape, theme.SlightShade);
				graphics2D.Render(new Stroke(bedShape), lightColor);

				var moreAlpha = extraLightColor.WithAlpha(150);

				for (var i = 0; i < lineCount; i++)
				{
					graphics2D.Render(
						new Stroke(
							new Ellipse(Vector2.Zero, radius - (i * steps))),
						moreAlpha);
				}
			}
			else
			{
				graphics2D.FillRectangle(bedBounds, theme.SlightShade);
				graphics2D.Rectangle(bedBounds, lightColor);

				var x = bedBounds.Left;
				var y = bedBounds.Bottom;

				var steps = bedBounds.Width / 20;
				for (var j = 1; j < steps; j++)
				{
					var step = j * 20;
					graphics2D.Line(x + step, y, x + step, y + bedBounds.Height, extraLightColor); // X
				}

				steps = bedBounds.Height / 20;
				for (var j = 1; j < steps; j++)
				{
					var step = j * 20;
					graphics2D.Line(x, y + step, x + bedBounds.Width, y + step, extraLightColor); // Y
				}
			}
		}
	}
}