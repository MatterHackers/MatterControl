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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.SlicerConfiguration;
using Newtonsoft.Json;
using System.Collections.ObjectModel;

namespace MatterHackers.MatterControl
{
	using System.IO.Compression;
	using System.Net;
	using System.Reflection;
	using System.Text;
	using System.Threading;
	using Agg.Font;
	using Agg.Image;
	using CustomWidgets;
	using MatterHackers.Agg.Platform;
	using MatterHackers.DataConverters3D;
	using MatterHackers.DataConverters3D.UndoCommands;
	using MatterHackers.MatterControl.ConfigurationPage.PrintLeveling;
	using MatterHackers.MatterControl.DesignTools;
	using MatterHackers.MatterControl.Library;
	using MatterHackers.MatterControl.PartPreviewWindow;
	using MatterHackers.MatterControl.PartPreviewWindow.View3D;
	using MatterHackers.MatterControl.PrinterControls.PrinterConnections;
	using MatterHackers.SerialPortCommunication;
	using SettingsManagement;

	public class AppContext
	{
		/// <summary>
		/// Native platform features 
		/// </summary>
		public static INativePlatformFeatures Platform { get; set; }

		public static bool IsLoading { get; internal set; } = true;

		/// <summary>
		/// The root SystemWindow
		/// </summary>
		public static SystemWindow RootSystemWindow { get; internal set; }
	}

	public class ApplicationController
	{
		public ThemeConfig Theme { get; set; } = new ThemeConfig();

		public RunningTasksConfig Tasks { get; set; } = new RunningTasksConfig();

		// A list of printers which are open (i.e. displaying a tab) on this instance of MatterControl
		public IEnumerable<PrinterConfig> ActivePrinters { get; } = new List<PrinterConfig>();

		private static PrinterConfig emptyPrinter = new PrinterConfig(PrinterSettings.Empty);

		private static string cacheDirectory = Path.Combine(ApplicationDataStorage.ApplicationUserDataPath, "data", "temp", "cache");

		// TODO: Any references to this property almost certainly need to be reconsidered. ActiveSliceSettings static references that assume a single printer 
		// selection are being redirected here. This allows us to break the dependency to the original statics and consolidates
		// us down to a single point where code is making assumptions about the presence of a printer, printer counts, etc. If we previously checked for
		// PrinterConnection.IsPrinterConnected, that could should be updated to iterate ActiverPrinters, checking each one and acting on each as it would
		// have for the single case
		public PrinterConfig ActivePrinter { get; private set; } = emptyPrinter;

		public Action RedeemDesignCode;
		public Action EnterShareCode;

		private static ApplicationController globalInstance;
		public RootedObjectEventHandler CloudSyncStatusChanged = new RootedObjectEventHandler();
		public RootedObjectEventHandler DoneReloadingAll = new RootedObjectEventHandler();
		public RootedObjectEventHandler PluginsLoaded = new RootedObjectEventHandler();

		public static Action SignInAction;
		public static Action SignOutAction;

		public static Action WebRequestFailed;
		public static Action WebRequestSucceeded;

#if DEBUG
		public const string EnvironmentName = "TestEnv_";
#else
		public const string EnvironmentName = "";
#endif

		public bool ApplicationExiting { get; internal set; } = false;

		public static Func<string, Task<Dictionary<string, string>>> GetProfileHistory;

		private readonly static object thumbsLock = new object();

		private Queue<Func<Task>> queuedThumbCallbacks = new Queue<Func<Task>>();

		public async Task SetActivePrinter(PrinterConfig printer, bool allowChangedEvent = true)
		{
			var initialPrinter = this.ActivePrinter;
			if (initialPrinter?.Settings.ID != printer.Settings.ID)
			{
				// TODO: Consider if autosave is appropriate
				if (initialPrinter != emptyPrinter)
				{
					initialPrinter.Bed.Save();
				}

				// If we have an active printer, run Disable
				if (initialPrinter.Settings != PrinterSettings.Empty)
				{
					initialPrinter?.Connection?.Disable();
				}

				// ActivePrinters is IEnumerable to force us to use SetActivePrinter until it's ingrained in our pattern - cast to list since it is and we need to add
				(this.ActivePrinters as List<PrinterConfig>).Add(printer);
				this.ActivePrinter = printer;

				// TODO: Decide if non-printer contexts should prompt for a printer, if we should have a default printer, or get "ActiveTab printer" working
				// HACK: short term solution to resolve printer reference for non-printer related contexts
				DragDropData.Printer = printer;

				if (!AppContext.IsLoading)
				{
					// Fire printer changed event
				}

				BedSettings.SetMakeAndModel(
					printer.Settings.GetValue(SettingsKey.make), 
					printer.Settings.GetValue(SettingsKey.model));

				ActiveSliceSettings.SwitchToPrinterTheme();

				if (allowChangedEvent)
				{
					ActiveSliceSettings.OnActivePrinterChanged(null);
				}

				if (!AppContext.IsLoading
					&& printer.Settings.PrinterSelected
					&& printer.Settings.GetValue<bool>(SettingsKey.auto_connect))
				{
					UiThread.RunOnIdle(() =>
					{
						printer.Settings.printer.Connection.Connect();
					}, 2);
				}

			}
		}

		internal async Task ClearActivePrinter()
		{
			await this.SetActivePrinter(emptyPrinter);
		}
		public void LaunchBrowser(string targetUri)
		{
			UiThread.RunOnIdle(() =>
			{
				Process.Start(targetUri);
			});
		}

		public void RefreshActiveInstance(PrinterSettings updatedPrinterSettings)
		{
			ActivePrinter.SwapToSettings(updatedPrinterSettings);

			/*
			// TODO: Should we rebroadcast settings changed events for each settings?
			bool themeChanged = ActivePrinter.Settings.GetValue(SettingsKey.active_theme_name) != updatedProfile.GetValue(SettingsKey.active_theme_name);
			ActiveSliceSettings.SettingChanged.CallEvents(null, new StringEventArgs(SettingsKey.printer_name));

			// TODO: Decide if non-printer contexts should prompt for a printer, if we should have a default printer, or get "ActiveTab printer" working
			// HACK: short term solution to resolve printer reference for non-printer related contexts
			DragDropData.Printer = printer;
			if (themeChanged)
			{
				UiThread.RunOnIdle(ActiveSliceSettings.SwitchToPrinterTheme);
			}
			else
			{
				UiThread.RunOnIdle(ApplicationController.Instance.ReloadAdvancedControlsPanel);
			}*/
		}

		private AutoResetEvent thumbGenResetEvent = new AutoResetEvent(false);

