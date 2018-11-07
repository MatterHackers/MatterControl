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
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PartPreviewWindow.PlusTab;
using MatterHackers.MatterControl.PrintLibrary;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;
using Newtonsoft.Json;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class MainViewWidget : FlowLayoutWidget
	{
		private EventHandler unregisterEvents;
		private ChromeTab printerTab = null;
		private ChromeTabs tabControl;

		private int partCount = 0;
		private ThemeConfig theme;
		private Toolbar statusBar;
		private FlowLayoutWidget tasksContainer;
		private GuiWidget stretchStatusPanel;

		public MainViewWidget(ThemeConfig theme)
			: base(FlowDirection.TopToBottom)
		{
			this.AnchorAll();
			this.theme = theme;
			this.Name = "PartPreviewContent";
			this.BackgroundColor = theme.BackgroundColor;

			// Push TouchScreenMode into GuiWidget
			GuiWidget.TouchScreenMode = UserSettings.Instance.IsTouchScreen;

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
				BackgroundColor = theme.BackgroundColor,
				BorderColor = theme.MinimalShade,
				Border = new BorderDouble(left: 1),
			};

			tabControl.PlusClicked += (s, e) =>
			{
				UiThread.RunOnIdle(() =>
				{
					this.CreatePartTab().ConfigureAwait(false);
				});
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

				ApplicationController.Instance.MainTabKey = tabControl.SelectedTabKey;
			};

			// Force the ActionArea to be as high as ButtonHeight
			tabControl.TabBar.ActionArea.MinimumSize = new Vector2(0, theme.ButtonHeight);
			tabControl.TabBar.BackgroundColor = theme.TabBarBackground;
			tabControl.TabBar.BorderColor = theme.BackgroundColor;

			// Force common padding into top region
			tabControl.TabBar.Padding = theme.TabbarPadding.Clone(top: theme.TabbarPadding.Top * 2, bottom: 0);

			// add in a what's new button
			var seeWhatsNewButton = new LinkLabel("What's New...".Localize(), theme)
			{
				Name = "What's New Link",
				ToolTipText = "See what's new in this version of MatterControl".Localize(),
				VAnchor = VAnchor.Center,
				Margin = new BorderDouble(10, 0),
				TextColor = theme.TextColor
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
				Name = "Update Available Link",
				ToolTipText = "There is a new update available for download".Localize(),
				VAnchor = VAnchor.Center,
				Margin = new BorderDouble(10, 0)
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
					if (showUpdateInterval != null)
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
					Color startColor = theme.TextColor;
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

			SetLinkButtonsVisibility(this, null);
			updateAvailableButton.Click += (s, e) => UiThread.RunOnIdle(() =>
			{
				UpdateControlData.Instance.CheckForUpdate();
				DialogWindow.Show<CheckForUpdatesPage>();
			});

			tabControl.TabBar.ActionArea.AddChild(updateAvailableButton);

			this.AddChild(tabControl);

			ApplicationController.Instance.NotifyPrintersTabRightElement(extensionArea);

			var printer = ApplicationController.Instance.ActivePrinter;

			// Store tab
			tabControl.AddTab(
				new ChromeTab("Store", "Store".Localize(), tabControl, new StoreTabPage(theme), theme, hasClose: false)
				{
					MinimumSize = new Vector2(0, theme.TabButtonHeight),
					Name = "Store Tab",
					Padding = new BorderDouble(15, 0),
				});

			// Library tab
			var libraryWidget = new LibraryWidget(this, theme)
			{
				BackgroundColor = theme.BackgroundColor
			};

			tabControl.AddTab(
				new ChromeTab("Library", "Library".Localize(), tabControl, libraryWidget, theme, hasClose: false)
				{
					MinimumSize = new Vector2(0, theme.TabButtonHeight),
					Name = "Library Tab",
					Padding = new BorderDouble(15, 0),
				});

			// Hardware tab
			tabControl.AddTab(
				new ChromeTab(
					"Hardware",
					"Hardware".Localize(),
					tabControl,
					new HardwareTabPage(theme)
					{
						BackgroundColor = theme.BackgroundColor
					},
					theme,
					hasClose: false)
				{
					MinimumSize = new Vector2(0, theme.TabButtonHeight),
					Name = "Hardware Tab",
					Padding = new BorderDouble(15, 0),
				});

			// Printer tab
			if (printer.Settings.PrinterSelected)
			{
				this.CreatePrinterTab(printer, theme);
			}
			else
			{
				if (ApplicationController.Instance.Workspaces.Count == 0)
				{
					this.CreatePartTab().ConfigureAwait(false);
				}
			}

			string tabKey = ApplicationController.Instance.MainTabKey;

			if (string.IsNullOrEmpty(tabKey))
			{
				if (printer.Settings.PrinterSelected)
				{
					tabKey = printer.Settings.GetValue(SettingsKey.printer_name);
				}
				else
				{
					tabKey = "Hardware";
				}
			}

			var brandMenu = new BrandMenuButton(theme)
			{
				HAnchor = HAnchor.Fit,
				VAnchor = VAnchor.Fit,
				BackgroundColor = theme.TabBarBackground,
				Padding = theme.TabbarPadding.Clone(right: theme.DefaultContainerPadding)
			};

			tabControl.TabBar.ActionArea.AddChild(brandMenu, 0);

			// Restore active tabs
			foreach (var workspace in ApplicationController.Instance.Workspaces)
			{
				this.CreatePartTab(workspace);
			}

			tabControl.SelectedTabKey = tabKey;

			statusBar = new Toolbar(theme)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Absolute,
				Padding = 1,
				Height = 22,
				BackgroundColor = theme.BackgroundColor,
				Border = new BorderDouble(top: 1),
				BorderColor = theme.BorderColor20,
			};
			this.AddChild(statusBar);

			statusBar.ActionArea.VAnchor = VAnchor.Stretch;

			tasksContainer = new FlowLayoutWidget(FlowDirection.LeftToRight)
			{
				HAnchor = HAnchor.Fit,
				VAnchor = VAnchor.Stretch,
				BackgroundColor = theme.MinimalShade,
				Name = "runningTasksPanel"
			};
			statusBar.AddChild(tasksContainer);

			var tasks = ApplicationController.Instance.Tasks;

			tasks.TasksChanged += (s, e) =>
			{
				RenderRunningTasks(theme, tasks);
			};

			stretchStatusPanel = new GuiWidget()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
				Padding = new BorderDouble(right: 3),
				Margin = new BorderDouble(right: 2, top: 1, bottom: 1),
				Border = new BorderDouble(1),
				BackgroundColor = theme.MinimalShade.WithAlpha(10),
				BorderColor = theme.SlightShade,
				Width = 200
			};
			statusBar.AddChild(stretchStatusPanel);

			var panelBackgroundColor = theme.MinimalShade.WithAlpha(10);

			statusBar.AddChild(
				this.CreateThemeStatusPanel(theme, panelBackgroundColor));

			statusBar.AddChild(
				this.CreateNetworkStatusPanel(theme));

			this.RenderRunningTasks(theme, tasks);

			UpdateControlData.Instance.UpdateStatusChanged.RegisterEvent((s, e) =>
			{
				SetLinkButtonsVisibility(s, new StringEventArgs("Unknown"));
			}, ref unregisterEvents);

			PrinterSettings.SettingChanged.RegisterEvent((s, e) =>
			{
				var activePrinter = ApplicationController.Instance.ActivePrinter;

				if (e is StringEventArgs stringEvent
					&& stringEvent.Data == SettingsKey.printer_name
					&& printerTab != null)
				{
					printerTab.Text = activePrinter.Settings.GetValue(SettingsKey.printer_name);
				}
			}, ref unregisterEvents);

			ApplicationController.Instance.ActivePrinterChanged.RegisterEvent((s, e) =>
			{
				var activePrinter = ApplicationController.Instance.ActivePrinter;

				// Close existing printer tabs
				if (tabControl.AllTabs.FirstOrDefault(t => t.TabContent is PrinterTabPage) is ITab tab
					&& tab.TabContent is PrinterTabPage printerPage
					&& (activePrinter == null || printerPage.printer != activePrinter))
				{
					tabControl.RemoveTab(tab);
				}

				if (activePrinter.Settings.PrinterSelected)
				{
					// Create and switch to new printer tab
					tabControl.ActiveTab = this.CreatePrinterTab(activePrinter, theme);
				}

				tabControl.RefreshTabPointers();
			}, ref unregisterEvents);

			ApplicationController.Instance.MainView = this;
		}

		private GuiWidget CreateNetworkStatusPanel(ThemeConfig theme)
		{
			var networkStatus = new GuiWidget()
			{
				HAnchor = HAnchor.Absolute,
				VAnchor = VAnchor.Stretch,
				Padding = new BorderDouble(right: 3),
				Margin = new BorderDouble(right: 2, top: 1, bottom: 1),
				Border = new BorderDouble(1),
				BackgroundColor = theme.MinimalShade.WithAlpha(10),
				BorderColor = theme.SlightShade,
				Width = 120
			};
			if (ApplicationController.ServicesStatusType != null)
			{
				var instance = Activator.CreateInstance(ApplicationController.ServicesStatusType);
				if (instance is GuiWidget guiWidget)
				{
					guiWidget.HAnchor = HAnchor.Stretch;
					guiWidget.VAnchor = VAnchor.Stretch;
					networkStatus.AddChild(guiWidget);
				}
			}

			return networkStatus;
		}

		private GuiWidget CreateThemeStatusPanel(ThemeConfig theme, Color panelBackgroundColor)
		{
			var themePanel = new GuiWidget()
			{
				HAnchor = HAnchor.Absolute,
				VAnchor = VAnchor.Stretch,
				Padding = new BorderDouble(right: 3),
				Margin = new BorderDouble(right: 2, top: 1, bottom: 1),
				Border = new BorderDouble(1),
				BackgroundColor = panelBackgroundColor,
				BorderColor = theme.SlightShade,
				Cursor = Cursors.Hand,
				ToolTipText = "Theme".Localize(),
				Width = 40
			};

			themePanel.AddChild(
				new ImageWidget(AggContext.StaticData.LoadIcon("theme.png", 16, 16, theme.InvertIcons), false)
				{
					HAnchor = HAnchor.Left | HAnchor.Absolute,
					VAnchor = VAnchor.Center | VAnchor.Absolute,
					Selectable = false
				});

			themePanel.AddChild(
				new ColorButton(theme.PrimaryAccentColor)
				{
					HAnchor = HAnchor.Right | HAnchor.Absolute,
					VAnchor = VAnchor.Center | VAnchor.Absolute,
					Width = 12,
					Height = 12,
					Selectable = false
				});

			themePanel.Click += (s, e) =>
			{
				themePanel.BackgroundColor = theme.DropList.Open.BackgroundColor;

				var menuTheme = AppContext.MenuTheme;
				var widget = new GuiWidget()
				{
					HAnchor = HAnchor.Absolute,
					VAnchor = VAnchor.Fit,
					Width = 600,
					Border = 1,
					BorderColor = theme.DropList.Open.BackgroundColor,
					// Padding = theme.DefaultContainerPadding,
					BackgroundColor = menuTheme.BackgroundColor
				};

				widget.Closed += (s2, e2) =>
				{
					themePanel.BackgroundColor = panelBackgroundColor;
				};

				var section = ApplicationSettingsPage.CreateThemePanel(menuTheme);
				widget.AddChild(section);

				var systemWindow = this.Parents<SystemWindow>().FirstOrDefault();
				systemWindow.ShowPopup(
					new MatePoint(themePanel)
					{
						Mate = new MateOptions(MateEdge.Right, MateEdge.Top),
						AltMate = new MateOptions(MateEdge.Right, MateEdge.Top)
					},
					new MatePoint(widget)
					{
						Mate = new MateOptions(MateEdge.Right, MateEdge.Bottom),
						AltMate = new MateOptions(MateEdge.Right, MateEdge.Bottom)
					});
			};
			return themePanel;
		}

		public ChromeTabs TabControl => tabControl;

		private ChromeTab CreatePrinterTab(PrinterConfig printer, ThemeConfig theme)
		{
			// Printer page is in fixed position
			var tab1 = tabControl.AllTabs.FirstOrDefault();

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
					tabImageUrl: ApplicationController.Instance.GetFavIconUrl(oemName: printer.Settings.GetValue(SettingsKey.make)))
				{
					Name = "3D View Tab",
					MinimumSize = new Vector2(120, theme.TabButtonHeight)
				};

				printerTab.CloseClicked += (s, e) =>
				{
					ApplicationController.Instance.ClearActivePrinter().ConfigureAwait(false);
				};

				PrinterSettings.SettingChanged.RegisterEvent((s, e) =>
				{
					string settingsName = (e as StringEventArgs)?.Data;
					if (settingsName != null && settingsName == SettingsKey.printer_name)
					{
						printerTab.Title = printer.Settings.GetValue(SettingsKey.printer_name);
					}
				}, ref unregisterEvents);

				// Add printer into fixed position
				if (tabControl.AllTabs.Any())
				{
					tabControl.AddTab(printerTab, 3);
				}
				else
				{
					tabControl.AddTab(printerTab);
				}

				return printerTab;
			}
			else if (printerTab != null)
			{
				tabControl.ActiveTab = tab1;
				return tab1 as ChromeTab;
			}

			return null;
		}

		public async Task<ChromeTab> CreatePartTab()
		{
			var history = ApplicationController.Instance.Library.PlatingHistory;

			var workspace = new PartWorkspace()
			{
				Name = "New Design".Localize() + (partCount == 0 ? "" : $" ({partCount})"),
				SceneContext = new BedConfig(history)
			};

			partCount++;

			await workspace.SceneContext.LoadContent(
				new EditContext()
				{
					ContentStore = ApplicationController.Instance.Library.PlatingHistory,
					SourceItem = history.NewPlatingItem()
				});

			ApplicationController.Instance.Workspaces.Add(workspace);

			var newTab = CreatePartTab(workspace);
			tabControl.ActiveTab = newTab;

			return newTab;
		}

		public ChromeTab CreatePartTab(PartWorkspace workspace)
		{
			var partTab = new ChromeTab(
				workspace.Name,
				workspace.Name,
				tabControl,
				new PartTabPage(null, workspace.SceneContext, theme, ""),
				theme,
				AggContext.StaticData.LoadIcon("cube.png", 16, 16, theme.InvertIcons))
			{
				Name = "newPart" + tabControl.AllTabs.Count(),
				MinimumSize = new Vector2(120, theme.TabButtonHeight)
			};

			tabControl.AddTab(partTab);

			partTab.CloseClicked += (s, e) =>
			{
				ApplicationController.Instance.Workspaces.Remove(workspace);
			};

			return partTab;
		}

		public override void OnClosed(EventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}

		private void RenderRunningTasks(ThemeConfig theme, RunningTasksConfig tasks)
		{
			var rows = tasksContainer.Children.OfType<RunningTaskStatusPanel>().ToList();
			var displayedTasks = new HashSet<RunningTaskDetails>(rows.Select(taskRow => taskRow.taskDetails));
			var runningTasks = tasks.RunningTasks;

			// Remove expired items
			foreach (var row in rows)
			{
				if (!runningTasks.Contains(row.taskDetails))
				{
					row.Close();
				}
			}

			var progressBackgroundColor = new Color(theme.AccentMimimalOverlay, 35);

			// Add new items
			foreach (var taskItem in tasks.RunningTasks.Where(t => !displayedTasks.Contains(t)))
			{
				var runningTaskPanel = new RunningTaskStatusPanel("", taskItem, theme)
				{
					HAnchor = HAnchor.Absolute,
					VAnchor = VAnchor.Stretch,
					Margin = new BorderDouble(right: 2, top: 1, bottom: 1),
					Border = new BorderDouble(1),
					BorderColor = theme.SlightShade,
					ProgressBackgroundColor = progressBackgroundColor,
					Width = 200
				};

				tasksContainer.AddChild(runningTaskPanel);
			}

			tasksContainer.Invalidate();
		}
	}
}