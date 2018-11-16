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
		private ThemeConfig theme;
		private GCodeFile gCodeMemoryFile;
		private PrinterConfig printer;
		private TextWidget costTextWidget;
		private TextWidget massTextWidget;
		private GuiWidget conditionalCostContainer;

		public GCodeDetailsView(GCodeFile gCodeMemoryFile, PrinterConfig printer, ThemeConfig theme)
			: base(FlowDirection.TopToBottom)
		{
			this.theme = theme;
			this.gCodeMemoryFile = gCodeMemoryFile;
			this.printer = printer;

			// put in the print time
			AddSetting("Print Time".Localize(), gCodeMemoryFile.EstimatedPrintTime());

			// show the filament used
			AddSetting("Filament Length".Localize(), gCodeMemoryFile.FilamentUsed(printer));

			AddSetting("Filament Volume".Localize(), gCodeMemoryFile.FilamentVolume(printer));

			// Cost info is only displayed when available - conditionalCostPanel is invisible when cost <= 0
			costTextWidget = AddSetting("Estimated Cost".Localize(), gCodeMemoryFile.EstimatedCost(printer));

			massTextWidget = AddSetting("Estimated Mass".Localize(), gCodeMemoryFile.EstimatedMass(printer));

			conditionalCostContainer = costTextWidget.Parent;
			conditionalCostContainer.Visible = gCodeMemoryFile.TotalCost(printer) > 0;

			printer.Settings.SettingChanged += Printer_SettingChanged;
		}

		private TextWidget AddSetting(string title, string value)
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

		public override void OnClosed(EventArgs e)
		{
			// Unregister listeners
			printer.Settings.SettingChanged -= Printer_SettingChanged;

			base.OnClosed(e);
		}

		private void Printer_SettingChanged(object s, EventArgs e)
		{
			if (e is StringEventArgs stringEvent)
			{
				if (stringEvent.Data == SettingsKey.filament_cost
					|| stringEvent.Data == SettingsKey.filament_diameter
					|| stringEvent.Data == SettingsKey.filament_density)
				{
					massTextWidget.Text = gCodeMemoryFile.EstimatedMass(printer);
					conditionalCostContainer.Visible = gCodeMemoryFile.TotalCost(printer) > 0;

					if (gCodeMemoryFile.TotalCost(printer) > 0)
					{
						costTextWidget.Text = gCodeMemoryFile.EstimatedCost(printer);
					}
				}
			}
		}
	}
}
