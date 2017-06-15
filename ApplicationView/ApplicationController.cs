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
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.SlicerConfiguration;
using Newtonsoft.Json;

namespace MatterHackers.MatterControl
{
	using System.Net;
	using System.Reflection;
	using System.Threading;
	using Agg.Font;
	using Agg.Image;
	using CustomWidgets;
	using MatterHackers.DataConverters3D;
	using MatterHackers.MatterControl.Library;
	using MatterHackers.MatterControl.PartPreviewWindow;
	using MatterHackers.VectorMath;
	using PrintHistory;
	using SettingsManagement;

	public class ApplicationController
	{
		public class ThemeConfig
		{
			protected readonly int fizedHeightA = (int)(25 * GuiWidget.DeviceScale + .5);
			protected readonly int fontSizeA = 11;

			protected readonly double fizedHeightB = 52 * GuiWidget.DeviceScale;
			protected readonly int fontSizeB = 14;

			protected readonly int borderWidth = 1;

			public TextImageButtonFactory ImageButtonFactory { get; private set; }
			public TextImageButtonFactory ActionRowButtonFactory { get; private set; }
			public TextImageButtonFactory PrinterConnectButtonFactory { get; private set; }
			public TextImageButtonFactory BreadCrumbButtonFactory { get; internal set; }
			public TextImageButtonFactory BreadCrumbButtonFactorySmallMargins { get; internal set; }

			public RGBA_Bytes TabBodyBackground => new RGBA_Bytes(ActiveTheme.Instance.TertiaryBackgroundColor, 175);

			public TextImageButtonFactory ViewControlsButtonFactory { get; internal set; }
			public RGBA_Bytes SplitterBackground { get; internal set; } = new RGBA_Bytes(0, 0, 0, 60);

			private EventHandler unregisterEvents;

			static ThemeConfig()
			{
				// EnsureRestoreButtonImages
				int size = (int)(16 * GuiWidget.DeviceScale);

				if (OsInformation.OperatingSystem == OSType.Android)
				{
					restoreNormal = ColorCircle(size, new RGBA_Bytes(200, 0, 0));
				}
				else
				{
					restoreNormal = ColorCircle(size, new RGBA_Bytes(128, 128, 128));
				}
				restoreHover = ColorCircle(size, new RGBA_Bytes(200, 0, 0));
				restorePressed = ColorCircle(size, new RGBA_Bytes(255, 0, 0));
			}

			public ThemeConfig()
			{
				ActiveTheme.ThemeChanged.RegisterEvent((s, e) => RebuildTheme(), ref unregisterEvents);
				RebuildTheme();
			}

			public void RebuildTheme()
			{
				var theme = ActiveTheme.Instance;
				this.ImageButtonFactory = new TextImageButtonFactory()
				{
					normalFillColor = RGBA_Bytes.Transparent,
					normalBorderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 200),
					normalTextColor = theme.SecondaryTextColor,
					pressedTextColor = theme.PrimaryTextColor,
					hoverTextColor = theme.PrimaryTextColor,
					hoverBorderColor = new RGBA_Bytes(theme.PrimaryTextColor, 200),
					disabledFillColor = RGBA_Bytes.Transparent,
					disabledBorderColor = new RGBA_Bytes(theme.PrimaryTextColor, 100),
					disabledTextColor = new RGBA_Bytes(theme.PrimaryTextColor, 100),
					FixedHeight = fizedHeightA,
					fontSize = fontSizeA,
					borderWidth = borderWidth
				};

				this.ActionRowButtonFactory = new TextImageButtonFactory()
				{
					normalTextColor = RGBA_Bytes.White,
					disabledTextColor = RGBA_Bytes.LightGray,
					hoverTextColor = RGBA_Bytes.White,
					pressedTextColor = RGBA_Bytes.White,
					AllowThemeToAdjustImage = false,
					borderWidth = borderWidth,
					FixedHeight = fizedHeightB,
					fontSize = fontSizeB,
					normalBorderColor = new RGBA_Bytes(255, 255, 255, 100),
					hoverBorderColor = new RGBA_Bytes(255, 255, 255, 100)
				};

