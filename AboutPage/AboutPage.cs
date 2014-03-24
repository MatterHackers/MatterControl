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
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ContactForm;
using MatterHackers.MatterControl.VersionManagement;
using MatterHackers.MatterControl.CustomWidgets;

namespace MatterHackers.MatterControl
{
    public class HTMLCanvas : GuiWidget
    {
        Dictionary<string, string> TemplateValue = new Dictionary<string, string>();

        public HTMLCanvas()
        {
        }

        public HTMLCanvas(string htmlContent)
        {
            LoadHTML(htmlContent);
        }

        public void AddReplacementString(string key, string value)
        {
            TemplateValue.Add("{{" + key + "}}", value);
        }

        public void LoadHTML(string htmlContent)
        {
            htmlContent = DoTemplateReplacements(htmlContent);
            TextWidget textwdigt = new TextWidget("some test text");
            textwdigt.AnchorCenter();
            AddChild(textwdigt);
        }

        public string DoTemplateReplacements(string htmlContent)
        {
            StringBuilder sb = new StringBuilder(htmlContent);

            foreach (KeyValuePair<string, string> replacement in TemplateValue)
            {
                sb.Replace(replacement.Key, replacement.Value);
            }

            return sb.ToString();
        }
    }

    public class UpdateControl : FlowLayoutWidget
    {
        bool updateInitiated = false;
        Button downloadUpdateLink;
        Button checkUpdateLink;
        Button installUpdateLink;
        int downloadPercent = 0;
        TextWidget updateStatusText;
        RGBA_Bytes offWhite = new RGBA_Bytes(245, 245, 245);
        TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();

