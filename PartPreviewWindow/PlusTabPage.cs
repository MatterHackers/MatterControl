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

using MatterHackers.Agg;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ActionBar;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.SettingsManagement;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class PlusTabPage : FlowLayoutWidget
	{
		public PlusTabPage()
			: base(FlowDirection.TopToBottom)
		{
			this.Name = "+";
			this.HAnchor = HAnchor.ParentLeftRight;
			this.VAnchor = VAnchor.ParentBottomTop;
			this.Padding = 15;

			var theme = ApplicationController.Instance.Theme;

			BorderDouble buttonSpacing = 3;

			// put in the add new design stuff
			var createItemsSection = CreateSection("Create New".Localize() + ":");

			var createPart = theme.ButtonFactory.Generate("Create Part".Localize());
			createPart.Margin = buttonSpacing;
			createPart.HAnchor = HAnchor.ParentLeft;
			createItemsSection.AddChild(createPart);

			var createPrinter = theme.ButtonFactory.Generate("Create Printer".Localize());
			createPrinter.Margin = buttonSpacing;
			createPrinter.HAnchor = HAnchor.ParentLeft;
			createPrinter.Click += (s, e) =>
			{
				if (PrinterConnection.Instance.PrinterIsPrinting
					|| PrinterConnection.Instance.PrinterIsPaused)
				{
					UiThread.RunOnIdle(() =>
						StyledMessageBox.ShowMessageBox(null, "Please wait until the print has finished and try again.".Localize(), "Can't add printers while printing".Localize())
					);
				}
				else
				{
					UiThread.RunOnIdle(() =>
					{
						WizardWindow.ShowPrinterSetup(true);
					});
				}
			};
			createItemsSection.AddChild(createPrinter);

			var existingPrinterSection = CreateSection("Open Existing".Localize() + ":");

			var printerSelector = new PrinterSelectEditDropdown()
			{
				Margin = new BorderDouble(left: 15)
			};
			existingPrinterSection.AddChild(printerSelector);

			var otherItemsSection = CreateSection("Other".Localize() + ":");

			var redeemDesignCode = theme.ButtonFactory.Generate("Redeem Design Code".Localize());
			redeemDesignCode.Margin = buttonSpacing;
			redeemDesignCode.HAnchor = HAnchor.ParentLeft;
			redeemDesignCode.Click += (s, e) =>
			{
				// Implementation already does RunOnIdle
				ApplicationController.Instance.RedeemDesignCode?.Invoke();
			};
			otherItemsSection.AddChild(redeemDesignCode);

			var redeemShareCode = theme.ButtonFactory.Generate("Enter Share Code".Localize());
			redeemShareCode.Margin = buttonSpacing;
			redeemShareCode.HAnchor = HAnchor.ParentLeft;
			redeemShareCode.Click += (s, e) =>
			{
				// Implementation already does RunOnIdle
				ApplicationController.Instance.EnterShareCode?.Invoke();
			};
			otherItemsSection.AddChild(redeemShareCode);

			var importButton = theme.ButtonFactory.Generate("Import".Localize());
			importButton.Margin = buttonSpacing;
			importButton.HAnchor = HAnchor.ParentLeft;
			importButton.Click += (s, e) =>
			{
				UiThread.RunOnIdle(() => WizardWindow.Show<ImportSettingsPage>("ImportSettingsPage", "Import Settings Page"));
			};
			otherItemsSection.AddChild(importButton);

			if (OemSettings.Instance.ShowShopButton)
			{
				var shopButton = theme.ButtonFactory.Generate("Buy Materials".Localize(), StaticData.Instance.LoadIcon("icon_shopping_cart_32x32.png", 24, 24));
				shopButton.ToolTipText = "Shop online for printing materials".Localize();
				shopButton.Name = "Buy Materials Button";
				shopButton.HAnchor = HAnchor.ParentLeft;
				shopButton.Margin = buttonSpacing;
				shopButton.Click += (sender, e) =>
				{
					double activeFilamentDiameter = 0;
					if (ActiveSliceSettings.Instance.PrinterSelected)
					{
						activeFilamentDiameter = 3;
						if (ActiveSliceSettings.Instance.GetValue<double>(SettingsKey.filament_diameter) < 2)
						{
							activeFilamentDiameter = 1.75;
						}
					}

					MatterControlApplication.Instance.LaunchBrowser("http://www.matterhackers.com/mc/store/redirect?d={0}&clk=mcs&a={1}".FormatWith(activeFilamentDiameter, OemSettings.Instance.AffiliateCode));
				};
				otherItemsSection.AddChild(shopButton);
			}
		}

		private FlowLayoutWidget CreateSection(string headingText)
		{
			// Add heading
			this.AddChild(new TextWidget(headingText, textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				HAnchor = HAnchor.ParentLeft
			});

			// Add container
			var container = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.ParentLeftRight,
				VAnchor = VAnchor.FitToChildren,
				Margin = new BorderDouble(10, 10, 10, 8),
			};
			this.AddChild(container);

			return container;
		}
	}
}
