using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.VersionManagement;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.ContactForm;
using MatterHackers.MatterControl.PluginSystem;
using MatterHackers.Localizations;

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
            textImageButtonFactory.normalFillColor = ActiveTheme.Instance.PrimaryTextColor;
            textImageButtonFactory.normalTextColor = RGBA_Bytes.Black;

            HAnchor = HAnchor.ParentLeftRight;
            BackgroundColor = ActiveTheme.Instance.TransparentDarkOverlay;
            Padding = new BorderDouble(6, 5);
            {
                updateStatusText = new TextWidget(string.Format(""), textColor: offWhite);
                updateStatusText.AutoExpandBoundsToText = true;
                updateStatusText.VAnchor = VAnchor.ParentCenter;

                GuiWidget horizontalSpacer = new GuiWidget();
                horizontalSpacer.HAnchor = Agg.UI.HAnchor.ParentLeftRight;

				checkUpdateLink = textImageButtonFactory.Generate(new LocalizedString("Check for Update").Translated);
                checkUpdateLink.VAnchor = VAnchor.ParentCenter;
                checkUpdateLink.Click += CheckForUpdate;
                checkUpdateLink.Visible = false;

				downloadUpdateLink = textImageButtonFactory.Generate(new LocalizedString("Download Update").Translated);
                downloadUpdateLink.VAnchor = VAnchor.ParentCenter;
                downloadUpdateLink.Click += DownloadUpdate;
                downloadUpdateLink.Visible = false;


				installUpdateLink = textImageButtonFactory.Generate(new LocalizedString("Install Update").Translated);
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
                    && DateTime.Compare(firstSession.SessionStart.AddDays(14), DateTime.Now) < 0)
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
				updateStatusText.Text = new LocalizedString("Checking for updates...").Translated;
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

            string updateFileName = System.IO.Path.Combine(updateFileLocation, string.Format("{0}.{1}", downloadToken, InstallerExtension));
            string releaseVersion = ApplicationSettings.Instance.get("CurrentReleaseVersion");
            string friendlyFileName = System.IO.Path.Combine(updateFileLocation, string.Format("MatterControlSetup-{0}.{1}", releaseVersion, InstallerExtension));

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
				updateStatusText.Text = string.Format(new LocalizedString("Oops! Unable to install update.").Translated);
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
				updateStatusText.Text = string.Format(new LocalizedString("Downloading updates...").Translated);
                updateInitiated = true;

                string downloadUri = string.Format("https://mattercontrol.appspot.com/downloads/development/{0}", ApplicationSettings.Instance.get("CurrentBuildToken"));
                string downloadToken = ApplicationSettings.Instance.get("CurrentBuildToken");

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
			updateStatusText.Text = string.Format(new LocalizedString("Downloading updates...").Translated);
        }

        void DownloadCompleted(object sender, EventArgs e)
        {
            this.updateInitiated = false;
			updateStatusText.Text = string.Format(new LocalizedString("New updates are ready to install.").Translated);
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
				updateStatusText.Text = string.Format(new LocalizedString("New updates may be available.").Translated);
                checkUpdateLink.Visible = true;
            }
            else if (System.IO.File.Exists(updateFileName))
            {
				updateStatusText.Text = string.Format(new LocalizedString("New updates are ready to install.").Translated);
                installUpdateLink.Visible = true;
                checkUpdateLink.Visible = false;
            }
            else
            {
                //updateStatusText.Text = string.Format("New version available: {0}", ApplicationSettings.Instance.get("CurrentReleaseVersion"));
				updateStatusText.Text = string.Format(new LocalizedString("There are updates available.").Translated);
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
				updateStatusText.Text = string.Format(new LocalizedString("Your application is up-to-date.").Translated);
                downloadUpdateLink.Visible = false;
                installUpdateLink.Visible = false;
                checkUpdateLink.Visible = true;

            }
            else if (System.IO.File.Exists(updateFileName))
            {
				updateStatusText.Text = string.Format(new LocalizedString("New updates are ready to install.").Translated);
                downloadUpdateLink.Visible = false;
                installUpdateLink.Visible = true;
                checkUpdateLink.Visible = false;

            }
            else
            {
				updateStatusText.Text = string.Format(new LocalizedString("There is a recommended update available.").Translated);
                //updateStatusText.Text = string.Format("New version available: {0}", ApplicationSettings.Instance.get("CurrentReleaseVersion"));
                downloadUpdateLink.Visible = true;
                installUpdateLink.Visible = false;
                checkUpdateLink.Visible = false;
            }

            MainSlidePanel.Instance.SetUpdateNotification(this, null);
        }

        void onVersionRequestFailed(object sender, EventArgs e)
        {
            this.updateInitiated = false;
			updateStatusText.Text = string.Format(new LocalizedString("No updates are currently available.").Translated);
            checkUpdateLink.Visible = true;
            downloadUpdateLink.Visible = false;
            installUpdateLink.Visible = false;
        }
    }

    public class AboutPage : GuiWidget
    {
        LinkButtonFactory linkButtonFactory = new LinkButtonFactory();
        TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();
        RGBA_Bytes offWhite = new RGBA_Bytes(245, 245, 245);
        
        public AboutPage()
        {
            this.HAnchor = HAnchor.ParentLeftRight;
            this.VAnchor = VAnchor.ParentTop;
            
            this.Padding = new BorderDouble(5);
            this.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

            linkButtonFactory.fontSize = 12;
            linkButtonFactory.textColor = offWhite;

            textImageButtonFactory.normalFillColor = ActiveTheme.Instance.PrimaryTextColor;
            textImageButtonFactory.normalTextColor = RGBA_Bytes.Black;

            FlowLayoutWidget customInfoTopToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
            customInfoTopToBottom.Name = "AboutPageCustomInfo";
            customInfoTopToBottom.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
            customInfoTopToBottom.VAnchor = Agg.UI.VAnchor.Max_FitToChildren_ParentHeight;
            customInfoTopToBottom.Padding = new BorderDouble(5, 10);

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
				TextWidget headerText = new TextWidget(string.Format(new LocalizedString("MatterControl").Translated), textColor: RGBA_Bytes.White, pointSize: 20);
                headerText.MinimumSize = new VectorMath.Vector2(headerText.Width, headerText.Height);
                headerText.HAnchor = Agg.UI.HAnchor.ParentCenter;
                headerContainer.AddChild(headerText);

				TextWidget versionText = new TextWidget(string.Format(new LocalizedString("Version {0}").Translated, VersionInfo.Instance.ReleaseVersion), textColor: RGBA_Bytes.White, pointSize: 10);
                versionText.MinimumSize = new VectorMath.Vector2(versionText.Width, versionText.Height);
                versionText.HAnchor = Agg.UI.HAnchor.ParentCenter;
                headerContainer.AddChild(versionText);

				TextWidget developedByText = new TextWidget(new LocalizedString("Developed by MatterHackers").Translated, textColor: RGBA_Bytes.White);
                developedByText.Margin = new BorderDouble(top: 5);
                developedByText.HAnchor = Agg.UI.HAnchor.ParentCenter;
                developedByText.MinimumSize = new VectorMath.Vector2(developedByText.Width, developedByText.Height);
                headerContainer.AddChild(developedByText);
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
					TextWidget donateStartText = new TextWidget(new LocalizedString("Please consider ").Translated, textColor: RGBA_Bytes.White);
                    donateTextContanier.AddChild(donateStartText);
					Button matterControlDonateLink = linkButtonFactory.Generate(new LocalizedString("donating").Translated);
                    matterControlDonateLink.OriginRelativeParent = new VectorMath.Vector2(matterControlDonateLink.OriginRelativeParent.x, matterControlDonateLink.OriginRelativeParent.y + donateStartText.Printer.TypeFaceStyle.DescentInPixels);
                    matterControlDonateLink.Click += (sender, mouseEvent) => { System.Diagnostics.Process.Start("http://www.matterhackers.com/store/printer-accessories/mattercontrol-donation"); };
                    donateTextContanier.AddChild(matterControlDonateLink);
					donateTextContanier.AddChild(new TextWidget(new LocalizedString(" to help support and improve MatterControl.").Translated, textColor: RGBA_Bytes.White));
                    donateTextContanier.HAnchor = Agg.UI.HAnchor.ParentCenter;
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

				Button feedbackLink = textImageButtonFactory.Generate(new LocalizedString("Send Feedback").Translated);

                feedbackLink.Click += (sender, mouseEvent) => { ContactFormWindow.Open(); };
                feedbackButtons.AddChild(feedbackLink);

                GuiWidget buttonSpacer = new GuiWidget(10, 10);
                feedbackButtons.AddChild(buttonSpacer);
            }

            topToBottom.AddChild(feedbackButtons);

			Button learnMoreLink = linkButtonFactory.Generate(new LocalizedString("www.matterhackers.com").Translated);
            learnMoreLink.Margin = new BorderDouble(right: 12);
            learnMoreLink.Click += (sender, mouseEvent) => { System.Diagnostics.Process.Start("http://www.matterhackers.com?clk=mc"); };
            learnMoreLink.HAnchor = HAnchor.ParentCenter;
            learnMoreLink.Margin = new BorderDouble(0, 5);
            topToBottom.AddChild(learnMoreLink);

			TextWidget copyrightText = new TextWidget(string.Format(new LocalizedString("Copyright © 2013 MatterHackers, Inc.").Translated), textColor: offWhite);
            copyrightText.HAnchor = Agg.UI.HAnchor.ParentCenter;
            topToBottom.AddChild(copyrightText);

            {
                FlowLayoutWidget leftToRightBuildInfo = new FlowLayoutWidget();
                leftToRightBuildInfo.HAnchor |= HAnchor.ParentCenter;

				TextWidget buildText = new TextWidget(string.Format(new LocalizedString("Build: {0} | ").Translated, VersionInfo.Instance.BuildVersion), textColor: offWhite, pointSize: 10);
                leftToRightBuildInfo.AddChild(buildText);

                double oldFontSize = linkButtonFactory.fontSize;
                linkButtonFactory.fontSize = 10;
				Button deleteCacheLink = linkButtonFactory.Generate(new LocalizedString("Clear Cache").Translated);
                linkButtonFactory.fontSize = oldFontSize;
                deleteCacheLink.OriginRelativeParent = new VectorMath.Vector2(deleteCacheLink.OriginRelativeParent.x, deleteCacheLink.OriginRelativeParent.y + buildText.Printer.TypeFaceStyle.DescentInPixels);
                deleteCacheLink.Click += (sender, mouseEvent) => { DeleteCacheData(); };
                leftToRightBuildInfo.AddChild(deleteCacheLink);

                topToBottom.AddChild(leftToRightBuildInfo);
            }
        }

        public static void InsertAttributionText(GuiWidget topToBottom, LinkButtonFactory linkButtonFactory)
        {
            // slicer credit
            {
                FlowLayoutWidget donateTextContainer = new FlowLayoutWidget();
				TextWidget thanksText = new TextWidget(new LocalizedString("Special thanks to Alessandro Ranellucci for his incredible work on ").Translated, textColor: RGBA_Bytes.White);
                donateTextContainer.AddChild(thanksText);
				Button slic3rOrgLink = linkButtonFactory.Generate(new LocalizedString("Slic3r").Translated);
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
				TextWidget donateStartText = new TextWidget(new LocalizedString("and to David Braam and Ultimaker BV, for the amazing ").Translated, textColor: RGBA_Bytes.White);
                curaEngineTextContanier.AddChild(donateStartText);

				Button curaEngineSourceLink = linkButtonFactory.Generate(new LocalizedString("CuraEngine").Translated);
                curaEngineSourceLink.OriginRelativeParent = new VectorMath.Vector2(curaEngineSourceLink.OriginRelativeParent.x, curaEngineSourceLink.OriginRelativeParent.y + donateStartText.Printer.TypeFaceStyle.DescentInPixels);
                curaEngineSourceLink.Click += (sender, mouseEvent) => { System.Diagnostics.Process.Start("https://github.com/Ultimaker/CuraEngine"); };
                curaEngineTextContanier.AddChild(curaEngineSourceLink);
                curaEngineTextContanier.AddChild(new TextWidget(".", textColor: RGBA_Bytes.White));

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

            graphics2D.FillRectangle(new RectangleDouble(0, this.Height - 1, this.Width, this.Height), RGBA_Bytes.White);
            base.OnDraw(graphics2D);
        }
    }
}