        public UpdateControl()
        {
            textImageButtonFactory.normalFillColor = RGBA_Bytes.Gray;
            textImageButtonFactory.normalTextColor = RGBA_Bytes.White;

            HAnchor = HAnchor.ParentLeftRight;
            BackgroundColor = ActiveTheme.Instance.TransparentDarkOverlay;
            Padding = new BorderDouble(6, 5);
            {
                updateStatusText = new TextWidget(string.Format(""), textColor: offWhite);
                updateStatusText.AutoExpandBoundsToText = true;
                updateStatusText.VAnchor = VAnchor.ParentCenter;

                GuiWidget horizontalSpacer = new GuiWidget();
                horizontalSpacer.HAnchor = Agg.UI.HAnchor.ParentLeftRight;

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
        static string updateFileLocation = System.IO.Path.Combine(applicationDataPath, "updates");

        public void InstallUpdate(object sender, MouseEventArgs e)
        {
            string downloadToken = ApplicationSettings.Instance.get("CurrentBuildToken");

            string updateFileName = System.IO.Path.Combine(updateFileLocation, "{0}.{1}".FormatWith(downloadToken, InstallerExtension));
            string releaseVersion = ApplicationSettings.Instance.get("CurrentReleaseVersion");
            string friendlyFileName = System.IO.Path.Combine(updateFileLocation, "MatterControlSetup-{0}.{1}".FormatWith(releaseVersion, InstallerExtension));

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

                int downloadSize;
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

                string updateFileName = System.IO.Path.Combine(updateFileLocation, string.Format("{0}.{1}", downloadToken, InstallerExtension));

                WebClient webClient = new WebClient();
                webClient.DownloadFileCompleted += new AsyncCompletedEventHandler(DownloadCompleted);
                webClient.DownloadProgressChanged += new DownloadProgressChangedEventHandler(DownloadProgressChanged);
                webClient.DownloadFileAsync(new Uri(downloadUri), updateFileName);
            }
        }

        void DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            this.downloadPercent = e.ProgressPercentage; //This doesn't work currently
            updateStatusText.Text = string.Format("Downloading updates...".Localize());
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
            string updateFileName = System.IO.Path.Combine(updateFileLocation, string.Format("{0}.{1}", currentBuildToken, InstallerExtension));

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
            string updateFileName = System.IO.Path.Combine(updateFileLocation, string.Format("{0}.{1}", currentBuildToken, InstallerExtension));

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

    public class AboutPage : GuiWidget
    {
        LinkButtonFactory linkButtonFactory = new LinkButtonFactory();
        TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();
        RGBA_Bytes aboutTextColor = ActiveTheme.Instance.PrimaryTextColor;
        
        public AboutPage()
        {
            this.HAnchor = HAnchor.ParentLeftRight;
            this.VAnchor = VAnchor.ParentTop;
            
            this.Padding = new BorderDouble(5);
            this.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

            linkButtonFactory.fontSize = 12;
            linkButtonFactory.textColor = aboutTextColor;

            textImageButtonFactory.normalFillColor = RGBA_Bytes.Gray;
            textImageButtonFactory.normalTextColor = RGBA_Bytes.White;

            FlowLayoutWidget customInfoTopToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
            customInfoTopToBottom.Name = "AboutPageCustomInfo";
            customInfoTopToBottom.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
            customInfoTopToBottom.VAnchor = Agg.UI.VAnchor.Max_FitToChildren_ParentHeight;
            customInfoTopToBottom.Padding = new BorderDouble(5, 10, 5, 0);

            customInfoTopToBottom.AddChild(new UpdateControl());
            AddMatterHackersInfo(customInfoTopToBottom);

            this.AddChild(customInfoTopToBottom);
        }

        private void AddMatterHackersInfo(FlowLayoutWidget topToBottom)
        {
            FlowLayoutWidget headerContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
            headerContainer.Margin = new BorderDouble(0, 0, 0, 10);
            headerContainer.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
            {
                TextWidget headerText = new TextWidget(string.Format("MatterControl"), textColor: aboutTextColor, pointSize: 20);
                headerText.MinimumSize = new VectorMath.Vector2(headerText.Width, headerText.Height);
                headerText.HAnchor = Agg.UI.HAnchor.ParentCenter;
                headerContainer.AddChild(headerText);

                TextWidget versionText = new TextWidget(string.Format("Version {0}".Localize(), VersionInfo.Instance.ReleaseVersion).ToUpper(), textColor: aboutTextColor, pointSize: 10);
                versionText.MinimumSize = new VectorMath.Vector2(versionText.Width, versionText.Height);
                versionText.HAnchor = Agg.UI.HAnchor.ParentCenter;
                headerContainer.AddChild(versionText);

                FlowLayoutWidget developedByContainer = new FlowLayoutWidget();
                developedByContainer.Margin = new BorderDouble(top: 5);
                developedByContainer.HAnchor = HAnchor.ParentLeftRight;

                TextWidget developedByText = new TextWidget("Developed By: ".Localize().ToUpper(), pointSize:10, textColor: aboutTextColor);
                                
                developedByText.MinimumSize = new VectorMath.Vector2(developedByText.Width, developedByText.Height);

                TextWidget MatterHackersText = new TextWidget("MatterHackers", pointSize: 14, textColor: aboutTextColor);


                developedByContainer.AddChild(new HorizontalSpacer());
                developedByContainer.AddChild(developedByText);
                developedByContainer.AddChild(MatterHackersText);
                developedByContainer.AddChild(new HorizontalSpacer());

                headerContainer.AddChild(developedByContainer);
            }
            topToBottom.AddChild(headerContainer);

            GuiWidget topSpacer = new GuiWidget();
            topSpacer.VAnchor = VAnchor.ParentBottomTop;
            topToBottom.AddChild(topSpacer);

            // Slicer and agg thanks
            {
                // donate to mc
                {
                    FlowLayoutWidget donateTextContanier = new FlowLayoutWidget();
                    donateTextContanier.HAnchor = Agg.UI.HAnchor.ParentLeftRight;

                    TextWidget donateStartText = new TextWidget("Please consider ".Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor);
                    TextWidget donateEndText = new TextWidget(" to help support MatterControl.".Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor);

                    donateTextContanier.AddChild(new HorizontalSpacer());
                    donateTextContanier.AddChild(donateStartText);
                    donateTextContanier.AddChild(getDonateLink(donateStartText));
                    donateTextContanier.AddChild(donateEndText);
                    donateTextContanier.AddChild(new HorizontalSpacer());

                    topToBottom.AddChild(donateTextContanier);
                }

                // spacer
                topToBottom.AddChild(new GuiWidget(10, 15));

                InsertAttributionText(topToBottom, linkButtonFactory);
            }

            GuiWidget bottomSpacer = new GuiWidget();
            bottomSpacer.VAnchor = VAnchor.ParentBottomTop;
            topToBottom.AddChild(bottomSpacer);

            FlowLayoutWidget feedbackButtons = new FlowLayoutWidget();
            feedbackButtons.Margin = new BorderDouble(bottom: 10);
            {
                feedbackButtons.HAnchor |= Agg.UI.HAnchor.ParentCenter;

                Button feedbackLink = textImageButtonFactory.Generate("Send Feedback".Localize());

                feedbackLink.Click += (sender, mouseEvent) => { ContactFormWindow.Open(); };
                feedbackButtons.AddChild(feedbackLink);

                GuiWidget buttonSpacer = new GuiWidget(10, 10);
                feedbackButtons.AddChild(buttonSpacer);
            }

            topToBottom.AddChild(feedbackButtons);

            Button learnMoreLink = linkButtonFactory.Generate("www.matterhackers.com");
            learnMoreLink.Margin = new BorderDouble(right: 12);
            learnMoreLink.Click += (sender, mouseEvent) => { System.Diagnostics.Process.Start("http://www.matterhackers.com?clk=mc"); };
            learnMoreLink.HAnchor = HAnchor.ParentCenter;
            learnMoreLink.Margin = new BorderDouble(0, 5);
            topToBottom.AddChild(learnMoreLink);

            TextWidget copyrightText = new TextWidget(string.Format("Copyright © 2014 MatterHackers, Inc."), textColor: aboutTextColor);
            copyrightText.HAnchor = Agg.UI.HAnchor.ParentCenter;
            topToBottom.AddChild(copyrightText);

            {
                FlowLayoutWidget leftToRightBuildInfo = new FlowLayoutWidget();
                leftToRightBuildInfo.HAnchor |= HAnchor.ParentCenter;

                TextWidget buildText = new TextWidget(string.Format("Build: {0} | ".Localize(), VersionInfo.Instance.BuildVersion), textColor: aboutTextColor, pointSize: 10);
                leftToRightBuildInfo.AddChild(buildText);

                double oldFontSize = linkButtonFactory.fontSize;
                linkButtonFactory.fontSize = 10;
                Button deleteCacheLink = linkButtonFactory.Generate("Clear Cache".Localize());
                linkButtonFactory.fontSize = oldFontSize;
                deleteCacheLink.OriginRelativeParent = new VectorMath.Vector2(deleteCacheLink.OriginRelativeParent.x, deleteCacheLink.OriginRelativeParent.y + buildText.Printer.TypeFaceStyle.DescentInPixels);
                deleteCacheLink.Click += (sender, mouseEvent) => { DeleteCacheData(); };
                leftToRightBuildInfo.AddChild(deleteCacheLink);

                topToBottom.AddChild(leftToRightBuildInfo);
            }
        }

        Button getDonateLink(TextWidget donateStartText )
        {
            Button matterControlDonateLink = linkButtonFactory.Generate("donating".Localize());
            matterControlDonateLink.OriginRelativeParent = new VectorMath.Vector2(matterControlDonateLink.OriginRelativeParent.x, matterControlDonateLink.OriginRelativeParent.y + donateStartText.Printer.TypeFaceStyle.DescentInPixels);
            matterControlDonateLink.Click += (sender, mouseEvent) => { System.Diagnostics.Process.Start("http://www.matterhackers.com/store/printer-accessories/mattercontrol-donation"); };
            return matterControlDonateLink;
        }

        public static void InsertAttributionText(GuiWidget topToBottom, LinkButtonFactory linkButtonFactory)
        {
            // slicer credit
            {
                FlowLayoutWidget donateTextContainer = new FlowLayoutWidget();
                TextWidget thanksText = new TextWidget("Special thanks to Alessandro Ranellucci for his incredible work on ".Localize() ,textColor: ActiveTheme.Instance.PrimaryTextColor);
                
                donateTextContainer.AddChild(thanksText);
                Button slic3rOrgLink = linkButtonFactory.Generate("Slic3r");
                //slic3rOrgLink.VAnchor = Agg.UI.VAnchor.Bottom;
                slic3rOrgLink.OriginRelativeParent = new VectorMath.Vector2(slic3rOrgLink.OriginRelativeParent.x, slic3rOrgLink.OriginRelativeParent.y + thanksText.Printer.TypeFaceStyle.DescentInPixels);
                slic3rOrgLink.Click += (sender, mouseEvent) => { System.Diagnostics.Process.Start("https://github.com/alexrj/Slic3r"); };
                donateTextContainer.AddChild(slic3rOrgLink);
                donateTextContainer.HAnchor = Agg.UI.HAnchor.ParentCenter;
                topToBottom.AddChild(donateTextContainer);
            }

            // cura engine credit
            {
                FlowLayoutWidget curaEngineTextContanier = new FlowLayoutWidget();
                TextWidget donateStartText = new TextWidget("and to David Braam and Ultimaker BV, for the amazing ".Localize(), textColor: ActiveTheme.Instance.PrimaryTextColor);
                curaEngineTextContanier.AddChild(donateStartText);

                Button curaEngineSourceLink = linkButtonFactory.Generate("CuraEngine");
                curaEngineSourceLink.OriginRelativeParent = new VectorMath.Vector2(curaEngineSourceLink.OriginRelativeParent.x, curaEngineSourceLink.OriginRelativeParent.y + donateStartText.Printer.TypeFaceStyle.DescentInPixels);
                curaEngineSourceLink.Click += (sender, mouseEvent) => { System.Diagnostics.Process.Start("https://github.com/Ultimaker/CuraEngine"); };
                curaEngineTextContanier.AddChild(curaEngineSourceLink);
                curaEngineTextContanier.AddChild(new TextWidget(".", textColor: ActiveTheme.Instance.PrimaryTextColor));

                curaEngineTextContanier.HAnchor = Agg.UI.HAnchor.ParentCenter;
                topToBottom.AddChild(curaEngineTextContanier);
            }
        }

        public static void DeleteCacheData()
        {
            // delete everything in the GCodeOutputPath
            //   AppData\Local\MatterControl\data\gcode
            // delete everything in the temp data that is not in use
            //   AppData\Local\MatterControl\data\temp
            //     plateImages
            //     project-assembly
            //     project-extract
            //     stl

            // first AppData\Local\MatterControl\data\gcode
            string gcodeOutputPath = DataStorage.ApplicationDataStorage.Instance.GCodeOutputPath;
            try
            {
                Directory.Delete(gcodeOutputPath, true);
            }
            catch (Exception)
            {
            }
        }


        bool firstDraw = true;
        public override void OnDraw(Graphics2D graphics2D)
        {
#if false
            if (firstDraw)
            {
                firstDraw = false;
                SystemWindow testAbout = new SystemWindow(600, 300);

                string path = Path.Combine(ApplicationDataStorage.Instance.ApplicationStaticDataPath, "OEMSettings", "AboutPage.html");
                string htmlText = File.ReadAllText(path);
                HTMLCanvas canvas = new HTMLCanvas(htmlText);
                canvas.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

                canvas.AddReplacementString("textColor", RGBA_Bytes.White.GetAsHTMLString());

                canvas.AnchorAll();
                testAbout.AddChild(canvas);

                testAbout.ShowAsSystemWindow();
            }
#endif

            graphics2D.FillRectangle(new RectangleDouble(0, this.Height - 1, this.Width, this.Height), ActiveTheme.Instance.PrimaryTextColor);
            base.OnDraw(graphics2D);
        }
    }
}
