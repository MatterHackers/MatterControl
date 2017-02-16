/*
Copyright (c) 2015, Lars Brubaker
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

using MatterHackers.Agg;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrintLibrary;
using MatterHackers.MatterControl.PrintLibrary.Provider;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl
{
	using Agg.Font;
	using System.Reflection;
	using System.Text.RegularExpressions;
	using SettingsManagement;
	using PrintHistory;
	using Agg.Image;
	using System.Net;

	public class OemProfileDictionary : Dictionary<string, Dictionary<string, PublicDevice>>
	{
	}

	public class PublicDevice
	{
		public string DeviceToken { get; set; }
		public string ProfileToken { get; set; }
		public string ShortProfileID { get; set; }
		public string CacheKey => this.ShortProfileID + ProfileManager.ProfileExtension;
	}

	public abstract class ApplicationView : GuiWidget
	{
		public abstract void CreateAndAddChildren();
	}

	public class TouchscreenView : ApplicationView
	{
		private FlowLayoutWidget TopContainer;
		private TouchscreenTabView touchscreenTabView;
		private QueueDataView queueDataView;
		private GuiWidget menuSeparator;
		private PrintProgressBar progressBar;
		private bool topIsHidden = false;

		public TouchscreenView()
		{
			CreateAndAddChildren();
			this.AnchorAll();
		}

		public void ToggleTopContainer()
		{
			topIsHidden = !topIsHidden;
			progressBar.WidgetIsExtended = !progressBar.WidgetIsExtended;

			//To do - Animate this (KP)
			this.menuSeparator.Visible = this.TopContainer.Visible;
			this.TopContainer.Visible = !this.TopContainer.Visible;
		}

		public override void CreateAndAddChildren()
		{
			topIsHidden = false;
			this.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

			FlowLayoutWidget container = new FlowLayoutWidget(FlowDirection.TopToBottom);
			container.AnchorAll();

			TopContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
			TopContainer.HAnchor = HAnchor.ParentLeftRight;

			if (!UserSettings.Instance.IsTouchScreen)
			{
#if !__ANDROID__
				// The application menu bar, which is suppressed on Android
				ApplicationMenuRow menuRow = new ApplicationMenuRow();
				TopContainer.AddChild(menuRow);
#endif
			}

			menuSeparator = new GuiWidget();
			menuSeparator.Height = 12;
			menuSeparator.HAnchor = HAnchor.ParentLeftRight;
			menuSeparator.MinimumSize = new Vector2(0, 12);
			menuSeparator.Visible = false;

			queueDataView = new QueueDataView();
			TopContainer.AddChild(new ActionBarPlus(queueDataView));

			container.AddChild(TopContainer);

			progressBar = new PrintProgressBar();

			container.AddChild(progressBar);
			container.AddChild(menuSeparator);
			touchscreenTabView = new TouchscreenTabView(queueDataView);

			container.AddChild(touchscreenTabView);
			this.AddChild(container);
		}
	}

	public class DesktopView : ApplicationView
	{
		private WidescreenPanel widescreenPanel;

		public DesktopView()
		{
			CreateAndAddChildren();
			this.AnchorAll();
		}

		public override void CreateAndAddChildren()
		{
			this.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

			var container = new FlowLayoutWidget(FlowDirection.TopToBottom);
			container.AnchorAll();

			if (!UserSettings.Instance.IsTouchScreen)
			{
#if !__ANDROID__
				// The application menu bar, which is suppressed on Android
				var menuRow = new ApplicationMenuRow();
				container.AddChild(menuRow);
#endif
			}

			var menuSeparator = new GuiWidget()
			{
				BackgroundColor = new RGBA_Bytes(200, 200, 200),
				Height = 2,
				HAnchor = HAnchor.ParentLeftRight,
				Margin = new BorderDouble(3, 6, 3, 3)
			};
			container.AddChild(menuSeparator);

			widescreenPanel = new WidescreenPanel();
			container.AddChild(widescreenPanel);

			using (new PerformanceTimer("ReloadAll", "AddChild"))
			{
				this.AddChild(container);
			}
		}
	}

	public class ApplicationController
	{
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
		public static Func<PrinterInfo,string, Task<PrinterSettings>> GetPrinterProfileAsync;
		public static Func<string, IProgress<SyncReportType>,Task> SyncPrinterProfiles;
		public static Func<Task<OemProfileDictionary>> GetPublicProfileList;
		public static Func<string, Task<PrinterSettings>> DownloadPublicProfileAsync;

		public SlicePresetsWindow EditMaterialPresetsWindow { get; set; }

		public SlicePresetsWindow EditQualityPresetsWindow { get; set; }

		public ApplicationView MainView;

		public event EventHandler ApplicationClosed;

		private EventHandler unregisterEvents;

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

		public ApplicationController()
		{
			// Name = "MainSlidePanel";
			ActiveTheme.ThemeChanged.RegisterEvent((s, e) => ReloadAll(), ref unregisterEvents);

			// Remove consumed ClientToken from running list on shutdown
			ApplicationClosed += (s, e) => ApplicationSettings.Instance.ReleaseClientToken();
		}

		public void StartSignIn()
		{
			if (PrinterConnectionAndCommunication.Instance.PrinterIsPrinting
				|| PrinterConnectionAndCommunication.Instance.PrinterIsPaused)
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

			try
			{
				if (File.Exists(cachePath))
				{
					// Load from cache and deserialize
					return JsonConvert.DeserializeObject<T>(File.ReadAllText(cachePath));
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
					return JsonConvert.DeserializeObject<T>(StaticData.Instance.ReadAllText(staticDataFallbackPath));
				}
			}
			catch
			{
				return default(T);
			}

			return default(T);
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

		public void StartSignOut()
		{
			if (PrinterConnectionAndCommunication.Instance.PrinterIsPrinting
				|| PrinterConnectionAndCommunication.Instance.PrinterIsPaused)
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

					WidescreenPanel.PreChangePanels.CallEvents(this, null);
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

						if (UserSettings.Instance.IsTouchScreen)
						{
							// make sure that on touchscreen (due to lazy tabs) we initialize our stating parts and queue
							var temp = new LibraryProviderSQLite(null, null, null, null);
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

		public void ReloadAdvancedControlsPanel()
		{
			AdvancedControlsPanelReloading.CallEvents(this, null);
		}

		public LibraryDataView CurrentLibraryDataView = null;
		public void SwitchToPurchasedLibrary()
		{
			if (CurrentLibraryDataView?.CurrentLibraryProvider?.GetRootProvider() != null)
			{
				// Switch to the purchased library
				LibraryProviderSelector libraryProviderSelector = CurrentLibraryDataView.CurrentLibraryProvider.GetRootProvider() as LibraryProviderSelector;
				if (libraryProviderSelector != null)
				{
					LibraryProvider purchaseProvider = libraryProviderSelector.GetPurchasedLibrary();
					UiThread.RunOnIdle(() =>
					{
						CurrentLibraryDataView.CurrentLibraryProvider = purchaseProvider;
					});
				}
			}
		}

		public void SwitchToSharedLibrary()
		{
			// Switch to the shared library
			if (CurrentLibraryDataView?.CurrentLibraryProvider?.GetRootProvider() != null)
			{
				LibraryProviderSelector libraryProviderSelector = CurrentLibraryDataView.CurrentLibraryProvider.GetRootProvider() as LibraryProviderSelector;
				if (libraryProviderSelector != null)
				{
					LibraryProvider sharedProvider = libraryProviderSelector.GetSharedLibrary();
					UiThread.RunOnIdle(() =>
					{
						CurrentLibraryDataView.CurrentLibraryProvider = sharedProvider;
					});
				}
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
					PrinterConnectionAndCommunication.Instance.ConnectToActivePrinter();
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
			PrinterConnectionAndCommunication.Instance.ConnectToActivePrinter();

			PrinterConnectionAndCommunication.Instance.CommunicationStateChanged.RegisterEvent((sender, e) =>
			{
				if (PrinterConnectionAndCommunication.Instance.CommunicationState == PrinterConnectionAndCommunication.CommunicationStates.Connected)
				{
					PrinterConnectionAndCommunication.Instance.PrintActivePartIfPossible();
				}
			}, ref unregisterEvent);
		}

		public void ReloadLibrarySelectorUI()
		{
			LibraryProviderSelector.Reload();
		}

		public void ReloadLibraryUI()
		{
			PrintLibraryWidget.Reload();
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

		/// <summary>
		/// Cancels prints within the first two minutes or interactively prompts the user to confirm cancellation
		/// </summary>
		/// <returns>A boolean value indicating if the print was canceled</returns>
		public bool ConditionalCancelPrint()
		{
			bool canceled = false;

			if (PrinterConnectionAndCommunication.Instance.SecondsPrinted > 120)
			{
				StyledMessageBox.ShowMessageBox(
					(bool response) =>
					{
						if (response)
						{
							UiThread.RunOnIdle(() => PrinterConnectionAndCommunication.Instance.Stop());
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
				PrinterConnectionAndCommunication.Instance.Stop();
				canceled = false;
			}

			return canceled;
		}
	}

	public class SyncReportType
	{
		public string actionLabel;
		public double percComplete;
	}
}