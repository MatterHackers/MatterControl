/*
Copyright (c) 2017, John Lewin
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
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl
{
	public class PrinterSelector : DropDownList, IIgnoredPopupChild
	{
		private EventHandler unregisterEvents;
		int lastSelectedIndex = -1;

		public PrinterSelector(ThemeConfig theme)
			: base("Printers".Localize() + "... ", theme.Colors.PrimaryTextColor, pointSize: theme.DefaultFontSize)
		{
			this.Name = "Printers... Menu";
			this.BorderColor = Color.Transparent;
			this.AutoScaleIcons = false;
			this.BackgroundColor = theme.MinimalShade;
			this.GutterWidth = 30;

			this.MenuItemsTextHoverColor = new Color("#ddd");

			this.Rebuild();

			this.SelectionChanged += (s, e) =>
			{
				string printerID = this.SelectedValue;
				if (printerID == "new"
					|| string.IsNullOrEmpty(printerID)
					|| printerID == ActiveSliceSettings.Instance.ID)
				{
					// do nothing
				}
				else
				{
					// TODO: when this opens a new tab we will not need to check any printer
					if (ApplicationController.Instance.ActivePrinter.Connection.PrinterIsPrinting
						|| ApplicationController.Instance.ActivePrinter.Connection.PrinterIsPaused)
					{
						if (this.SelectedIndex != lastSelectedIndex)
						{
							UiThread.RunOnIdle(() =>
							StyledMessageBox.ShowMessageBox("Please wait until the print has finished and try again.".Localize(), "Can't switch printers while printing".Localize())
							);
							this.SelectedIndex = lastSelectedIndex;
						}
					}
					else
					{
						lastSelectedIndex = this.SelectedIndex;

						ProfileManager.Instance.LastProfileID = this.SelectedValue;
					}
				}
			};

			ActiveSliceSettings.SettingChanged.RegisterEvent((s, e) =>
			{
				string settingsName = (e as StringEventArgs)?.Data;
				if (settingsName != null && settingsName == SettingsKey.printer_name)
				{
					if (ProfileManager.Instance.ActiveProfile != null)
					{
						ProfileManager.Instance.ActiveProfile.Name = ActiveSliceSettings.Instance.GetValue(SettingsKey.printer_name);
						Rebuild();
					}
				}
			}, ref unregisterEvents);

			// Rebuild the droplist any time the ActivePrinter changes
			ActiveSliceSettings.ActivePrinterChanged.RegisterEvent((s, e) =>
			{
				this.Rebuild();
			}, ref unregisterEvents);

			// Rebuild the droplist any time the Profiles list changes
			ProfileManager.ProfilesListChanged.RegisterEvent((s, e) =>
			{
				this.Rebuild();
			}, ref unregisterEvents);

			HAnchor = HAnchor.Fit;
			Cursor = Cursors.Hand;
			Margin = 0;
		}

		public void Rebuild()
		{
			this.MenuItems.Clear();

			// Always reset to -1, then search for match below
			this.SelectedIndex = -1;

			//Add the menu items to the menu itself
			foreach (var printer in ProfileManager.Instance.ActiveProfiles.OrderBy(p => p.Name))
			{
				this.AddItem(this.GetOemIcon(printer.Make), printer.Name, printer.ID);
			}

			string lastProfileID = ProfileManager.Instance.LastProfileID;
			if (!string.IsNullOrEmpty(lastProfileID))
			{
				this.SelectedValue = lastProfileID;
				lastSelectedIndex = this.SelectedIndex;
			}
			else
			{
				this.SelectedIndex = -1;
			}
		}

		private ImageBuffer GetOemIcon(string oemName)
		{
			var imageBuffer = new ImageBuffer(16, 16);

			ApplicationController.Instance.DownloadToImageAsync(
				imageBuffer,
				ApplicationController.Instance.GetFavIconUrl(oemName),
				scaleToImageX: false);

			return imageBuffer;
		}

		public override void OnClosed(EventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}

		public bool KeepMenuOpen()
		{
			return false;
		}
	}
}