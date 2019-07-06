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
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Timers;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.SettingsManagement;
using MatterHackers.MatterControl.VersionManagement;

namespace MatterHackers.MatterControl
{
	public class UpdateControlData
	{
		private WebClient webClient;
		private int downloadSize;

		public int DownloadPercent { get; private set; }

		public enum UpdateStatusStates { MayBeAvailable, CheckingForUpdate, UpdateAvailable, UpdateDownloading, ReadyToInstall, UpToDate, UnableToConnectToServer, UpdateRequired };

		private bool WaitingToCompleteTransaction()
		{
			switch (UpdateStatus)
			{
				case UpdateStatusStates.CheckingForUpdate:
				case UpdateStatusStates.UpdateDownloading:
					return true;

				default:
					return false;
			}
		}

		public RootedObjectEventHandler UpdateStatusChanged = new RootedObjectEventHandler();

#if __ANDROID__
		static string updateFileLocation = Path.Combine(ApplicationDataStorage.Instance.PublicDataStoragePath, "updates");
#else
		private static string applicationDataPath = ApplicationDataStorage.ApplicationUserDataPath;
		private static string updateFileLocation = Path.Combine(applicationDataPath, "updates");
#endif

		private UpdateStatusStates updateStatus;

		public UpdateStatusStates UpdateStatus => updateStatus;

		private void CheckVersionStatus()
		{
			string currentBuildToken = ApplicationSettings.Instance.get(LatestVersionRequest.VersionKey.CurrentBuildToken);
			string updateFileName = Path.Combine(updateFileLocation, string.Format("{0}.{1}", currentBuildToken, InstallerExtension));

			string applicationBuildToken = VersionInfo.Instance.BuildToken;

			if (applicationBuildToken == currentBuildToken || currentBuildToken == null)
			{
				SetUpdateStatus(UpdateStatusStates.MayBeAvailable);
			}
			else if (File.Exists(updateFileName))
			{
				SetUpdateStatus(UpdateStatusStates.ReadyToInstall);
			}
			else
			{
				SetUpdateStatus(UpdateStatusStates.UpdateAvailable);
			}
		}

		private static bool haveShowUpdateRequired = false;

		private void SetUpdateStatus(UpdateStatusStates updateStatus)
		{
			if (this.updateStatus != updateStatus)
			{
				this.updateStatus = updateStatus;
				OnUpdateStatusChanged(null);

				if (this.UpdateRequired && !haveShowUpdateRequired)
				{
					haveShowUpdateRequired = true;
					if (!UserSettings.Instance.IsTouchScreen)
					{
#if !__ANDROID__
						UiThread.RunOnIdle(() => DialogWindow.Show<CheckForUpdatesPage>());
#endif
					}
				}
			}
		}

		private string InstallerExtension
		{
			get
			{
				if (AggContext.OperatingSystem == OSType.Mac)
				{
					return "dmg";
				}
				else if (AggContext.OperatingSystem == OSType.X11)
				{
					return "deb";
				}
				else if (AggContext.OperatingSystem == OSType.Android)
				{
					return "apk";
				}
				else
				{
					return "exe";
				}
			}
		}

		private static UpdateControlData instance;

		static public UpdateControlData Instance
		{
			get
			{
				if (instance == null)
				{
					instance = new UpdateControlData();
				}

				return instance;
			}
		}

		public bool UpdateRequired
		{
			get
			{
				return updateStatus == UpdateStatusStates.UpdateAvailable 
					&& ApplicationSettings.Instance.get(LatestVersionRequest.VersionKey.UpdateRequired) == "True";
			}
		}

		public void CheckForUpdate()
		{
			if (!WaitingToCompleteTransaction())
			{
				SetUpdateStatus(UpdateStatusStates.CheckingForUpdate);

				var request = new LatestVersionRequest();
				request.RequestSucceeded += VersionRequest_Succeeded;
				request.RequestFailed += VersionRequest_Failed;
				request.Request();
			}
		}

		private void VersionRequest_Succeeded(object sender, EventArgs e)
		{
			string currentBuildToken = ApplicationSettings.Instance.get(LatestVersionRequest.VersionKey.CurrentBuildToken);
			string updateFileName = Path.Combine(updateFileLocation, string.Format("{0}.{1}", currentBuildToken, InstallerExtension));

			string applicationBuildToken = VersionInfo.Instance.BuildToken;

			if (applicationBuildToken == currentBuildToken)
			{
				SetUpdateStatus(UpdateStatusStates.UpToDate);
			}
			else if (File.Exists(updateFileName))
			{
				SetUpdateStatus(UpdateStatusStates.ReadyToInstall);
			}
			else
			{
				SetUpdateStatus(UpdateStatusStates.UpdateAvailable);
				bool firstUpdateRequest = ApplicationSettings.Instance.GetClientToken() == null;
				if (firstUpdateRequest)
				{
					UiThread.RunOnIdle(() =>
					{
						StyledMessageBox.ShowMessageBox(
							this.ProcessDialogResponse,
							"There is a recommended update available for MatterControl. Would you like to download it now?".Localize(),
							"Recommended Update Available".Localize(),
							StyledMessageBox.MessageType.YES_NO,
							"Download Now".Localize(),
							"Remind Me Later".Localize());

						// show a dialog to tell the user there is an update
					});
				}
			}
		}

		private void ProcessDialogResponse(bool messageBoxResponse)
		{
			if (messageBoxResponse)
			{
				InitiateUpdateDownload();
				// Switch to the about page so we can see the download progress.
				GuiWidget aboutTabWidget = ApplicationController.Instance.MainView.FindDescendant("About Tab");

				if (aboutTabWidget is Tab aboutTab)
				{
					aboutTab.TabBarContaningTab.SelectTab(aboutTab);
				}
			}
		}

