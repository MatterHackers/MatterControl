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
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.VersionManagement;

namespace MatterHackers.MatterControl
{
    public class UpdateControlData
    {
        WebClient webClient;

        int downloadPercent;
        int downloadSize;
        bool updateInitiated;

        public int DownloadPercent { get { return downloadPercent; } }

        public enum UpdateStatusStates { MayBeAvailable, CheckingForUpdate, UpdateAvailable, UpdateDownloading, ReadyToInstall, UpToDate }; 
        
        public RootedObjectEventHandler UpdateStatusChanged = new RootedObjectEventHandler();

        static string applicationDataPath = DataStorage.ApplicationDataStorage.Instance.ApplicationUserDataPath;
        static string updateFileLocation = Path.Combine(applicationDataPath, "updates");

        UpdateStatusStates updateStatus;
        public UpdateStatusStates UpdateStatus
        {
            get 
            {
                return updateStatus;
            }
        }

        void CheckVersionStatus()
        {
            string currentBuildToken = ApplicationSettings.Instance.get("CurrentBuildToken");
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

        void SetUpdateStatus(UpdateStatusStates updateStatus)
        {
            if (this.updateStatus != updateStatus)
            {
                this.updateStatus = updateStatus;
                OnUpdateStatusChanged(null);
            }
        }

        string InstallerExtension
        {
            get
            {
                if (MatterHackers.Agg.UI.WindowsFormsAbstract.GetOSType() == WindowsFormsAbstract.OSType.Mac)
                {
                    return "pkg";
                }
                else
                {
                    return "exe";
                }
            }
        }

        static UpdateControlData instance;
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

        public void CheckForUpdate()
        {
            if (!updateInitiated)
            {
                SetUpdateStatus(UpdateStatusStates.CheckingForUpdate);
                updateInitiated = true;
                RequestLatestVersion request = new RequestLatestVersion();
                request.RequestSucceeded += new EventHandler(onVersionRequestSucceeded);
                request.RequestFailed += new EventHandler(onVersionRequestFailed);
                request.Request();
            }
        }

        void onVersionRequestSucceeded(object sender, EventArgs e)
        {
            this.updateInitiated = false;
            string currentBuildToken = ApplicationSettings.Instance.get("CurrentBuildToken");
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
                if (DownloadHasZeroSize())
                {
                    SetUpdateStatus(UpdateStatusStates.UpToDate);
                }
                else
                {
                    SetUpdateStatus(UpdateStatusStates.UpdateAvailable);
                }
            }
        }

        void onVersionRequestFailed(object sender, EventArgs e)
        {
            this.updateInitiated = false;
            SetUpdateStatus(UpdateStatusStates.UpToDate);
        }

        public void InitiateUpdateDownload()
        {
            if (!updateInitiated)
            {
                updateInitiated = true;

                SetUpdateStatus(UpdateStatusStates.UpdateDownloading);

                string downloadUri = string.Format("https://mattercontrol.appspot.com/downloads/development/{0}", ApplicationSettings.Instance.get("CurrentBuildToken"));
                string downloadToken = ApplicationSettings.Instance.get("CurrentBuildToken");

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
                    //Unknown download size
                    downloadSize = 0;
                }

                if (!System.IO.Directory.Exists(updateFileLocation))
                {
                    System.IO.Directory.CreateDirectory(updateFileLocation);
                }

                string updateFileName = Path.Combine(updateFileLocation, string.Format("{0}.{1}", downloadToken, InstallerExtension));

                webClient = new WebClient();
                webClient.DownloadFileCompleted += new AsyncCompletedEventHandler(DownloadCompleted);
                webClient.DownloadProgressChanged += new DownloadProgressChangedEventHandler(DownloadProgressChanged);
                webClient.DownloadFileAsync(new Uri(downloadUri), updateFileName);
            }
        }

        public bool DownloadHasZeroSize()
        {
            bool zeroSizeDownload = false;
            string downloadUri = string.Format("https://mattercontrol.appspot.com/downloads/development/{0}", ApplicationSettings.Instance.get("CurrentBuildToken"));
            string downloadToken = ApplicationSettings.Instance.get("CurrentBuildToken");

            //Make HEAD request to determine the size of the download (required by GAE)
            System.Net.WebRequest request = System.Net.WebRequest.Create(downloadUri);
            request.Method = "HEAD";

            try
            {
                WebResponse response = request.GetResponse();
                downloadSize = (int)response.ContentLength;
                if (downloadSize == 0)
                {
                    zeroSizeDownload = true;
                }

            }
            catch
            {
                //Unknown download size
                downloadSize = 0;
            }
            return zeroSizeDownload;
        }

        void DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {            
            if (downloadSize > 0)
            {
                this.downloadPercent = (int)(e.BytesReceived * 100 / downloadSize);
            }
            UpdateStatusChanged.CallEvents(this, e);            
        }

        void DownloadCompleted(object sender, EventArgs e)
        {
            updateInitiated = false;
            SetUpdateStatus(UpdateStatusStates.ReadyToInstall);
            webClient.Dispose();
        }

        private UpdateControlData()
        {
            CheckVersionStatus();
            if (ApplicationSettings.Instance.get("ClientToken") != null)
            {
                //If we have already requested an update once, check on load
                CheckForUpdate();
            }
            else
            {
                DataStorage.ApplicationSession firstSession;
                firstSession = DataStorage.Datastore.Instance.dbSQLite.Table<DataStorage.ApplicationSession>().OrderBy(v => v.SessionStart).Take(1).FirstOrDefault();
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

        public bool InstallUpdate(GuiWidget windowToClose)
        {
            string downloadToken = ApplicationSettings.Instance.get("CurrentBuildToken");

            string updateFileName = Path.Combine(updateFileLocation, "{0}.{1}".FormatWith(downloadToken, InstallerExtension));
            string releaseVersion = ApplicationSettings.Instance.get("CurrentReleaseVersion");
            string friendlyFileName = Path.Combine(updateFileLocation, "MatterControlSetup-{0}.{1}".FormatWith(releaseVersion, InstallerExtension));

            if (System.IO.File.Exists(friendlyFileName))
            {
                System.IO.File.Delete(friendlyFileName);
            }

            try
            {
                //Change download file to friendly file name
                System.IO.File.Move(updateFileName, friendlyFileName);

                int tries = 0;
                do
                {
                    Thread.Sleep(10);
                } while (tries++ < 100 && !File.Exists(friendlyFileName));

                //Run installer file
                Process installUpdate = new Process();
                installUpdate.StartInfo.FileName = friendlyFileName;
                installUpdate.Start();

                while (windowToClose != null && windowToClose as SystemWindow == null)
                {
                    windowToClose = windowToClose.Parent;
                }

                //Attempt to close current application
                SystemWindow topSystemWindow = windowToClose as SystemWindow;
                if (topSystemWindow != null)
                {
                    topSystemWindow.CloseOnIdle();
                    return true;
                }
            }
            catch
            {                
                if (System.IO.File.Exists(friendlyFileName))
                {
                    System.IO.File.Delete(friendlyFileName);
                }
            }

            return false;
        }
    }
}
