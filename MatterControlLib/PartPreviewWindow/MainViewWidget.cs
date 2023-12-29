﻿/*
Copyright (c) 2023, Lars Brubaker, John Lewin
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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MatterControlLib;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.ImageProcessing;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.Library;
using MatterHackers.MatterControl.Library.Widgets;
using MatterHackers.MatterControl.PartPreviewWindow.PlusTab;
using MatterHackers.MatterControl.SettingsManagement;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class MainViewWidget : FlowLayoutWidget
	{
		private EventHandler unregisterEvents;
		private ChromeTabs tabControl;

		private int partCount = 0;
		private ThemeConfig theme;
		private Toolbar statusBar;
		private GuiWidget tasksContainer;
		private GuiWidget statusMessage;
		private GuiWidget stretchStatusPanel;
		private LinkLabel updateAvailableButton;

		public MainViewWidget(ThemeConfig theme)
			: base(FlowDirection.TopToBottom)
		{
			this.AnchorAll();
			this.theme = theme;
			this.Name = "PartPreviewContent";
			this.BackgroundColor = theme.BackgroundColor;

			// Push TouchScreenMode into GuiWidget
			GuiWidget.TouchScreenMode = ApplicationSettings.Instance.IsTouchScreen;

			AddStandardUi(theme);
			ApplicationController.Instance.WorkspacesChanged += Workspaces_Changed;
			ApplicationController.Instance.Tasks.TasksChanged += Tasks_TasksChanged;
			ApplicationController.Instance.UiHintChanged += Tasks_TasksChanged;
			tabControl.ActiveTabChanged += TabControl_ActiveTabChanged;

            ApplicationController.Instance.ShellFileOpened += this.Instance_OpenNewFile;

			ApplicationController.Instance.MainView = this;
		}

		private async void AddStandardUi(ThemeConfig theme)
		{
			var extensionArea = new LeftClipFlowLayoutWidget()
			{
				BackgroundColor = theme.TabBarBackground,
				VAnchor = VAnchor.Stretch,
				Padding = new BorderDouble(left: 8)
			};

			SearchPanel searchPanel = null;

			bool searchPanelOpenOnMouseDown = false;

			var searchButton = theme.CreateSearchButton();
			searchButton.Name = "App Search Button";
			searchButton.MouseDown += (s, e) =>
			{
				searchPanelOpenOnMouseDown = searchPanel != null;
			};

			searchButton.Click += SearchButton_Click;
			extensionArea.AddChild(searchButton);

			async void SearchButton_Click(object sender, EventArgs e)
			{
				if (searchPanel == null && !searchPanelOpenOnMouseDown)
				{
					void ShowSearchPanel()
					{
						searchPanel = new SearchPanel(this.TabControl, searchButton, theme);
						searchPanel.Closed += SearchPanel_Closed;

						var systemWindow = this.Parents<SystemWindow>().FirstOrDefault();
						systemWindow.ShowRightSplitPopup(
                            theme,
							new MatePoint(searchButton),
							new MatePoint(searchPanel),
							borderWidth: 0);
					}

					if (HelpIndex.IndexExists)
					{
						ShowSearchPanel();
					}
					else
					{
						searchButton.Enabled = false;

						try
						{
							// Show popover
							var popover = new Popover(ArrowDirection.Up, 7, 5, 0)
							{
								TagColor = theme.AccentMimimalOverlay
							};

							popover.AddChild(new TextWidget("Preparing help".Localize() + "...", pointSize: theme.DefaultFontSize - 1, textColor: theme.TextColor));

							popover.ArrowOffset = (int)(popover.Width - (searchButton.Width / 2));

							this.Parents<SystemWindow>().FirstOrDefault().ShowPopover(
								new MatePoint(searchButton)
								{
									Mate = new MateOptions(MateEdge.Right, MateEdge.Bottom),
									AltMate = new MateOptions(MateEdge.Right, MateEdge.Bottom),
									Offset = new RectangleDouble(12, 0, 12, 0)
								},
								new MatePoint(popover)
								{
									Mate = new MateOptions(MateEdge.Right, MateEdge.Top),
									AltMate = new MateOptions(MateEdge.Left, MateEdge.Bottom)
								});

							await Task.Run(async () =>
							{
								// Start index generation
								await HelpIndex.RebuildIndex();

								UiThread.RunOnIdle(() =>
								{
									// Close popover
									popover.Close();

									// Continue to original task
									ShowSearchPanel();
								});
							});
						}
						catch
						{
						}

						searchButton.Enabled = true;
					}
				}
				else
				{
					searchPanel?.CloseOnIdle();
					searchPanelOpenOnMouseDown = false;
				}
			}

			void SearchPanel_Closed(object sender, EventArgs e)
			{
				// Unregister
				searchPanel.Closed -= SearchPanel_Closed;

				// Release
				searchPanel = null;
			}

			tabControl = new ChromeTabs(extensionArea, theme)
			{
				VAnchor = VAnchor.Stretch,
				HAnchor = HAnchor.Stretch,
				BackgroundColor = theme.BackgroundColor,
				BorderColor = theme.MinimalShade,
				Border = new BorderDouble(left: 1),
			};

			tabControl.PlusClicked += (s, e) => UiThread.RunOnIdle(() =>
			{
                CreateNewDesignTab(true);
			});

			// Force the ActionArea to be as high as ButtonHeight
			tabControl.TabBar.ActionArea.MinimumSize = new Vector2(0, theme.ButtonHeight);
			tabControl.TabBar.BackgroundColor = theme.TabBarBackground;
			tabControl.TabBar.BorderColor = theme.BackgroundColor;

			// Force common padding into top region
			tabControl.TabBar.Padding = theme.TabbarPadding.Clone(top: theme.TabbarPadding.Top * 2, bottom: 0);

			if (Application.EnableNetworkTraffic)
			{
				// add in the update available button
				updateAvailableButton = new LinkLabel("Update Available".Localize(), theme)
				{
					Visible = false,
					Name = "Update Available Link",
					ToolTipText = "There is a new update available for download".Localize(),
					VAnchor = VAnchor.Center,
					Margin = new BorderDouble(10, 0),
					TextColor = theme.PrimaryAccentColor
				};

				// Register listeners
				UserSettings.Instance.SettingChanged += SetLinkButtonsVisibility;

				SetLinkButtonsVisibility(this, null);

				updateAvailableButton.Click += (s, e) =>
				{
					UpdateControlData.Instance.CheckForUpdate();
					DialogWindow.Show<CheckForUpdatesPage>();
				};

				tabControl.TabBar.ActionArea.AddChild(updateAvailableButton);

				UpdateControlData.Instance.UpdateStatusChanged.RegisterEvent((s, e) =>
				{
					SetLinkButtonsVisibility(s, new StringEventArgs("Unknown"));
				}, ref unregisterEvents);
			}

			this.AddChild(tabControl);

			ApplicationController.Instance.NotifyPrintersTabRightElement(extensionArea);

			ChromeTab tab = null;

			// Upgrade tab
			if (!ApplicationController.Instance.IsMatterControlPro())
			{
				tab = new ChromeTab("Upgrade", "Upgrade".Localize(), tabControl, new UpgradeToProTabPage(theme), theme, hasClose: false)
				{
					MinimumSize = new Vector2(0, theme.TabButtonHeight),
					Name = "Upgrade",
					Padding = new BorderDouble(15, 0),
				};
				tabControl.AddTab(tab);

				ChromeTab upgradeTab = tab;

				tab.AfterDraw += (s, e) =>
				{
					var textWidget = upgradeTab.Descendants<TextWidget>().FirstOrDefault();

					var localLabelEndPosition = textWidget.TransformToScreenSpace(textWidget.Printer.GetSize()) - upgradeTab.TransformToScreenSpace(Vector2.Zero);

					double radius = 5 * DeviceScale;
					e.Graphics2D.Circle(localLabelEndPosition.X + radius + 3 * DeviceScale,
						upgradeTab.LocalBounds.Bottom + upgradeTab.Height / 2 - 1 * DeviceScale,
						radius,
						theme.PrimaryAccentColor);
				};
			}

			// Store tab
			tabControl.AddTab(
				tab = new ChromeTab("Store", "Store".Localize(), tabControl, new StoreTabPage(theme), theme, hasClose: false)
				{
					MinimumSize = new Vector2(0, theme.TabButtonHeight),
					Name = "Store Tab",
					Padding = new BorderDouble(15, 0),
				});

			EnableReduceWidth(tab, theme);

			// Library tab
			var libraryWidget = new LibraryWidget(this, theme)
			{
				BackgroundColor = theme.BackgroundColor
			};

			tabControl.AddTab(
				tab = new ChromeTab("Library", "Library".Localize(), tabControl, libraryWidget, theme, hasClose: false)
				{
					MinimumSize = new Vector2(0, theme.TabButtonHeight),
					Name = "Library Tab",
					Padding = new BorderDouble(15, 0),
				});
			EnableReduceWidth(tab, theme);

			SetInitialTab();

			var brandMenu = new BrandMenuButton(theme)
			{
				HAnchor = HAnchor.Fit,
				VAnchor = VAnchor.Fit,
				BackgroundColor = theme.TabBarBackground,
				Padding = theme.TabbarPadding.Clone(right: theme.DefaultContainerPadding)
			};

			tabControl.TabBar.ActionArea.AddChild(brandMenu, 0);

			tabControl.TabBar.ActionArea.VAnchor = VAnchor.Absolute;
			tabControl.TabBar.ActionArea.Height = brandMenu.Height;
			tabControl.FirstMovableTab = tabControl.AllTabs.Count();

			// Restore active workspace tabs
			foreach (var workspace in ApplicationController.Instance.Workspaces)
			{
				ChromeTab newTab;

				// Create and switch to new printer tab
				newTab = this.CreateDesignTab(workspace, false);

				if (newTab.Key == ApplicationController.Instance.MainTabKey)
				{
					tabControl.ActiveTab = newTab;
				}
			}

			statusBar = new Toolbar(theme.TabbarPadding)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Absolute,
				Padding = 1,
				Height = 22 * GuiWidget.DeviceScale,
				BackgroundColor = theme.BackgroundColor,
				Border = new BorderDouble(top: 1),
				BorderColor = theme.BorderColor20,
			};
			this.AddChild(statusBar);

			statusBar.ActionArea.VAnchor = VAnchor.Stretch;

			tasksContainer = statusBar.AddChild(new FlowLayoutWidget(FlowDirection.LeftToRight)
			{
				HAnchor = HAnchor.Fit,
				VAnchor = VAnchor.Stretch,
				BackgroundColor = theme.MinimalShade,
				Name = "runningTasksPanel"
			});

			statusMessage = tasksContainer.AddChild(new TextWidget("")
			{
				Margin = new BorderDouble(5, 0, 5, 3),
				TextColor = theme.TextColor,
				VAnchor = VAnchor.Center,
				PointSize = theme.FontSize9,
				Visible = false,
				AutoExpandBoundsToText = true,
			});

			stretchStatusPanel = new GuiWidget()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
				Padding = new BorderDouble(right: 3),
				Margin = new BorderDouble(right: 2, top: 1, bottom: 1),
				Border = new BorderDouble(1),
				BackgroundColor = theme.MinimalShade.WithAlpha(10),
				BorderColor = theme.SlightShade,
				Width = 200 * GuiWidget.DeviceScale
			};

			statusBar.AddChild(stretchStatusPanel);

			var panelBackgroundColor = theme.MinimalShade.WithAlpha(10);

			statusBar.AddChild(this.CreateThemeStatusPanel(theme, panelBackgroundColor));

			statusBar.AddChild(this.CreateNetworkStatusPanel(theme));

			this.RenderRunningTasks(theme, ApplicationController.Instance.Tasks);
		}

		private void SetInitialTab()
		{
			// Initial tab selection - workspace load will reset if applicable
			string tabKey = ApplicationController.Instance.MainTabKey;

			if (string.IsNullOrEmpty(tabKey)
				|| !tabControl.AllTabs.Any(t => t.Key == tabKey))
			{
				tabKey = "Hardware";
			}

			tabControl.SelectedTabKey = tabKey;
		}

		// you can use this code to test the ShellOpenFile code
#if false
		public override void OnKeyPress(KeyPressEventArgs keyPressEvent)
        {
			var files = new string[]
			{
				@"C:\Users\LarsBrubaker\Downloads\LIGHTSABER-HILT.stl",
				@"C:\Users\larsb\Desktop\test 33.mcx",
				@"C:\Users\LarsBrubaker\Downloads\CokeCan.mcx",
				@"C:\Users\larsb\Downloads\Print Stuff\heart.png",
				@"C:\Users\larsb\Downloads\Print Stuff\vat_comb_ver2.stl",
				@"C:\Users\larsb\Desktop\PICT0137.JPG"
			};
			switch(keyPressEvent.KeyChar)
            {
				case 's':
					ApplicationController.Instance.ShellOpenFile(files.Where(i => File.Exists(i) && Path.GetExtension(i).ToLower() == ".stl").First());
					break;

				case 'm':
					ApplicationController.Instance.ShellOpenFile(files.Where(i => File.Exists(i) && Path.GetExtension(i).ToLower() == ".mcx").First());
					break;

				case 'p':
					ApplicationController.Instance.ShellOpenFile(files.Where(i => File.Exists(i) && Path.GetExtension(i).ToLower() == ".png").First());
					break;

				case 'j':
					ApplicationController.Instance.ShellOpenFile(files.Where(i => File.Exists(i) && Path.GetExtension(i).ToLower() == ".jpg").First());
					break;
			}

			base.OnKeyPress(keyPressEvent);
        }
#endif

		public void OpenFile(string filePath)
        {
			Instance_OpenNewFile(this, filePath);
		}

		private async void Instance_OpenNewFile(object sender, string filePath)
		{
			if (!string.IsNullOrEmpty(filePath)
				&& File.Exists(filePath))
			{
				switch (Path.GetExtension(filePath).ToLower())
				{
					case ".mcx":
						{
							if (ApplicationController.Instance.SwitchToWorkspaceIfAlreadyOpen(filePath))
							{
								return;
							}

							var history = ApplicationController.Instance.Library.PlatingHistory;
							var workspace = new PartWorkspace(new BedConfig(history));
							// Load the previous content
							await workspace.SceneContext.LoadContent(new EditContext()
							{
								ContentStore = history,
								SourceItem = new FileSystemFileItem(filePath)
							}, null);

							ApplicationController.Instance.OpenWorkspace(workspace, WorkspacesChangedEventArgs.OperationType.Add);
						}
						break;

					case ".ttf":
					case ".otf":
						{
							var fileName = Path.GetFileName(filePath);
                            // check if the file is already in the fonts directory
                            if (!File.Exists(Path.Combine(ApplicationDataStorage.Instance.ApplicationFontsDataPath, fileName)))
							{
                                // make sure the directory exists
                                Directory.CreateDirectory(ApplicationDataStorage.Instance.ApplicationFontsDataPath);

                                // copy the file to the fonts directory
                                var newFilePath = Path.Combine(ApplicationDataStorage.Instance.ApplicationFontsDataPath, fileName);
                                File.Copy(filePath, newFilePath, true);
                                ApplicationController.Instance.ShowNotification($"Font added: {fileName}", 5);
                            }
                            else
							{
                                ApplicationController.Instance.ShowNotification($"Font already present: {fileName}", 5);
							}
                        }
						break;

                    default:
						{
							var workspace = await CreateNewDesignTab(false);
							workspace.SceneContext.AddToPlate(new string[] { filePath }, false);
						}
						break;
				}
            }
		}

		private void TabControl_ActiveTabChanged(object sender, EventArgs e)
		{
			if (this.tabControl.ActiveTab?.TabContent is DesignTabPage tabPage)
			{
				var dragDropData = ApplicationController.Instance.DragDropData;

				// Set reference on tab change
				dragDropData.View3DWidget = tabPage.view3DWidget;
				dragDropData.SceneContext = tabPage.sceneContext;
			}

			ApplicationController.Instance.MainTabKey = tabControl.SelectedTabKey;
		}

		private void Tasks_TasksChanged(object sender, EventArgs e)
		{
			this.RenderRunningTasks(theme, ApplicationController.Instance.Tasks);
		}

		bool showQuickTiming = false;
		public override void OnKeyDown(KeyEventArgs keyEvent)
		{
#if DEBUG
			if (keyEvent.KeyCode == Keys.F3)
			{
				showQuickTiming = !showQuickTiming;
				Invalidate();
			}
#endif

			base.OnKeyDown(keyEvent);
		}
		
		public override void OnDraw(Graphics2D graphics2D)
		{
			using (new QuickTimerReport("MainViewWidget.OnDraw"))
			{
				base.OnDraw(graphics2D);
			}

			if (showQuickTiming)
			{
				QuickTimerReport.ReportAndRestart(graphics2D, 10, Height - 26);
			}
        }

		private void ShowUpdateAvailableAnimation()
		{
			double displayTime = 2;
			double pulseTime = 1;
			double totalSeconds = 0;

			var textWidgets = updateAvailableButton.Descendants<TextWidget>().Where((w) => w.Visible == true).ToArray();
			Color startColor = theme.PrimaryAccentColor;

			// Show a highlight on the button as the user did not click it
			var flashBackground = new Animation()
			{
				DrawTarget = updateAvailableButton,
				FramesPerSecond = 10,
			};

			flashBackground.Update += (s1, updateEvent) =>
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
			};

			flashBackground.Start();
		}

		private void SetLinkButtonsVisibility(object s, StringEventArgs e)
		{
			if (UpdateControlData.Instance.UpdateStatus == UpdateControlData.UpdateStatusStates.UpdateAvailable)
			{
				if (!updateAvailableButton.Visible)
				{
					updateAvailableButton.Visible = true;

					this.ShowUpdateAvailableAnimation();
				}
			}
			else
			{
				updateAvailableButton.Visible = false;
			}
		}

		private void Workspaces_Changed(object sender, WorkspacesChangedEventArgs e)
		{
			var workspace = e.Workspace;
			var activePrinter = workspace.Printer;

			if (e.Operation == WorkspacesChangedEventArgs.OperationType.Add
				|| e.Operation == WorkspacesChangedEventArgs.OperationType.Restore)
			{
				// Create printer or part tab
				bool isPrinter = activePrinter?.Settings.PrinterSelected == true;
				if (!isPrinter)
				{
					ChromeTab newTab = CreateDesignTab(workspace, false);

					if (e.Operation == WorkspacesChangedEventArgs.OperationType.Add)
					{
						ApplicationController.Instance.MainTabKey = newTab.Key;
					}

					// Activate tab with previously active key
					if (newTab.Key == ApplicationController.Instance.MainTabKey)
					{
						tabControl.ActiveTab = newTab;
					}
				}
			}
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
				Width = 120 * GuiWidget.DeviceScale
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
				HAnchor = HAnchor.Fit,
				VAnchor = VAnchor.Stretch,
				Margin = new BorderDouble(right: 2, top: 1, bottom: 1),
				Border = new BorderDouble(1),
				BackgroundColor = panelBackgroundColor,
				BorderColor = theme.SlightShade,
				Cursor = Cursors.Hand,
				ToolTipText = "Theme".Localize(),
				Name = "Theme Select Button"
			};

			themePanel.AddChild(
				new ImageWidget(StaticData.Instance.LoadIcon("theme.png", 16, 16), false)
				{
					HAnchor = HAnchor.Center | HAnchor.Absolute,
					VAnchor = VAnchor.Center | VAnchor.Absolute,
					Margin = new BorderDouble(5, 0),
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
					Width = 650 * GuiWidget.DeviceScale,
					Border = 1,
					BorderColor = theme.DropList.Open.BackgroundColor,
					// Padding = theme.DefaultContainerPadding,
					BackgroundColor = menuTheme.BackgroundColor
				};

				var section = ApplicationSettingsPage.CreateThemePanel(menuTheme);
				widget.AddChild(section);

				var systemWindow = this.Parents<SystemWindow>().FirstOrDefault();
				systemWindow.ShowPopup(
					theme,
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

		private static int debugPrinterTabIndex = 0;

		private void AddRightClickTabMenu(ChromeTabs tabs, ChromeTab printerTab, PrinterConfig printer, PartWorkspace workspace, MouseEventArgs mouseEvent)
		{
			var menuTheme = ApplicationController.Instance.MenuTheme;
			var popupMenu = new PopupMenu(menuTheme);
			
			var renameMenuItem = popupMenu.CreateMenuItem("Rename".Localize());
			renameMenuItem.Click += (s, e) =>
			{
				if (workspace != null)
				{
					workspace.SceneContext?.EditContext?.SourceItem?.Rename();
				}
				else if (printer != null)
				{
					DialogWindow.Show(
						new InputBoxPage(
							"Rename Item".Localize(),
							"Name".Localize(),
							printer.PrinterName,
							"Enter New Name Here".Localize(),
							"Rename".Localize(),
							(newName) =>
							{
								printer.Settings.SetValue(SettingsKey.printer_name, newName);
							}));
				}
			};


			var moveButtons = new FlowLayoutWidget();

			var textWidget = new TextWidget("Move Tab", pointSize: theme.DefaultFontSize, textColor: theme.TextColor)
			{
				Margin = PopupMenu.MenuPadding.Clone(PopupMenu.MenuPadding.Left - 5, right: 5),
				VAnchor = VAnchor.Center,
			};
			moveButtons.AddChild(textWidget);
			var buttonSize = 24 * DeviceScale;
			var moveLeftButton = new ThemedIconButton(StaticData.Instance.LoadIcon("fa-angle-right_12.png", 14, 14).GrayToColor(theme.TextColor).MirrorX(), theme)
			{
				Width = buttonSize,
				Height = buttonSize,
				Margin = new BorderDouble(3, 0),
				HoverColor = theme.AccentMimimalOverlay,
				VAnchor = VAnchor.Center,
				Enabled = tabs.GetTabIndex(printerTab) > tabs.FirstMovableTab,
			};
			moveLeftButton.Click += (s, e) =>
			{
				tabs.MoveTabLeft(printerTab);
				popupMenu.Unfocus();
			};
			moveButtons.AddChild(moveLeftButton);

			var moveRightButton = new ThemedIconButton(StaticData.Instance.LoadIcon("fa-angle-right_12.png", 14, 14).GrayToColor(theme.TextColor), theme)
			{
				Width = buttonSize,
				Height = buttonSize,
				Margin = new BorderDouble(3, 0),
				HoverColor = theme.AccentMimimalOverlay,
				VAnchor = VAnchor.Center,
				Enabled = printerTab.NextTab != null,
			};

			moveRightButton.Click += (s, e) =>
			{
				tabs.MoveTabRight(printerTab);
				popupMenu.Unfocus();
			};
			moveButtons.AddChild(moveRightButton);

			popupMenu.AddChild(moveButtons);

			popupMenu.ShowMenu(printerTab, mouseEvent);
		}

		public async Task<PartWorkspace> CreateNewDesignTab(bool addPhilToBed)
		{
			var history = ApplicationController.Instance.Library.PlatingHistory;

			var workspace = new PartWorkspace(new BedConfig(history));

			await workspace.SceneContext.LoadContent(new EditContext(), null);

			ApplicationController.Instance.Workspaces.Add(workspace);

			var newTab = CreateDesignTab(workspace, true);
			tabControl.ActiveTab = newTab;

			if (addPhilToBed)
			{
				workspace.SceneContext.AddPhilToBed();
			}

			ApplicationController.Instance.MainTabKey = workspace.Name;

			return workspace;
		}

		private static void HookupNameChangeCallback(ChromeTab partTab, PartWorkspace workspace)
		{
			var editContext = workspace.SceneContext.EditContext;
			ILibraryItem sourceItem = editContext?.SourceItem;

			void UpdateLinks(object s, EventArgs e)
            {
				editContext = workspace.SceneContext.EditContext;
				// remove any exisitng delegate
				if (sourceItem != null)
				{
					sourceItem.NameChanged -= UpdateTabName;
				}

				// hook up a new delegate
				if (editContext != null)
				{
					sourceItem = editContext.SourceItem;
					if (sourceItem != null)
					{
						sourceItem.NameChanged += UpdateTabName;
					}
				}
			}

			void UpdateTabName(object s, EventArgs e)
			{
				UpdateLinks(s, e);
				if (sourceItem != null)
				{
					partTab.Text = sourceItem.Name;
					if (sourceItem is FileSystemFileItem fileSystemFileItem)
					{
						partTab.ToolTipText = fileSystemFileItem.FilePath;
					}
				}

				ApplicationController.Instance.PersistOpenTabsLayout();
			}

			if (sourceItem != null)
			{
				sourceItem.NameChanged += UpdateTabName;
			}

			if (editContext != null)
			{
				editContext.SourceItemChanged += UpdateTabName;
			}

			workspace.SceneContext.SceneLoaded += UpdateTabName;

			partTab.Closed += (s, e) =>
			{
				if (sourceItem != null)
				{
					sourceItem.NameChanged -= UpdateTabName;
				}
				editContext.SourceItemChanged -= UpdateTabName;
				workspace.SceneContext.SceneLoaded -= UpdateTabName;
			};

			UpdateTabName(null, null);
		}

		public ChromeTab CreateDesignTab(PartWorkspace workspace, bool saveLayout)
		{
			var partTab = new ChromeTab(
				workspace.Name,
				workspace.Name,
				tabControl,
				new DesignTabPage(workspace, theme, ""),
				theme,
				StaticData.Instance.LoadIcon("cube.png", 16, 16).GrayToColor(theme.TextColor))
			{
				Name = "newPart" + tabControl.AllTabs.Count(),
			};

			HookupNameChangeCallback(partTab, workspace);

			EnableReduceWidth(partTab, theme);

			tabControl.AddTab(partTab);

			void Tab_CloseClicked(object sender, EventArgs args)
			{
				ApplicationController.Instance.Workspaces.Remove(workspace);
			}

			// add a right click menu
			partTab.Click += (s, e) =>
			{
				if (e.Button == MouseButtons.Right)
				{
					AddRightClickTabMenu(tabControl, partTab, null, workspace, e);
				}
			};

			void Widget_Closed(object sender, EventArgs args)
			{
				partTab.CloseClicked -= Tab_CloseClicked;
				partTab.Closed -= Widget_Closed;
			}

			partTab.CloseClicked += Tab_CloseClicked;
			partTab.Closed += Widget_Closed;

			return partTab;
		}

		private static void EnableReduceWidth(ChromeTab partTab, ThemeConfig theme)
		{
			var scale = GuiWidget.DeviceScale;
			partTab.MinimumSize = new Vector2(40 * scale, theme.TabButtonHeight);

			var textWidget = partTab.Descendants<TextWidget>().First();
			var tabPill = partTab.Descendants<SimpleTab.TabPill>().First();
			tabPill.HAnchor = HAnchor.Stretch;
			var closeBox = partTab.Descendants<ImageWidget>().FirstOrDefault();
			if (closeBox != null)
			{
				var tabPillMarign = tabPill.Margin;
				tabPill.Margin = new BorderDouble(tabPillMarign.Left, tabPillMarign.Bottom, tabPillMarign.Right + 10, tabPillMarign.Top);
			}

			UpadetMaxWidth();

			// delay this for an update so that the layout of the text widget has happened and its size has been updated.
			textWidget.TextChanged += (s, e) => UiThread.RunOnIdle(UpadetMaxWidth);

			void UpadetMaxWidth()
			{
				// the text
				var width = textWidget.Width;
				// the tab pill
				width += (tabPill.Margin.Width + tabPill.Padding.Width) * scale;
				if (closeBox != null)
				{
					// the close box
					width += closeBox.Width;
				}
				else
				{
					width += 32 * scale;
				}

				partTab.MaximumSize = new Vector2(width, partTab.MaximumSize.Y);
				partTab.Width -= 1;
			}

			partTab.HAnchor = HAnchor.Stretch;
		}

		public override void OnClosed(EventArgs e)
		{
			// Unregister listeners
			UserSettings.Instance.SettingChanged -= SetLinkButtonsVisibility;
			ApplicationController.Instance.WorkspacesChanged -= Workspaces_Changed;
			ApplicationController.Instance.Tasks.TasksChanged -= Tasks_TasksChanged;
			ApplicationController.Instance.UiHintChanged -= Tasks_TasksChanged;
			ApplicationController.Instance.ShellFileOpened -= Instance_OpenNewFile;
			if (tabControl != null)
			{
				tabControl.ActiveTabChanged -= TabControl_ActiveTabChanged;
			}

			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}

#if DEBUG
		~MainViewWidget()
		{
			Console.WriteLine();
		}
#endif

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
				// TODO: find out how we are getting a null task item in the list
				if (taskItem == null)
				{
					continue;
				}

				var runningTaskPanel = new RunningTaskStatusPanel("", taskItem, theme)
				{
					HAnchor = HAnchor.Absolute,
					VAnchor = VAnchor.Stretch,
					Margin = new BorderDouble(right: 2, top: 1, bottom: 1),
					Border = new BorderDouble(1),
					BorderColor = theme.SlightShade,
					ProgressBackgroundColor = progressBackgroundColor,
					Width = 200 * GuiWidget.DeviceScale
				};

				tasksContainer.AddChild(runningTaskPanel);
			}

			if (!string.IsNullOrEmpty(ApplicationController.Instance.GetUiHint()))
			{
				statusMessage.Text = ApplicationController.Instance.GetUiHint();
				statusMessage.Visible = true;
				var parent = statusMessage.Parent;
				if (parent.Children.IndexOf(statusMessage) != parent.Children.Count - 1)
				{
					parent.RemoveChild(statusMessage);
					statusMessage.ClearRemovedFlag();
					parent.AddChild(statusMessage);
				}
			}
			else
			{
				statusMessage.Visible = false;
			}

			tasksContainer.Invalidate();
		}
	}
}