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
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.AboutPage;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PartPreviewWindow.PlusTab;
using MatterHackers.MatterControl.SettingsManagement;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class PartPreviewContent : FlowLayoutWidget
	{
		private EventHandler unregisterEvents;
		private MainTab printerTab = null;
		private ChromeTabs tabControl;

		public PartPreviewContent()
			: base(FlowDirection.TopToBottom)
		{
			var printer = ApplicationController.Instance.ActivePrinter;
			var theme = ApplicationController.Instance.Theme;

			this.AnchorAll();

			var extensionArea = new FlowLayoutWidget();

			tabControl = new ChromeTabs(extensionArea, theme)
			{
				VAnchor = VAnchor.Stretch,
				HAnchor = HAnchor.Stretch,
				BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor,
				NewTabPage = () =>
				{
					return new PlusTabPage(this, tabControl, theme);
				}
			};

			tabControl.ActiveTabChanged += (s, e) =>
			{
				if (this.tabControl.ActiveTab?.TabContent is PartTabPage tabPage)
				{
					var dragDropData = ApplicationController.Instance.DragDropData;

					// Set reference on tab change
					dragDropData.View3DWidget = tabPage.view3DWidget;
					dragDropData.SceneContext = tabPage.sceneContext;
				}
			};

			tabControl.TabBar.BorderColor = theme.ActiveTabColor;
			tabControl.TabBar.Padding = new BorderDouble(top: 4);
			tabControl.TabBar.Border = new BorderDouble(bottom: 2);

			Color selectedTabColor = ActiveTheme.Instance.TabLabelSelected;

			// Add a tab for the current printer
			if (ActiveSliceSettings.Instance.PrinterSelected)
			{
				string tabTitle = ActiveSliceSettings.Instance.GetValue(SettingsKey.printer_name);
				printerTab = CreatePrinterTab(printer, theme, tabTitle);

				tabControl.AddTab(printerTab);
			}
			else
			{
				this.CreatePartTab(
					"New Part",
					new BedConfig(
						new EditContext()
						{
							ContentStore = ApplicationController.Instance.Library.PlatingHistory,
							SourceItem = BedConfig.NewPlatingItem()
						}),
					theme);
			}

			// add in the update available button
			Button updateAvailableButton = theme.LinkButtonFactory.Generate("Update Available");
			updateAvailableButton.Name = "Update Available Link";
			updateAvailableButton.Visible = UpdateControlData.Instance.UpdateStatus == UpdateControlData.UpdateStatusStates.UpdateAvailable;
			updateAvailableButton.ToolTipText = "There is a new update available for download".Localize();
			updateAvailableButton.VAnchor = VAnchor.Center;
			updateAvailableButton.Margin = new Agg.BorderDouble(10, 0);
			updateAvailableButton.Click += (s, e) => UiThread.RunOnIdle(() =>
			{
				UiThread.RunOnIdle(() =>
				{
					UpdateControlData.Instance.CheckForUpdateUserRequested();
					DialogWindow.Show<CheckForUpdatesPage>();
				});
			});

			tabControl.TabBar.ActionBar.AddChild(updateAvailableButton);

			UpdateControlData.Instance.UpdateStatusChanged.RegisterEvent((s, e) =>
			{
				updateAvailableButton.Visible = UpdateControlData.Instance.UpdateStatus == UpdateControlData.UpdateStatusStates.UpdateAvailable;
			}, ref unregisterEvents);

			// this causes the update button to be centered
			//tabControl.TabBar.AddChild(new HorizontalSpacer());

			//rightPanelArea.AddChild(
			//	new ImageWidget(
			//		AggContext.StaticData.LoadImage(Path.Combine("Images", "minimize.png")))
			//	{
			//		VAnchor = VAnchor.Top,
			//		DebugShowBounds  = true
			//	});

			//this.AddChild(tabControl);

			this.AddChild(tabControl);

			ActiveSliceSettings.SettingChanged.RegisterEvent((s, e) =>
			{
				if (e is StringEventArgs stringEvent
					&& stringEvent.Data == SettingsKey.printer_name
					&& printerTab != null)
				{
					printerTab.Text = ActiveSliceSettings.Instance.GetValue(SettingsKey.printer_name);
				}

			}, ref unregisterEvents);

			ApplicationController.Instance.NotifyPrintersTabRightElement(extensionArea);

			// When the application is first started, plugins are loaded after the MainView control has been initialized,
			// and as such they not around when this constructor executes. In that case, we run the AddRightElement 
			// delegate after the plugins have been initialized via the PluginsLoaded event
			ApplicationController.Instance.PluginsLoaded.RegisterEvent((s, e) =>
			{
				ApplicationController.Instance.NotifyPrintersTabRightElement(extensionArea);
			}, ref unregisterEvents);
		}

		private MainTab CreatePrinterTab(PrinterConfig printer, ThemeConfig theme, string tabTitle)
		{
			string oemName = printer.Settings.GetValue(SettingsKey.make);

			OemSettings.Instance.OemUrls.TryGetValue(oemName, out string oemUrl);

			return new MainTab(
				tabTitle,
				tabControl,
				new PrinterTabPage(printer, theme, tabTitle.ToUpper()),
				theme,
				"https://www.google.com/s2/favicons?domain=" + oemUrl ?? "www.matterhackers.com")
			{
				Name = "3D View Tab",
				MinimumSize = new Vector2(120, theme.shortButtonHeight)
			};
		}

		internal MainTab CreatePartTab(string tabTitle, BedConfig sceneContext, ThemeConfig theme)
		{
			var partTab = new MainTab(
				tabTitle,
				tabControl,
				new PartTabPage(null, sceneContext, theme, "xxxxx"),
				theme,
				"https://i.imgur.com/nkeYgfU.png")
			{
				Name = "newPart" + tabControl.AllTabs.Count(),
				MinimumSize = new Vector2(120, theme.shortButtonHeight)
			};

			tabControl.AddTab(partTab);

			return partTab;
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}
	}
}