		Task thumbnailGenerator = null;

		internal void QueueForGeneration(Func<Task> func)
		{
			lock(thumbsLock)
			{
				if (thumbnailGenerator == null)
				{
					// Spin up a new thread once needed
					thumbnailGenerator = Task.Run((Action)ThumbGeneration);
				}

				queuedThumbCallbacks.Enqueue(func);
				thumbGenResetEvent.Set();
			}
		}

		private async void ThumbGeneration()
		{
			Thread.CurrentThread.Name = $"ThumbnailGeneration";

			while(!this.ApplicationExiting)
			{
				Thread.Sleep(100);

				try
				{
					if (queuedThumbCallbacks.Count > 0)
					{
						Func<Task> callback;
						lock (thumbsLock)
						{
							callback = queuedThumbCallbacks.Dequeue();
						}

						await callback();
					}
					else
					{
						// Process until queuedThumbCallbacks is empty then wait for new tasks via QueueForGeneration 
						thumbGenResetEvent.WaitOne();
					}
				}
				catch (AppDomainUnloadedException)
				{
					return;
				}
				catch (ThreadAbortException)
				{
					return;
				}
				catch (Exception ex)
				{
					Console.WriteLine("Error generating thumbnail: " + ex.Message);
				}
			}

			// Null task reference on exit
			thumbnailGenerator = null;
		}

		public static Func<PrinterInfo,string, Task<PrinterSettings>> GetPrinterProfileAsync;
		public static Func<string, IProgress<ProgressStatus>,Task> SyncPrinterProfiles;
		public static Func<Task<OemProfileDictionary>> GetPublicProfileList;
		public static Func<string, Task<PrinterSettings>> DownloadPublicProfileAsync;

		public SlicePresetsWindow EditMaterialPresetsWindow { get; set; }

		public SlicePresetsWindow EditQualityPresetsWindow { get; set; }

		public GuiWidget MainView;

		private EventHandler unregisterEvents;

		private Dictionary<string, List<PrintItemAction>> registeredLibraryActions = new Dictionary<string, List<PrintItemAction>>();

		private List<SceneSelectionOperation> registeredSceneOperations = new List<SceneSelectionOperation>()
		{
			new SceneSelectionOperation()
			{
				TitleResolver = () => "Group".Localize(),
				Action = (scene) => scene.GroupSelection(),
				IsEnabled = (scene) => scene.HasSelection
					&& scene.SelectedItem is SelectionGroup
					&& scene.SelectedItem.Children.Count > 1,
				Icon = AggContext.StaticData.LoadIcon("group.png", 16, 16).SetPreMultiply(),
			},
			new SceneSelectionOperation()
			{
				TitleResolver = () => "Ungroup".Localize(),
				Action = (scene) => scene.UngroupSelection(),
				IsEnabled = (scene) => scene.HasSelection,
				Icon = AggContext.StaticData.LoadIcon("ungroup.png", 16, 16).SetPreMultiply(),
			},
			new SceneSelectionSeparator(),
			new SceneSelectionOperation()
			{
				TitleResolver = () => "Duplicate".Localize(),
				Action = (scene) => scene.DuplicateSelection(),
				IsEnabled = (scene) => scene.HasSelection,
				Icon = AggContext.StaticData.LoadIcon("duplicate.png").SetPreMultiply(),
			},
			new SceneSelectionOperation()
			{
				TitleResolver = () => "Remove".Localize(),
				Action = (scene) => scene.DeleteSelection(),
				IsEnabled = (scene) => scene.HasSelection,
				Icon = AggContext.StaticData.LoadIcon("remove.png").SetPreMultiply(),
			},
			new SceneSelectionSeparator(),
			new SceneSelectionOperation()
			{
				TitleResolver = () => "Lay Flat".Localize(),
				Action = (scene) =>
				{
					if (scene.HasSelection)
					{
						scene.MakeLowestFaceFlat(scene.SelectedItem, scene.RootItem);
					}
				},
				IsEnabled = (scene) => scene.HasSelection,
				Icon = AggContext.StaticData.LoadIcon("lay_flat.png").SetPreMultiply(),
			},
			new SceneSelectionOperation()
			{
				TitleResolver = () => "Make Support".Localize(),
				Action = (scene) =>
				{
					if (scene.SelectedItem != null
						&& !scene.SelectedItem.VisibleMeshes().All(i => i.OutputType == PrintOutputTypes.Support))
					{
						scene.UndoBuffer.AddAndDo(new MakeSupport(scene.SelectedItem));
					}
				},
				Icon = AggContext.StaticData.LoadIcon("support.png").SetPreMultiply(),
				IsEnabled = (scene) => scene.HasSelection,
			},
			new SceneSelectionSeparator(),
			new SceneSelectionOperation()
			{
				TitleResolver = () => "Combine".Localize(),
				Action = (scene) => DoMeshWrapOperation(scene, nameof(CombineEditor), "Combine"),
				Icon = AggContext.StaticData.LoadIcon("combine.png").SetPreMultiply(),
				IsEnabled = (scene) => scene.SelectedItem is SelectionGroup,
			},
			new SceneSelectionOperation()
			{
				TitleResolver = () => "Subtract".Localize(),
				Action = (scene) => DoMeshWrapOperation(scene, nameof(SubtractEditor), "Subtract"),
				Icon = AggContext.StaticData.LoadIcon("subtract.png").SetPreMultiply(),
				IsEnabled = (scene) => scene.SelectedItem is SelectionGroup,
			},
			new SceneSelectionOperation()
			{
				TitleResolver = () => "Intersect".Localize(),
				Action = (scene) => DoMeshWrapOperation(scene, nameof(IntersectionEditor), "Intersect"),
				Icon = AggContext.StaticData.LoadIcon("intersect.png"),
				IsEnabled = (scene) => scene.SelectedItem is SelectionGroup,
			},
			new SceneSelectionOperation()
			{
				TitleResolver = () => "Subtract & Replace".Localize(),
				Action = (scene) => DoMeshWrapOperation(scene, nameof(SubtractAndReplace), "Subtract & Replace"),
				Icon = AggContext.StaticData.LoadIcon("paint.png").SetPreMultiply(),
				IsEnabled = (scene) => scene.SelectedItem is SelectionGroup,
			},
#if DEBUG // keep this work in progress to the editor for now
			new SceneSelectionSeparator(),
			new SceneSelectionOperation()
			{
				TitleResolver = () => "Package".Localize(),
				Action = (scene) =>
				{
					scene.WrapSelection(new Package());
				},
				IsEnabled = (scene) => scene.HasSelection,
			},
			new SceneSelectionOperation()
			{
				TitleResolver = () => "Proportional Scale".Localize(),
				Action = (scene) => HoldChildProportional.AddSelectionAsChildren(scene, nameof(ProportionalEditor), "Proportional Scale"),
				//Icon = AggContext.StaticData.LoadIcon("subtract.png").SetPreMultiply(),
				IsEnabled = (scene) => scene.HasSelection,
			},
			new SceneSelectionOperation()
			{
				TitleResolver = () => "Bend".Localize(),
				Action = (scene) => new BendOperation(scene.SelectedItem),
				IsEnabled = (scene) => scene.HasSelection,
			},
			new SceneSelectionOperation()
			{
				// Should be a pinch command that makes a pinch object with the correct controls
				TitleResolver = () => "Pinch".Localize(),
				Action = (scene) => scene.UndoBuffer.AddAndDo(new GroupCommand(scene, scene.SelectedItem)),
				IsEnabled = (scene) => scene.HasSelection,
			}
#endif
		};

