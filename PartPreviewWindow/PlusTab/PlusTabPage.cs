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

using System.IO;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterControls.PrinterConnections;
using MatterHackers.MatterControl.SettingsManagement;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.PartPreviewWindow.PlusTab
{
	public class PlusTabPage : FlowLayoutWidget
	{
		public PlusTabPage(PartPreviewContent partPreviewContent, SimpleTabs simpleTabs, ThemeConfig theme)
		{
			var leftContent = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				VAnchor = VAnchor.Stretch,
				Padding = 15
			};
			this.AddChild(leftContent);

			if (OemSettings.Instance.ShowShopButton)
			{
				this.AddChild(new ExplorePanel(theme));
			}

			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Stretch;

			BorderDouble buttonSpacing = 3;

			// put in the add new design stuff
			var createItemsSection = CreateSection(leftContent, "Create New".Localize() + ":");

			var createPart = theme.ButtonFactory.Generate("Create Part".Localize());
			createPart.Margin = buttonSpacing;
			createPart.HAnchor = HAnchor.Left;
			createItemsSection.AddChild(createPart);
			createPart.Click += (s, e) =>
			{
				UiThread.RunOnIdle(() =>
				{
					BedConfig bed;
					simpleTabs.RemoveTab(simpleTabs.ActiveTab);
					partPreviewContent.CreatePartTab(
						"New Part", 
						bed = new BedConfig(), 
						theme);

					bed.LoadContent(
						new EditContext()
						{
							ContentStore = ApplicationController.Instance.Library.PlatingHistory,
							SourceItem = BedConfig.NewPlatingItem()
						}).ConfigureAwait(false);
				});
			};

			var createPrinter = theme.ButtonFactory.Generate("Create Printer".Localize());
			createPrinter.Name = "Create Printer";
			createPrinter.Margin = buttonSpacing;
			createPrinter.HAnchor = HAnchor.Left;
			createPrinter.Click += (s, e) =>
			{
				UiThread.RunOnIdle(() =>
				{
					simpleTabs.RemoveTab(simpleTabs.ActiveTab);

					if (ApplicationController.Instance.ActivePrinter.Connection.PrinterIsPrinting
					|| ApplicationController.Instance.ActivePrinter.Connection.PrinterIsPaused)
					{
						StyledMessageBox.ShowMessageBox("Please wait until the print has finished and try again.".Localize(), "Can't add printers while printing".Localize());
					}
					else
					{
						DialogWindow.Show(PrinterSetup.GetBestStartPage(PrinterSetup.StartPageOptions.ShowMakeModel));
					}
				});
			};
			createItemsSection.AddChild(createPrinter);

			var importButton = theme.ButtonFactory.Generate("Import Printer".Localize());
			importButton.Margin = buttonSpacing;
			importButton.HAnchor = HAnchor.Left;
			importButton.Click += (s, e) =>
			{
				UiThread.RunOnIdle(() =>
				{
					AggContext.FileDialogs.OpenFileDialog(
						new OpenFileDialogParams(
							"settings files|*.ini;*.printer;*.slice"),
							(result) =>
							{
								if (!string.IsNullOrEmpty(result.FileName)
									&& File.Exists(result.FileName))
								{
									simpleTabs.RemoveTab(simpleTabs.ActiveTab);
									ImportSettingsPage.ImportFromExisting(result.FileName);
								}
							});
				});
			};
			createItemsSection.AddChild(importButton);

			var existingPrinterSection = CreateSection(leftContent, "Open Existing".Localize() + ":");

			var printerSelector = new PrinterSelector()
			{
				Margin = new BorderDouble(left: 15)
			};
			existingPrinterSection.AddChild(printerSelector);

			var otherItemsSection = CreateSection(leftContent, "Other".Localize() + ":");

			var redeemDesignCode = theme.ButtonFactory.Generate("Redeem Design Code".Localize());
			redeemDesignCode.Name = "Redeem Design Code Button";
			redeemDesignCode.Margin = buttonSpacing;
			redeemDesignCode.HAnchor = HAnchor.Left;
			redeemDesignCode.Click += (s, e) =>
			{
				UiThread.RunOnIdle(() =>
				{
					simpleTabs.RemoveTab(simpleTabs.ActiveTab);
					// Implementation already does RunOnIdle
					ApplicationController.Instance.RedeemDesignCode?.Invoke();
				});
			};
			otherItemsSection.AddChild(redeemDesignCode);

			var redeemShareCode = theme.ButtonFactory.Generate("Enter Share Code".Localize());
			redeemShareCode.Name = "Enter Share Code Button";
			redeemShareCode.Margin = buttonSpacing;
			redeemShareCode.HAnchor = HAnchor.Left;
			redeemShareCode.Click += (s, e) =>
			{
				UiThread.RunOnIdle(() =>
				{
					simpleTabs.RemoveTab(simpleTabs.ActiveTab);

					// Implementation already does RunOnIdle
					ApplicationController.Instance.EnterShareCode?.Invoke();
				});
			};
			otherItemsSection.AddChild(redeemShareCode);
		}

		private FlowLayoutWidget CreateSection(GuiWidget parent, string headingText)
		{
			// Add heading
			parent.AddChild(new TextWidget(headingText, textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				HAnchor = HAnchor.Left
			});

			// Add container
			var container = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				Margin = new BorderDouble(10, 10, 10, 8),
			};
			parent.AddChild(container);

			return container;
		}
	}
}