		private void VersionRequest_Failed(object sender, ResponseErrorEventArgs e)
		{
			SetUpdateStatus(UpdateStatusStates.UpToDate);
		}

		private int downloadAttempts = 0;
		private string updateFileName;

		public void InitiateUpdateDownload()
		{
			downloadAttempts = 0;
			DownloadUpdate();
		}

		private void DownloadUpdate()
		{
			(new Thread(new ThreadStart(() => DownloadUpdateTask()))).Start();
		}

		private void DownloadUpdateTask()
		{
			if (!WaitingToCompleteTransaction())
			{
				string downloadToken = ApplicationSettings.Instance.get(LatestVersionRequest.VersionKey.CurrentBuildToken);

				if (downloadToken == null)
				{
#if DEBUG
					throw new Exception("Build token should not be null");
#endif
					//
				}
				else
				{
					downloadAttempts++;
					SetUpdateStatus(UpdateStatusStates.UpdateDownloading);
					string downloadUri = $"{MatterControlApplication.MCWSBaseUri}/downloads/development/{ApplicationSettings.Instance.get(LatestVersionRequest.VersionKey.CurrentBuildToken)}";

					//Make HEAD request to determine the size of the download (required by GAE)
					System.Net.WebRequest request = System.Net.WebRequest.Create(downloadUri);
					request.Method = "HEAD";

					try
					{
						WebResponse response = request.GetResponse();
						downloadSize = (int)response.ContentLength;
					}
					catch
					{
						GuiWidget.BreakInDebugger();
						//Unknown download size
						downloadSize = 0;
					}

					if (!Directory.Exists(updateFileLocation))
					{
						Directory.CreateDirectory(updateFileLocation);
					}

					updateFileName = Path.Combine(updateFileLocation, string.Format("{0}.{1}", downloadToken, InstallerExtension));

					webClient = new WebClient();
					webClient.DownloadFileCompleted += DownloadCompleted;
					webClient.DownloadProgressChanged += DownloadProgressChanged;
					try
					{
						webClient.DownloadFileAsync(new Uri(downloadUri), updateFileName);
					}
					catch
					{
					}
				}
			}
		}

		private void DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
		{
			if (downloadSize > 0)
			{
				this.DownloadPercent = (int)(e.BytesReceived * 100 / downloadSize);
			}
			UiThread.RunOnIdle(() => UpdateStatusChanged.CallEvents(this, e));
		}

		private void DownloadCompleted(object sender, AsyncCompletedEventArgs e)
		{
			if (e.Error != null)
			{
				//Delete empty/partially downloaded file
				if (File.Exists(updateFileName))
				{
					File.Delete(updateFileName);
				}

				//Try downloading again - one time
				if (downloadAttempts == 1)
				{
					DownloadUpdate();
				}
				else
				{
					UiThread.RunOnIdle(() => SetUpdateStatus(UpdateStatusStates.UnableToConnectToServer));
				}
			}
			else
			{
				UiThread.RunOnIdle(() => SetUpdateStatus(UpdateStatusStates.ReadyToInstall));
			}

			webClient.Dispose();
		}

		private UpdateControlData()
		{
			this.CheckVersionStatus();

			// Always check for updates on startup
			this.CheckForUpdate();

			// Now that we are running, check for an update every 24 hours.
			var checkUpdatesDaily = new System.Timers.Timer(24 * 60 * 60 * 1000); //one day in milliseconds
			checkUpdatesDaily.Elapsed += DailyTimer_Elapsed;
			checkUpdatesDaily.Start();
		}

		private void DailyTimer_Elapsed(object source, ElapsedEventArgs e)
		{
			CheckForUpdate();
		}

		public void OnUpdateStatusChanged(EventArgs e)
		{
			UpdateStatusChanged.CallEvents(this, e);
		}

		public static event EventHandler InstallUpdateFromMainActivity = null;

		public bool InstallUpdate()
		{
			string downloadToken = ApplicationSettings.Instance.get(LatestVersionRequest.VersionKey.CurrentBuildToken);

			string updateFileName = Path.Combine(updateFileLocation, "{0}.{1}".FormatWith(downloadToken, InstallerExtension));
#if __ANDROID__
			string friendlyFileName = Path.Combine(updateFileLocation, "MatterControlSetup.apk");
#else
			string releaseVersion = ApplicationSettings.Instance.get(LatestVersionRequest.VersionKey.CurrentReleaseVersion);
			string friendlyFileName = Path.Combine(updateFileLocation, "MatterControlSetup-{0}.{1}".FormatWith(releaseVersion, InstallerExtension));
#endif

			if (File.Exists(friendlyFileName))
			{
				File.Delete(friendlyFileName);
			}

			try
			{
				//Change download file to friendly file name
				File.Move(updateFileName, friendlyFileName);
#if __ANDROID__
				InstallUpdateFromMainActivity?.Invoke(this, null);
				return true;
#else
				int tries = 0;
				do
				{
					Thread.Sleep(10);
				} while (tries++ < 100 && !File.Exists(friendlyFileName));

				//Run installer file
				Process installUpdate = new Process();
				installUpdate.StartInfo.FileName = friendlyFileName;
				installUpdate.Start();

				//Attempt to close current application
				SystemWindow topSystemWindow = AppContext.RootSystemWindow;
				if (topSystemWindow != null)
				{
					topSystemWindow.CloseOnIdle();
					return true;
				}
#endif
			}
			catch
			{
				GuiWidget.BreakInDebugger();
				if (File.Exists(friendlyFileName))
				{
					File.Delete(friendlyFileName);
				}
			}

			return false;
		}
	}
}