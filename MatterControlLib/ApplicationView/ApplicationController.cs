/*
Copyright (c) 2022, Lars Brubaker, John Lewin
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
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using global::MatterControl.Printing;
using MatterHackers.Agg;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.ImageProcessing;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.MatterControl.Extensibility;
using MatterHackers.MatterControl.Library;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.Plugins;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrinterControls.PrinterConnections;
using MatterHackers.MatterControl.PrintHistory;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.SettingsManagement;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MatterControl.Tour;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.VectorMath;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

[assembly: InternalsVisibleTo("MatterControl.Tests")]
[assembly: InternalsVisibleTo("MatterControl.AutomationTests")]
[assembly: InternalsVisibleTo("CloudServices.Tests")]

namespace MatterHackers.MatterControl
{
	[JsonConverter(typeof(StringEnumConverter))]
	public enum NamedTypeFace
	{
		Alfa_Slab,
		Audiowide,
		Bangers,
		Courgette,
		Damion,
		Firefly_Sung,
		Fredoka,
		Great_Vibes,
		Liberation_Mono,
		Liberation_Sans,
		Liberation_Sans_Bold,
		Lobster,
		Nunito_Regular,
		Nunito_Bold,
		Nunito_Bold_Italic,
		Nunito_Italic,
		Pacifico,
		Poppins,
		Questrial,
		Righteous,
		Russo,
		Titan,
		Titillium,
	}

	public class ApplicationController
	{
		public event EventHandler<string> ApplicationError;

		public event EventHandler<string> ApplicationEvent;

		public HelpArticle HelpArticles { get; set; }

		public ThemeConfig Theme => AppContext.Theme;

		public ThemeConfig MenuTheme => AppContext.MenuTheme;

		public event EventHandler<string> ShellFileOpened;

		public event EventHandler AnyPrintStarted;

		public event EventHandler AnyPrintCanceled;

		public event EventHandler AnyPrintComplete;

		public static string[] ShellFileExtensions => new string[] { ".stl", ".amf", ".3mf", ".obj", ".mcx", ".png", ".jpg", ".jpeg" };

		public bool IsMatterControlPro()
		{
			var result = ApplicationController.Instance.UserHasPro?.Invoke();
			if (result != null)
			{
				return result.Value;
			}

			return false;
		}

		public RunningTasksConfig Tasks { get; set; } = new RunningTasksConfig();

		public IEnumerable<PrinterConfig> ActivePrinters => this.Workspaces.Where(w => w.Printer != null).Select(w => w.Printer);

		public ExtensionsConfig Extensions { get; }

		public PopupMenu GetActionMenuForSceneItem(bool addInSubmenu, View3DWidget view3DWidget)
		{
			var menuTheme = this.MenuTheme;
			var popupMenu = new PopupMenu(menuTheme);

			var sceneContext = view3DWidget?.sceneContext;
			var selectedItem = sceneContext?.Scene?.SelectedItem;
			var selectedItemType = selectedItem?.GetType();

			if (selectedItem == null)
			{
				return popupMenu;
			}

			if (!selectedItemType.IsDefined(typeof(ImmutableAttribute), false))
			{
				AddActionMenuItems(sceneContext, addInSubmenu, menuTheme, popupMenu);
			}

			var workspaceActions = GetWorkspaceActions(view3DWidget);
			var printer = view3DWidget.Printer;

			var actions = new[]
			{
				new ActionSeparator(),
				workspaceActions["Edit"],
				workspaceActions["PasteInto"],
				new ActionSeparator(),
				new NamedAction()
				{
			 		Title = "Save As".Localize(),
			 		Action = () => UiThread.RunOnIdle(() =>
					{
						DialogWindow.Show(
							new SaveAsPage(
								(container, newName) =>
								{
									sceneContext.SaveAs(container, newName);
								}));
					}),
			 		IsEnabled = () => sceneContext.EditableScene
				},
				new NamedAction()
				{
					ID = "Export",
					Title = "Export".Localize(),
					Icon = StaticData.Instance.LoadIcon("cube_export.png", 16, 16).SetToColor(MenuTheme.TextColor),
					Action = () =>
					{
						Instance.ExportLibraryItems(
							new[] { new InMemoryLibraryItem(selectedItem) },
							centerOnBed: false,
							printer: printer);
					}
				},
				new ActionSeparator(),
				workspaceActions["Delete"]
			};

			menuTheme.CreateMenuItems(popupMenu, actions);

			if (selectedItem is IRightClickMenuProvider menuProvider)
			{
                menuProvider.AddRightClickMenuItemsItems(popupMenu);
            }

			var parent = selectedItem.Parent;
			if (parent != null)
			{
				var orderChildrenByIndex = parent.GetType().GetCustomAttributes(typeof(OrderChildrenByIndexAttribute), true).Any();
				if (orderChildrenByIndex)
				{
					AddReorderChildrenRightClickMenuItems(popupMenu, selectedItem);
				}
			}

			return popupMenu;
		}

		public void AddReorderChildrenRightClickMenuItems(PopupMenu popupMenu, IObject3D itemRightClicked)
		{
			popupMenu.CreateSeparator();
			var parent = itemRightClicked.Parent;
            if(parent == null)
            {
				return;
            }

			// move to the top
			var moveTopItem = popupMenu.CreateMenuItem("↑↑ Move Top".Localize());

			moveTopItem.Enabled = parent.Children.IndexOf(itemRightClicked) != 0;
			moveTopItem.Click += (s, e) =>
			{
				parent.Children.Modify((list) =>
				{
					list.Remove(itemRightClicked);
					list.Insert(0, itemRightClicked);
				});
			};

			// move up one position
			var moveUpItem = popupMenu.CreateMenuItem("↑ Move Up".Localize());

			moveUpItem.Enabled = parent.Children.IndexOf(itemRightClicked) != 0;
			moveUpItem.Click += (s, e) =>
			{
				parent.Children.Modify((list) =>
				{
					var index = list.IndexOf(itemRightClicked);
					list.Remove(itemRightClicked);
					list.Insert(index - 1, itemRightClicked);
				});
			};

			// move down one position
			var moveDownItem = popupMenu.CreateMenuItem("↓ Move Down".Localize());

			moveDownItem.Enabled = parent.Children.IndexOf(itemRightClicked) != parent.Children.Count - 1;
			moveDownItem.Click += (s, e) =>
			{
				parent.Children.Modify((list) =>
				{
					var index = list.IndexOf(itemRightClicked);
					list.Remove(itemRightClicked);
					list.Insert(index + 1, itemRightClicked);
				});
			};

			// move to the bottom
			var moveBottomItem = popupMenu.CreateMenuItem("↓↓ Move Bottom".Localize());

			moveBottomItem.Enabled = parent.Children.IndexOf(itemRightClicked) != parent.Children.Count - 1;
			moveBottomItem.Click += (s, e) =>
			{
				parent.Children.Modify((list) =>
				{
					var index = list.IndexOf(itemRightClicked);
					list.Remove(itemRightClicked);
					list.Add(itemRightClicked);
				});
			};
		}

		public PopupMenu GetModifyMenu(ISceneContext sceneContext)
		{
			var popupMenu = new PopupMenu(this.MenuTheme);

			AddActionMenuItems(sceneContext,
				false,
				this.MenuTheme,
				popupMenu);

			return popupMenu;
		}

		private static void AddActionMenuItems(ISceneContext sceneContext, bool useSubMenu, ThemeConfig menuTheme, PopupMenu popupMenu)
		{
			var renameMenuItem = popupMenu.CreateMenuItem("Rename".Localize());
			renameMenuItem.Click += (s, e) =>
			{
				var scene = sceneContext.Scene;
				var selectedItem = scene.SelectedItem;
				if (selectedItem != null)
				{
					selectedItem.ShowRenameDialog(scene.UndoBuffer);
				}
			};

			popupMenu.CreateSeparator();

			if (useSubMenu)
			{
				// Create items in a 'Modify' sub-menu
				popupMenu.CreateSubMenu("Modify".Localize(),
					menuTheme,
					(modifyMenu) => SceneOperations.AddModifyItems(modifyMenu, menuTheme, sceneContext));
			}
			else
			{
				// Create items directly in the referenced menu
				SceneOperations.AddModifyItems(popupMenu, menuTheme, sceneContext);
			}
		}

		public void PersistOpenTabsLayout()
		{
			// Project workspace definitions to serializable structure
			var workspaces = this.Workspaces
				.Where(w => w.SceneContext?.EditContext?.SourceFilePath?.Contains("\\Library\\CloudData") == false)
				.Select(w =>
				{
					if (w.Printer == null)
					{
						return new PartWorkspace(w.SceneContext)
						{
							ContentPath = w.SceneContext.EditContext?.SourceFilePath,
						};
					}
					else
					{
						return new PartWorkspace(w.Printer)
						{
							ContentPath = w.SceneContext.EditContext?.SourceFilePath,
						};
					}
				});

			lock (workspaces)
			{
				var content = JsonConvert.SerializeObject(
						workspaces,
						Formatting.Indented,
						new JsonSerializerSettings
						{
							NullValueHandling = NullValueHandling.Ignore
						});
				
				// Persist workspace definition to disk
				File.WriteAllText(ProfileManager.Instance.OpenTabsPath, content);
			}
		}

		public async Task PersistPrintTabsContent()
		{
			// Persist all pending changes in all workspaces to disk
			foreach (var workspace in this.Workspaces.ToArray())
			{
				if (workspace.Printer != null)
				{
					await this.Tasks.Execute("Saving".Localize() + $" \"{workspace.Name}\" ...", workspace, workspace.SceneContext.SaveChanges);
				}
			}
		}

		internal void ExportAsMatterControlConfig(PrinterConfig printer)
		{
			AggContext.FileDialogs.SaveFileDialog(
				new SaveFileDialogParams("MatterControl Printer Export|*.printer", title: "Export Printer Settings")
				{
					FileName = printer.PrinterName
				},
				(saveParams) =>
				{
					try
					{
						if (!string.IsNullOrWhiteSpace(saveParams.FileName))
						{
							File.WriteAllText(saveParams.FileName, JsonConvert.SerializeObject(printer.Settings, Formatting.Indented));
						}
					}
					catch (Exception e)
					{
						UiThread.RunOnIdle(() =>
						{
							StyledMessageBox.ShowMessageBox(e.Message, "Couldn't save file".Localize());
						});
					}
				});
		}

		public void LogError(string errorMessage)
		{
			this.ApplicationError?.Invoke(this, errorMessage);
		}

		public void LogInfo(string message)
		{
			this.ApplicationEvent?.Invoke(this, message);
		}

		public Action RedeemDesignCode { get; set; }

		public Action EnterShareCode { get; set; }

		// check permission to an IObject3D instance
#if DEBUG
		public Func<IObject3D, bool> UserHasPermission { get; set; } = (item) => true;
#else
		public Func<IObject3D, bool> UserHasPermission { get; set; } = (item) => false;
#endif

		// check permission to a purchase
		public Func<bool> UserHasPro { get; set; }

		public Func<IObject3D, ThemeConfig, (string url, GuiWidget markdownWidget)> GetUnlockData { get; set; }

		public Func<PrintTask, Task<Dictionary<string, string>>> PushPrintTaskToServer { get; set; }

		private static ApplicationController globalInstance;

		public RootedObjectEventHandler CloudSyncStatusChanged { get; private set; } = new RootedObjectEventHandler();

		public RootedObjectEventHandler DoneReloadingAll = new RootedObjectEventHandler();
		public RootedObjectEventHandler ActiveProfileModified = new RootedObjectEventHandler();

		public event EventHandler<WorkspacesChangedEventArgs> WorkspacesChanged;

		public event EventHandler ReloadSettingsTriggered;

		public void ReloadSettings(PrinterConfig printer)
		{
			var printerTabPage = this.MainView.Descendants<PrinterTabPage>().Where(page => page.Printer == printer).FirstOrDefault();
			if (printerTabPage != null)
			{
				ApplicationController.Instance.IsReloading = true;
				var settingsContext = new SettingsContext(
						printer,
						null,
						NamedSettingsLayers.All);

				var sideBar = printerTabPage.Descendants<DockingTabControl>().FirstOrDefault();
				
				if (printer.ViewState.ConfigurePrinterVisible)
				{
					sideBar.ReplacePage(
						"Printer",
						new ConfigurePrinterWidget(settingsContext, printer, Theme)
						{
							HAnchor = HAnchor.Stretch,
							VAnchor = VAnchor.Stretch,
						},
						false);
				}

				sideBar.ReplacePage("Slice Settings", new SliceSettingsWidget(printer, settingsContext, Theme));
				ApplicationController.Instance.IsReloading = false;
			}

			ReloadSettingsTriggered?.Invoke(null, null);
		}

		public static Action WebRequestFailed;
		public static Action WebRequestSucceeded;

		public static Action<DialogWindow> ChangeToPrintNotification = null;

#if DEBUG
		public const string EnvironmentName = "TestEnv_";
#else
		public const string EnvironmentName = "";
#endif

		public bool ApplicationExiting { get; internal set; } = false;

		public static Func<string, Task<Dictionary<string, string>>> GetProfileHistory;

		public void OnWorkspacesChanged(PartWorkspace workspace, WorkspacesChangedEventArgs.OperationType operationType)
		{
			this.WorkspacesChanged?.Invoke(this, new WorkspacesChangedEventArgs(
				workspace,
				operationType));

			if (operationType != WorkspacesChangedEventArgs.OperationType.Restore)
			{
				Instance.PersistOpenTabsLayout();
			}
		}

		public string GetFavIconUrl(string oemName)
		{
			if (OemSettings.Instance.OemUrls.TryGetValue(oemName, out string oemUrl)
				&& !string.IsNullOrWhiteSpace(oemUrl))
			{
				return "https://www.google.com/s2/favicons?domain=" + oemUrl;
			}

			return null;
		}

		public void ClosePrinter(PrinterConfig printer, bool allowChangedEvent = true)
		{
			// Actually clear printer
			ProfileManager.Instance.ClosePrinter(printer.Settings.ID);

			// Shutdown the printer connection
			printer.Connection.Disable();

			if (allowChangedEvent)
			{
				if (this.Workspaces.FirstOrDefault(w => w.Printer?.Settings.ID == printer.Settings.ID) is PartWorkspace workspace)
				{
					this.Workspaces.Remove(workspace);

					this.OnWorkspacesChanged(workspace, WorkspacesChangedEventArgs.OperationType.Remove);
				}
			}

			printer.Dispose();
		}

		public static void LaunchBrowser(string targetUri)
		{
			UiThread.RunOnIdle(() =>
			{
				if (!string.IsNullOrEmpty(OemSettings.Instance.AffiliateCode)
					&& targetUri.Contains("matterhackers.com"))
				{
					string internalLink = "";
					// if we have a trailing internal link
					if (targetUri.Contains("#"))
					{
						internalLink = targetUri.Substring(targetUri.IndexOf("#"));
						targetUri = targetUri.Substring(0, targetUri.Length - internalLink.Length);
					}

					if (targetUri.Contains("?"))
					{
						targetUri += $"&aff={OemSettings.Instance.AffiliateCode}";
					}
					else
					{
						targetUri += $"?aff={OemSettings.Instance.AffiliateCode}";
					}

					targetUri += internalLink;
				}

				ProcessStart(targetUri);
			});
		}

        public static void ProcessStart(string input)
        {
			try
			{
				Process.Start(input);
			}
			catch
			{
				// hack because of this: https://github.com/dotnet/corefx/issues/10361
				if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				{
					input = input.Replace("&", "^&");
					Process.Start(new ProcessStartInfo("cmd", $"/c start {input}") { CreateNoWindow = true });
				}
				else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
				{
					Process.Start("xdg-open", input);
				}
				else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
				{
					Process.Start("open", input);
				}
				else
				{
					throw;
				}
			}
		}

		internal void MakeGrayscale(ImageBuffer sourceImage)
		{
			var buffer = sourceImage.GetBuffer();
			int destIndex = 0;
			for (int y = 0; y < sourceImage.Height; y++)
			{
				for (int x = 0; x < sourceImage.Width; x++)
				{
					int b = buffer[destIndex + 0];
					int g = buffer[destIndex + 1];
					int r = buffer[destIndex + 2];

					int c = (r * 77) + (g * 151) + (b * 28);
					byte gray = (byte)(c >> 8);

					buffer[destIndex + 0] = gray;
					buffer[destIndex + 1] = gray;
					buffer[destIndex + 2] = gray;

					destIndex += 4;
				}
			}
		}

		// Plugin Registration Points

		// Returns the user printer profile from the webservices plugin
		public static Func<PrinterInfo, string, Task<PrinterSettings>> GetPrinterProfileAsync;

		// Executes the user printer profile sync logic in the webservices plugin
		public static Func<string, IProgress<ProgressStatus>, Task> SyncCloudProfiles;

		public static Action<string> QueueCloudProfileSync;

		// Returns all public printer profiles from the webservices plugin
		public static Func<Task<OemProfileDictionary>> GetPublicProfileList;

		// Returns the public printer profile from the webservices plugin
		public static Func<string, Task<PrinterSettings>> DownloadPublicProfileAsync;

		// Indicates if guest, rather than an authenticated user, is active
		public static Func<bool> GuestUserActive { get; set; }

		// Returns the authentication dialog from the authentication plugin
		public static Func<AuthenticationContext, DialogPage> GetAuthPage;

		public SlicePresetsPage AcitveSlicePresetsPage { get; set; }


		public MainViewWidget MainView;

		private readonly Dictionary<string, List<LibraryAction>> registeredLibraryActions = new Dictionary<string, List<LibraryAction>>();

		public ThumbnailsConfig Thumbnails { get; }

		public Dictionary<string, NamedAction> GetWorkspaceActions(View3DWidget view3DWidget)
		{
			var sceneContext = view3DWidget.sceneContext;
			var printer = sceneContext.Printer;

			var theme = Instance.MenuTheme;

			// Build workspace actions, each having a unique ID
			var actions = new[]
			{
				new NamedAction()
				{
					ID = "Print",
					Title = "Print".Localize(),
					Shortcut = "Ctrl+P",
					Action = view3DWidget.PushToPrinterAndPrint,
					IsEnabled = () =>
					{
						if (sceneContext.Printer != null)
						{
							return sceneContext.Printer.PrintButtonEnabled();
						}

						return sceneContext.EditableScene
							|| (sceneContext.EditContext.SourceItem is ILibraryAsset libraryAsset
								&& string.Equals(Path.GetExtension(libraryAsset.FileName), ".gcode", StringComparison.OrdinalIgnoreCase));
					},
				},
				new NamedActionGroup()
				{
					ID = "Edit",
					Title = "Edit",
					Group = new NamedAction[]
					{
						new NamedAction()
						{
							ID = "Cut",
							Title = "Cut".Localize(),
							Action = () => sceneContext.Scene.Cut(),
							IsEnabled = () => sceneContext.Scene.SelectedItem != null
						},
						new NamedAction()
						{
							ID = "Copy",
							Title = "Copy".Localize(),
							Action = () => sceneContext.Scene.Copy(),
							IsEnabled = () => sceneContext.Scene.SelectedItem != null
						},
						new NamedAction()
						{
							ID = "Paste",
							Title = "Paste".Localize(),
							Action = () => sceneContext.Paste(),
							IsEnabled = () => Clipboard.Instance.ContainsImage || Clipboard.Instance.GetText() == "!--IObjectSelection--!"
						}
					},
					IsEnabled = () => true,
				},
				new NamedAction()
				{
					ID = "PasteInto",
					Title = "Paste Into".Localize(),
					Action = () => sceneContext.PasteIntoSelection(),
					IsEnabled = () =>
					{
						var selectedItem = sceneContext.Scene.SelectedItem;
						var clipboardItem = ApplicationController.ClipboardItem;
						// there is an object in the clipboard
						if (Clipboard.Instance.ContainsText
							&& Clipboard.Instance.GetText() == "!--IObjectSelection--!"
							// there is a selected item to paste into
							&& selectedItem != null 
							// the selected item is not a primitve
							&& !(selectedItem is PrimitiveObject3D)
							&& clipboardItem != null)
						{
							return true;
						}

						return false;
					}
				},
				new NamedAction()
				{
					ID = "Delete",
					Icon = StaticData.Instance.LoadIcon("remove.png", 16, 16).SetToColor(theme.TextColor).SetPreMultiply(),
					Title = "Remove".Localize(),
					Action = sceneContext.Scene.DeleteSelection,
					IsEnabled = () => sceneContext.Scene.SelectedItem != null
				},
				new NamedAction()
				{
					ID = "Export",
					Title = "Export".Localize(),
					Icon = StaticData.Instance.LoadIcon("cube_export.png", 16, 16).SetToColor(theme.TextColor),
					Action = () =>
					{
						ApplicationController.Instance.ExportLibraryItems(
							new[] { new InMemoryLibraryItem(sceneContext.Scene) },
							centerOnBed: false,
							printer: printer);
					},
					IsEnabled = () => sceneContext.EditableScene
						|| (sceneContext.EditContext.SourceItem is ILibraryAsset libraryAsset
							&& string.Equals(Path.GetExtension(libraryAsset.FileName), ".gcode", StringComparison.OrdinalIgnoreCase))
				},
				new NamedAction()
				{
					ID = "Save",
					Title = "Save".Localize(),
					Shortcut = "Ctrl+S",
					Action = () =>
					{
						ApplicationController.Instance.Tasks.Execute("Saving".Localize(), printer, sceneContext.SaveChanges).ConfigureAwait(false);
					},
					IsEnabled = () => sceneContext.EditableScene
				},
				new NamedAction()
				{
					ID = "SaveAs",
					Title = "Save As".Localize(),
					Action = () => UiThread.RunOnIdle(() =>
					{
						DialogWindow.Show(
							new SaveAsPage(
								async (container, newName) =>
								{
									sceneContext.SaveAs(container, newName);
								}));
					}),
					IsEnabled = () => sceneContext.EditableScene
				},
				new NamedAction()
				{
					ID = "ArrangeAll",
					Title = "Arrange All Parts".Localize(),
					Action = async () =>
					{
						await sceneContext.Scene.AutoArrangeChildren(view3DWidget.BedCenter).ConfigureAwait(false);
					},
					IsEnabled = () => sceneContext.EditableScene,
					Icon = StaticData.Instance.LoadIcon("arrange_all.png", 16, 16).SetToColor(theme.TextColor),
				},
				new NamedAction()
				{
					ID = "ClearBed",
					Title = "Clear Bed".Localize(),
					Action = () =>
					{
						UiThread.RunOnIdle(() =>
						{
							view3DWidget.ClearPlate();
						});
					}
				}
			};

			// Construct dictionary from workspace actions by ID
			return actions.ToDictionary(a => a.ID);
		}

		public static void OpenFileWithSystemDialog(Action<string[]> openFiles)
		{
			var extensionsWithoutPeriod = new HashSet<string>(ApplicationSettings.OpenDesignFileParams.Split('|').First().Split(',').Select(t => t.Trim().Trim('.')));

			foreach (var extension in ApplicationController.Instance.Library.ContentProviders.Keys)
			{
				extensionsWithoutPeriod.Add(extension.ToUpper());
			}

			var extensionsArray = extensionsWithoutPeriod.OrderBy(t => t).ToArray();

			string filter = string.Format(
				"{0}|{1}",
				string.Join(",", extensionsArray),
				string.Join("", extensionsArray.Select(t => $"*.{t.ToLower()};").ToArray()));

			UiThread.RunOnIdle(() =>
			{
				AggContext.FileDialogs.OpenFileDialog(
					new OpenFileDialogParams(filter, multiSelect: true),
					(openParams) =>
					{
						if (openParams != null && openParams.FileNames != null)
						{
							openFiles?.Invoke(openParams.FileNames);
						}
					});
			}, .1);
		}

		public async Task OpenIntoNewTab(IEnumerable<ILibraryItem> selectedLibraryItems)
		{
			await this.MainView.CreateNewDesignTab(false);
			
			var workspace = this.Workspaces.Last();
			var insertionGroup = workspace.SceneContext.AddToPlate(selectedLibraryItems);

            // wait for the insertion to finish
            await insertionGroup.LoadingItemsTask;
            // then clear the undo buffer so we don't ask to save and undoing does not remove the starting part
			workspace.SceneContext.Scene.UndoBuffer.ClearHistory();
		}

		internal void BlinkTab(ITab tab)
		{
			var theme = this.Theme;
			if (tab is GuiWidget guiWidget)
			{
				guiWidget.Descendants<TextWidget>().FirstOrDefault().FlashBackground(theme.PrimaryAccentColor.WithContrast(theme.TextColor, 6).ToColor());
			}
		}

		public void ShowApplicationHelp(string guideKey)
		{
			this.ActivateHelpTab(guideKey);
		}

		public void ShowAboutPage()
		{
			UiThread.RunOnIdle(() =>
			{
				DialogWindow.Show<AboutPage>();
			});
		}

		public ImageSequence GetProcessingSequence(Color color)
		{
			int size = (int)Math.Round(80 * GuiWidget.DeviceScale);
			var workingAnimation = new ImageSequence();
			var frameCount = 30.0;
			var strokeWidth = 4 * GuiWidget.DeviceScale;

			for (int i = 0; i < frameCount; i++)
			{
				var frame = new ImageBuffer(size, size);
				var graphics = frame.NewGraphics2D();
				graphics.Render(new Stroke(new Arc(frame.Width / 2,
					frame.Height / 2,
					size / 4 - strokeWidth / 2,
					size / 4 - strokeWidth / 2,
					MathHelper.Tau / frameCount * i,
					MathHelper.Tau / 4 + MathHelper.Tau / frameCount * i),
					strokeWidth),
					color);
				workingAnimation.AddImage(frame);
			}

			return workingAnimation;
		}

		private static int applicationInstanceCount = 0;

		public static int ApplicationInstanceCount
		{
			get
			{
				if (AggContext.OperatingSystem == OSType.Mac)
				{
					return 1;
				}

				if (applicationInstanceCount == 0)
				{
					var mcAssembly = Assembly.GetEntryAssembly();
					if (mcAssembly != null)
					{
						string applicationName = Path.GetFileNameWithoutExtension(mcAssembly.Location).ToUpper();
						Process[] p1 = Process.GetProcesses();
						foreach (System.Diagnostics.Process pro in p1)
						{
							try
							{
								if (pro?.ProcessName != null
								   && pro.ProcessName.ToUpper().Contains(applicationName))
								{
									applicationInstanceCount++;
								}
							}
							catch
							{
							}
						}
					}
				}

				return applicationInstanceCount;
			}
		}

		public LibraryConfig Library { get; }

		public ILibraryContext LibraryTabContext { get; private set; }

		private void InitializeLibrary()
		{
			this.Library.RegisterContainer(
				new DynamicContainerLink(
					"Computer".Localize(),
					StaticData.Instance.LoadIcon(Path.Combine("Library", "folder.png")),
					StaticData.Instance.LoadIcon(Path.Combine("Library", "computer_icon.png")),
					() => new ComputerCollectionContainer()));

			var rootLibraryCollection = Datastore.Instance.dbSQLite.Table<PrintItemCollection>().Where(v => v.Name == "_library").Take(1).FirstOrDefault();
			if (rootLibraryCollection != null)
			{
				var forceAddLocalLibrary = false;
#if DEBUG
				forceAddLocalLibrary = true;
#endif
				// only add the local library if there are items in it
				var localLibrary = new SqliteLibraryContainer(rootLibraryCollection.Id);
				localLibrary.Load();
				if (forceAddLocalLibrary || localLibrary.ChildContainers.Any() || localLibrary.Items.Any())
				{
					this.Library.RegisterContainer(
						new DynamicContainerLink(
							"Local Library".Localize(),
							StaticData.Instance.LoadIcon(Path.Combine("Library", "folder.png")),
							StaticData.Instance.LoadIcon(Path.Combine("Library", "local_library_icon.png")),
							() => localLibrary));
				}
			}

			var forceAddQueue = false;
#if DEBUG
			forceAddQueue = true;
#endif
			// only add the queue if there are items in it
			var queueDirectory = LegacyQueueFiles.QueueDirectory;
			LegacyQueueFiles.ImportFromLegacy();
			if (forceAddQueue || Directory.Exists(queueDirectory))
			{
				// make sure the queue directory exists
				Directory.CreateDirectory(queueDirectory);

				this.Library.RegisterContainer(new DynamicContainerLink(
						"Queue".Localize(),
						StaticData.Instance.LoadIcon(Path.Combine("Library", "folder.png")),
						StaticData.Instance.LoadIcon(Path.Combine("Library", "queue_icon.png")),
						() => new FileSystemContainer(queueDirectory)
						{
							UseIncrementedNameDuringTypeChange = true,
							DefaultSort = new LibrarySortBehavior()
							{
								SortKey = SortKey.ModifiedDate,
							}
						}));
			}

			this.Library.DesignAppsCollectionContainer = new DesignAppsCollectionContainer();
			// this.Library.LibraryCollectionContainer.HeaderMarkdown = "Here you can find the collection of libraries you can use".Localize();

			this.Library.RegisterContainer(
				new DynamicContainerLink(
					"Design Apps".Localize(),
					StaticData.Instance.LoadIcon(Path.Combine("Library", "folder.png")),
					StaticData.Instance.LoadIcon(Path.Combine("Library", "design_apps_icon.png")),
					() => this.Library.DesignAppsCollectionContainer)
                {
					IsReadOnly = true
                });

			if (File.Exists(ApplicationDataStorage.Instance.CustomLibraryFoldersPath))
			{
				// Add each path defined in the CustomLibraryFolders file as a new FileSystemContainerItem
				foreach (string directory in File.ReadLines(ApplicationDataStorage.Instance.CustomLibraryFoldersPath))
				{
					// if (Directory.Exists(directory))
					{
						this.Library.RegisterContainer(
							new FileSystemContainer.DirectoryContainerLink(directory)
							{
								UseIncrementedNameDuringTypeChange = true
							});
					}
				}
			}

			this.Library.PlatingHistory = new PlatingHistoryContainer();

			this.Library.RegisterContainer(
				new DynamicContainerLink(
					"History".Localize(),
					StaticData.Instance.LoadIcon(Path.Combine("Library", "folder.png")),
					StaticData.Instance.LoadIcon(Path.Combine("Library", "history_icon.png")),
					() => new RootHistoryContainer())
				{
					IsReadOnly = true
				});

			// Create a new library context for the SaveAs view
			this.LibraryTabContext = new LibraryConfig()
			{
				ActiveContainer = new WrappedLibraryContainer(this.Library.RootLibaryContainer)
				{
					ExtraContainers = new List<ILibraryContainerLink>()
					{
						new DynamicContainerLink(
							"Printers".Localize(),
							StaticData.Instance.LoadIcon(Path.Combine("Library", "folder.png")),
							StaticData.Instance.LoadIcon(Path.Combine("Library", "printer_icon.png")),
							() => new OpenPrintersContainer())
					}
				}
			};
		}

		public void ExportLibraryItems(IEnumerable<ILibraryItem> libraryItems, bool centerOnBed = true, PrinterConfig printer = null)
		{
			UiThread.RunOnIdle(() =>
			{
				if (printer != null || this.ActivePrinters.Count() == 1)
				{
					// If unspecified but count is one, select the one active printer
					if (printer == null)
					{
						printer = this.ActivePrinters.First();
					}

					DialogWindow.Show(
						new ExportPrintItemPage(libraryItems, centerOnBed, printer));
				}
				else
				{
					// If there are no printers setup show the export dialog but have the gcode option disabled
					if (ProfileManager.Instance.ActiveProfiles.Count() == 0
						|| ProfileManager.Instance.ActiveProfiles.Count() > 1)
					{
						DialogWindow.Show(new ExportPrintItemPage(libraryItems, centerOnBed, null));
					}
					else // If there is only one printer constructed, use it.
					{
						var historyContainer = this.Library.PlatingHistory;

						var printerInfo = ProfileManager.Instance.ActiveProfiles.First();
						ProfileManager.LoadSettingsAsync(printerInfo.ID).ContinueWith(task =>
						{
							var settings = task.Result;
							var onlyPrinter = new PrinterConfig(settings);

							onlyPrinter.Bed.LoadEmptyContent(
								new EditContext()
								{
									ContentStore = historyContainer,
									SourceItem = historyContainer.NewBedPlate(onlyPrinter.Bed)
								});

							UiThread.RunOnIdle(() =>
							{
								DialogWindow.Show(new ExportPrintItemPage(libraryItems, centerOnBed, onlyPrinter));
							});
						});
					}
				}
			});
		}

		private ApplicationController()
		{
			Workspaces = new ObservableCollection<PartWorkspace>();

			Workspaces.CollectionChanged += (s, e) =>
			{
				if (!restoringWorkspaces)
				{
					PersistOpenTabsLayout();
				}
			};

			this.Thumbnails = new ThumbnailsConfig();

			ProfileManager.UserChanged += (s, e) =>
			{
				// _activePrinters = new List<PrinterConfig>();
			};

			this.Extensions = new ExtensionsConfig(this.Library);
			this.Extensions.Register(new SheetEditor());
			this.Extensions.Register(new PublicPropertyEditor());

			HelpArticle helpArticle = null;

			string helpPath = Path.Combine("OEMSettings", "toc.json");
			if (StaticData.Instance.FileExists(helpPath))
			{
				try
				{
					helpArticle = JsonConvert.DeserializeObject<HelpArticle>(StaticData.Instance.ReadAllText(helpPath));
				}
				catch { }
			}

			this.HelpArticles = helpArticle ?? new HelpArticle();

			Object3D.AssetsPath = Path.Combine(ApplicationDataStorage.Instance.ApplicationLibraryDataPath, "Assets");

			using (var meshSteam = StaticData.Instance.OpenStream(Path.Combine("Stls", "missing.stl")))
			{
				Object3D.FileMissingMesh = StlProcessing.Load(meshSteam, CancellationToken.None);
			}

			ScrollBar.DefaultMargin = new BorderDouble(right: 1);
			ScrollBar.ScrollBarWidth = 11 * GuiWidget.DeviceScale;
			ScrollBar.GrowThumbBy = 3 * GuiWidget.DeviceScale;

			// Initialize statics
			Object3D.AssetsPath = ApplicationDataStorage.Instance.LibraryAssetsPath;

			this.Library = new LibraryConfig();
			this.Library.ContentProviders.Add(new[] { "stl", "obj", "3mf", "amf", "mcx" }, new MeshContentProvider());
			this.Library.ContentProviders.Add("gcode", new GCodeContentProvider());
			this.Library.ContentProviders.Add(new[] { "png", "gif", "jpg", "jpeg" }, new ImageContentProvider());
			this.Library.ContentProviders.Add(new[] { "scad" }, new OpenScadContentProvider());

			this.InitializeLibrary();
		}

		/// <summary>
		/// Show a notification on screen. This is usually due to a system error of some kind
		/// like a bad save or load.
		/// </summary>
		/// <param name="message">The message to show</param>
		/// <param name="durationSeconds">The length of time to show the message</param>
		public void ShowNotification(string message, double durationSeconds = 10)
        {
			foreach(var printer in ActivePrinters)
            {
				var terminal = printer?.Connection?.TerminalLog;
				if (terminal != null)
                {
					terminal.WriteLine(message);
                }
            }

			// show the message for the time requested
			this.Tasks.Execute(message,
				null,
				(progress, cancellationToken) =>
				{
					var time = UiThread.CurrentTimerMs;
					var status = new ProgressStatus();
					while (UiThread.CurrentTimerMs < time + durationSeconds * 1000)
					{
						Thread.Sleep(30);
						status.Progress0To1 = (UiThread.CurrentTimerMs - time) / 1000.0 / durationSeconds;
						progress.Report(status);
					}

					return Task.CompletedTask;
				});
        }

        public void Connection_ErrorReported(object sender, string line)
		{
			if (line != null)
			{
				if (sender is PrinterConnection printerConnection)
				{
					string message = "Your printer is reporting a HARDWARE ERROR and has been paused. Check the error and cancel the print if required.".Localize()
						+ "\n"
						+ "\n"
						+ "Error Reported".Localize() + ":"
						+ $" \"{line}\".";

					UiThread.RunOnIdle(() =>
					{
						var prinerName = printerConnection.Printer.PrinterName;
						var messageBox = new StyledMessageBox.MessageBoxPage((clickedOk) =>
						{
							if (clickedOk && printerConnection.Paused)
							{
								printerConnection.Resume();
							}
						},
						message,
						prinerName + " is reporting a Hardware Error".Localize(),
						StyledMessageBox.MessageType.YES_NO,
						null,
						400,
						300,
						"Resume".Localize(),
						"OK".Localize(),
						ApplicationController.Instance.Theme);

						var exportButton = Theme.CreateDialogButton("Export Print Log...".Localize());
						exportButton.Click += (s, e) =>
						{
							UiThread.RunOnIdle(() => TerminalLog.Export(printerConnection), .1);
						};
						messageBox.AddPageAction(exportButton);

						DialogWindow.Show(messageBox);
					});
				}
			}
		}

		public void Connection_TemporarilyHoldingTemp(object sender, EventArgs e)
		{
			if (sender is PrinterConnection printerConnection)
			{
				if (printerConnection.AnyHeatIsOn)
				{
					var paused = false;
					Tasks.Execute("", printerConnection.Printer, (reporter, cancellationToken) =>
					{
						var progressStatus = new ProgressStatus();

						while (printerConnection.SecondsToHoldTemperature > 0
							&& !cancellationToken.IsCancellationRequested
							&& printerConnection.ContinueHoldingTemperature)
						{
							if (paused)
							{
								progressStatus.Status = "Holding Temperature".Localize();
							}
							else
							{
								if (printerConnection.SecondsToHoldTemperature > 60)
								{
									progressStatus.Status = string.Format(
										"{0} {1:0}m {2:0}s",
										"Automatic Heater Shutdown in".Localize(),
										(int)printerConnection.SecondsToHoldTemperature / 60,
										(int)printerConnection.SecondsToHoldTemperature % 60);
								}
								else
								{
									progressStatus.Status = string.Format(
										"{0} {1:0}s",
										"Automatic Heater Shutdown in".Localize(),
										printerConnection.SecondsToHoldTemperature);
								}
							}

							progressStatus.Progress0To1 = printerConnection.SecondsToHoldTemperature / printerConnection.TimeToHoldTemperature;
							reporter.Report(progressStatus);
							Thread.Sleep(20);
						}

						return Task.CompletedTask;
					},
					taskActions: new RunningTaskOptions()
					{
						PauseAction = () => UiThread.RunOnIdle(() =>
						{
							paused = true;
							printerConnection.TimeHaveBeenHoldingTemperature.Stop();
						}),
						PauseToolTip = "Pause automatic heater shutdown".Localize(),
						ResumeAction = () => UiThread.RunOnIdle(() =>
						{
							paused = false;
							printerConnection.TimeHaveBeenHoldingTemperature.Start();
						}),
						ResumeToolTip = "Resume automatic heater shutdown".Localize(),
						StopAction = (abortCancel) => UiThread.RunOnIdle(() =>
						{
							printerConnection.TurnOffBedAndExtruders(TurnOff.Now);
						}),
						StopToolTip = "Immediately turn off heaters".Localize()
					});
				}
			}
		}

		public void Shutdown()
		{
			// Ensure all threads shutdown gracefully on close

			// Release any waiting generator threads
			this.Thumbnails.Shutdown();

			// Kill all long running tasks (this will release the slicing thread if running)
			foreach (var task in Tasks.RunningTasks)
			{
				task.CancelTask();
			}
		}

		private static readonly Dictionary<NamedTypeFace, TypeFace> TypeFaceCache = new Dictionary<NamedTypeFace, TypeFace>()
		{
			[NamedTypeFace.Liberation_Sans] = LiberationSansFont.Instance,
			[NamedTypeFace.Liberation_Sans_Bold] = LiberationSansBoldFont.Instance,
			[NamedTypeFace.Liberation_Mono] = TypeFace.LoadFrom(StaticData.Instance.ReadAllText(Path.Combine("Fonts", "LiberationMono.svg")))
		};

		private static object locker = new object();

		public static TypeFace GetTypeFace(NamedTypeFace namedTypeFace)
		{
			lock (locker)
			{
				if (!TypeFaceCache.ContainsKey(namedTypeFace))
				{
					var typeFace = new TypeFace();
					var path = Path.Combine("Fonts", $"{namedTypeFace}.ttf");
					var exists = StaticData.Instance.FileExists(path);
					var stream = exists ? StaticData.Instance.OpenStream(path) : null;
					if (stream != null
						&& typeFace.LoadTTF(stream))
					{
						TypeFaceCache.Add(namedTypeFace, typeFace);
					}
					else
					{
						// try the svg
						path = Path.Combine("Fonts", $"{namedTypeFace}.svg");
						exists = StaticData.Instance.FileExists(path);
						typeFace = exists ? TypeFace.LoadFrom(StaticData.Instance.ReadAllText(path)) : null;
						if (typeFace != null)
						{
							TypeFaceCache.Add(namedTypeFace, typeFace);
						}
						else
						{
							// assign it to the default
							TypeFaceCache.Add(namedTypeFace, TypeFaceCache[NamedTypeFace.Liberation_Sans]);
						}
					}

					stream?.Dispose();
				}

				return TypeFaceCache[namedTypeFace];
			}
		}

		private static TypeFace titilliumTypeFace = null;

		public static TypeFace TitilliumTypeFace
		{
			get
			{
				if (titilliumTypeFace == null)
				{
					titilliumTypeFace = TypeFace.LoadFrom(StaticData.Instance.ReadAllText(Path.Combine("Fonts", "TitilliumWeb-Black.svg")));
				}

				return titilliumTypeFace;
			}
		}

		public static string LoadCachedFile(string cacheKey, string cacheScope)
		{
			string cachePath = CacheablePath(cacheScope, cacheKey);

			if (File.Exists(cachePath))
			{
				// Load from cache and deserialize
				return File.ReadAllText(cachePath);
			}

			return null;
		}

		public static Task<T> LoadCacheableAsync<T>(string cacheKey, string cacheScope, string staticDataFallbackPath = null) where T : class
		{
			if (LoadCachedFile(cacheKey, cacheScope) is string cachedFile)
			{
				// Load from cache and deserialize
				return Task.FromResult(
					JsonConvert.DeserializeObject<T>(cachedFile));
			}

			try
			{
				if (staticDataFallbackPath != null
					&& StaticData.Instance.FileExists(staticDataFallbackPath))
				{
					return Task.FromResult(
						JsonConvert.DeserializeObject<T>(StaticData.Instance.ReadAllText(staticDataFallbackPath)));
				}
			}
			catch
			{
			}

			return Task.FromResult(default(T));
		}

		// Requests fresh content from online services, falling back to cached content if offline
		public static async Task<T> LoadCacheableAsync<T>(string cacheKey, string cacheScope, Func<Task<T>> collector, string staticDataFallbackPath = null) where T : class
		{
			string cachePath = CacheablePath(cacheScope, cacheKey);

			try
			{
				// Try to update the document
				T item = await collector();
				if (item != null)
				{
					// update cache on success
					File.WriteAllText(cachePath, JsonConvert.SerializeObject(item, Formatting.Indented));
					return item;
				}
			}
			catch
			{
				// Fall back to preexisting cache if failed
			}

			return await LoadCacheableAsync<T>(cacheKey, cacheScope, staticDataFallbackPath);
		}

		public static string CacheablePath(string cacheScope, string cacheKey)
		{
			string scopeDirectory = Path.Combine(ApplicationDataStorage.Instance.CacheDirectory, cacheScope);

			// Ensure directory exists
			Directory.CreateDirectory(scopeDirectory);

			return Path.Combine(scopeDirectory, cacheKey);
		}

		// Indicates if given file can be opened on the design surface
		public bool IsLoadableFile(string filePath)
		{
			string extension = Path.GetExtension(filePath).ToLower();
			string extensionWithoutPeriod = extension.Trim('.');

			return !string.IsNullOrEmpty(extension)
				&& (ApplicationSettings.OpenDesignFileParams.Contains(extension)
					|| this.Library.ContentProviders.Keys.Contains(extensionWithoutPeriod));
		}

		public bool IsReloading { get; private set; } = false;

		private GuiWidget reloadingOverlay;

		public async Task ReloadAll()
		{
			try
			{
#if DEBUG
				StaticData.Instance.PurgeCache();
#endif

				this.IsReloading = true;

				var theme = ApplicationController.Instance.Theme;
				SingleWindowProvider.SetWindowTheme(theme.TextColor,
					theme.DefaultFontSize - 1,
					() => theme.CreateSmallResetButton(),
					theme.ToolbarPadding,
					theme.TabBarBackground,
					new Color(theme.PrimaryAccentColor, 175));

				reloadingOverlay = new GuiWidget
				{
					HAnchor = HAnchor.Stretch,
					VAnchor = VAnchor.Stretch,
					BackgroundColor = this.Theme.DarkShade
				};

				reloadingOverlay.AddChild(new TextWidget("Reloading".Localize() + "...", textColor: Color.White, pointSize: this.Theme.DefaultFontSize * 1.5)
				{
					HAnchor = HAnchor.Center,
					VAnchor = VAnchor.Center
				});

				AppContext.RootSystemWindow.AddChild(reloadingOverlay);

				await Task.Delay(50);

				GuiWidget.LayoutCount = 0;

				using (new QuickTimer($"ReloadAll_{reloadCount++}:"))
				{
					MainView = new MainViewWidget(this.Theme);
					this.DoneReloadingAll?.CallEvents(null, null);

					using (new QuickTimer("Time to AddMainview: "))
					{
						AppContext.RootSystemWindow.CloseChildren();
						AppContext.RootSystemWindow.AddChild(MainView);
					}
				}
			}
			catch (Exception ex)
			{
				reloadingOverlay?.CloseOnIdle();

				UiThread.RunOnIdle(() =>
				{
					StyledMessageBox.ShowMessageBox("An unexpected error occurred during reload".Localize() + ": \n\n" + ex.Message, "Reload Failed".Localize());
				});
			}
			finally
			{
				this.IsReloading = false;
			}

			Debug.WriteLine($"LayoutCount: {GuiWidget.LayoutCount:0.0}");
		}

		private static int reloadCount = 0;

		public void OnApplicationClosed()
		{
			this.Thumbnails.Shutdown();

			ApplicationSettings.Instance.ReleaseClientToken();
		}

		public static ApplicationController Instance
		{
			get
			{
				if (globalInstance == null)
				{
					globalInstance = new ApplicationController();
				}

				return globalInstance;
			}
		}

		public bool SwitchToWorkspaceIfAlreadyOpen(string assetPath)
        {
			var mainViewWidget = Instance.MainView;
			foreach (var openWorkspace in Instance.Workspaces)
			{
				if (openWorkspace.SceneContext.EditContext.SourceFilePath == assetPath
					|| (openWorkspace.SceneContext.EditContext.SourceItem is IAssetPath cloudItem
						&& cloudItem.AssetPath == assetPath))
				{
					foreach (var tab in mainViewWidget.TabControl.AllTabs)
					{
						if (tab.TabContent is DesignTabPage tabContent
							&& (tabContent.sceneContext.EditContext.SourceFilePath == assetPath
								|| (tabContent.sceneContext.EditContext.SourceItem is IAssetPath cloudItem2
									&& cloudItem2.AssetPath == assetPath)))
						{
							mainViewWidget.TabControl.ActiveTab = tab;
							return true;
						}
					}
				}
			}

			return false;
		}

		public DragDropData DragDropData { get; set; } = new DragDropData();

		private string _uiHint = "";
		/// <summary>
		/// Set or get the current ui hint for the thing the mouse is over
		/// </summary>
		public string UiHint
		{
			get => _uiHint;
			
			set
			{
				if (_uiHint != value)
				{
					_uiHint = value;
					UiHintChanged?.Invoke(this, null);
				}
			}
		}

		public event EventHandler UiHintChanged;

		public string ShortProductName
		{
			get
			{
				if (this.IsMatterControlPro())
				{
					return "MatterControl Pro";
				}

				return "MatterControl";
			}
		}

		public string ProductName
		{
			get
			{
				if (this.IsMatterControlPro())
				{
					return "MatterControl Pro Edition";
				}

				return "MatterHackers: MatterControl";
			}
		}

		public void SwitchToPurchasedLibrary()
		{
			var purchasedContainer = Library.RootLibaryContainer.ChildContainers.Where(c => c.ID == "LibraryProviderPurchasedKey").FirstOrDefault();
			if (purchasedContainer != null)
			{
				// TODO: Navigate to purchased container
				throw new NotImplementedException("SwitchToPurchasedLibrary");
			}
		}

		public void OnLoadActions()
		{
			if (AssetObject3D.AssetManager == null)
			{
				AssetObject3D.AssetManager = new AssetManager();
			}
		}

		private static void RunSetupIfRequired()
		{
			if (!ProfileManager.Instance.ActiveProfiles.Any())
			{
				// Start the setup wizard if no profiles exist
				UiThread.RunOnIdle(() => DialogWindow.Show(PrinterSetup.GetBestStartPage()));
			}
		}

		public void SwitchToSharedLibrary()
		{
			// Switch to the shared library
			var libraryContainer = Library.RootLibaryContainer.ChildContainers.Where(c => c.ID == "LibraryProviderSharedKey").FirstOrDefault();
			if (libraryContainer != null)
			{
				// TODO: Navigate to purchased container
				throw new NotImplementedException("SwitchToSharedLibrary");
			}
		}

		public void ChangeCloudSyncStatus(bool userAuthenticated, string reason = "")
		{
			UserSettings.Instance.set(UserSettingsKey.CredentialsInvalid, userAuthenticated ? "false" : "true");
			UserSettings.Instance.set(UserSettingsKey.CredentialsInvalidReason, userAuthenticated ? "" : reason);

			CloudSyncStatusChanged.CallEvents(this, new CloudSyncEventArgs() { IsAuthenticated = userAuthenticated });

			if (!string.IsNullOrEmpty(AuthenticationData.Instance.ActiveSessionUsername)
				&& AuthenticationData.Instance.ActiveSessionUsername != AuthenticationData.Instance.LastSessionUsername)
			{
				AuthenticationData.Instance.LastSessionUsername = AuthenticationData.Instance.ActiveSessionUsername;
			}

			// TODO: Unclear why we'd reload on status change - it seems like this state should be managed entirely from ProfileManager and removed from this location
			ProfileManager.ReloadActiveUser();
		}

		public Stream LoadHttpAsset(string url)
		{
			string fingerPrint = ToSHA1(url);
			string cachePath = ApplicationController.CacheablePath("HttpAssets", fingerPrint);

			if (File.Exists(cachePath))
			{
				return File.Open(cachePath, FileMode.Open);
			}
			else
			{
				var client = new WebClient();
				var bytes = client.DownloadData(url);

				File.WriteAllBytes(cachePath, bytes);

				return new MemoryStream(bytes);
			}
		}

		public async Task<PrinterConfig> LoadPrinter(string printerID)
		{
			var printer = this.ActivePrinters.FirstOrDefault(p => p.Settings.ID == printerID);
			if (printer == null)
			{
				if (!string.IsNullOrEmpty(printerID)
					&& ProfileManager.Instance[printerID] != null)
				{
					printer = new PrinterConfig(await ProfileManager.LoadSettingsAsync(printerID));
				}
			}

			if (printer != null
				&& printer.Settings.PrinterSelected
				&& printer.Settings.GetValue<bool>(SettingsKey.auto_connect))
			{
				printer.Connection.Connect();
			}

			return printer;
		}

		public async Task<PrinterConfig> OpenEmptyPrinter(string printerID, bool addPhilToBed = false)
		{
			if (!string.IsNullOrEmpty(printerID)
				&& ProfileManager.Instance[printerID] != null)
			{
				var printer = await this.LoadPrinter(printerID);

				// Add workspace for printer
				var workspace = new PartWorkspace(printer);
				var history = this.Library.PlatingHistory;

				await workspace.SceneContext.LoadContent(new EditContext()
				{
					ContentStore = history,
					SourceItem = history.NewBedPlate(workspace.Printer.Bed)
				},
				null);

				this.OpenWorkspace(workspace);

				if (addPhilToBed)
                {
					workspace.SceneContext.AddPhilToBed();
				}

				return printer;
			}

			return null;
		}

		public void OpenPrinter(PrinterInfo printerInfo)
		{
			if (this.ActivePrinters.FirstOrDefault(p => p.Settings.ID == printerInfo.ID) is PrinterConfig printer
				&& this.MainView.TabControl.AllTabs.FirstOrDefault(t => t.TabContent is PrinterTabPage printerTabPage && printerTabPage.Printer == printer) is ITab tab)
			{
				// Switch to existing printer tab
				this.MainView.TabControl.ActiveTab = tab;
			}
			else
			{
				// Open new printer tab
				this.OpenEmptyPrinter(printerInfo.ID).ConfigureAwait(false);
			}
		}

		public void OpenWorkspace(PartWorkspace workspace)
		{
			this.OpenWorkspace(workspace, WorkspacesChangedEventArgs.OperationType.Add);
		}

		public void OpenWorkspace(PartWorkspace workspace, WorkspacesChangedEventArgs.OperationType operationType)
		{
			this.Workspaces.Add(workspace);
			this.OnWorkspacesChanged(workspace, operationType);
		}

		public void RestoreWorkspace(PartWorkspace workspace)
		{
			this.OpenWorkspace(workspace, WorkspacesChangedEventArgs.OperationType.Restore);
		}

		private string loadedUserTabs = null;

		public async Task RestoreUserTabs()
		{
			// Prevent reload of loaded user
			if (loadedUserTabs == ProfileManager.Instance.UserName)
			{
				return;
			}

			restoringWorkspaces = true;

			loadedUserTabs = ProfileManager.Instance.UserName;

			var history = this.Library.PlatingHistory;

			Workspaces.Clear();

			if (File.Exists(ProfileManager.Instance.OpenTabsPath))
			{
				try
				{
					string openTabsText = File.ReadAllText(ProfileManager.Instance.OpenTabsPath);
					var persistedWorkspaces = JsonConvert.DeserializeObject<List<PartWorkspace>>(
						openTabsText,
						new ContentStoreConverter(),
						new LibraryItemConverter());

					var loadedPrinters = new HashSet<string>();

					await Tasks.Execute(
						"Restoring".Localize() + "...",
						null,
						async (reporter, cancellationTokenSource) =>
						{
							var progressStatus = new ProgressStatus();
							for (int i=0; i<persistedWorkspaces.Count; i++)
							{
								var persistedWorkspace = persistedWorkspaces[i];
								try
								{
									// Load the actual workspace if content file exists
									if (File.Exists(persistedWorkspace.ContentPath))
									{
										string printerID = persistedWorkspace.PrinterID;

										PartWorkspace workspace = null;

										if (!string.IsNullOrEmpty(printerID)
											&& ProfileManager.Instance[printerID] != null)
										{
											// Only create one workspace per printer
											if (!loadedPrinters.Contains(printerID))
											{
												// Add workspace for printer
												workspace = new PartWorkspace(await this.LoadPrinter(persistedWorkspace.PrinterID));

												loadedPrinters.Add(printerID);
											}
											else
											{
												// Ignore additional workspaces for the same printer once one is loaded
												continue;
											}
										}
										else
										{
											// Add workspace for part
											workspace = new PartWorkspace(new BedConfig(history));
										}

										// Load the previous content
										await workspace.SceneContext.LoadContent(new EditContext()
										{
											ContentStore = history,
											SourceItem = new FileSystemFileItem(persistedWorkspace.ContentPath)
										},
										(progress, message) =>
										{
											var ratioPerWorkspace = 1.0 / persistedWorkspaces.Count;
											var completed = ratioPerWorkspace * i;
											progressStatus.Progress0To1 = completed + progress * ratioPerWorkspace;
											progressStatus.Status = message;
											reporter.Report(progressStatus);
										});

										this.RestoreWorkspace(workspace);
									}
								}
								catch
								{
									// Suppress workspace load exceptions and continue to the next workspace
								}
							}
						});
				}
				catch
				{
					// Suppress deserialization issues with opentabs.json and continue with an empty Workspaces lists
				}
			}

			// If the use does not have a workspace open and has not setup any hardware, show the startup screen
			if (this.Workspaces.Count == 0
				&& !ProfileManager.Instance.ActiveProfiles.Any()
				&& SystemWindow.AllOpenSystemWindows.Count() < 2)
			{
				UiThread.RunOnIdle(() =>
				{
					DialogWindow.Show<StartupPage>();
				});
			}

			restoringWorkspaces = false;
		}

		/// <summary>
		/// Compute hash for string encoded as UTF8
		/// </summary>
		/// <param name="s">String to be hashed</param>
		public static string ToSHA1(string s)
		{
			byte[] bytes = Encoding.UTF8.GetBytes(s);

			// var timer = Stopwatch.StartNew();
			using (var sha1 = System.Security.Cryptography.SHA1.Create())
			{
				byte[] hash = sha1.ComputeHash(bytes);
				string sHA1 = BitConverter.ToString(hash).Replace("-", string.Empty);

				// Console.WriteLine("{0} {1} {2}", SHA1, timer.ElapsedMilliseconds, filePath);
				return sHA1;
			}
		}

		/// <summary>
		/// Register the given PrintItemAction into the named section
		/// </summary>
		/// <param name="section">The section to register in</param>
		/// <param name="printItemAction">The action to register</param>
		public void RegisterLibraryAction(string section, LibraryAction printItemAction)
		{
			if (!registeredLibraryActions.TryGetValue(section, out List<LibraryAction> items))
			{
				items = new List<LibraryAction>();
				registeredLibraryActions.Add(section, items);
			}

			items.Add(printItemAction);
		}

		/// <summary>
		/// Enumerate the given section, returning all registered actions
		/// </summary>
		/// <param name="section">The section to enumerate</param>
		/// <returns>The registered Actions</returns>
		public IEnumerable<LibraryAction> RegisteredLibraryActions(string section)
		{
			if (registeredLibraryActions.TryGetValue(section, out List<LibraryAction> items))
			{
				return items;
			}

			return Enumerable.Empty<LibraryAction>();
		}

		public static IObject3D ClipboardItem { get; internal set; }

		public Action<ILibraryItem> ShareLibraryItem { get; set; }

		public ObservableCollection<PartWorkspace> Workspaces { get; }

		public AppViewState ViewState { get; } = new AppViewState();

		public Uri HelpArticleSource { get; set; }

		public Dictionary<string, HelpArticle> HelpArticlesByID { get; set; }

		public string MainTabKey
		{
			get => UserSettings.Instance.get(UserSettingsKey.MainTabKey);
			set => UserSettings.Instance.set(UserSettingsKey.MainTabKey, value);
		}

		public static List<StartupAction> StartupActions { get; } = new List<StartupAction>();

		public static List<StartupTask> StartupTasks { get; } = new List<StartupTask>();

		public static Type ServicesStatusType { get; set; }

		/// <summary>
		/// Gets a value indicating whether any ActivePrinter is running a print task, either in paused or printing states
		/// </summary>
		public bool AnyPrintTaskRunning => this.ActivePrinters.Any(p => p.Connection.Printing || p.Connection.Paused || p.Connection.CommunicationState == CommunicationStates.PreparingToPrint);

		public event EventHandler<ApplicationTopBarCreatedEventArgs> ApplicationTopBarCreated;

		public void NotifyPrintersTabRightElement(GuiWidget sourceExentionArea)
		{
			ApplicationTopBarCreated?.Invoke(this, new ApplicationTopBarCreatedEventArgs(sourceExentionArea));

			// after adding content to the right side make sure we hold the space in the tab bar
			var leftChild = sourceExentionArea.Parent.Children.First();
			var padding = leftChild.Padding;
			leftChild.Padding = new BorderDouble(padding.Left, padding.Bottom, sourceExentionArea.Width, padding.Height);
		}

		public async Task PrintPart(EditContext editContext, PrinterConfig printer, IProgress<ProgressStatus> reporter, CancellationToken cancellationToken, PrinterConnection.PrintingModes printingMode)
		{
			var partFilePath = editContext.SourceFilePath;
			var gcodeFilePath = await editContext.GCodeFilePath(printer);
			var printItemName = editContext.SourceItem.Name;

			// Exit if called in a non-applicable state
			if (printer.Connection.CommunicationState != CommunicationStates.Connected
				&& printer.Connection.CommunicationState != CommunicationStates.FinishedPrint)
			{
				return;
			}

			try
			{
				if (PrinterCalibrationWizard.SetupRequired(printer, requiresLoadedFilament: true))
				{
					UiThread.RunOnIdle(() =>
					{
						DialogWindow.Show(
							new PrinterCalibrationWizard(printer, AppContext.Theme),
							advanceToIncompleteStage: true);
					});

					return;
				}

				printer.Connection.PrintingItemName = printItemName;

				var errors = new List<ValidationError>();
				printer.ValidateSettings(errors, validatePrintBed: !printer.Bed.EditContext.IsGGCodeSource);
				if (errors.Any(e => e.ErrorLevel == ValidationErrorLevel.Error))
				{
					this.ShowValidationErrors("Validation Error".Localize(), errors);
				}
				else // there are no errors continue printing
				{
					// clear the output cache prior to starting a print
					printer.Connection.TerminalLog.Clear();

					string hideGCodeWarning = ApplicationSettings.Instance.get(ApplicationSettingsKey.HideGCodeWarning);

					if (Path.GetExtension(partFilePath).ToUpper() == ".GCODE")
					{
						if (hideGCodeWarning != "true")
						{
							var hideGCodeWarningCheckBox = new CheckBox("Don't remind me again".Localize())
							{
								TextColor = this.Theme.TextColor,
								Margin = new BorderDouble(top: 6, left: 6),
								HAnchor = Agg.UI.HAnchor.Left
							};
							hideGCodeWarningCheckBox.Click += (sender, e) =>
							{
								if (hideGCodeWarningCheckBox.Checked)
								{
									ApplicationSettings.Instance.set(ApplicationSettingsKey.HideGCodeWarning, "true");
								}
								else
								{
									ApplicationSettings.Instance.set(ApplicationSettingsKey.HideGCodeWarning, null);
								}
							};

							UiThread.RunOnIdle(() =>
							{
								StyledMessageBox.ShowMessageBox(
									(messageBoxResponse) =>
									{
										if (messageBoxResponse)
										{
											printer.Connection.CommunicationState = CommunicationStates.PreparingToPrint;
											this.ArchiveAndStartPrint(partFilePath, gcodeFilePath, printer, printingMode);
										}
									},
									"The file you are attempting to print is a GCode file.\n\nIt is recommended that you only print Gcode files known to match your printer's configuration.\n\nAre you sure you want to print this GCode file?".Localize(),
									"Warning - GCode file".Localize(),
									new GuiWidget[]
									{
										hideGCodeWarningCheckBox
									},
									StyledMessageBox.MessageType.YES_NO);
							});
						}
						else
						{
							printer.Connection.CommunicationState = CommunicationStates.PreparingToPrint;
							this.ArchiveAndStartPrint(partFilePath, gcodeFilePath, printer, printingMode);
						}
					}
					else
					{
						printer.Connection.CommunicationState = CommunicationStates.PreparingToPrint;

						(bool slicingSucceeded, string finalPath) = await this.SliceItemLoadOutput(
							printer,
							printer.Bed.Scene,
							gcodeFilePath);

						// Only start print if slicing completed
						if (slicingSucceeded)
						{
							this.ArchiveAndStartPrint(partFilePath, finalPath, printer, printingMode);
						}
						else
						{
							// TODO: Need to reset printing state? This seems like I shouldn't own this indicator
							printer.Connection.CommunicationState = CommunicationStates.Connected;
						}
					}
				}
			}
			catch (Exception)
			{
			}
		}

		public void ShowValidationErrors(string windowTitle, List<ValidationError> errors)
		{
			UiThread.RunOnIdle(() =>
			{
				var dialogPage = new DialogPage("Close".Localize())
				{
					HAnchor = HAnchor.Stretch,
					WindowTitle = windowTitle,
					HeaderText = "Action Required".Localize()
				};

				dialogPage.ContentRow.AddChild(new ValidationErrorsPanel(errors, AppContext.Theme)
				{
					HAnchor = HAnchor.Stretch
				});

				DialogWindow.Show(dialogPage);
			});
		}

		public void ResetTranslationMap()
		{
			LoadTranslationMap();
		}

		public static void LoadTranslationMap()
		{
			// Select either the user supplied language name or the current thread language name
			string twoLetterIsoLanguageName = string.IsNullOrEmpty(UserSettings.Instance.Language) ?
				Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName.ToLower() :
				UserSettings.Instance.Language.ToLower();

			if (twoLetterIsoLanguageName == "ja"
				|| twoLetterIsoLanguageName == "zh")
			{
				AggContext.DefaultFont = ApplicationController.GetTypeFace(NamedTypeFace.Firefly_Sung);
				AggContext.DefaultFontBold = ApplicationController.GetTypeFace(NamedTypeFace.Firefly_Sung);
				AggContext.DefaultFontItalic = ApplicationController.GetTypeFace(NamedTypeFace.Firefly_Sung);
				AggContext.DefaultFontBoldItalic = ApplicationController.GetTypeFace(NamedTypeFace.Firefly_Sung);
			}
			else
			{
				AggContext.DefaultFont = LiberationSansFont.Instance;
				AggContext.DefaultFontBold = LiberationSansBoldFont.Instance;
				AggContext.DefaultFontItalic = LiberationSansFont.Instance;
				AggContext.DefaultFontBoldItalic = LiberationSansBoldFont.Instance;
			}

			string machineTranslation = Path.Combine("Translations", twoLetterIsoLanguageName, "Translation.txt");
			string humanTranslation = Path.Combine("Translations", twoLetterIsoLanguageName, "override.txt");

			if (twoLetterIsoLanguageName == "en")
			{
				machineTranslation = Path.Combine("Translations", "Master.txt");
				humanTranslation = null;
			}

			if (StaticData.Instance.FileExists(machineTranslation))
			{
				StreamReader humanTranlationReader = null;

				if (humanTranslation != null
					&& StaticData.Instance.FileExists(humanTranslation))
				{
					var humanTranslationStream = StaticData.Instance.OpenStream(humanTranslation);
					humanTranlationReader = new StreamReader(humanTranslationStream);
				}

				var machineTranslationStream = StaticData.Instance.OpenStream(machineTranslation);
				var machineTranlationReader = new StreamReader(machineTranslationStream);
				TranslationMap.ActiveTranslationMap = new TranslationMap(machineTranlationReader, humanTranlationReader, twoLetterIsoLanguageName);

				machineTranlationReader.Close();
				humanTranlationReader?.Close();
			}
			else
            {
				TranslationMap.ActiveTranslationMap = new TranslationMap(twoLetterIsoLanguageName);
			}
		}

		public void MonitorPrintTask(PrinterConfig printer)
		{
			this.Tasks.Execute(
				"Printing".Localize(),
				printer,
				(reporterB, cancellationTokenB) =>
				{
					var progressStatus = new ProgressStatus();
					reporterB.Report(progressStatus);

					return Task.Run(() =>
					{
						string printing = "Printing".Localize();
						int totalLayers = printer.Connection.TotalLayersInPrint;

						while (!printer.Connection.Printing
							&& !cancellationTokenB.IsCancellationRequested)
						{
							// Wait for printing
							Thread.Sleep(200);
						}

						while ((printer.Connection.Printing || printer.Connection.Paused)
							&& !cancellationTokenB.IsCancellationRequested)
						{
							var layerCount = printer.Bed.LoadedGCode == null ? "?" : printer.Bed.LoadedGCode.LayerCount.ToString();
							progressStatus.Status = $"{printing} ({printer.Connection.CurrentlyPrintingLayer + 1} of {layerCount}) - {printer.Connection.PercentComplete:0}%";

							progressStatus.Progress0To1 = printer.Connection.PercentComplete / 100;
							reporterB.Report(progressStatus);
							Thread.Sleep(200);
						}
					});
				},
				taskActions: new RunningTaskOptions()
				{
					ExpansionSerializationKey = $"{nameof(MonitorPrintTask)}_expanded",
					RichProgressWidget = () => PrinterTabPage.PrintProgressWidget(printer, this.Theme),
					PauseAction = () => UiThread.RunOnIdle(() =>
					{
						printer?.Connection.TerminalLog.WriteLine("User Requested Pause");
						printer.Connection.RequestPause();
					}),
					IsPaused = () =>
					{
						return printer.Connection.Paused || printer.Connection.WaitingToPause;
					},
					PauseToolTip = "Pause Print".Localize(),
					PauseText = "Pause".Localize(),
					ResumeAction = () => UiThread.RunOnIdle(() =>
					{
						printer.Connection.Resume();
					}),
					ResumeToolTip = "Resume Print".Localize(),
					ResumeText = "Resume".Localize(),
					StopAction = (abortCancel) => UiThread.RunOnIdle(() =>
					{
						printer.CancelPrint(abortCancel);
					}),
					StopToolTip = "Cancel Print".Localize(),
					StopText = "Stop".Localize(),
				});
		}

		private static PluginManager pluginManager = null;
        private bool restoringWorkspaces;

        public static PluginManager Plugins
		{
			get
			{
				// PluginManager initialization must occur late, after the config is loaded and after localization libraries
				// have occurred, which currently is driven by MatterControlApplication init
				if (pluginManager == null)
				{
					pluginManager = new PluginManager();
				}

				return pluginManager;
			}
		}

		public bool Allow32BitReSlice { get; set; }
		public Action<bool> KeepAwake { get; set; }

		/// <summary>
		/// Archives MCX and validates GCode results before starting a print operation
		/// </summary>
		/// <param name="sourcePath">The source file which originally caused the slice->print operation</param>
		/// <param name="gcodeFilePath">The resulting GCode to print</param>
		private async void ArchiveAndStartPrint(string sourcePath, string gcodeFilePath, PrinterConfig printer, PrinterConnection.PrintingModes printingMode)
		{
			if (File.Exists(sourcePath)
				&& File.Exists(gcodeFilePath))
			{
				bool originalIsGCode = Path.GetExtension(sourcePath).ToUpper() == ".GCODE";
				if (File.Exists(gcodeFilePath))
				{
					// Create archive point for printing attempt
					if (Path.GetExtension(sourcePath).ToUpper() == ".MCX")
					{
						string now = "Workspace " + DateTime.Now.ToString("yyyy-MM-dd HH_mm_ss");
						string archivePath = Path.Combine(ApplicationDataStorage.Instance.PrintHistoryPath, now + ".zip");

						string settingsFilePath = ProfileManager.Instance.ProfilePath(printer.Settings.ID);

						// if the printer was deleted while printing the path can be null
						if (settingsFilePath != null)
						{
							using (var file = File.OpenWrite(archivePath))
							{
								using (var zip = new ZipArchive(file, ZipArchiveMode.Create))
								{
									zip.CreateEntryFromFile(sourcePath, "PrinterPlate.mcx");
									zip.CreateEntryFromFile(settingsFilePath, printer.PrinterName + ".printer");
									zip.CreateEntryFromFile(gcodeFilePath, "sliced.gcode");
								}
							}
						}
					}

					if (originalIsGCode)
					{
						await printer.Connection.StartPrint(gcodeFilePath, printingMode: printingMode);

						MonitorPrintTask(printer);

						return;
					}
					else
					{
						// Ask for slicer specific gcode validation
						if (printer.Settings.Slicer.ValidateFile(gcodeFilePath))
						{
							await printer.Connection.StartPrint(gcodeFilePath, printingMode: printingMode);
							MonitorPrintTask(printer);
							return;
						}
					}
				}

				printer.Connection.CommunicationState = CommunicationStates.Connected;
			}
		}

		/// <summary>
		/// Slice the given IObject3D to the target GCode file using the referenced printer settings
		/// </summary>
		/// <param name="printer">The printer/settings to use</param>
		/// <param name="object3D">The IObject3D to slice</param>
		/// <param name="gcodeFilePath">The path to write the file to</param>
		/// <returns>A boolean indicating if the slicing operation completed without aborting</returns>
		public async Task<(bool, string)> SliceItemLoadOutput(PrinterConfig printer, IObject3D object3D, string gcodeFilePath)
		{
			// Slice
			bool slicingSucceeded = false;

			printer.ViewState.SlicingItem = true;

			await this.Tasks.Execute("Slicing".Localize(), printer, async (reporter, cancellationTokenSource) =>
			{
				slicingSucceeded = await Slicer.SliceItem(
					object3D,
					gcodeFilePath,
					printer,
					reporter,
					cancellationTokenSource.Token);
			});

			printer.ViewState.SlicingItem = false;

			// Skip loading GCode output if slicing failed
			if (!slicingSucceeded)
			{
				return (false, gcodeFilePath);
			}

			var postProcessors = printer.Bed.Scene.Children.OfType<IGCodePostProcessor>();
			if (postProcessors.Any())
			{
				using (var resultStream = File.OpenRead(gcodeFilePath))
				{
					Stream contextStream = resultStream;

					// Execute each post processor
					foreach (var processor in postProcessors)
					{
						// Invoke the processor and store the resulting output to the context stream reference
						contextStream = processor.ProcessOutput(contextStream);

						// Reset to the beginning
						contextStream.Position = 0;
					}

					// Modify final file name
					gcodeFilePath = Path.ChangeExtension(gcodeFilePath, GCodeFile.PostProcessedExtension);

					// Copy the final stream to the revised gcodeFilePath
					using (var finalStream = File.OpenWrite(gcodeFilePath))
					{
						contextStream.CopyTo(finalStream);
					}
				}
			}

			await this.Tasks.Execute("Loading GCode".Localize(), printer, (innerProgress, concelationTokenSource) =>
			{
				var status = new ProgressStatus();

				innerProgress.Report(status);

				printer.Bed.LoadActiveSceneGCode(gcodeFilePath, concelationTokenSource.Token, (progress0to1, statusText) =>
				{
					UiThread.RunOnIdle(() =>
					{
						status.Progress0To1 = progress0to1;
						status.Status = statusText;

						innerProgress.Report(status);
					});
				});

				if (printer.Bed.LoadedGCode is GCodeMemoryFile gcodeMemoryFile)
				{
					// try to validate the gcode file and warn if it seems invalid.
					// for now the definition of invalid is that it has a print time of < 30 seconds
					var estimatedPrintSeconds = gcodeMemoryFile.EstimatedPrintSeconds();
					if (estimatedPrintSeconds < 30)
					{
						var message = "The time to print this G-Code is estimated to be {0} seconds.\n\nPlease check your part for errors if this is unexpected."
							.Localize()
							.FormatWith((int)estimatedPrintSeconds);
						UiThread.RunOnIdle(() =>
						{
							StyledMessageBox.ShowMessageBox(message, "Warning, very short print".Localize());
						});
					}
				}

				// Switch to the 3D layer view if on Model view and slicing succeeded
				if (printer.ViewState.ViewMode == PartViewMode.Model)
				{
					printer.ViewState.ViewMode = PartViewMode.Layers3D;
				}

				return Task.CompletedTask;
			});

			return (slicingSucceeded, gcodeFilePath);
		}

		public void ShellOpenFile(string file)
		{
			UiThread.RunOnIdle(() =>
			{
				ShellFileOpened?.Invoke(this, file);
				AppContext.RootSystemWindow.BringToFront();
			});
		}

		private void KeepAwakeIfNeeded()
		{
			KeepAwake?.Invoke(AnyPrintTaskRunning);
		}

		public void Connection_PrintStarted(object sender, EventArgs e)
		{
			KeepAwakeIfNeeded();
			AnyPrintStarted?.Invoke(sender, e);
		}

		public void Connection_PrintFinished(object sender, (string printerName, string itemName) e)
		{
			if (sender is PrinterConnection printerConnection)
			{
				var activePrintTask = printerConnection.ActivePrintTask;
				switch (printerConnection.PrintingMode)
				{
					case PrinterConnection.PrintingModes.Normal:
						var printTasks = PrintHistoryData.Instance.GetHistoryItems(10);
						var printHistoryEditor = new PrintHistoryEditor(((PrinterConnection)sender).Printer, AppContext.Theme, activePrintTask, printTasks);
						printHistoryEditor.CollectInfoPrintFinished();
						break;

					case PrinterConnection.PrintingModes.Calibration:
						break;

					case PrinterConnection.PrintingModes.Autopilot:
						break;
				}
			}

			KeepAwakeIfNeeded();
			AnyPrintComplete?.Invoke(sender, null);
		}

		public void Connection_PrintCanceled(object sender, EventArgs e)
		{
			if (sender is PrinterConnection printerConnection
				&& printerConnection.PrintingMode == PrinterConnection.PrintingModes.Normal)
			{
				var printTasks = PrintHistoryData.Instance.GetHistoryItems(10);
				var printHistoryEditor = new PrintHistoryEditor(((PrinterConnection)sender).Printer, AppContext.Theme, printerConnection.CanceledPrintTask, printTasks);
				printHistoryEditor.CollectInfoPrintCanceled();
			}

			KeepAwakeIfNeeded();
			AnyPrintCanceled?.Invoke(sender, e);
		}

		public void ConnectToPrinter(PrinterConfig printer)
		{
			if (!printer.Settings.PrinterSelected)
			{
				return;
			}

			bool listenForConnectFailed = true;
			long connectStartMs = UiThread.CurrentTimerMs;

			void Connection_Failed(object s, EventArgs e)
			{
#if !__ANDROID__
				// TODO: Someday this functionality should be revised to an awaitable Connect() call in the Connect button that
				// shows troubleshooting on failed attempts, rather than hooking the failed event and trying to determine if the
				// Connect button started the task
				if (listenForConnectFailed
					&& UiThread.CurrentTimerMs - connectStartMs < 25000)
				{
					UiThread.RunOnIdle(() =>
					{
						// User initiated connect attempt failed, show port selection dialog
						DialogWindow.Show(new SetupStepComPortOne(printer));
					});
				}
#endif
				ClearEvents();
			}

			void Connection_Succeeded(object s, EventArgs e)
			{
				ClearEvents();
			}

			void ClearEvents()
			{
				listenForConnectFailed = false;

				printer.Connection.ConnectionFailed -= Connection_Failed;
				printer.Connection.ConnectionSucceeded -= Connection_Succeeded;
			}

			printer.Connection.ConnectionFailed += Connection_Failed;
			printer.Connection.ConnectionSucceeded += Connection_Succeeded;

			if (AppContext.Platform.HasPermissionToDevice(printer))
			{
				printer.Connection.HaltConnectionThread();
				printer.Connection.Connect();
			}
		}

		/// <summary>
		/// Replace invalid filename characters with the given replacement value to ensure working paths for the current filesystem
		/// </summary>
		/// <param name="name">The filename name to consider</param>
		/// <param name="replacementCharacter">The replacement character to use</param>
		/// <returns>A sanitized file name that is safe to use on the current system</returns>
		public string SanitizeFileName(string name, string replacementCharacter = "_")
		{
			if (string.IsNullOrEmpty(name))
			{
				return name;
			}

			string invalidChars = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
			string invalidRegStr = string.Format(@"([{0}]*\.+$)|([{0}]+)", invalidChars);

			return Regex.Replace(name, invalidRegStr, replacementCharacter);
		}

		public ChromeTab ActivateHelpTab(string guideKey)
		{
			var tabControl = this.MainView.TabControl;
			var theme = AppContext.Theme;

			var helpDocsTab = tabControl.AllTabs.FirstOrDefault(t => t.Key == "HelpDocs") as ChromeTab;
			if (helpDocsTab == null)
			{
				var helpTreePanel = new HelpTreePanel(theme, guideKey)
				{
					HAnchor = HAnchor.Stretch,
					VAnchor = VAnchor.Stretch
				};

				var icon = StaticData.Instance.LoadIcon("help_page.png", 16, 16).SetToColor(theme.TextColor);

				helpDocsTab = new ChromeTab("HelpDocs", "Help".Localize(), tabControl, helpTreePanel, theme, icon)
				{
					MinimumSize = new Vector2(0, theme.TabButtonHeight),
					Name = "Help Tab",
				};

				tabControl.AddTab(helpDocsTab);
			}
			else
			{

			}

			tabControl.ActiveTab = helpDocsTab;

			return helpDocsTab;
		}

		public class CloudSyncEventArgs : EventArgs
		{
			public bool IsAuthenticated { get; set; }
		}

		public class StartupTask
		{
			public string Title { get; set; }

			public int Priority { get; set; }

			public Func<IProgress<ProgressStatus>, CancellationTokenSource, Task> Action { get; set; }
		}

		public class StartupAction
		{
			public string Title { get; set; }

			public int Priority { get; set; }

			public Action Action { get; set; }
		}
	}
}