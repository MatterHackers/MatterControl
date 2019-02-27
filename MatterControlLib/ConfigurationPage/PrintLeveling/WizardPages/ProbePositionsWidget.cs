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
		private Vector2 bedSize;
		private bool circularBed;
		private RectangleDouble scaledBedRect;
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

			bedSize = printer.Settings.GetValue<Vector2>(SettingsKey.bed_size);
			//printCenter = printer.Settings.GetValue<Vector2>(SettingsKey.print_center);
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
				scalingFactor = this.Width / bedSize.X;
			}
			else
			{
				scalingFactor = this.Height / bedSize.Y;
			}

			scaledBedRect = new RectangleDouble(0, 0, bedSize.X * scalingFactor, bedSize.Y * scalingFactor);

			base.OnBoundsChanged(e);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			graphics2D.FillRectangle(scaledBedRect, theme.SlightShade);
			graphics2D.Rectangle(scaledBedRect, lightColor);

			// Draw some basic bed gridlines
			this.RenderBedGrid(graphics2D);

			// Build hotend path
			if (this.RenderProbePath)
			{
				this.RenderProbingPath(graphics2D);
			}

			if (this.RenderLevelingData)
			{
				if (currentLevelingFunctions == null)
				{
					PrintLevelingData levelingData = printer.Settings.Helpers.GetPrintLevelingData();
					currentLevelingFunctions = new LevelingFunctions(printer.Settings, levelingData);
				}

				var levelingTriangles = new VertexStorage();

				foreach (var region in currentLevelingFunctions.Regions)
				{
					levelingTriangles.MoveTo(region.V0.X * scalingFactor, region.V0.Y * scalingFactor);

					levelingTriangles.LineTo(region.V1.X * scalingFactor, region.V1.Y * scalingFactor);
					levelingTriangles.LineTo(region.V2.X * scalingFactor, region.V2.Y * scalingFactor);
					levelingTriangles.LineTo(region.V0.X * scalingFactor, region.V0.Y * scalingFactor);
				}

				graphics2D.Render(
					new Stroke(levelingTriangles),
					opaqueMinimumAccent);
			}

			// Render probe points
			int i = 0;
			foreach (var position in probePoints)
			{
				var center = new Vector2(position.X * scalingFactor, position.Y * scalingFactor);

				var circleColor = lightColor;

				if (this.SimplePoints)
				{
					graphics2D.Render(
						new Ellipse(center, 4),
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
						new Ellipse(center, 8),
						circleColor);

					graphics2D.DrawString(
						$"{1 + i++}",
						center.X,
						center.Y,
						justification: Agg.Font.Justification.Center,
						baseline: Agg.Font.Baseline.BoundsCenter,
						pointSize: theme.FontSize7,
						color: theme.TextColor);
				}
			}

			base.OnDraw(graphics2D);
		}

		private void RenderProbingPath(Graphics2D graphics2D)
		{
			var firstPosition = probePoints.First();

			var path = new VertexStorage();
			path.MoveTo(firstPosition.X * scalingFactor, firstPosition.Y * scalingFactor);

			foreach (var position in probePoints)
			{
				var center = new Vector2(position.X * scalingFactor, position.Y * scalingFactor);
				path.LineTo(center);
			}

			// Render total path before probe points
			graphics2D.Render(
				new Stroke(path),
				theme.AccentMimimalOverlay);
		}

		private void RenderBedGrid(Graphics2D graphics2D)
		{
			var steps = bedSize.X / 20;
			for (var j = 1; j < steps; j++)
			{
				var step = j * 20 * scalingFactor;
				graphics2D.Line(step, 0, step, scaledBedRect.Height, extraLightColor); // X
			}

			steps = bedSize.Y / 20;
			for (var j = 1; j < steps; j++)
			{
				var step = j * 20 * scalingFactor;
				graphics2D.Line(0, step, scaledBedRect.Width, step, extraLightColor); // Y
			}
		}
	}
}