				this.PrinterConnectButtonFactory = new TextImageButtonFactory()
				{
					normalTextColor = theme.PrimaryTextColor,
					normalBorderColor = (theme.IsDarkTheme) ? new RGBA_Bytes(77, 77, 77) : new RGBA_Bytes(190, 190, 190),
					hoverTextColor = theme.PrimaryTextColor,
					pressedTextColor = theme.PrimaryTextColor,
					disabledTextColor = theme.TabLabelUnselected,
					disabledFillColor = theme.PrimaryBackgroundColor,
					disabledBorderColor = theme.SecondaryBackgroundColor,
					hoverFillColor = theme.PrimaryBackgroundColor,
					hoverBorderColor = new RGBA_Bytes(128, 128, 128),
					invertImageLocation = false,
					borderWidth = 1
				};

				this.BreadCrumbButtonFactory = new TextImageButtonFactory()
				{
					normalTextColor = theme.PrimaryTextColor,
					//normalFillColor = RGBA_Bytes.Blue,
					hoverTextColor = theme.PrimaryTextColor,
					pressedTextColor = theme.PrimaryTextColor,
					disabledTextColor = theme.TertiaryBackgroundColor,
					Margin = new BorderDouble(16, 0),
					borderWidth = 0,
					FixedHeight = 32,
				};

				this.BreadCrumbButtonFactorySmallMargins = new TextImageButtonFactory()
				{
					normalTextColor = theme.PrimaryTextColor,
					hoverTextColor = theme.PrimaryTextColor,
					pressedTextColor = theme.PrimaryTextColor,
					disabledTextColor = theme.TertiaryBackgroundColor,
					Margin = new BorderDouble(8, 0),
					borderWidth = 0,
					FixedHeight = 32,
				};

				int buttonHeight;
				if (UserSettings.Instance.IsTouchScreen)
				{
					buttonHeight = 40;
				}
				else
				{
					buttonHeight = 0;
				}

				this.ViewControlsButtonFactory = new TextImageButtonFactory()
				{
					normalTextColor = ActiveTheme.Instance.PrimaryTextColor,
					hoverTextColor = ActiveTheme.Instance.PrimaryTextColor,
					disabledTextColor = ActiveTheme.Instance.PrimaryTextColor,
					pressedTextColor = ActiveTheme.Instance.PrimaryTextColor,
					FixedHeight = buttonHeight,
					FixedWidth = buttonHeight,
					AllowThemeToAdjustImage = false,
					checkedBorderColor = RGBA_Bytes.White
				};

			}

			internal TabControl CreateTabControl()
			{
				var advancedControls = new TabControl(separator: new HorizontalLine(alpha: 50));
				advancedControls.TabBar.BorderColor = RGBA_Bytes.Transparent; // theme.SecondaryTextColor;
				advancedControls.TabBar.Margin = 0;
				advancedControls.TabBar.Padding = 0;
				advancedControls.TextPointSize = 14;

				return advancedControls;
			}

			private static ImageBuffer ColorCircle(int size, RGBA_Bytes color)
			{
				ImageBuffer imageBuffer = new ImageBuffer(size, size);
				Graphics2D normalGraphics = imageBuffer.NewGraphics2D();
				Vector2 center = new Vector2(size / 2.0, size / 2.0);
				normalGraphics.Circle(center, size / 2.0, color);
				normalGraphics.Line(center + new Vector2(-size / 4.0, -size / 4.0), center + new Vector2(size / 4.0, size / 4.0), RGBA_Bytes.White, 2 * GuiWidget.DeviceScale);
				normalGraphics.Line(center + new Vector2(-size / 4.0, size / 4.0), center + new Vector2(size / 4.0, -size / 4.0), RGBA_Bytes.White, 2 * GuiWidget.DeviceScale);

				return imageBuffer;
			}

			internal Button CreateSmallResetButton()
			{
				return new Button(
					new ButtonViewStates(
						new ImageWidget(restoreNormal),
						new ImageWidget(restoreHover),
						new ImageWidget(restorePressed),
						new ImageWidget(restoreNormal)))
				{
					VAnchor = VAnchor.ParentCenter,
					Margin = new BorderDouble(0, 0, 5, 0)
				};
		}
		}

		internal void ClearPlate()
		{
			string now = DateTime.Now.ToString("yyyyMMdd-HHmmss");

			string platingDirectory = Path.Combine(ApplicationDataStorage.Instance.ApplicationTempDataPath, "Plating");
			Directory.CreateDirectory(platingDirectory);

			string mcxPath = Path.Combine(platingDirectory, now + ".mcx");

			PrinterConnection.Instance.ActivePrintItem = new PrintItemWrapper(new PrintItem(now, mcxPath));

			File.WriteAllText(mcxPath, new Object3D().ToJson());
		}

