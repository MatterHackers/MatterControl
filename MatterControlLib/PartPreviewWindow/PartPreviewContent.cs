﻿/*
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
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PartPreviewWindow.PlusTab;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;
using Newtonsoft.Json;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class PartPreviewContent : FlowLayoutWidget
	{
		private EventHandler unregisterEvents;
		private ChromeTab printerTab = null;
		private ChromeTabs tabControl;

		public PartPreviewContent(ThemeConfig theme)
			: base(FlowDirection.TopToBottom)
		{
			this.AnchorAll();

			var extensionArea = new LeftClipFlowLayoutWidget()
			{
				BackgroundColor = theme.TabBarBackground,
				VAnchor = VAnchor.Stretch,
				Padding = new BorderDouble(left: 8)
			};

			tabControl = new ChromeTabs(extensionArea, theme)
			{
				VAnchor = VAnchor.Stretch,
				HAnchor = HAnchor.Stretch,
				BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor,
				BorderColor = theme.MinimalShade,
				Border = new BorderDouble(left: 1),
				NewTabPage = () =>
				{
					return new StartTabPage(this, theme);
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

			// Force the ActionArea to be as high as ButtonHeight
			tabControl.TabBar.ActionArea.MinimumSize = new Vector2(0, theme.ButtonHeight);
			tabControl.TabBar.BackgroundColor = theme.TabBarBackground;
			tabControl.TabBar.BorderColor = theme.ActiveTabColor;

			// Force common padding into top region
			tabControl.TabBar.Padding = theme.TabbarPadding.Clone(top: theme.TabbarPadding.Top * 2, bottom: 0);

			// add in a what's new button
			var seeWhatsNewButton = new LinkLabel("What's New...".Localize(), theme)
			{
				Name = "What's New Link",
				ToolTipText = "See what's new in this version of MatterControl".Localize(),
				VAnchor = VAnchor.Center,
				Margin = new BorderDouble(10, 0),
				TextColor = theme.Colors.PrimaryTextColor
			};
			seeWhatsNewButton.Click += (s, e) => UiThread.RunOnIdle(() =>
			{
				UserSettings.Instance.set(UserSettingsKey.LastReadWhatsNew, JsonConvert.SerializeObject(DateTime.Now));
				DialogWindow.Show(new HelpPage("What's New"));
			});

			tabControl.TabBar.ActionArea.AddChild(seeWhatsNewButton);

			// add in the update available button
			var updateAvailableButton = new LinkLabel("Update Available".Localize(), theme)
			{
				Visible = false,
			};

			// make the function inline so we don't have to create members for the buttons
			EventHandler<StringEventArgs> SetLinkButtonsVisibility = (s, e) =>
			{
				if (UserSettings.Instance.HasLookedAtWhatsNew())
				{
					// hide it
					seeWhatsNewButton.Visible = false;
				}

				if (UpdateControlData.Instance.UpdateStatus == UpdateControlData.UpdateStatusStates.UpdateAvailable)
				{
					updateAvailableButton.Visible = true;
					// if we are going to show the update link hide the whats new link no matter what
					seeWhatsNewButton.Visible = false;
				}
				else
				{
					updateAvailableButton.Visible = false;
				}
			};

			UserSettings.Instance.SettingChanged += SetLinkButtonsVisibility;
			this.Closed += (s, e) => UserSettings.Instance.SettingChanged -= SetLinkButtonsVisibility;

			RunningInterval showUpdateInterval = null;
			updateAvailableButton.VisibleChanged += (s, e) =>
			{
				if (!updateAvailableButton.Visible)
				{
					if(showUpdateInterval != null)
					{
						showUpdateInterval.Continue = false;
						showUpdateInterval = null;
					}
					return;
				}

				showUpdateInterval = UiThread.SetInterval(() =>
				{
					double displayTime = 1;
					double pulseTime = 1;
					double totalSeconds = 0;
					var textWidgets = updateAvailableButton.Descendants<TextWidget>().Where((w) => w.Visible == true).ToArray();
					Color startColor = theme.Colors.PrimaryTextColor;
					// Show a highlight on the button as the user did not click it
					Animation flashBackground = null;
					flashBackground = new Animation()
					{
						DrawTarget = updateAvailableButton,
						FramesPerSecond = 10,
						Update = (s1, updateEvent) =>
						{
							totalSeconds += updateEvent.SecondsPassed;
							if (totalSeconds < displayTime)
							{
								double blend = AttentionGetter.GetFadeInOutPulseRatio(totalSeconds, pulseTime);
								var color = new Color(startColor, (int)((1 - blend) * 255));
								foreach (var textWidget in textWidgets)
								{
									textWidget.TextColor = color;
								}
							}
							else
							{
								foreach (var textWidget in textWidgets)
								{
									textWidget.TextColor = startColor;
								}
								flashBackground.Stop();
							}
						}
					};
					flashBackground.Start();
				}, 120);
			};

			updateAvailableButton.Name = "Update Available Link";
			SetLinkButtonsVisibility(this, null);
			updateAvailableButton.ToolTipText = "There is a new update available for download".Localize();
			updateAvailableButton.VAnchor = VAnchor.Center;
			updateAvailableButton.Margin = new BorderDouble(10, 0);
			updateAvailableButton.Click += (s, e) => UiThread.RunOnIdle(() =>
			{
				UiThread.RunOnIdle(() =>
				{
					UpdateControlData.Instance.CheckForUpdate();
					DialogWindow.Show<CheckForUpdatesPage>();
				});
			});

			tabControl.TabBar.ActionArea.AddChild(updateAvailableButton);

			UpdateControlData.Instance.UpdateStatusChanged.RegisterEvent((s, e) =>
			{
				SetLinkButtonsVisibility(s, new StringEventArgs("Unknown"));
			}, ref unregisterEvents);

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

			ActiveSliceSettings.ActivePrinterChanged.RegisterEvent((s, e) =>
			{
				var activePrinter = ApplicationController.Instance.ActivePrinter;

				// If ActivePrinter has been nulled and a printer tab is open, close it
				var tab1 = tabControl.AllTabs.Skip(1).FirstOrDefault();
				if ((activePrinter == null || !activePrinter.Settings.PrinterSelected)
					&& tab1?.TabContent is PrinterTabPage)
				{
					tabControl.RemoveTab(tab1);
				}
				else
				{
					this.CreatePrinterTab(activePrinter, theme);
				}
			}, ref unregisterEvents);

			ApplicationController.Instance.NotifyPrintersTabRightElement(extensionArea);

			// Show fixed start page
			tabControl.AddTab(
				new ChromeTab("Start", "Start".Localize(),  tabControl, tabControl.NewTabPage(), theme, hasClose: false)
				{
					MinimumSize = new Vector2(0, theme.TabButtonHeight),
					Name = "Start Tab",
					Padding = new BorderDouble(15, 0)
				});

			// Add a tab for the current printer
			if (ActiveSliceSettings.Instance.PrinterSelected)
			{
				this.CreatePrinterTab(ApplicationController.Instance.ActivePrinter, theme);
			}

			// Restore active tabs
			foreach (var bed in ApplicationController.Instance.Workspaces)
			{
				this.CreatePartTab("New Part", bed, theme);
			}
		}

		public ChromeTabs TabControl => tabControl;

		private ChromeTab CreatePrinterTab(PrinterConfig printer, ThemeConfig theme)
		{
			// Printer page is in fixed position
			var tab1 = tabControl.AllTabs.Skip(1).FirstOrDefault();

			var printerTabPage = tab1?.TabContent as PrinterTabPage;
			if (printerTabPage == null
				|| printerTabPage.printer != printer)
			{
				// TODO - call save before remove
				// printerTabPage.sceneContext.SaveChanges();

				if (printerTabPage != null)
				{
					tabControl.RemoveTab(tab1);
				}

				printerTab = new ChromeTab(
					printer.Settings.GetValue(SettingsKey.printer_name),
					printer.Settings.GetValue(SettingsKey.printer_name),
					tabControl,
					new PrinterTabPage(printer, theme, "unused_tab_title"),
					theme,
					tabImageUrl: ApplicationController.Instance.GetFavIconUrl(oemName: printer.Settings.GetValue(SettingsKey.make)),
					hasClose: false)
				{
					Name = "3D View Tab",
					MinimumSize = new Vector2(120, theme.TabButtonHeight)
				};

				ActiveSliceSettings.SettingChanged.RegisterEvent((s, e) =>
				{
					string settingsName = (e as StringEventArgs)?.Data;
					if (settingsName != null && settingsName == SettingsKey.printer_name)
					{
						printerTab.Title = printer.Settings.GetValue(SettingsKey.printer_name);
					}
				}, ref unregisterEvents);


				// Add printer into fixed position
				tabControl.AddTab(printerTab, 1);

				return printerTab;
			}
			else if (printerTab != null)
			{
				tabControl.ActiveTab = tab1;
				return tab1 as ChromeTab;
			}

			return null;
		}

		public ChromeTab CreatePartTab(string tabTitle, BedConfig sceneContext, ThemeConfig theme)
		{
			var partTab = new ChromeTab(
				tabTitle,
				tabTitle,
				tabControl,
				new PartTabPage(null, sceneContext, theme, ""),
				theme,
				AggContext.StaticData.LoadIcon("cube.png", 16, 16, theme.InvertIcons))
			{
				Name = "newPart" + tabControl.AllTabs.Count(),
				MinimumSize = new Vector2(120, theme.TabButtonHeight)
			};

			tabControl.AddTab(partTab);

			return partTab;
		}

		public override void OnClosed(EventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}
	}
}