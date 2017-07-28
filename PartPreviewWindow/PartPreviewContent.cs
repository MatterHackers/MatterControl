/*
Copyright (c) 2017, Lars Brubaker
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
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class PartPreviewContent : FlowLayoutWidget
	{
		private EventHandler unregisterEvents;

		public PartPreviewContent(PrintItemWrapper printItem)
		{
			this.AnchorAll();

			var activeSettings = ActiveSliceSettings.Instance;

			var tabControl = ApplicationController.Instance.Theme.CreateTabControl();

			string tabTitle = !activeSettings.PrinterSelected ? "Printer".Localize() : activeSettings.GetValue(SettingsKey.printer_name);

			RGBA_Bytes selectedTabColor;
			if (!UserSettings.Instance.IsTouchScreen)
			{
				tabControl.TabBar.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
				selectedTabColor = ActiveTheme.Instance.TabLabelSelected;
			}
			else
			{
				tabControl.TabBar.BackgroundColor = ActiveTheme.Instance.TransparentLightOverlay;
				selectedTabColor = ActiveTheme.Instance.SecondaryAccentColor;
			}

			// Add a tab for the current printer
			var printerTab = new SimpleTextTabWidget(
				new TabPage(new PrinterTabPage(ActiveSliceSettings.Instance, printItem), tabTitle.ToUpper()),
				"3D View Tab",
				tabControl.TextPointSize,
				selectedTabColor,
				new RGBA_Bytes(),
				ActiveTheme.Instance.TabLabelUnselected,
				new RGBA_Bytes(),
				useUnderlineStyling: true);
			printerTab.ToolTipText = "Preview 3D Design".Localize();
			tabControl.AddTab(printerTab);

			// TODO: add in the printers and designs that are currently open (or were open last run).

			// Add a tab for the current printer
			var plusTabSelect = new SimpleTextTabWidget(
				new TabPage(new PlusTabPage(), "+"),
				"Create New",
				tabControl.TextPointSize,
				selectedTabColor,
				new RGBA_Bytes(),
				ActiveTheme.Instance.TabLabelUnselected,
				new RGBA_Bytes(),
				useUnderlineStyling: true);
			plusTabSelect.ToolTipText = "Create New".Localize();
			tabControl.AddTab(plusTabSelect);

			this.AddChild(tabControl);

	
			ActiveSliceSettings.SettingChanged.RegisterEvent((s, e) =>
			{
				if (e is StringEventArgs stringEvent 
					&& stringEvent.Data == SettingsKey.printer_name)
				{
					printerTab.TabPage.Text = ActiveSliceSettings.Instance.GetValue(SettingsKey.printer_name);
				}
				
			}, ref unregisterEvents);

		}
	}
}