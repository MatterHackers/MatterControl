/*
Copyright (c) 2014, Lars Brubaker
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
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.AboutPage;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.SettingsManagement;
using MatterHackers.MatterControl.VersionManagement;

namespace MatterHackers.MatterControl
{
	public class UpdateControlData
	{
		private WebClient webClient;

		private int downloadPercent;
		private int downloadSize;

		public int DownloadPercent { get { return downloadPercent; } }

		public enum UpdateRequestType { UserRequested, Automatic, FirstTimeEver };

		private UpdateRequestType updateRequestType;

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

		public UpdateStatusStates UpdateStatus
		{
			get
			{
				return updateStatus;
			}
		}

		private void CheckVersionStatus()
		{
			string currentBuildToken = ApplicationSettings.Instance.get(LatestVersionRequest.VersionKey.CurrentBuildToken);
			string updateFileName = Path.Combine(updateFileLocation, string.Format("{0}.{1}", currentBuildToken, InstallerExtension));

			string applicationBuildToken = VersionInfo.Instance.BuildToken;

			if (applicationBuildToken == currentBuildToken || currentBuildToken == null)
			{
				SetUpdateStatus(UpdateStatusStates.MayBeAvailable);
			}
			else if (System.IO.File.Exists(updateFileName))
			{
				SetUpdateStatus(UpdateStatusStates.ReadyToInstall);
			}
			else
			{
				SetUpdateStatus(UpdateStatusStates.UpdateAvailable);
			}
		}

		static bool haveShowUpdateRequired = false;
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
					return "pkg";
				}
				else if (AggContext.OperatingSystem == OSType.X11)
				{
					return "tar.gz";
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
				return updateStatus == UpdateStatusStates.UpdateAvailable && ApplicationSettings.Instance.get(LatestVersionRequest.VersionKey.UpdateRequired) == "True";
			}

			private set {}
		}

		public void CheckForUpdateUserRequested()
		{
			updateRequestType = UpdateRequestType.UserRequested;
			CheckForUpdate();
		}

		private void CheckForUpdate()
		{
			if (!WaitingToCompleteTransaction())
			{
				SetUpdateStatus(UpdateStatusStates.CheckingForUpdate);
				LatestVersionRequest request = new LatestVersionRequest();
				request.RequestSucceeded += new EventHandler(onVersionRequestSucceeded);
				request.RequestFailed += onVersionRequestFailed;
				request.Request();
			}
		}

		private void onVersionRequestSucceeded(object sender, EventArgs e)
		{
			string updateAvailableMessage = "There is a recommended update available for MatterControl. Would you like to download it now?".Localize();
			string updateAvailableTitle = "Recommended Update Available".Localize();
			string downloadNow = "Download Now".Localize();
			string remindMeLater = "Remind Me Later".Localize();

			string currentBuildToken = ApplicationSettings.Instance.get(LatestVersionRequest.VersionKey.CurrentBuildToken);
			string updateFileName = Path.Combine(updateFileLocation, string.Format("{0}.{1}", currentBuildToken, InstallerExtension));

			string applicationBuildToken = VersionInfo.Instance.BuildToken;

			if (applicationBuildToken == currentBuildToken)
			{
				SetUpdateStatus(UpdateStatusStates.UpToDate);
			}
			else if (System.IO.File.Exists(updateFileName))
			{
				SetUpdateStatus(UpdateStatusStates.ReadyToInstall);
			}
			else
			{
				SetUpdateStatus(UpdateStatusStates.UpdateAvailable);
				if (updateRequestType == UpdateRequestType.FirstTimeEver)
				{
					UiThread.RunOnIdle(() =>
					{
						StyledMessageBox.ShowMessageBox(ProcessDialogResponse, updateAvailableMessage, updateAvailableTitle, StyledMessageBox.MessageType.YES_NO, downloadNow, remindMeLater);
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
				GuiWidget aboutTabWidget = ApplicationController.Instance.MainView.FindNamedChildRecursive("About Tab");

				if (aboutTabWidget is Tab aboutTab)
				{
					aboutTab.TabBarContaningTab.SelectTab(aboutTab);
				}
			}
		}

		private void onVersionRequestFailed(object sender, ResponseErrorEventArgs e)
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

					if (!System.IO.Directory.Exists(updateFileLocation))
					{
						System.IO.Directory.CreateDirectory(updateFileLocation);
					}

					updateFileName = Path.Combine(updateFileLocation, string.Format("{0}.{1}", downloadToken, InstallerExtension));

					webClient = new WebClient();
					webClient.DownloadFileCompleted += new AsyncCompletedEventHandler(DownloadCompleted);
					webClient.DownloadProgressChanged += new DownloadProgressChangedEventHandler(DownloadProgressChanged);
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
				this.downloadPercent = (int)(e.BytesReceived * 100 / downloadSize);
			}
			UiThread.RunOnIdle(() => UpdateStatusChanged.CallEvents(this, e) );
		}

		private void DownloadCompleted(object sender, AsyncCompletedEventArgs e)
		{
			if (e.Error != null)
			{
				//Delete empty/partially downloaded file
				if (System.IO.File.Exists(updateFileName))
				{
					System.IO.File.Delete(updateFileName);
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
			CheckVersionStatus();
			if (ApplicationSettings.Instance.GetClientToken() != null
				|| OemSettings.Instance.CheckForUpdatesOnFirstRun)
			{
				if (ApplicationSettings.Instance.GetClientToken() == null)
				{
					updateRequestType = UpdateRequestType.FirstTimeEver;
				}
				else
				{
					updateRequestType = UpdateRequestType.Automatic;
				}
				//If we have already requested an update once, check on load
				CheckForUpdate();
			}
			else
			{
				ApplicationSession firstSession;
				firstSession = Datastore.Instance.dbSQLite.Table<ApplicationSession>().OrderBy(v => v.SessionStart).Take(1).FirstOrDefault();
				if (firstSession != null
					&& DateTime.Compare(firstSession.SessionStart.AddDays(7), DateTime.Now) < 0)
				{
					SetUpdateStatus(UpdateStatusStates.UpdateAvailable);
				}
			}
		}

		public void OnUpdateStatusChanged(EventArgs e)
		{
			UpdateStatusChanged.CallEvents(this, e);
		}

		public static EventHandler InstallUpdateFromMainActivity = null;

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

			if (System.IO.File.Exists(friendlyFileName))
			{
				System.IO.File.Delete(friendlyFileName);
			}

			try
			{
				//Change download file to friendly file name
				System.IO.File.Move(updateFileName, friendlyFileName);
#if __ANDROID__
				if (InstallUpdateFromMainActivity != null)
				{
					InstallUpdateFromMainActivity(this, null);
				}
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
				if (System.IO.File.Exists(friendlyFileName))
				{
					System.IO.File.Delete(friendlyFileName);
				}
			}

			return false;
		}
	}
}
