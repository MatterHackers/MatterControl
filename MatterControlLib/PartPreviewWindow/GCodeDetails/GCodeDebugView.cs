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
using MatterControl.Printing;
using MatterHackers.Agg.UI;
using MatterHackers.GCodeVisualizer;
using MatterHackers.Localizations;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class GCodeDebugView : GCodeDetailsPanel
	{
		private GCodeFile gCodeFile;
		private PrinterTabPage printerTabPage;
		private ISceneContext sceneContext;
		private GCodeMemoryFile gCodeMemoryFile;
		private TextWidget startPointWidget;
		private TextWidget endPointWidget;
		private TextWidget slopeWidget;
		private TextWidget lengthWidget;
		private TextWidget yInterceptWidget;
		private TextWidget xInterceptWidget;
		private TextWidget timeToToolChange;
		private TextWidget rawLine;

		public GCodeDebugView(PrinterTabPage printerTabPage, GCodeFile gCodeFile, ISceneContext sceneContext, ThemeConfig theme)
			: base(theme)
		{
			this.gCodeFile = gCodeFile;
			this.printerTabPage = printerTabPage;
			this.sceneContext = sceneContext;

			gCodeMemoryFile = gCodeFile as GCodeMemoryFile;
			if (gCodeMemoryFile != null)
			{
				rawLine = this.AddSetting("G-Code Line".Localize(), "");
			}

			startPointWidget = this.AddSetting("Start".Localize(), "");
			endPointWidget = this.AddSetting("End".Localize(), "");
			lengthWidget = this.AddSetting("Length".Localize(), "");
			//slopeWidget = this.AddSetting("Slope".Localize(), "");
			//yInterceptWidget = this.AddSetting("Y Intercept".Localize(), "");
			//xInterceptWidget = this.AddSetting("X Intercept".Localize(), "");
			if (gCodeMemoryFile != null)
			{
				timeToToolChange = this.AddSetting("Time to Tool Change".Localize(), "");
			}

			// Register listeners
			printerTabPage.LayerFeaturesScrollbar.SecondValueChanged += this.LayerFeaturesScrollbar_SecondValueChanged;
		}

		public override void OnClosed(EventArgs e)
		{
			// Unregister listeners
			printerTabPage.LayerFeaturesScrollbar.SecondValueChanged -= this.LayerFeaturesScrollbar_SecondValueChanged;

			base.OnClosed(e);
		}

		private void LayerFeaturesScrollbar_SecondValueChanged(object sender, EventArgs e)
		{
			var renderInfo = sceneContext.RenderInfo;
			int layerIndex = renderInfo.EndLayerIndex - 1;
			int featuresOnLayer = sceneContext.GCodeRenderer.GetNumFeatures(layerIndex);
			int featureIndex = (int)(featuresOnLayer * renderInfo.FeatureToEndOnRatio0To1 + .5);

			int activeFeatureIndex = Math.Max(0, Math.Min(featureIndex, featuresOnLayer - 1) - 1);

			if (sceneContext.GCodeRenderer[layerIndex, activeFeatureIndex] is RenderFeatureTravel line)
			{
				if (rawLine != null)
				{
					rawLine.Text = gCodeMemoryFile.Instruction(line.InstructionIndex).Line;
				}

				var start = line.Start;
				var end = line.End;

				startPointWidget.Text = $"{start}";
				endPointWidget.Text = $"{end}";

				var length = new Vector2(start).Distance(new Vector2(end));
				lengthWidget.Text = $"{length:0.###}";

				// Slope
				// m = (y_2 - y_1) / (x_2 - x_1)

				// Y-Intercept
				// n = -x_1 * (y_2 - y_1) / (x_2 - x_1) + y_1

				var slope = (end.Y - start.Y) / (end.X - start.X);
				if (slopeWidget != null)
				{
					slopeWidget.Text = $"{slope:0.###}";
				}

				if (yInterceptWidget != null)
				{
					// -x_1 * (y_2 - y_1) / (x_2 - x_1) + y_1
					var yIntercept = -start.X * slope + start.Y;
					yInterceptWidget.Text = $"{yIntercept:0.###}";
				}

				if (xInterceptWidget != null)
				{
					// x_1 - y_1*(x_2-x_1)/(y_2-y_1)
					var xIntercept = start.X - start.Y * (end.X - start.X) / (end.Y - start.Y);
					xInterceptWidget.Text = $"{xIntercept:0.###}";
				}

				// put in the time until the next tool change
				if (timeToToolChange != null)
				{
					var toolChange = gCodeMemoryFile.NextToolChange(line.InstructionIndex);
					if (toolChange.time < double.PositiveInfinity)
					{
						timeToToolChange.Text = $"T{toolChange.toolIndex} : {toolChange.time:0.00}s";
					}
					else
					{
						timeToToolChange.Text = $"No More Changes";
					}
				}
			}
		}
	}
}