		private static void DoMeshWrapOperation(InteractiveScene scene, string classDescriptor, string editorName)
		{
			if (scene.HasSelection && scene.SelectedItem.Children.Count() > 1)
			{
				var children = scene.SelectedItem.Children;
				scene.SelectedItem = null;

				var meshWrapperOperation = new MeshWrapperOperation(new List<IObject3D>(children.Select((i) => i.Clone())))
				{
					ActiveEditor = classDescriptor,
					Name = editorName,
				};

				scene.UndoBuffer.AddAndDo(
					new ReplaceCommand(
						new List<IObject3D>(children),
						new List<IObject3D> { meshWrapperOperation }));

				meshWrapperOperation.MakeNameNonColliding();
				scene.SelectedItem = meshWrapperOperation;
			}
		}

		static int applicationInstanceCount = 0;
		public static int ApplicationInstanceCount
		{
			get
			{
				if (applicationInstanceCount == 0)
				{
					Assembly mcAssembly = Assembly.GetEntryAssembly();
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

		public event EventHandler ShowHelpChanged;

		public bool ShowHelpControls
		{
			get => UserSettings.Instance.get(UserSettingsKey.SliceSettingsShowHelp) == "true";
			set
			{
				UserSettings.Instance.set(UserSettingsKey.SliceSettingsShowHelp, value.ToString().ToLower());
				ShowHelpChanged?.Invoke(null, null);
			}
		}

		public LibraryConfig Library { get; }

		private void InitializeLibrary()
		{
			if (Directory.Exists(ApplicationDataStorage.Instance.DownloadsDirectory))
			{
				this.Library.RegisterContainer(
					new DynamicContainerLink(
						() => "Downloads".Localize(),
						AggContext.StaticData.LoadIcon(Path.Combine("FileDialog", "download_folder.png")),
						() => new FileSystemContainer(ApplicationDataStorage.Instance.DownloadsDirectory)
						{
							UseIncrementedNameDuringTypeChange = true
						}));
			}

			this.Library.RegisterContainer(
				new DynamicContainerLink(
					() => "Calibration Parts".Localize(),
					AggContext.StaticData.LoadIcon(Path.Combine("FileDialog", "folder.png")),
					() => new CalibrationPartsContainer())
				{
					IsReadOnly = true
				});

			this.Library.RegisterContainer(
				new DynamicContainerLink(
					() => "Print Queue".Localize(),
					AggContext.StaticData.LoadIcon(Path.Combine("FileDialog", "queue_folder.png")),
					() => new PrintQueueContainer()));

			var rootLibraryCollection = Datastore.Instance.dbSQLite.Table<PrintItemCollection>().Where(v => v.Name == "_library").Take(1).FirstOrDefault();
			if (rootLibraryCollection != null)
			{
				this.Library.RegisterContainer(
					new DynamicContainerLink(
						() => "Local Library".Localize(),
						AggContext.StaticData.LoadIcon(Path.Combine("FileDialog", "library_folder.png")),
						() => new SqliteLibraryContainer(rootLibraryCollection.Id)));
			}

			this.Library.RegisterContainer(
				new DynamicContainerLink(
					() => "Print History".Localize(),
					AggContext.StaticData.LoadIcon(Path.Combine("FileDialog", "folder.png")),
					() => new PrintHistoryContainer())
				{
					IsReadOnly = true
				});

			if (File.Exists(ApplicationDataStorage.Instance.CustomLibraryFoldersPath))
			{
				// Add each path defined in the CustomLibraryFolders file as a new FileSystemContainerItem
				foreach (string directory in File.ReadLines(ApplicationDataStorage.Instance.CustomLibraryFoldersPath))
				{
					//if (Directory.Exists(directory))
					{
						this.Library.RegisterContainer(
							new FileSystemContainer.DirectoryContainerLink(directory)
							{
								UseIncrementedNameDuringTypeChange = true
							});
					}
				}
			}

			this.Library.RegisterContainer(
				new DynamicContainerLink(
						() => "SD Card".Localize(),
						AggContext.StaticData.LoadIcon(Path.Combine("FileDialog", "sd_folder.png")),
						() => new SDCardContainer(),
						() =>
						{
							var printer = this.ActivePrinter;

							return printer.Settings.GetValue<bool>(SettingsKey.has_sd_card_reader)
								&& printer.Connection.PrinterIsConnected
								&& !(printer.Connection.PrinterIsPrinting || printer.Connection.PrinterIsPaused);
						})
				{
					IsReadOnly = true
				});

			this.Library.PlatingHistory = new PlatingHistoryContainer();

			this.Library.RegisterContainer(
				new DynamicContainerLink(
					() => "Plating History".Localize(),
					AggContext.StaticData.LoadIcon(Path.Combine("FileDialog", "folder.png")),
					() => ApplicationController.Instance.Library.PlatingHistory));
		}

		public ApplicationController()
		{
			ScrollBar.DefaultMargin = new BorderDouble(right: 1);
			ScrollBar.ScrollBarWidth = 8 * GuiWidget.DeviceScale;
			ScrollBar.GrowThumbBy = 2;

			// Initialize statics
			DefaultThumbBackground.DefaultBackgroundColor = Color.Transparent;
			Object3D.AssetsPath = ApplicationDataStorage.Instance.LibraryAssetsPath;

			this.Library = new LibraryConfig();
			this.Library.ContentProviders.Add(new[] { "stl", "obj", "amf", "mcx" }, new MeshContentProvider());
			this.Library.ContentProviders.Add("gcode", new GCodeContentProvider());

			// Name = "MainSlidePanel";
			ActiveTheme.ThemeChanged.RegisterEvent((s, e) =>
			{
				if (!AppContext.IsLoading)
				{
					ReloadAll();
				}
			}, ref unregisterEvents);

			ActiveSliceSettings.SettingChanged.RegisterEvent((s, e) =>
			{
				if (e is StringEventArgs stringArg
					&& SettingsOrganizer.SettingsData.TryGetValue(stringArg.Data, out SliceSettingData settingsData)
					&& settingsData.ReloadUiWhenChanged)
				{
					UiThread.RunOnIdle(ReloadAll);
				}
			}, ref unregisterEvents);

			PrinterConnection.ErrorReported.RegisterEvent((s, e) =>
			{
				var foundStringEventArgs = e as FoundStringEventArgs;
				if (foundStringEventArgs != null)
				{
					string message = "Your printer is reporting a hardware Error. This may prevent your printer from functioning properly.".Localize()
						+ "\n"
						+ "\n"
						+ "Error Reported".Localize() + ":"
						+ $" \"{foundStringEventArgs.LineToCheck}\".";
					UiThread.RunOnIdle(() =>
						StyledMessageBox.ShowMessageBox(message, "Printer Hardware Error".Localize())
					);
				}
			}, ref unregisterEvent);

			this.InitializeLibrary();

			PrinterConnection.AnyConnectionSucceeded.RegisterEvent((s, e) =>
			{
				// run the print leveling wizard if we need to for this printer
				var printer = ApplicationController.Instance.ActivePrinters.Where(p => p.Connection == s).FirstOrDefault();
				if (printer != null
					&& (printer.Settings.GetValue<bool>(SettingsKey.print_leveling_required_to_print)
					|| printer.Settings.GetValue<bool>(SettingsKey.print_leveling_enabled)))
				{
					PrintLevelingData levelingData = printer.Settings.Helpers.GetPrintLevelingData();
					if (levelingData?.HasBeenRunAndEnabled() != true)
					{
						UiThread.RunOnIdle(() => LevelWizardBase.ShowPrintLevelWizard(printer));
					}
				}
			}, ref unregisterEvents);
		}

		internal void Shutdown()
		{
			// Ensure all threads shutdown gracefully on close

			// Release any waiting generator threads
			thumbGenResetEvent?.Set();
		}

		private static TypeFace monoSpacedTypeFace = null;
		public static TypeFace MonoSpacedTypeFace
		{
			get
			{
				if (monoSpacedTypeFace == null)
				{
					monoSpacedTypeFace = TypeFace.LoadFrom(AggContext.StaticData.ReadAllText(Path.Combine("Fonts", "LiberationMono.svg")));
				}

				return monoSpacedTypeFace;
			}
		}

		private static TypeFace titilliumTypeFace = null;
		public static TypeFace TitilliumTypeFace
		{
			get
			{
				if (titilliumTypeFace == null)
				{
					titilliumTypeFace = TypeFace.LoadFrom(AggContext.StaticData.ReadAllText(Path.Combine("Fonts", "TitilliumWeb-Black.svg")));
				}

				return titilliumTypeFace;
			}
		}

		private static TypeFace damionTypeFace = null;
		public static TypeFace DamionTypeFace
		{
			get
			{
				if (damionTypeFace == null)
				{
					damionTypeFace = TypeFace.LoadFrom(AggContext.StaticData.ReadAllText(Path.Combine("Fonts", "Damion-Regular.svg")));
				}

				return damionTypeFace;
			}
		}

		public static Task<T> LoadCacheableAsync<T>(string cacheKey, string cacheScope, string staticDataFallbackPath = null) where T : class
		{
			string cachePath = CacheablePath(cacheScope, cacheKey);

			try
			{
				if (File.Exists(cachePath))
				{
					// Load from cache and deserialize
					return Task.FromResult(
						JsonConvert.DeserializeObject<T>(File.ReadAllText(cachePath)));
				}
			}
			catch
			{
				// Fall back to StaticData
			}

			try
			{
				if (staticDataFallbackPath != null
					&& AggContext.StaticData.FileExists(staticDataFallbackPath))
				{
					return Task.FromResult(
						JsonConvert.DeserializeObject<T>(AggContext.StaticData.ReadAllText(staticDataFallbackPath)));
				}
			}
			catch
			{
			}

			return Task.FromResult(default(T));
		}

		/// <summary>
		/// Requests fresh content from online services, falling back to cached content if offline
		/// </summary>
		/// <param name="collector">The custom collector function to load the content</param>
		/// <returns></returns>
		public async static Task<T> LoadCacheableAsync<T>(string cacheKey, string cacheScope, Func<Task<T>> collector, string staticDataFallbackPath = null) where T : class
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
			string scopeDirectory = Path.Combine(cacheDirectory, cacheScope);

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

		public void ReloadAll()
		{
			var reloadingOverlay = new GuiWidget
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

			this.IsReloading = true;

			UiThread.RunOnIdle(() =>
			{
				using (new QuickTimer($"ReloadAll_{reloadCount++}:"))
				{
					MainView = new WidescreenPanel();
					this.DoneReloadingAll?.CallEvents(null, null);

					using (new QuickTimer("Time to AddMainview: "))
					{
						AppContext.RootSystemWindow.CloseAllChildren();
						AppContext.RootSystemWindow.AddChild(MainView);
					}
				}

				this.IsReloading = false;
			});
		}

		static int reloadCount = 0;

		public void OnApplicationClosed()
		{
			// Release the waiting ThumbnailGeneration task so it can shutdown gracefully
			thumbGenResetEvent?.Set();

			// Save changes before close
			if (this.ActivePrinter != null
				&& this.ActivePrinter != emptyPrinter)
			{
				this.ActivePrinter.Bed.Save();
			}

			ApplicationSettings.Instance.ReleaseClientToken();
		}

		internal static void LoadOemOrDefaultTheme()
		{
			// if not check for the oem color and use it if set
			// else default to "Blue - Light"
			string oemColor = OemSettings.Instance.ThemeColor;
			if (string.IsNullOrEmpty(oemColor))
			{
				ActiveTheme.Instance = ActiveTheme.GetThemeColors("Blue - Light");
			}
			else
			{
				ActiveTheme.Instance = ActiveTheme.GetThemeColors(oemColor);
			}
		}

		public static ApplicationController Instance
		{
			get
			{
				if (globalInstance == null)
				{
					globalInstance = new ApplicationController();

					ActiveSliceSettings.ActivePrinterChanged.RegisterEvent((s, e) =>
					{
						if (!AppContext.IsLoading)
						{
							ApplicationController.Instance.ReloadAll();
						}
					}, ref globalInstance.unregisterEvents);
				}
				return globalInstance;
			}
		}

		public DragDropData DragDropData { get; set; } = new DragDropData();

		public View3DWidget ActiveView3DWidget { get; internal set; }

		public string PrintingItemName { get; set; }

		public string ShortProductName => "MatterControl";
		public string ProductName => "MatterHackers: MatterControl";

		public string ThumbnailCachePath(ILibraryItem libraryItem)
		{
			// TODO: Use content SHA
			return string.IsNullOrEmpty(libraryItem.ID) ? null : ApplicationController.CacheablePath("ItemThumbnails", $"{libraryItem.ID}.png");
		}

		public string ThumbnailCachePath(ILibraryItem libraryItem, int width, int height)
		{
			// TODO: Use content SHA
			return string.IsNullOrEmpty(libraryItem.ID) ? null : ApplicationController.CacheablePath("ItemThumbnails", $"{libraryItem.ID}-{width}x{height}.png");
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
			// TODO: Calling UserChanged seems wrong. Load the right user before we spin up controls, rather than after
			// Pushing this after load fixes that empty printer list
			/////////////////////ApplicationController.Instance.UserChanged();

			bool showAuthWindow = PrinterSetup.ShouldShowAuthPanel?.Invoke() ?? false;
			if (showAuthWindow)
			{
				if (ApplicationSettings.Instance.get(ApplicationSettingsKey.SuppressAuthPanel) != "True")
				{
					//Launch window to prompt user to sign in
					UiThread.RunOnIdle(() => DialogWindow.Show(PrinterSetup.GetBestStartPage()));
				}
			}
			else
			{
				//If user in logged in sync before checking to prompt to create printer
				if (ApplicationController.SyncPrinterProfiles == null)
				{
					RunSetupIfRequired();
				}
				else
				{
					ApplicationController.SyncPrinterProfiles.Invoke("ApplicationController.OnLoadActions()", null).ContinueWith((task) =>
					{
						RunSetupIfRequired();
					});
				}
			}

			// TODO: This should be moved into the splash screen and shown instead of MainView
			if (AggContext.OperatingSystem == OSType.Android)
			{
				// show this last so it is on top
				if (UserSettings.Instance.get("SoftwareLicenseAccepted") != "true")
				{
					UiThread.RunOnIdle(() => DialogWindow.Show<LicenseAgreementPage>());
				}
			}

			if (ApplicationController.Instance.ActivePrinter is PrinterConfig printer
				&& printer.Settings.PrinterSelected
				&& printer.Settings.GetValue<bool>(SettingsKey.auto_connect))
			{
				UiThread.RunOnIdle(() =>
				{
					//PrinterConnectionAndCommunication.Instance.HaltConnectionThread();
					printer.Connection.Connect();
				}, 2);
			}

			//HtmlWindowTest();
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

			// Only fire UserChanged if it actually happened - prevents runaway positive feedback loop
			if (!string.IsNullOrEmpty(AuthenticationData.Instance.ActiveSessionUsername)
				&& AuthenticationData.Instance.ActiveSessionUsername != AuthenticationData.Instance.LastSessionUsername)
			{
				// only set it if it is an actual user name
				AuthenticationData.Instance.LastSessionUsername = AuthenticationData.Instance.ActiveSessionUsername;
			}

			UserChanged();
		}

		// Called after every startup and at the completion of every authentication change
		public void UserChanged()
		{
			ProfileManager.ReloadActiveUser();

			// Ensure SQLite printers are imported
			ProfileManager.Instance.EnsurePrintersImported();

			var guest = ProfileManager.Load("guest");

			// If profiles.json was created, run the import wizard to pull in any SQLite printers
			if (guest?.Profiles?.Any() == true
				&& !ProfileManager.Instance.IsGuestProfile 
				&& !ProfileManager.Instance.PrintersImported)
			{
				// Show the import printers wizard
				DialogWindow.Show<CopyGuestProfilesToUser>();
			}
		}

		private EventHandler unregisterEvent;

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

		public string ComputeFileSha1(string filePath)
		{
			using (var stream = File.OpenRead(filePath))
			{
				return GenerateSha1(stream);
			}
		}

		private string GenerateSha1(Stream stream)
		{
			// var timer = Stopwatch.StartNew();
			using (var sha1 = System.Security.Cryptography.SHA1.Create())
			{
				byte[] hash = sha1.ComputeHash(stream);
				string SHA1 = BitConverter.ToString(hash).Replace("-", String.Empty);

				// Console.WriteLine("{0} {1} {2}", SHA1, timer.ElapsedMilliseconds, filePath);
				return SHA1;
			}
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
				string SHA1 = BitConverter.ToString(hash).Replace("-", string.Empty);

				// Console.WriteLine("{0} {1} {2}", SHA1, timer.ElapsedMilliseconds, filePath);
				return SHA1;
			}
		}


		/// <summary>
		/// Download an image from the web into the specified ImageBuffer
		/// </summary>
		/// <param name="uri"></param>
		public void DownloadToImageAsync(ImageBuffer imageToLoadInto, string uriToLoad, bool scaleToImageX, IRecieveBlenderByte scalingBlender = null)
		{
			if (scalingBlender == null)
			{
				scalingBlender = new BlenderBGRA();
			}

			WebClient client = new WebClient();
			client.DownloadDataCompleted += (object sender, DownloadDataCompletedEventArgs e) =>
			{
				try // if we get a bad result we can get a target invocation exception. In that case just don't show anything
				{
					// scale the loaded image to the size of the target image
					byte[] raw = e.Result;
					Stream stream = new MemoryStream(raw);
					ImageBuffer unScaledImage = new ImageBuffer(10, 10);
					if (scaleToImageX)
					{
						AggContext.StaticData.LoadImageData(stream, unScaledImage);
						// If the source image (the one we downloaded) is more than twice as big as our dest image.
						while (unScaledImage.Width > imageToLoadInto.Width * 2)
						{
							// The image sampler we use is a 2x2 filter so we need to scale by a max of 1/2 if we want to get good results.
							// So we scale as many times as we need to get the Image to be the right size.
							// If this were going to be a non-uniform scale we could do the x and y separately to get better results.
							ImageBuffer halfImage = new ImageBuffer(unScaledImage.Width / 2, unScaledImage.Height / 2, 32, scalingBlender);
							halfImage.NewGraphics2D().Render(unScaledImage, 0, 0, 0, halfImage.Width / (double)unScaledImage.Width, halfImage.Height / (double)unScaledImage.Height);
							unScaledImage = halfImage;
						}

						double finalScale = imageToLoadInto.Width / (double)unScaledImage.Width;
						imageToLoadInto.Allocate(imageToLoadInto.Width, (int)(unScaledImage.Height * finalScale), imageToLoadInto.Width * (imageToLoadInto.BitDepth / 8), imageToLoadInto.BitDepth);
						imageToLoadInto.NewGraphics2D().Render(unScaledImage, 0, 0, 0, finalScale, finalScale);
					}
					else
					{
						AggContext.StaticData.LoadImageData(stream, imageToLoadInto);
					}

					imageToLoadInto.MarkImageChanged();
				}
				catch
				{
				}
			};

			try
			{
				client.DownloadDataAsync(new Uri(uriToLoad));
			}
			catch
			{
			}
		}

		/// <summary>
		/// Cancels prints within the first two minutes or interactively prompts the user to confirm cancellation
		/// </summary>
		/// <returns>A boolean value indicating if the print was canceled</returns>
		public bool ConditionalCancelPrint()
		{
			bool canceled = false;

			if (this.ActivePrinter.Connection.SecondsPrinted > 120)
			{
				StyledMessageBox.ShowMessageBox(
					(bool response) =>
					{
						if (response)
						{
							UiThread.RunOnIdle(() => this.ActivePrinter.Connection.Stop());
							canceled = true;
						}

						canceled = false;
					},
					"Cancel the current print?".Localize(),
					"Cancel Print?".Localize(),
					StyledMessageBox.MessageType.YES_NO,
					"Cancel Print".Localize(),
					"Continue Printing".Localize());
			}
			else
			{
				this.ActivePrinter.Connection.Stop();
				canceled = false;
			}

			return canceled;
		}

		/// <summary>
		/// Register the given PrintItemAction into the named section
		/// </summary>
		/// <param name="section">The section to register in</param>
		/// <param name="printItemAction">The action to register</param>
		public void RegisterLibraryAction(string section, PrintItemAction printItemAction)
		{
			List<PrintItemAction> items;
			if (!registeredLibraryActions.TryGetValue(section, out items))
			{
				items = new List<PrintItemAction>();
				registeredLibraryActions.Add(section, items);
			}

			items.Add(printItemAction);
		}

		/// <summary>
		/// Enumerate the given section, returning all registered actions
		/// </summary>
		/// <param name="section">The section to enumerate</param>
		/// <returns></returns>
		public IEnumerable<PrintItemAction> RegisteredLibraryActions(string section)
		{
			List<PrintItemAction> items;
			if (registeredLibraryActions.TryGetValue(section, out items))
			{
				return items;
			}

			return Enumerable.Empty<PrintItemAction>();
		}

		public IEnumerable<SceneSelectionOperation> RegisteredSceneOperations => registeredSceneOperations;

		public event EventHandler<WidgetSourceEventArgs> AddPrintersTabRightElement;

		public void NotifyPrintersTabRightElement(GuiWidget sourceExentionArea)
		{
			AddPrintersTabRightElement?.Invoke(this, new WidgetSourceEventArgs(sourceExentionArea));
		}

		private string doNotAskAgainMessage = "Don't remind me again".Localize();

		public async Task PrintPart(string partFilePath, string gcodeFilePath, string printItemName, PrinterConfig printer, IProgress<ProgressStatus> reporter, CancellationToken cancellationToken, bool overrideAllowGCode = false)
		{
			// Exit if called in a non-applicable state
			if (this.ActivePrinter.Connection.CommunicationState != CommunicationStates.Connected
				&& this.ActivePrinter.Connection.CommunicationState != CommunicationStates.FinishedPrint)
			{
				return;
			}

			try
			{
				// If leveling is required or is currently on
				if (ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.print_leveling_required_to_print)
					|| ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.print_leveling_enabled))
				{
					PrintLevelingData levelingData = ActiveSliceSettings.Instance.Helpers.GetPrintLevelingData();
					if (levelingData?.HasBeenRunAndEnabled() != true)
					{
						LevelWizardBase.ShowPrintLevelWizard(printer);
						return;
					}
				}

				if (!string.IsNullOrEmpty(partFilePath) 
					&& File.Exists(partFilePath))
				{
					this.PrintingItemName = printItemName;

					if (ActiveSliceSettings.Instance.IsValid())
					{
						{
							// clear the output cache prior to starting a print
							this.ActivePrinter.Connection.TerminalLog.Clear();

							string hideGCodeWarning = ApplicationSettings.Instance.get(ApplicationSettingsKey.HideGCodeWarning);

							if (Path.GetExtension(partFilePath).ToUpper() == ".GCODE"
								&& hideGCodeWarning == null
								&& !overrideAllowGCode)
							{
								var hideGCodeWarningCheckBox = new CheckBox(doNotAskAgainMessage)
								{
									TextColor = ActiveTheme.Instance.PrimaryTextColor,
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
												this.ActivePrinter.Connection.CommunicationState = CommunicationStates.PreparingToPrint;
												partToPrint_SliceDone(partFilePath, gcodeFilePath);
											}
										},
										"The file you are attempting to print is a GCode file.\n\nIt is recommended that you only print Gcode files known to match your printer's configuration.\n\nAre you sure you want to print this GCode file?".Localize(),
										"Warning - GCode file".Localize(),
										new GuiWidget[]
										{
											new VerticalSpacer(),
											hideGCodeWarningCheckBox
										},
										StyledMessageBox.MessageType.YES_NO);

								});
							}
							else
							{
								this.ActivePrinter.Connection.CommunicationState = CommunicationStates.PreparingToPrint;

								await ApplicationController.Instance.SliceFileLoadOutput(
									printer,
									partFilePath,
									gcodeFilePath);

								partToPrint_SliceDone(partFilePath, gcodeFilePath);
							}

							await ApplicationController.Instance.Tasks.Execute(
								(reporterB, cancellationTokenB) =>
								{
									var progressStatus = new ProgressStatus()
									{
										Status = "Printing".Localize()
									};
									reporterB.Report(progressStatus);

									return Task.Run(() =>
									{
										string printing = "Printing".Localize();
										int totalLayers = printer.Connection.TotalLayersInPrint;

										while (!printer.Connection.PrinterIsPrinting
											&& !cancellationTokenB.IsCancellationRequested)
										{
											// Wait for printing
											Thread.Sleep(200);
										}

										while ((printer.Connection.PrinterIsPrinting || printer.Connection.PrinterIsPaused)
											&& !cancellationTokenB.IsCancellationRequested)
										{
											//progressStatus.Status = $"{printing} Layer ({printer.Connection.CurrentlyPrintingLayer } of {totalLayers})";
											progressStatus.Status = $"{printing} ({printer.Connection.CurrentlyPrintingLayer})";
											progressStatus.Progress0To1 = printer.Connection.PercentComplete / 100;
											reporterB.Report(progressStatus);
											Thread.Sleep(200);
										}
									});
								},
								taskActions: new RunningTaskActions()
								{
									RichProgressWidget = () => PrinterTabPage.PrintProgressWidget(printer),
									Pause = () => UiThread.RunOnIdle(() =>
									{
										printer.Connection.RequestPause();
									}),
									Resume = () => UiThread.RunOnIdle(() =>
									{
										printer.Connection.Resume();
									}),
									Stop = () => UiThread.RunOnIdle(() =>
									{
										ApplicationController.Instance.ConditionalCancelPrint();
									})
								});
						}
					}
				}
			}
			catch (Exception)
			{
			}
		}

		private void partToPrint_SliceDone(string partFilePath, string gcodeFilePath)
		{
			if (!string.IsNullOrEmpty(partFilePath) 
				&& File.Exists(partFilePath))
			{
				if (gcodeFilePath != "")
				{
					bool originalIsGCode = Path.GetExtension(partFilePath).ToUpper() == ".GCODE";
					if (File.Exists(gcodeFilePath))
					{
						// Create archive point for printing attempt
						if (Path.GetExtension(partFilePath).ToUpper() == ".MCX")
						{
							// TODO: We should zip mcx and settings when starting a print
							string platingDirectory = Path.Combine(ApplicationDataStorage.Instance.ApplicationLibraryDataPath, "PrintHistory");
							Directory.CreateDirectory(platingDirectory);

							string now = "Workspace " + DateTime.Now.ToString("yyyy-MM-dd HH_mm_ss");
							string archivePath = Path.Combine(platingDirectory, now + ".zip");

							using (var file = File.OpenWrite(archivePath))
							using (var zip = new ZipArchive(file, ZipArchiveMode.Create))
							{
								zip.CreateEntryFromFile(partFilePath, "PrinterPlate.mcx");
								zip.CreateEntryFromFile(ActiveSliceSettings.Instance.DocumentPath, ActiveSliceSettings.Instance.GetValue(SettingsKey.printer_name) + ".printer");
								zip.CreateEntryFromFile(gcodeFilePath, "sliced.gcode");
							}
						}

						// read the last few k of the file and see if it says "filament used". We use this marker to tell if the file finished writing
						if (originalIsGCode)
						{
							this.ActivePrinter.Connection.StartPrint(gcodeFilePath);
							return;
						}
						else
						{
							int bufferSize = 32000;
							using (Stream fileStream = new FileStream(gcodeFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
							{
								byte[] buffer = new byte[bufferSize];
								fileStream.Seek(Math.Max(0, fileStream.Length - bufferSize), SeekOrigin.Begin);
								int numBytesRead = fileStream.Read(buffer, 0, bufferSize);
								fileStream.Close();

								string fileEnd = System.Text.Encoding.UTF8.GetString(buffer);
								if (fileEnd.Contains("filament used"))
								{
									this.ActivePrinter.Connection.StartPrint(gcodeFilePath);
									return;
								}
							}
						}
					}

					this.ActivePrinter.Connection.CommunicationState = CommunicationStates.Connected;
				}
			}
		}

		public async Task SliceFileLoadOutput(PrinterConfig printer, string partFilePath, string gcodeFilePath)
		{
			// Slice
			await ApplicationController.Instance.Tasks.Execute((reporter, cancellationToken) =>
			{
				reporter.Report(new ProgressStatus() { Status = "Slicing".Localize() });

				return Slicer.SliceFile(
					partFilePath, 
					gcodeFilePath, 
					printer,
					new SliceProgressReporter(reporter, printer),
					cancellationToken);
			});
			
			await ApplicationController.Instance.Tasks.Execute((innerProgress, token) =>
			{
				var status = new ProgressStatus()
				{
					Status = "Loading GCode"
				};

				innerProgress.Report(status);

				Thread.Sleep(800);

				printer.Bed.LoadGCode(gcodeFilePath, token, (progress0to1, statusText) =>
				{
					UiThread.RunOnIdle(() =>
					{
						status.Progress0To1 = progress0to1;
						status.Status = statusText;

						innerProgress.Report(status);
					});
				});

				return Task.CompletedTask;
			});
		}

		public class CloudSyncEventArgs : EventArgs
		{
			public bool IsAuthenticated { get; set; }
		}
	}

	public class WidgetSourceEventArgs : EventArgs
	{
		public GuiWidget Source { get; }

		public WidgetSourceEventArgs(GuiWidget source)
		{
			this.Source = source;
		}
	}

	public class DragDropData
	{
		public View3DWidget View3DWidget { get; set; }
		public PrinterConfig Printer { get; internal set; }
		public BedConfig SceneContext { get; set; }

		public void Reset()
		{
			this.View3DWidget = null;
			this.SceneContext = null;
		}
	}

	public class RunningTaskDetails : IProgress<ProgressStatus>
	{
		public event EventHandler<ProgressStatus> ProgressChanged;

		public Func<GuiWidget> DetailsItemAction { get; set; }

		public RunningTaskDetails(CancellationTokenSource tokenSource)
		{
			this.tokenSource = tokenSource;
		}

		public string Title { get; set; }
		public Action<Printer> PauseAction { get; internal set; }
		public Action<Printer> ResumeAction { get; internal set; }
		public Action<Printer> StopAction { get; internal set; }
		public RunningTaskActions TaskActions { get; internal set; }

		private CancellationTokenSource tokenSource;

		public void Report(ProgressStatus progressStatus)
		{
			this.ProgressChanged?.Invoke(this, progressStatus);
		}

		public void CancelTask()
		{
			this.tokenSource.Cancel();
		}
	}

	public class RunningTaskActions
	{
		public Func<GuiWidget> RichProgressWidget { get; set; }
		public Action Pause { get; set; }
		public Action Resume { get; set; }
		public Action Stop { get; set; }
	}

	public class RunningTasksConfig
	{
		public event EventHandler TasksChanged;

		private ObservableCollection<RunningTaskDetails> executingTasks = new ObservableCollection<RunningTaskDetails>();

		public IEnumerable<RunningTaskDetails> RunningTasks => executingTasks.ToList();

		public RunningTasksConfig()
		{
			executingTasks.CollectionChanged += (s, e) =>
			{
				this.TasksChanged?.Invoke(this, null);
			};
		}

		public Task Execute(Func<IProgress<ProgressStatus>, CancellationToken, Task> func, RunningTaskActions taskActions = null)
		{
			var tokenSource = new CancellationTokenSource();

			var taskDetails = new RunningTaskDetails(tokenSource)
			{
				TaskActions = taskActions,
			};

			executingTasks.Add(taskDetails);

			return Task.Run(async () =>
			{
				try
				{
					await func?.Invoke(taskDetails, tokenSource.Token);
				}
				catch
				{

				}

				executingTasks.Remove(taskDetails);
			});
		}
	}

	public enum ReportSeverity2 { Warning, Error }

	public interface INativePlatformFeatures
	{
		event EventHandler PictureTaken;
		void TakePhoto(string imageFileName);
		void OpenCameraPreview();
		void PlaySound(string fileName);
		void ConfigureWifi();
		bool CameraInUseByExternalProcess { get; set; }
		bool IsNetworkConnected();
		void FindAndInstantiatePlugins(SystemWindow systemWindow);
		void ProcessCommandline();
		void ReportException(Exception e, string key = "", string value = "", ReportSeverity2 warningLevel = ReportSeverity2.Warning);
		void PlatformInit(Action<string> reporter);
	}

	public static class Application
	{
		private static ProgressBar progressBar;
		private static TextWidget statusText;
		private static FlowLayoutWidget progressPanel;
		private static string lastSection = "";
		private static Stopwatch timer;

		public static string PlatformFeaturesProvider { get; set; } = "MatterHackers.MatterControl.WindowsPlatformsFeatures, MatterControl";

		public static SystemWindow LoadRootWindow(int width, int height)
		{
			timer = Stopwatch.StartNew();

			var systemWindow = new RootSystemWindow(width, height)
			{
				BackgroundColor = Color.DarkGray
			};

			var overlay = new GuiWidget();
			overlay.AnchorAll();

			systemWindow.AddChild(overlay);

			progressPanel = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Center,
				VAnchor = VAnchor.Center,
				MinimumSize = new VectorMath.Vector2(400, 100),
			};
			overlay.AddChild(progressPanel);

			progressPanel.AddChild(statusText = new TextWidget("", textColor: new Color("#bbb"))
			{
				MinimumSize = new VectorMath.Vector2(200, 30)
			});

			progressPanel.AddChild(progressBar = new ProgressBar()
			{
				FillColor = new Color("#3D4B72"),
				BorderColor = new Color("#777"),
				Height = 11,
				Width = 300,
				HAnchor = HAnchor.Absolute,
				VAnchor = VAnchor.Absolute
			});

			AppContext.RootSystemWindow = systemWindow;

			// Hook SystemWindow load and spin up MatterControl once we've hit first draw
			systemWindow.Load += (s, e) =>
			{
				ReportStartupProgress(0.02, "First draw->RunOnIdle");

				//UiThread.RunOnIdle(() =>
				Task.Run(async () =>
				{
					ReportStartupProgress(0.15, "MatterControlApplication.Initialize");
					var mainView = await Initialize(systemWindow, (progress0To1, status) =>
					{
						ReportStartupProgress(0.2 + progress0To1 * 0.7, status);
					});

					ReportStartupProgress(0.9, "AddChild->MainView");
					systemWindow.AddChild(mainView, 0);

					ReportStartupProgress(1, "");
					systemWindow.BackgroundColor = Color.Transparent;
					overlay.Close();

					AppContext.IsLoading = false;
				});
			};

			// Block indefinitely
			ReportStartupProgress(0, "ShowAsSystemWindow");

			return systemWindow;
		}

		public static async Task<GuiWidget> Initialize(SystemWindow systemWindow, Action<double, string> reporter)
		{
			AppContext.Platform = AggContext.CreateInstanceFrom<INativePlatformFeatures>(PlatformFeaturesProvider);

			reporter?.Invoke(0.01, "PlatformInit");
			AppContext.Platform.PlatformInit((status) =>
			{
				reporter?.Invoke(0.01, status);
			});

			// TODO: Appears to be unused and should be removed
			// set this at startup so that we can tell next time if it got set to true in close
			UserSettings.Instance.Fields.StartCount = UserSettings.Instance.Fields.StartCount + 1;

			reporter?.Invoke(0.05, "ApplicationController");
			var na = ApplicationController.Instance;

			// Set the default theme colors
			reporter?.Invoke(0.1, "LoadOemOrDefaultTheme");
			ApplicationController.LoadOemOrDefaultTheme();

			// Accessing any property on ProfileManager will run the static constructor and spin up the ProfileManager instance
			reporter?.Invoke(0.2, "ProfileManager");
			bool na2 = ProfileManager.Instance.IsGuestProfile;

			await ProfileManager.Instance.Initialize();

			reporter?.Invoke(0.3, "MainView");
			ApplicationController.Instance.MainView = new WidescreenPanel();

			// now that we are all set up lets load our plugins and allow them their chance to set things up
			reporter?.Invoke(0.8, "Plugins");
			AppContext.Platform.FindAndInstantiatePlugins(systemWindow);
			if (ApplicationController.Instance.PluginsLoaded != null)
			{
				ApplicationController.Instance.PluginsLoaded.CallEvents(null, null);
			}

			reporter?.Invoke(0.9, "Process Commandline");
			AppContext.Platform.ProcessCommandline();

			reporter?.Invoke(0.91, "OnLoadActions");
			ApplicationController.Instance.OnLoadActions();

			UiThread.RunOnIdle(CheckOnPrinter);

			return ApplicationController.Instance.MainView;
		}

		private static void ReportStartupProgress(double progress0To1, string section)
		{
			UiThread.RunOnIdle(() =>
			{
				statusText.Text = section;
				progressBar.RatioComplete = progress0To1;
				progressPanel.Invalidate();

				Console.WriteLine($"Time to '{lastSection}': {timer.ElapsedMilliseconds}");
				timer.Restart();

				lastSection = section;
			});
		}

		private static void CheckOnPrinter()
		{
			try
			{
				// TODO: UiThread should not be driving anything in Printer.Connection
				ApplicationController.Instance.ActivePrinter.Connection.OnIdle();
			}
			catch (Exception e)
			{
				Debug.Print(e.Message);
				GuiWidget.BreakInDebugger();
#if DEBUG
				throw e;
#endif
			}
			UiThread.RunOnIdle(CheckOnPrinter);
		}
	}
}