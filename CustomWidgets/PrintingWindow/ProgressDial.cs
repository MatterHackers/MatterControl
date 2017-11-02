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
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Localizations;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.CustomWidgets
{
	public class ProgressDial : GuiWidget
	{
		private Color borderColor;
		private Stroke borderStroke;
		private double completedRatio = -1;
		private double innerRingRadius;

		private double layerCompletedRatio = 0;

		private int layerCount = -1;

		private TextWidget layerCountWidget;

		private double outerRingRadius;

		private double outerRingStrokeWidth = 7 * DeviceScale;

		private TextWidget percentCompleteWidget;

		private Color PrimaryAccentColor = ActiveTheme.Instance.PrimaryAccentColor;

		private Color PrimaryAccentShade = ActiveTheme.Instance.PrimaryAccentColor.AdjustLightness(0.7).ToColor();

		private double innerRingStrokeWidth = 10 * GuiWidget.DeviceScale;

		public ProgressDial()
		{
			percentCompleteWidget = new TextWidget("", pointSize: 22, textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				AutoExpandBoundsToText = true,
				VAnchor = VAnchor.Center,
				HAnchor = HAnchor.Center,
				Margin = new BorderDouble(bottom: 20)
			};

			CompletedRatio = 0;

			layerCountWidget = new TextWidget("", pointSize: 12, textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				AutoExpandBoundsToText = true,
				VAnchor = VAnchor.Center,
				HAnchor = HAnchor.Center,
				Margin = new BorderDouble(top: 32)
			};

			LayerCount = 0;

			this.AddChild(percentCompleteWidget);
			this.AddChild(layerCountWidget);

			borderColor = ActiveTheme.Instance.PrimaryTextColor;
			borderColor.Alpha0To1 = 0.3f;
		}

		public double CompletedRatio
		{
			get { return completedRatio; }
			set
			{
				if (completedRatio != value)
				{
					completedRatio = Math.Min(value, 1);

					// Flag for redraw
					this.Invalidate();

					percentCompleteWidget.Text = $"{CompletedRatio * 100:0}%";
				}
			}
		}

		public double LayerCompletedRatio
		{
			get { return layerCompletedRatio; }
			set
			{
				if (layerCompletedRatio != value)
				{
					layerCompletedRatio = value;
					this.Invalidate();
				}
			}
		}

		public int LayerCount
		{
			get { return layerCount; }
			set
			{
				if (layerCount != value)
				{
					layerCount = value;
					if (layerCount == 0)
					{
						layerCountWidget.Text = "Printing".Localize() + "...";
					}
					else
					{
						layerCountWidget.Text = "Layer".Localize() + " " + layerCount;
					}
				}
			}
		}

		public override void OnBoundsChanged(EventArgs e)
		{
			double borderRadius = this.LocalBounds.Width / 2 - 20 * DeviceScale;
			outerRingRadius = borderRadius - (outerRingStrokeWidth / 2) - 6 * DeviceScale;
			innerRingRadius = outerRingRadius - (outerRingStrokeWidth / 2) - (innerRingStrokeWidth / 2);

			borderStroke = new Stroke(new Ellipse(Vector2.Zero, borderRadius, borderRadius));

			base.OnBoundsChanged(e);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			var bounds = this.LocalBounds;

			// Draw border ring
			graphics2D.Render(
				borderStroke.Translate(bounds.Center),
				borderColor);

			// Draw outer progress ring
			var ringArc = new Arc(
				Vector2.Zero,
				new Vector2(outerRingRadius, outerRingRadius),
				0,
				MathHelper.DegreesToRadians(360) * LayerCompletedRatio, // percentCompletedInRadians
				Arc.Direction.ClockWise);

			var arcStroke = new Stroke(ringArc);
			arcStroke.width(outerRingStrokeWidth);
			graphics2D.Render(
				arcStroke.Rotate(90, AngleType.Degrees).Translate(bounds.Center),
				PrimaryAccentShade);

			// Draw inner progress ring
			ringArc = new Arc(
				Vector2.Zero,
				new Vector2(innerRingRadius, innerRingRadius),
				0,
				MathHelper.DegreesToRadians(360) * CompletedRatio, // percentCompletedInRadians
				Arc.Direction.ClockWise);
			arcStroke = new Stroke(ringArc);
			arcStroke.width(innerRingStrokeWidth);
			graphics2D.Render(
				arcStroke.Rotate(90, AngleType.Degrees).Translate(bounds.Center),
				PrimaryAccentColor);

			// Draw child controls
			base.OnDraw(graphics2D);
		}
	}
}