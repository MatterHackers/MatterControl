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
    public class UpdateControl : FlowLayoutWidget
    {
        bool updateInitiated = false;
        Button downloadUpdateLink;
        Button checkUpdateLink;
        Button installUpdateLink;
        int downloadPercent = 0;
        int downloadSize = 0;
        TextWidget updateStatusText;
        RGBA_Bytes offWhite = new RGBA_Bytes(245, 245, 245);
        TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();

        public UpdateControl()
        {
            textImageButtonFactory.normalFillColor = RGBA_Bytes.Gray;
            textImageButtonFactory.normalTextColor = ActiveTheme.Instance.PrimaryTextColor;

            HAnchor = HAnchor.ParentLeftRight;
            BackgroundColor = ActiveTheme.Instance.TransparentDarkOverlay;
            Padding = new BorderDouble(6, 5);
            {
                updateStatusText = new TextWidget(string.Format(""), textColor: ActiveTheme.Instance.PrimaryTextColor);
                updateStatusText.AutoExpandBoundsToText = true;
                updateStatusText.VAnchor = VAnchor.ParentCenter;

                GuiWidget horizontalSpacer = new GuiWidget();
                horizontalSpacer.HAnchor = HAnchor.ParentLeftRight;

                checkUpdateLink = textImageButtonFactory.Generate("Check for Update".Localize());
                checkUpdateLink.VAnchor = VAnchor.ParentCenter;
                checkUpdateLink.Click += CheckForUpdate;
                checkUpdateLink.Visible = false;

                downloadUpdateLink = textImageButtonFactory.Generate("Download Update".Localize());
                downloadUpdateLink.VAnchor = VAnchor.ParentCenter;
                downloadUpdateLink.Click += DownloadUpdate;
                downloadUpdateLink.Visible = false;


                installUpdateLink = textImageButtonFactory.Generate("Install Update".Localize());
                installUpdateLink.VAnchor = VAnchor.ParentCenter;
                installUpdateLink.Click += InstallUpdate;
                installUpdateLink.Visible = false;

                AddChild(updateStatusText);
                AddChild(horizontalSpacer);
                AddChild(checkUpdateLink);
                AddChild(downloadUpdateLink);
                AddChild(installUpdateLink);
            }

            CheckVersionStatus();
            if (ApplicationSettings.Instance.get("ClientToken") != null)
            {
                //If we have already requested an update once, check on load
                CheckForUpdate(this, null);
            }
            else
            {
                DataStorage.ApplicationSession firstSession;
                firstSession = DataStorage.Datastore.Instance.dbSQLite.Table<DataStorage.ApplicationSession>().OrderBy(v => v.SessionStart).Take(1).FirstOrDefault();
                if (firstSession != null
                    && DateTime.Compare(firstSession.SessionStart.AddDays(7), DateTime.Now) < 0)
                {
                    NeedToCheckForUpdateFirstTimeEver = true;
                }
            }
        }

        public static bool NeedToCheckForUpdateFirstTimeEver { get; set; }

        public void CheckForUpdate(object sender, MouseEventArgs e)
        {
            if (!updateInitiated)
            {
                updateStatusText.Text = "Checking for updates...".Localize();
                checkUpdateLink.Visible = false;

                updateInitiated = true;
                RequestLatestVersion request = new RequestLatestVersion();
                request.RequestSucceeded += new EventHandler(onVersionRequestSucceeded);
                request.RequestFailed += new EventHandler(onVersionRequestFailed);
                request.Request();
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

        static string applicationDataPath = DataStorage.ApplicationDataStorage.Instance.ApplicationUserDataPath;
        static string updateFileLocation = Path.Combine(applicationDataPath, "updates");

        public void InstallUpdate(object sender, MouseEventArgs e)
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

                GuiWidget parent = Parent;
                while (parent != null && parent as SystemWindow == null)
                {
                    parent = parent.Parent;
                }

                //Attempt to close current application
                SystemWindow topSystemWindow = parent as SystemWindow;
                if (topSystemWindow != null)
                {
                    topSystemWindow.Close();
                }
            }
            catch
            {
                installUpdateLink.Visible = false;
                updateStatusText.Text = string.Format("Oops! Unable to install update.".Localize());
                if (System.IO.File.Exists(friendlyFileName))
                {
                    System.IO.File.Delete(friendlyFileName);
                }
            }
        }

        public void DownloadUpdate(object sender, MouseEventArgs e)
        {
            if (!updateInitiated)
            {
                downloadUpdateLink.Visible = false;
                updateStatusText.Text = string.Format("Downloading updates...".Localize());
                updateInitiated = true;

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

                WebClient webClient = new WebClient();
                webClient.DownloadFileCompleted += new AsyncCompletedEventHandler(DownloadCompleted);
                webClient.DownloadProgressChanged += new DownloadProgressChangedEventHandler(DownloadProgressChanged);
                webClient.DownloadFileAsync(new Uri(downloadUri), updateFileName);
            }
        }

        void DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            string newText = "Downloading updates...".Localize();
            if (downloadSize > 0)
            {
                this.downloadPercent = (int)(e.BytesReceived * 100 / downloadSize);
                newText = "{0} {1}%".FormatWith(newText, downloadPercent);
            }

            updateStatusText.Text = newText;
        }

        void DownloadCompleted(object sender, EventArgs e)
        {
            this.updateInitiated = false;
            updateStatusText.Text = string.Format("New updates are ready to install.".Localize());
            downloadUpdateLink.Visible = false;
            installUpdateLink.Visible = true;
            checkUpdateLink.Visible = false;
        }

        void CheckVersionStatus()
        {
            string currentBuildToken = ApplicationSettings.Instance.get("CurrentBuildToken");
            string updateFileName = Path.Combine(updateFileLocation, string.Format("{0}.{1}", currentBuildToken, InstallerExtension));

            string applicationBuildToken = VersionInfo.Instance.BuildToken;

            if (applicationBuildToken == currentBuildToken || currentBuildToken == null)
            {
                updateStatusText.Text = string.Format("New updates may be available.".Localize());
                checkUpdateLink.Visible = true;
            }
            else if (System.IO.File.Exists(updateFileName))
            {
                updateStatusText.Text = string.Format("New updates are ready to install.".Localize());
                installUpdateLink.Visible = true;
                checkUpdateLink.Visible = false;
            }
            else
            {
                //updateStatusText.Text = string.Format("New version available: {0}", ApplicationSettings.Instance.get("CurrentReleaseVersion"));
                updateStatusText.Text = string.Format("There are updates available.".Localize());
                downloadUpdateLink.Visible = true;
                checkUpdateLink.Visible = false;
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
                updateStatusText.Text = string.Format("Your application is up-to-date.".Localize());
                downloadUpdateLink.Visible = false;
                installUpdateLink.Visible = false;
                checkUpdateLink.Visible = true;

            }
            else if (System.IO.File.Exists(updateFileName))
            {
                updateStatusText.Text = string.Format("New updates are ready to install.".Localize());
                downloadUpdateLink.Visible = false;
                installUpdateLink.Visible = true;
                checkUpdateLink.Visible = false;

            }
            else
            {
                updateStatusText.Text = string.Format("There is a recommended update available.".Localize());
                //updateStatusText.Text = string.Format("New version available: {0}", ApplicationSettings.Instance.get("CurrentReleaseVersion"));
                downloadUpdateLink.Visible = true;
                installUpdateLink.Visible = false;
                checkUpdateLink.Visible = false;
            }

            //MainSlidePanel.Instance.SetUpdateNotification(this, null);
        }

        void onVersionRequestFailed(object sender, EventArgs e)
        {
            this.updateInitiated = false;
            updateStatusText.Text = string.Format("No updates are currently available.".Localize());
            checkUpdateLink.Visible = true;
            downloadUpdateLink.Visible = false;
            installUpdateLink.Visible = false;
        }
    }
}
