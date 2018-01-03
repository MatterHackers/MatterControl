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
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.SetupWizard;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class SliceSettingsOverflowMenu : OverflowMenu
	{
		public SliceSettingsOverflowMenu(PrinterConfig printer, SliceSettingsWidget sliceSettingsWidget)
		{
			this.VAnchor = VAnchor.Fit | VAnchor.Center;
			this.AlignToRightEdge = true;
			this.Name = "Slice Settings Overflow Menu";

			this.PopupContent = GenerateMenuContents(printer, sliceSettingsWidget);
		}

		// On load walk back to the first ancestor with background colors and copy
		public override void OnLoad(EventArgs args)
		{
			var firstBackgroundColor = this.Parents<GuiWidget>().Where(p => p.BackgroundColor.Alpha0To1 == 1).FirstOrDefault()?.BackgroundColor;
			this.BackgroundColor = firstBackgroundColor ?? Color.Transparent;

			base.OnLoad(args);
		}

		private FlowLayoutWidget GenerateMenuContents(PrinterConfig printer, SliceSettingsWidget sliceSettingsWidget)
		{
			var popupMenu = new PopupMenu(ApplicationController.Instance.Theme);

			var checkedIcon = AggContext.StaticData.LoadIcon("fa-check_16.png");

			var icon = sliceSettingsWidget.ShowHelpControls ? checkedIcon : null;

			popupMenu.CreateMenuItem("Show Help".Localize(), icon).Click += (s, e) =>
			{
				sliceSettingsWidget.ShowHelpControls = !sliceSettingsWidget.ShowHelpControls;
				sliceSettingsWidget.RebuildSliceSettingsTabs();
			};

			popupMenu.CreateHorizontalLine();

			PopupMenu.MenuItem menuItem;

			menuItem = popupMenu.CreateMenuItem("Export".Localize());
			menuItem.Click += (s, e) =>
			{
				DialogWindow.Show<ExportSettingsPage>();
			};

			menuItem = popupMenu.CreateMenuItem("Restore Settings".Localize());
			menuItem.Click += (s, e) =>
			{
				DialogWindow.Show<PrinterProfileHistoryPage>();
			};
			menuItem.Enabled = !string.IsNullOrEmpty(AuthenticationData.Instance.ActiveSessionUsername);

			menuItem = popupMenu.CreateMenuItem("Reset to Defaults".Localize());
			menuItem.Click += (s, e) =>
			{
				UiThread.RunOnIdle(() =>
				{
					StyledMessageBox.ShowMessageBox(
						revertSettings =>
						{
							if (revertSettings)
							{
								bool onlyReloadSliceSettings = true;
								if (printer.Settings.GetValue<bool>(SettingsKey.print_leveling_required_to_print)
								&& printer.Settings.GetValue<bool>(SettingsKey.print_leveling_enabled))
								{
									onlyReloadSliceSettings = false;
								}

								printer.Settings.ClearUserOverrides();
								printer.Settings.Save();

								if (onlyReloadSliceSettings)
								{
									printer?.Bed.GCodeRenderer?.Clear3DGCode();
								}
								else
								{
									ApplicationController.Instance.ReloadAll();
								}
							}
						},
						"Resetting to default values will remove your current overrides and restore your original printer settings.\nAre you sure you want to continue?".Localize(),
						"Revert Settings".Localize(),
						StyledMessageBox.MessageType.YES_NO);
				});
			};

			return popupMenu;
		}
	}
}