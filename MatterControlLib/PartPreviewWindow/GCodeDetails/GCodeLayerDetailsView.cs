/*
Copyright (c) 2018, Lars Brubaker, John Lewin
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
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ConfigurationPage;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class GCodeLayerDetailsView : FlowLayoutWidget
	{
		private ThemeConfig theme;

		public GCodeLayerDetailsView(GCodeFile gCodeMemoryFile, BedConfig sceneContext, ThemeConfig theme)
			: base(FlowDirection.TopToBottom)
		{
			this.theme = theme;

			GuiWidget layerIndex = AddSetting("Number".Localize(), "");
			GuiWidget layerTime = AddSetting("Time".Localize(), "");
			GuiWidget layerTimeToHere = AddSetting("Time From Start".Localize(), "");
			GuiWidget layerTimeFromHere = AddSetting("Time to End".Localize(), "");
			GuiWidget layerHeight = AddSetting("Height".Localize(), "");
			GuiWidget layerWidth = AddSetting("Layer Top".Localize(), "");
			GuiWidget layerFanSpeeds = AddSetting("Fan Speed".Localize(), "");

			void UpdateLayerDisplay(object sender, EventArgs e)
			{
				layerIndex.Text = $"{sceneContext.ActiveLayerIndex + 1}";
				layerTime.Text = gCodeMemoryFile.LayerTime(sceneContext.ActiveLayerIndex);
				layerTimeToHere.Text = gCodeMemoryFile.LayerTimeToHere(sceneContext.ActiveLayerIndex);
				layerTimeFromHere.Text = gCodeMemoryFile.LayerTimeFromeHere(sceneContext.ActiveLayerIndex);
				layerHeight.Text = $"{gCodeMemoryFile.GetLayerHeight(sceneContext.ActiveLayerIndex):0.###}";
				layerWidth.Text = $"{gCodeMemoryFile.GetLayerTop(sceneContext.ActiveLayerIndex):0.###}";
				var fanSpeed = gCodeMemoryFile.GetLayerFanSpeeds(sceneContext.ActiveLayerIndex);
				layerFanSpeeds.Text = string.IsNullOrWhiteSpace(fanSpeed) ? "Unchanged" : fanSpeed;
			}

			sceneContext.ActiveLayerChanged += UpdateLayerDisplay;

			// and do the initial setting
			UpdateLayerDisplay(this, null);
		}

		TextWidget AddSetting(string title, string value)
		{
			var textWidget = new TextWidget(value, textColor: theme.TextColor, pointSize: theme.DefaultFontSize)
			{
				AutoExpandBoundsToText = true,
				VAnchor = VAnchor.Center
			};

			var settingsItem = new SettingsItem(
				title,
				textWidget,
				theme,
				enforceGutter: false);

			this.AddChild(settingsItem);

			return textWidget;
		}
	}
}
