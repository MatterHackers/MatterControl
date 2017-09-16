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
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class PartPreviewContent : FlowLayoutWidget
	{
		private EventHandler unregisterEvents;
		private MainTab printerTab = null;

		public PartPreviewContent()
		{
			var printerConfig = ApplicationController.Instance.Printer;
			var theme = ApplicationController.Instance.Theme;

			this.AnchorAll();

			var activeSettings = ActiveSliceSettings.Instance;

			var tabControl = ApplicationController.Instance.Theme.CreateTabControl(2);

			var separator = tabControl.Children<HorizontalLine>().FirstOrDefault();
			separator.BackgroundColor = ApplicationController.Instance.Theme.PrimaryTabFillColor;

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
			if (ActiveSliceSettings.Instance.PrinterSelected)
			{
				printerTab = CreatePrinterTab(printerConfig, theme, tabTitle);
				tabControl.AddTab(printerTab);
			}
			else
			{
				PlusTabPage.CreatePartTab(tabControl, printerConfig.Bed, theme, 0);
			}

			// TODO: add in the printers and designs that are currently open (or were open last run).
			var plusTabSelect = new TextTab(
				new TabPage(new PlusTabPage(tabControl, printerConfig, theme), "+"),
				"Create New",
				tabControl.TextPointSize,
				selectedTabColor,
				new RGBA_Bytes(),
				ActiveTheme.Instance.TabLabelUnselected,
				new RGBA_Bytes(),
				fixedSize: 16,
				useUnderlineStyling: true);

			plusTabSelect.VAnchor = VAnchor.Bottom;

			plusTabSelect.MinimumSize = new VectorMath.Vector2(16, 16);
			plusTabSelect.Margin = new BorderDouble(left: 10, top: 6);
			plusTabSelect.Padding = 0;
			plusTabSelect.ToolTipText = "Create New".Localize();
			tabControl.AddTab(plusTabSelect);

			tabControl.TabBar.AddChild(new HorizontalSpacer());

			// add in the update available button
			LinkButtonFactory linkButtonFactory = new LinkButtonFactory()
			{
				textColor = ActiveTheme.Instance.PrimaryAccentColor,
				fontSize = 12,
			};

			Button updateAvailableButton = linkButtonFactory.Generate("Update Available");
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
					WizardWindow.Show<CheckForUpdatesPage>();
				});
			});
			tabControl.TabBar.AddChild(updateAvailableButton);

			UpdateControlData.Instance.UpdateStatusChanged.RegisterEvent((s, e) =>
			{
				updateAvailableButton.Visible = UpdateControlData.Instance.UpdateStatus == UpdateControlData.UpdateStatusStates.UpdateAvailable;
			}, ref unregisterEvents);

			// this causes the update button to be centered
			tabControl.TabBar.AddChild(new HorizontalSpacer());

			// put in the login logout control
			var rightPanelArea = new FlowLayoutWidget()
			{
				VAnchor = VAnchor.Stretch
			};

			var extensionArea = new FlowLayoutWidget();

			rightPanelArea.AddChild(extensionArea);

			//rightPanelArea.AddChild(
			//	new ImageWidget(
			//		AggContext.StaticData.LoadImage(Path.Combine("Images", "minimize.png")))
			//	{
			//		VAnchor = VAnchor.Top,
			//		DebugShowBounds  = true
			//	});

			tabControl.TabBar.AddChild(rightPanelArea);

			this.AddChild(tabControl);

			ActiveSliceSettings.SettingChanged.RegisterEvent((s, e) =>
			{
				if (e is StringEventArgs stringEvent
					&& stringEvent.Data == SettingsKey.printer_name
					&& printerTab != null)
				{
					printerTab.TabPage.Text = ActiveSliceSettings.Instance.GetValue(SettingsKey.printer_name);
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

		private static MainTab CreatePrinterTab(PrinterConfig printerConfig, ThemeConfig theme, string tabTitle)
		{
			var printerTab = new MainTab(
				tabTitle,
				"3D View Tab",
				new PrinterTabPage(printerConfig, theme, tabTitle.ToUpper()),
				"https://www.google.com/s2/favicons?domain=www.printrbot.com" // "https://www.google.com/s2/favicons?domain=www.lulzbot.com"
				);
			printerTab.ToolTipText = "Preview 3D Design".Localize();

			theme.SetPrinterTabStyles(printerTab);
			return printerTab;
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}
	}
}