		public ThemeConfig Theme { get; set; } = new ThemeConfig();

		public Action RedeemDesignCode;
		public Action EnterShareCode;

		private static ApplicationController globalInstance;
		public RootedObjectEventHandler AdvancedControlsPanelReloading = new RootedObjectEventHandler();
		public RootedObjectEventHandler CloudSyncStatusChanged = new RootedObjectEventHandler();
		public RootedObjectEventHandler DoneReloadingAll = new RootedObjectEventHandler();
		public RootedObjectEventHandler PluginsLoaded = new RootedObjectEventHandler();

		public static Action SignInAction;
		public static Action SignOutAction;
		
		public static Action WebRequestFailed;
		public static Action WebRequestSucceeded;

		private static ImageBuffer restoreNormal;
		private static ImageBuffer restoreHover;
		private static ImageBuffer restorePressed;

		public TerminalRedirector Terminal { get; } = new TerminalRedirector();

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

			while(!MatterControlApplication.Instance.HasBeenClosed)
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
				catch (ThreadAbortException e)
				{
					return;
				}
				catch (Exception ex)
				{
					Console.WriteLine("Error generating thumbnail: " + ex.Message);
				}
			}
		}

		public static Func<PrinterInfo,string, Task<PrinterSettings>> GetPrinterProfileAsync;
		public static Func<string, IProgress<SyncReportType>,Task> SyncPrinterProfiles;
		public static Func<Task<OemProfileDictionary>> GetPublicProfileList;
		public static Func<string, Task<PrinterSettings>> DownloadPublicProfileAsync;

		public SlicePresetsWindow EditMaterialPresetsWindow { get; set; }

		public SlicePresetsWindow EditQualityPresetsWindow { get; set; }

		public ApplicationView MainView;

		public event EventHandler ApplicationClosed;

		private EventHandler unregisterEvents;

		private Dictionary<string, List<PrintItemAction>> registeredLibraryActions = new Dictionary<string, List<PrintItemAction>>();

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
					() => new CalibrationPartsContainer()));

			this.Library.RegisterRootProvider(
				new DynamicContainerLink(
					"Print Queue".Localize(),
					LibraryProviderHelpers.LoadInvertIcon("FileDialog", "queue_folder.png"),
					() => new PrintQueueContainer()));

			var rootLibraryCollection = Datastore.Instance.dbSQLite.Table<PrintItemCollection>().Where(v => v.Name == "_library").Take(1).FirstOrDefault();
			if (rootLibraryCollection != null)
			{
				int rooteLibraryID = rootLibraryCollection.Id;

				this.Library.RegisterRootProvider(
					new DynamicContainerLink(
						"Local Library".Localize(),
						LibraryProviderHelpers.LoadInvertIcon("FileDialog", "library_folder.png"),
						() => new SqliteLibraryContainer(rooteLibraryID)));
			}

			this.Library.RegisterRootProvider(
				new DynamicContainerLink(
					"Print History".Localize(),
					LibraryProviderHelpers.LoadInvertIcon("FileDialog", "folder.png"),
					() => new HistoryContainer()));

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
							var printer = PrinterConnection.Instance;

							return ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.has_sd_card_reader)
								&& printer.PrinterIsConnected
								&& !(printer.PrinterIsPrinting || printer.PrinterIsPaused);
						}));

		}

		public ApplicationController()
		{
			this.Library = new LibraryConfig();
			this.Library.ContentProviders.Add(new[] { "stl", "amf", "mcx" }, new MeshContentProvider());

			// Name = "MainSlidePanel";
			ActiveTheme.ThemeChanged.RegisterEvent((s, e) => ReloadAll(), ref unregisterEvents);

			ActiveSliceSettings.MaterialPresetChanged += (s, e) =>
			{
				ApplicationController.Instance.ReloadAdvancedControlsPanel();
			};

			// Remove consumed ClientToken from running list on shutdown
			ApplicationClosed += (s, e) =>
			{
				ApplicationSettings.Instance.ReleaseClientToken();

				// Release the waiting ThumbnailGeneration task so it can shutdown gracefully
				thumbGenResetEvent?.Set();
			};

			PrinterConnection.Instance.CommunicationStateChanged.RegisterEvent((s, e) =>
			{
				switch (PrinterConnection.Instance.CommunicationState)
				{
					case CommunicationStates.Printing:
						if (UserSettings.Instance.IsTouchScreen)
						{
							UiThread.RunOnIdle(PrintingWindow.Show);
						}

						break;
				}
			}, ref unregisterEvents);

			this.InitializeLibrary();


		}

		public void StartSignIn()
		{
			if (PrinterConnection.Instance.PrinterIsPrinting
				|| PrinterConnection.Instance.PrinterIsPaused)
			{
				// can't sign in while printing
				UiThread.RunOnIdle(() =>
					StyledMessageBox.ShowMessageBox(null, "Please wait until the print has finished and try again.".Localize(), "Can't sign in while printing".Localize())
				);
			}
			else // do the regular sign in
			{
				SignInAction?.Invoke();
			}
		}

		private static TypeFace monoSpacedTypeFace = null;
		public static TypeFace MonoSpacedTypeFace
		{
			get
			{
				if (monoSpacedTypeFace == null)
				{
					monoSpacedTypeFace = TypeFace.LoadFrom(StaticData.Instance.ReadAllText(Path.Combine("Fonts", "LiberationMono.svg")));
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

		public void StartSignOut()
		{
			if (PrinterConnection.Instance.PrinterIsPrinting
				|| PrinterConnection.Instance.PrinterIsPaused)
			{
				// can't log out while printing
				UiThread.RunOnIdle(() =>
					StyledMessageBox.ShowMessageBox(null, "Please wait until the print has finished and try again.".Localize(), "Can't log out while printing".Localize())
				);
			}
			else // do the regular log out
			{
				bool allowShowingSignOutWarning = true;
				if (allowShowingSignOutWarning)
				{
					// Warn on sign out that no access to user printers and cloud library put a 'Don't remind me again' check box
					StyledMessageBox.ShowMessageBox((clickedSignOut) =>
					{
						if (clickedSignOut)
						{
							SignOutAction?.Invoke();
						}
					}, "Are you sure you want to sign out? You will not have access to your printer profiles or cloud library.".Localize(), "Sign Out?".Localize(), StyledMessageBox.MessageType.YES_NO, "Sign Out".Localize(), "Cancel".Localize());
				}
				else // just run the sign out event
				{					
					SignOutAction?.Invoke();
				}
			}
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
					// give the widget a chance to hear about the close before they are actually closed.
					PopOutManager.SaveIfClosed = false;

					MainView?.CloseAllChildren();
					using (new QuickTimer("ReloadAll_AddElements"))
					{
						MainView?.CreateAndAddChildren();
					}
					PopOutManager.SaveIfClosed = true;
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
			ActiveTheme.SuspendEvents();

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

			ActiveTheme.ResumeEvents();
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

						// TODO: Short-term workaround to force DesktopView on Android
#if __ANDROID__
						if (false)
#else
						if (UserSettings.Instance.IsTouchScreen)
#endif
						{
							// and make sure we have the check for print recovery wired up needed for lazy tabs.
							var temp2 = PrintHistoryData.Instance;
							// now bulid the ui
							globalInstance.MainView = new TouchscreenView();
						}
						else
						{
							globalInstance.MainView = new DesktopView();
						}

						ActiveSliceSettings.ActivePrinterChanged.RegisterEvent((s, e) => ApplicationController.Instance.ReloadAll(), ref globalInstance.unregisterEvents);
					}
				}
				return globalInstance;
			}
		}

		public class MeshViewState
		{
			public Matrix4X4 RotationMatrix { get; internal set; } = Matrix4X4.Identity;
			public Matrix4X4 TranslationMatrix { get; internal set; } = Matrix4X4.Identity;
		}

		public MeshViewState PartPreviewState { get; set; } = new MeshViewState();

		public View3DWidget ActiveView3DWidget { get; internal set; }
		public int ActiveAdvancedControlsTab { get; internal set; }

		public bool PrintSettingsPinned { get; internal set; }

		public string CachePath(ILibraryItem libraryItem)
		{
			// TODO: Use content SHA
			return string.IsNullOrEmpty(libraryItem.ID) ? null : ApplicationController.CacheablePath("ItemThumbnails", $"{libraryItem.ID}.png");
		}

		/*
		private static string CachePath(ILibraryItem libraryItem, int width, int height)
		{
			// TODO: Use content SHA
			return string.IsNullOrEmpty(libraryItem.ID) ? null : ApplicationController.CacheablePath("ItemThumbnails", $"{libraryItem.ID}_{width}x{height}.png");
		}*/

		public void ReloadAdvancedControlsPanel()
		{
			AdvancedControlsPanelReloading.CallEvents(this, null);
		}

		// public LibraryDataView CurrentLibraryDataView = null;

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
				WizardWindow.Show<CopyGuestProfilesToUser>("/CopyGuestProfiles", "Copy Printers");
			}
		}

		public class CloudSyncEventArgs : EventArgs
		{
			public bool IsAuthenticated { get; set; }
		}

		public void OnLoadActions()
		{
			Load?.Invoke(this, null);

			// Pushing this after load fixes that empty printer list
			ApplicationController.Instance.UserChanged();

			if (!System.IO.File.Exists(@"/storage/sdcard0/Download/LaunchTestPrint.stl"))
			{
				bool showAuthWindow = WizardWindow.ShouldShowAuthPanel?.Invoke() ?? false;
                if (showAuthWindow)
                {
					if (ApplicationSettings.Instance.get(ApplicationSettingsKey.SuppressAuthPanel) != "True")
					{
						//Launch window to prompt user to sign in
						UiThread.RunOnIdle(() => WizardWindow.ShowPrinterSetup());
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

				if (OsInformation.OperatingSystem == OSType.Android)
				{
					// show this last so it is on top
					if (UserSettings.Instance.get("SoftwareLicenseAccepted") != "true")
					{
						UiThread.RunOnIdle(() => WizardWindow.Show<LicenseAgreementPage>("SoftwareLicense", "Software License Agreement"));
					}
				}
			}
			else
			{
				StartPrintingTest();
			}

			if (ActiveSliceSettings.Instance.PrinterSelected
				&& ActiveSliceSettings.Instance.GetValue<bool>(SettingsKey.auto_connect))
			{
				UiThread.RunOnIdle(() =>
				{
					//PrinterConnectionAndCommunication.Instance.HaltConnectionThread();
					PrinterConnection.Instance.ConnectToActivePrinter();
				}, 2);
			}
		}

        private static void RunSetupIfRequired()
        {
            ApplicationController.Instance.ReloadAdvancedControlsPanel();
            if (!ProfileManager.Instance.ActiveProfiles.Any())
            {
                // Start the setup wizard if no profiles exist
                UiThread.RunOnIdle(() => WizardWindow.ShowPrinterSetup());
            }
        }

		private EventHandler unregisterEvent;
		public void StartPrintingTest()
		{
			QueueData.Instance.RemoveAll();
			QueueData.Instance.AddItem(new PrintItemWrapper(new PrintItem("LaunchTestPrint", @"/storage/sdcard0/Download/LaunchTestPrint.stl")));
			PrinterConnection.Instance.ConnectToActivePrinter();

			PrinterConnection.Instance.CommunicationStateChanged.RegisterEvent((sender, e) =>
			{
				if (PrinterConnection.Instance.CommunicationState == CommunicationStates.Connected)
				{
					PrinterConnection.Instance.PrintActivePartIfPossible();
				}
			}, ref unregisterEvent);
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
						StaticData.Instance.LoadImageData(stream, unScaledImage);
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
						StaticData.Instance.LoadImageData(stream, imageToLoadInto);
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

		ExportPrintItemWindow exportingWindow = null;

		public async void OpenExportWindow()
		{
			/*
			if (exportingWindow == null)
			{
				exportingWindow = new ExportPrintItemWindow(await this.GetPrintItemWrapperAsync());
				exportingWindow.Closed += ExportQueueItemWindow_Closed;
				exportingWindow.ShowAsSystemWindow();
			}
			else
			{
				exportingWindow.BringToFront();
			} */
		}

		/// <summary>
		/// Cancels prints within the first two minutes or interactively prompts the user to confirm cancellation
		/// </summary>
		/// <returns>A boolean value indicating if the print was canceled</returns>
		public bool ConditionalCancelPrint()
		{
			bool canceled = false;

			if (PrinterConnection.Instance.SecondsPrinted > 120)
			{
				StyledMessageBox.ShowMessageBox(
					(bool response) =>
					{
						if (response)
						{
							UiThread.RunOnIdle(() => PrinterConnection.Instance.Stop());
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
				PrinterConnection.Instance.Stop();
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
	}
}