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
using System.Linq;
using MatterControl.Printing;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ConfigurationPage;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class GCodeDetailsView : FlowLayoutWidget
	{
		private EventHandler unregisterEvents;
		private ThemeConfig theme;

		public GCodeDetailsView(GCodeFile gCodeMemoryFile, PrinterConfig printerConfig, ThemeConfig theme)
			: base(FlowDirection.TopToBottom)
		{
			this.theme = theme;

			// put in the print time
			AddSetting("Print Time".Localize(), gCodeMemoryFile.EstimatedPrintTime());

			// show the filament used
			AddSetting("Filament Length".Localize(), gCodeMemoryFile.FilamentUsed(printerConfig));

			AddSetting("Filament Volume".Localize(), gCodeMemoryFile.FilamentVolume(printerConfig));

			// Cost info is only displayed when available - conditionalCostPanel is invisible when cost <= 0
			TextWidget costTextWidget = AddSetting("Estimated Cost".Localize(), gCodeMemoryFile.EstimatedCost(printerConfig));

			TextWidget massTextWidget = AddSetting("Estimated Mass".Localize(), gCodeMemoryFile.EstimatedMass(printerConfig));

			var conditionalCostContainer = costTextWidget.Parent;
			conditionalCostContainer.Visible = gCodeMemoryFile.TotalCost(printerConfig) > 0;

			PrinterSettings.SettingChanged.RegisterEvent((s, e) =>
			{
				if (e is StringEventArgs stringEvent)
				{
					if (stringEvent.Data == SettingsKey.filament_cost
						|| stringEvent.Data == SettingsKey.filament_diameter
						|| stringEvent.Data == SettingsKey.filament_density)
					{
						massTextWidget.Text = gCodeMemoryFile.EstimatedMass(printerConfig);
						conditionalCostContainer.Visible = gCodeMemoryFile.TotalCost(printerConfig) > 0;

						if (gCodeMemoryFile.TotalCost(printerConfig) > 0)
						{
							costTextWidget.Text = gCodeMemoryFile.EstimatedCost(printerConfig);
						}
					}
				}
			}, ref unregisterEvents);
		}

		TextWidget AddSetting(string title, string value)
		{
			var textWidget = new TextWidget(value, textColor: theme.Colors.PrimaryTextColor, pointSize: theme.DefaultFontSize)
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

		public override void OnClosed(EventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}
	}
}
