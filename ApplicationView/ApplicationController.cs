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
	using MatterHackers.MatterControl.ConfigurationPage.PrintLeveling;
	using MatterHackers.MatterControl.Library;
	using MatterHackers.MatterControl.PartPreviewWindow;
	using MatterHackers.MatterControl.PartPreviewWindow.View3D;
	using MatterHackers.MatterControl.PrinterControls.PrinterConnections;
	using MatterHackers.MatterControl.SimplePartScripting;
	using MatterHackers.MeshVisualizer;
	using MatterHackers.SerialPortCommunication;
	using MatterHackers.VectorMath;
	using SettingsManagement;

	public class ApplicationController
	{
		public ThemeConfig Theme { get; set; } = new ThemeConfig();

		// A list of printers which are open (i.e. displaying a tab) on this instance of MatterControl
		public IEnumerable<PrinterConfig> ActivePrinters { get; } = new List<PrinterConfig>();

		private static PrinterConfig emptyPrinter = new PrinterConfig(false, PrinterSettings.Empty);

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

		/// <summary>
		/// Allows application components to hook initial SystemWindow Load event without an existing Widget instance
		/// </summary>
		public static event EventHandler Load;

		public static Func<string, Task<Dictionary<string, string>>> GetProfileHistory;

		private readonly static object thumbsLock = new object();

		private Queue<Func<Task>> queuedThumbCallbacks = new Queue<Func<Task>>();

		public void SetActivePrinter(PrinterConfig printer, bool allowChangedEvent = true)
		{
			var initialPrinter = this.ActivePrinter;
			if (initialPrinter?.Settings.ID != printer.Settings.ID)
			{
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

				if (!MatterControlApplication.IsLoading)
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

				if (!MatterControlApplication.IsLoading
					&& printer.Settings.PrinterSelected
					&& printer.Settings.GetValue<bool>(SettingsKey.auto_connect))
				{
					UiThread.RunOnIdle(() =>
					{
						printer.Settings.printer.Connection.Connect(false);
					}, 2);
				}

			}
		}

		internal void ClearActivePrinter()
		{
			this.ActivePrinter = emptyPrinter;
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

			while(!MatterControlApplication.Instance.ApplicationExiting)
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
				catch (ThreadAbortException)
				{
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

		public ApplicationView MainView;

		public event EventHandler ApplicationClosed;

		private EventHandler unregisterEvents;

		private Dictionary<string, List<PrintItemAction>> registeredLibraryActions = new Dictionary<string, List<PrintItemAction>>();

		private List<SceneSelectionOperation> registeredSceneOperations = new List<SceneSelectionOperation>()
		{
			{
				"Make Support".Localize(),
				(scene) => scene.SelectedItem.OutputType = PrintOutputTypes.Support
			},
			{
				"Subtract".Localize(),
				(scene) =>
				{
					var difference = new MeshWrapperOperation(scene.SelectedItem.Children)
					{
						ActiveEditor = nameof(SubtractEditor),
						Name = "Subtract",
					};
					scene.SelectedItem.Children.Modify((list) =>
					{
						list.Clear();
					});
					scene.Children.Add(difference);
					scene.SelectedItem = difference;
				}
			},
			{
				"Intersect".Localize(),
				(scene) =>
				{
					var intersection = new MeshWrapperOperation(scene.SelectedItem.Children)
					{
						ActiveEditor = nameof(IntersectionEditor),
						Name = "Intersect",
					};
					scene.SelectedItem.Children.Modify((list) =>
					{
						list.Clear();
					});

					scene.Children.Add(intersection);
					scene.SelectedItem = intersection;
				}
			},
#if DEBUG // keep this work in progress to the editor for now
			{
				"Paint Material".Localize(),
				(scene) =>
				{
					var materialPaint = new MeshWrapperOperation(scene.SelectedItem.Children)
					{
						ActiveEditor = nameof(PaintMaterialEditor),
						Name = "Material Paint",
					};
					scene.SelectedItem.Children.Modify((list) =>
					{
						list.Clear();
					});
					scene.Children.Add(materialPaint);
					scene.SelectedItem = materialPaint;
				}
			},
			{
				"Bend".Localize(),
				(scene) => new BendOperation(scene.SelectedItem)
			},
			{
				"Cut Out".Localize(), (scene) => Console.WriteLine("Cut out")
			},
			{
				// Should be a pinch command that makes a pinch object with the correct controls
				"Pinch".Localize(), (scene) => scene.UndoBuffer.AddAndDo(new GroupCommand(scene, scene.SelectedItem))
			}
#endif
		};

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

		public LibraryConfig Library { get; }

		private void InitializeLibrary()
		{
			if (Directory.Exists(ApplicationDataStorage.Instance.DownloadsDirectory))
			{
				this.Library.RegisterRootProvider(
					new DynamicContainerLink(
						"Downloads".Localize(),
						LibraryProviderHelpers.LoadInvertIcon("FileDialog", "download_folder.png"),
						() => new FileSystemContainer(ApplicationDataStorage.Instance.DownloadsDirectory)
						{
							UseIncrementedNameDuringTypeChange = true
						}));
			}

			this.Library.RegisterRootProvider(
				new DynamicContainerLink(
					"Calibration Parts".Localize(),
					LibraryProviderHelpers.LoadInvertIcon("FileDialog", "folder.png"),
					() => new CalibrationPartsContainer())
				{
					IsReadOnly = true
				});

			this.Library.RegisterRootProvider(
				new DynamicContainerLink(
					"Print Queue".Localize(),
					LibraryProviderHelpers.LoadInvertIcon("FileDialog", "queue_folder.png"),
					() => new PrintQueueContainer()));

			var rootLibraryCollection = Datastore.Instance.dbSQLite.Table<PrintItemCollection>().Where(v => v.Name == "_library").Take(1).FirstOrDefault();
			if (rootLibraryCollection != null)
			{
				this.Library.RegisterRootProvider(
					new DynamicContainerLink(
						"Local Library".Localize(),
						LibraryProviderHelpers.LoadInvertIcon("FileDialog", "library_folder.png"),
						() => new SqliteLibraryContainer(rootLibraryCollection.Id)));
			}

			this.Library.RegisterRootProvider(
				new DynamicContainerLink(
					"Print History".Localize(),
					LibraryProviderHelpers.LoadInvertIcon("FileDialog", "folder.png"),
					() => new PrintHistoryContainer())
				{
					IsReadOnly = true
				});

			if (File.Exists(ApplicationDataStorage.Instance.CustomLibraryFoldersPath))
			{
				// Add each path defined in the CustomLibraryFolders file as a new FileSystemContainerItem
				foreach (string directory in File.ReadLines(ApplicationDataStorage.Instance.CustomLibraryFoldersPath))
				{
					if (Directory.Exists(directory))
					{
						this.Library.RegisterRootProvider(
							new FileSystemContainer.DirectoryContainerLink(directory)
							{
								UseIncrementedNameDuringTypeChange = true
							});
					}
				}
			}

			this.Library.RegisterRootProvider(
				new DynamicContainerLink(
						"SD Card".Localize(),
						LibraryProviderHelpers.LoadInvertIcon("FileDialog", "sd_folder.png"),
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

		}

		public ApplicationController()
		{
			ScrollBar.DefaultMargin = new BorderDouble(right: 3);
			ScrollBar.ScrollBarWidth = 10 * GuiWidget.DeviceScale;
			DefaultThumbBackground.DefaultBackgroundColor = Color.Transparent;

			Object3D.AssetsPath = ApplicationDataStorage.Instance.LibraryAssetsPath;

			this.Library = new LibraryConfig();
			this.Library.ContentProviders.Add(new[] { "stl", "amf", "mcx" }, new MeshContentProvider());

			this.Library.ContentProviders.Add("gcode", new GCodeContentProvider());

			// Name = "MainSlidePanel";
			ActiveTheme.ThemeChanged.RegisterEvent((s, e) =>
			{
				if (!MatterControlApplication.IsLoading)
				{
					ReloadAll();
				}
			}, ref unregisterEvents);

			ActiveSliceSettings.SettingChanged.RegisterEvent((s, e) =>
			{
				if (e is StringEventArgs stringArg
					&& SliceSettingsOrganizer.SettingsData.TryGetValue(stringArg.Data, out SliceSettingData settingsData)
					&& settingsData.ReloadUiWhenChanged)
				{
					UiThread.RunOnIdle(ReloadAll);
				}
			}, ref unregisterEvents);

			// Remove consumed ClientToken from running list on shutdown
			ApplicationClosed += (s, e) =>
			{
				ApplicationSettings.Instance.ReleaseClientToken();

				// Release the waiting ThumbnailGeneration task so it can shutdown gracefully
				thumbGenResetEvent?.Set();
			};

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

			PrinterConnection.AnyCommunicationStateChanged.RegisterEvent((s, e) =>
			{
				var printerConnection = s as PrinterConnection;

				switch (printerConnection.CommunicationState)
				{
					case CommunicationStates.Printing:
						if (UserSettings.Instance.IsTouchScreen)
						{
							// TODO: This basic hook won't work with multi-tenancy. Need to lookup the passed in sender from ActivePrinters use the found instance instead of the .ActivePrinter below
							UiThread.RunOnIdle(() => PrintingWindow.Show(ApplicationController.Instance.ActivePrinter)); // HACK: We need to show the instance that's printing not the static instance
						}

						break;
				}
			}, ref unregisterEvents);

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

		private static string cacheDirectory = Path.Combine(ApplicationDataStorage.ApplicationUserDataPath, "data", "temp", "cache");

		public static string CacheablePath(string cacheScope, string cacheKey)
		{
			string scopeDirectory = Path.Combine(cacheDirectory, cacheScope);

			// Ensure directory exists
			Directory.CreateDirectory(scopeDirectory);

			string cachePath = Path.Combine(scopeDirectory, cacheKey);
			return cachePath;
		}

		// Indicates if given file can be opened on the design surface
		public bool IsLoadableFile(string filePath)
		{
			string extension = Path.GetExtension(filePath).ToLower();
			string extensionWithoutPeriod = extension.Trim('.');

			return !string.IsNullOrEmpty(extension)
				&& (ApplicationSettings.OpenDesignFileParams.Contains(extension) 
					|| ApplicationController.Instance.Library.ContentProviders.Keys.Contains(extensionWithoutPeriod));
		}

		bool pendingReloadRequest = false;
		public void ReloadAll()
		{
			if (pendingReloadRequest || MainView == null)
			{
				return;
			}

			pendingReloadRequest = true;

			UiThread.RunOnIdle(() =>
			{
				using (new QuickTimer($"ReloadAll_{reloadCount++}:"))
				{
					MainView?.CloseAllChildren();
					using (new QuickTimer("ReloadAll_AddElements"))
					{
						MainView?.CreateAndAddChildren();
					}
					this.DoneReloadingAll?.CallEvents(null, null);
				}

				pendingReloadRequest = false;
			});
		}

		static int reloadCount = 0;

		public void OnApplicationClosed()
		{
			ApplicationClosed?.Invoke(null, null);
		}

		static void LoadOemOrDefaultTheme()
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
					//using (new PerformanceTimer("Startup", "AppController Instance"))
					{
						globalInstance = new ApplicationController();

						// Set the default theme colors
						LoadOemOrDefaultTheme();

						// Accessing any property on ProfileManager will run the static constructor and spin up the ProfileManager instance
						bool na = ProfileManager.Instance.IsGuestProfile;

						globalInstance.MainView = new DesktopView();

						ActiveSliceSettings.ActivePrinterChanged.RegisterEvent((s, e) =>
						{
							if (!MatterControlApplication.IsLoading)
							{
								ApplicationController.Instance.ReloadAll();
							}
						}, ref globalInstance.unregisterEvents);
					}
				}
				return globalInstance;
			}
		}

		public DragDropData DragDropData { get; set; } = new DragDropData();

		public View3DWidget ActiveView3DWidget { get; internal set; }

		public string PrintingItemName { get; set; }

		public string ProductName => "MatterHackers: MatterControl";

		public string ThumbnailCachePath(ILibraryItem libraryItem)
		{
			// TODO: Use content SHA
			return string.IsNullOrEmpty(libraryItem.ID) ? null : ApplicationController.CacheablePath("ItemThumbnails", $"{libraryItem.ID}.png");
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
				WizardWindow.Show<CopyGuestProfilesToUser>();
			}
		}

		public void OnLoadActions()
		{
			Load?.Invoke(this, null);

			// Pushing this after load fixes that empty printer list
			ApplicationController.Instance.UserChanged();

			bool showAuthWindow = PrinterSetup.ShouldShowAuthPanel?.Invoke() ?? false;
			if (showAuthWindow)
			{
				if (ApplicationSettings.Instance.get(ApplicationSettingsKey.SuppressAuthPanel) != "True")
				{
					//Launch window to prompt user to sign in
					UiThread.RunOnIdle(() => WizardWindow.Show(PrinterSetup.GetBestStartPage()));
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

			if (AggContext.OperatingSystem == OSType.Android)
			{
				// show this last so it is on top
				if (UserSettings.Instance.get("SoftwareLicenseAccepted") != "true")
				{
					UiThread.RunOnIdle(() => WizardWindow.Show<LicenseAgreementPage>());
				}
			}

			if (this.ActivePrinter.Settings.PrinterSelected
				&& this.ActivePrinter.Settings.GetValue<bool>(SettingsKey.auto_connect))
			{
				UiThread.RunOnIdle(() =>
				{
					//PrinterConnectionAndCommunication.Instance.HaltConnectionThread();
					this.ActivePrinter.Connection.Connect();
				}, 2);
			}
		}

		private static void RunSetupIfRequired()
		{
			if (!ProfileManager.Instance.ActiveProfiles.Any())
			{
				// Start the setup wizard if no profiles exist
				UiThread.RunOnIdle(() => WizardWindow.Show(PrinterSetup.GetBestStartPage()));
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
							// So we scale as many times as we need to to get the Image to be the right size.
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

		public IEnumerable<SceneSelectionOperation> RegisteredSceneOperations()
		{
			return registeredSceneOperations;
		}

		public event EventHandler<WidgetSourceEventArgs> AddPrintersTabRightElement;

		public void NotifyPrintersTabRightElement(GuiWidget sourceExentionArea)
		{
			AddPrintersTabRightElement?.Invoke(this, new WidgetSourceEventArgs(sourceExentionArea));
		}

		private string doNotAskAgainMessage = "Don't remind me again".Localize();

		public async Task PrintPart(PrintItemWrapper printItem, PrinterConfig printer, View3DWidget view3DWidget, SliceProgressReporter reporter, bool overrideAllowGCode = false)
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
						LevelWizardBase.ShowPrintLevelWizard(ApplicationController.Instance.ActivePrinter);// HACK: We need to show the instance that's printing not the static instance
						return;
					}
				}

				// Save any pending changes before starting the print
				await ApplicationController.Instance.ActiveView3DWidget.PersistPlateIfNeeded();

				if (printItem != null)
				{
					this.PrintingItemName = printItem.Name;
					string pathAndFile = printItem.FileLocation;

					if (ActiveSliceSettings.Instance.IsValid())
					{
						if (File.Exists(pathAndFile))
						{
							// clear the output cache prior to starting a print
							this.ActivePrinter.Connection.TerminalLog.Clear();

							string hideGCodeWarning = ApplicationSettings.Instance.get(ApplicationSettingsKey.HideGCodeWarning);

							if (Path.GetExtension(pathAndFile).ToUpper() == ".GCODE"
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
										async (messageBoxResponse) =>
										{
											if (messageBoxResponse)
											{
												this.ActivePrinter.Connection.CommunicationState = CommunicationStates.PreparingToPrint;
												partToPrint_SliceDone(printItem);
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
									printItem,
									view3DWidget,
									reporter);

								partToPrint_SliceDone(printItem);
							}
						}
					}
				}
			}
			catch (Exception)
			{
			}
		}

		private void partToPrint_SliceDone(PrintItemWrapper partToPrint)
		{
			if (partToPrint != null)
			{
				string gcodePathAndFileName = partToPrint.GetGCodePathAndFileName();
				if (gcodePathAndFileName != "")
				{
					bool originalIsGCode = Path.GetExtension(partToPrint.FileLocation).ToUpper() == ".GCODE";
					if (File.Exists(gcodePathAndFileName))
					{
						// Create archive point for printing attempt
						if (Path.GetExtension(partToPrint.FileLocation).ToUpper() == ".MCX")
						{
							// TODO: We should zip mcx and settings when starting a print
							string platingDirectory = Path.Combine(ApplicationDataStorage.Instance.ApplicationLibraryDataPath, "PrintHistory");
							Directory.CreateDirectory(platingDirectory);

							string now = DateTime.Now.ToString("yyyyMMdd-HHmmss");
							string archivePath = Path.Combine(platingDirectory, now + ".zip");

							using (var file = File.OpenWrite(archivePath))
							using (var zip = new ZipArchive(file, ZipArchiveMode.Create))
							{
								zip.CreateEntryFromFile(partToPrint.FileLocation, "PrinterPlate.mcx");
								zip.CreateEntryFromFile(ActiveSliceSettings.Instance.DocumentPath, ActiveSliceSettings.Instance.GetValue(SettingsKey.printer_name) + ".printer");
								zip.CreateEntryFromFile(gcodePathAndFileName, "sliced.gcode");
							}
						}

						// read the last few k of the file and see if it says "filament used". We use this marker to tell if the file finished writing
						if (originalIsGCode)
						{
							this.ActivePrinter.Connection.StartPrint(gcodePathAndFileName);
							return;
						}
						else
						{
							int bufferSize = 32000;
							using (Stream fileStream = new FileStream(gcodePathAndFileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
							{
								byte[] buffer = new byte[bufferSize];
								fileStream.Seek(Math.Max(0, fileStream.Length - bufferSize), SeekOrigin.Begin);
								int numBytesRead = fileStream.Read(buffer, 0, bufferSize);
								fileStream.Close();

								string fileEnd = System.Text.Encoding.UTF8.GetString(buffer);
								if (fileEnd.Contains("filament used"))
								{
									this.ActivePrinter.Connection.StartPrint(gcodePathAndFileName);
									return;
								}
							}
						}
					}

					this.ActivePrinter.Connection.CommunicationState = CommunicationStates.Connected;
				}
			}
		}

		public async Task SliceFileLoadOutput(PrinterConfig printer, PrintItemWrapper printItem, View3DWidget view3DWidget, SliceProgressReporter reporter)
		{
			var gcodeLoadCancellationTokenSource = new CancellationTokenSource();

			// Save any pending changes
			await view3DWidget.PersistPlateIfNeeded();

			// Slice
			reporter?.StartReporting();
			await Slicer.SliceFileAsync(printItem, reporter);
			reporter?.EndReporting();

			// Load
			printer.Bed.LoadGCode(
				printItem.GetGCodePathAndFileName(),
				gcodeLoadCancellationTokenSource.Token,
				null);
				// TODO: use not yet implemented standard processing notification system to report GCode load
				//view3DWidget.gcodeViewer.LoadProgress_Changed);
				//SetProcessingMessage(string.Format("{0} {1:0}%...", "Loading G-Code".Localize(), progress0To1 * 100));

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
}