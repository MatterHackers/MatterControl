using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ContactForm;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.ConfigurationPage;
using MatterHackers.MatterControl.HtmlParsing;
using MatterHackers.MatterControl.PrintLibrary;
using MatterHackers.MatterControl.PrintQueue;
using System.IO;
using System.Net;


namespace MatterHackers.MatterControl.AboutPage
{

    public class CheckForUpdateWindow : SystemWindow
    {

        private static CheckForUpdateWindow checkUpdate = null;
        TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();
        LinkButtonFactory linkButtonFactory = new LinkButtonFactory();
        string pathToStableBuildFeatures;
        StyledDropDownList releaseOptionsDropList;
        FlowLayoutWidget additionalInfoContainer;
        TextWidget stableInfoLabel;
        TextWidget alphaInfoLabel;
        TextWidget betaInfoLabel;

        public CheckForUpdateWindow()
            : base (620, 350)
        {
            linkButtonFactory.fontSize = 10;
            linkButtonFactory.textColor = ActiveTheme.Instance.SecondaryAccentColor;

            FlowLayoutWidget topToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
            topToBottom.AnchorAll();
            topToBottom.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;
            Padding = new BorderDouble(left: 5, right: 5);

            FlowLayoutWidget mainLabelContainer = new FlowLayoutWidget(FlowDirection.LeftToRight);
            mainLabelContainer.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
            mainLabelContainer.VAnchor = VAnchor.FitToChildren;
            mainLabelContainer.HAnchor = HAnchor.ParentLeftRight;
            
            FlowLayoutWidget currentFeedAndDropDownContainer = new FlowLayoutWidget(FlowDirection.LeftToRight);
            currentFeedAndDropDownContainer.VAnchor = VAnchor.FitToChildren;
            currentFeedAndDropDownContainer.HAnchor = HAnchor.ParentLeftRight;
            currentFeedAndDropDownContainer.Margin = new BorderDouble(0,5,0,0);
            currentFeedAndDropDownContainer.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;
           
            TextWidget checkUpdateLabel = new TextWidget("Check for Update".Localize(), pointSize: 20);
            checkUpdateLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
            checkUpdateLabel.Margin = new BorderDouble(5, 10, 10, 5);
            
            UpdateControlView updateStatusWidget = new UpdateControlView();
            
            String fullCurrentFeedLabel = "Update Channel".Localize();
            TextWidget feedLabel = new TextWidget(fullCurrentFeedLabel, pointSize: 12);
            feedLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
            feedLabel.VAnchor = VAnchor.ParentCenter;
            feedLabel.Margin = new BorderDouble(left: 5);

            releaseOptionsDropList = new StyledDropDownList("Development", maxHeight: 200);
            releaseOptionsDropList.HAnchor = HAnchor.ParentLeftRight;

            MenuItem releaseOptionsDropDownItem = releaseOptionsDropList.AddItem("Stable".Localize(), "release");
            releaseOptionsDropDownItem.Selected += new EventHandler(FixTabDot);

            MenuItem preReleaseDropDownItem = releaseOptionsDropList.AddItem("Beta".Localize(), "pre-release");
            preReleaseDropDownItem.Selected += new EventHandler(FixTabDot);

            MenuItem developmentDropDownItem = releaseOptionsDropList.AddItem("Alpha".Localize(), "development");
            developmentDropDownItem.Selected += new EventHandler(FixTabDot);

            List<string> acceptableUpdateFeedTypeValues = new List<string>() { "release", "pre-release", "development" };
            string currentUpdateFeedType = UserSettings.Instance.get("UpdateFeedType");

            if (acceptableUpdateFeedTypeValues.IndexOf(currentUpdateFeedType) == -1)
            {
                UserSettings.Instance.set("UpdateFeedType", "release");
            }

            releaseOptionsDropList.SelectedValue = UserSettings.Instance.get("UpdateFeedType");
            releaseOptionsDropList.SelectionChanged += new EventHandler(ReleaseOptionsDropList_SelectionChanged);

            FlowLayoutWidget additionalInfoContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
            additionalInfoContainer.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;
            additionalInfoContainer.HAnchor = HAnchor.ParentLeftRight;
            additionalInfoContainer.Padding = new BorderDouble(left: 6, top: 6);
             
            string stableFeedInfoText = "Stable: Get the current release version of MatterControl(recommended).".Localize();
            stableInfoLabel = new TextWidget(stableFeedInfoText);
            stableInfoLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
            stableInfoLabel.HAnchor = HAnchor.ParentLeftRight;
            stableInfoLabel.Margin = new BorderDouble(bottom:10);
            additionalInfoContainer.AddChild(stableInfoLabel);

            string betaFeedInfoText = "Beta: Release candidate versions of MatterControl.".Localize();
            betaInfoLabel = new TextWidget(betaFeedInfoText);
            betaInfoLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
            betaInfoLabel.HAnchor = HAnchor.ParentLeftRight;
            betaInfoLabel.Margin = new BorderDouble(bottom: 10);
            additionalInfoContainer.AddChild(betaInfoLabel);

            string alphaFeedInfoText = "Alpha: Access development versions of MatterControl(not recommended).".Localize();
            alphaInfoLabel = new TextWidget(alphaFeedInfoText);
            alphaInfoLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
            alphaInfoLabel.HAnchor = HAnchor.ParentLeftRight;
            alphaInfoLabel.Margin = new BorderDouble(bottom: 10);
            additionalInfoContainer.AddChild(alphaInfoLabel);

            FlowLayoutWidget buttonContainer = new FlowLayoutWidget(FlowDirection.LeftToRight);
            buttonContainer.HAnchor = HAnchor.ParentLeftRight;
            buttonContainer.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
            
            Button closeButton = textImageButtonFactory.Generate("Close".Localize(), centerText: true);
            closeButton.Click += (sender, e) =>
            {
                CloseOnIdle();
            };

            Button whatsThisLink = linkButtonFactory.Generate("What's this?".Localize());
            whatsThisLink.VAnchor = VAnchor.ParentCenter;
            whatsThisLink.Margin = new BorderDouble(left: 6);
            whatsThisLink.Click += (sender, e) =>
            {
                UiThread.RunOnIdle(() =>
                {
                    if (!additionalInfoContainer.Visible)
                    {
                        additionalInfoContainer.Visible = true;
                    }
                    else
                    {
                        additionalInfoContainer.Visible = false;
                    }
                });
            };

            

            mainLabelContainer.AddChild(checkUpdateLabel);
            topToBottom.AddChild(mainLabelContainer);
            topToBottom.AddChild(updateStatusWidget);
            currentFeedAndDropDownContainer.AddChild(feedLabel);
            currentFeedAndDropDownContainer.AddChild(whatsThisLink);
            currentFeedAndDropDownContainer.AddChild(new HorizontalSpacer());
            currentFeedAndDropDownContainer.AddChild(releaseOptionsDropList);
            topToBottom.AddChild(currentFeedAndDropDownContainer);
            topToBottom.AddChild(additionalInfoContainer);
            buttonContainer.AddChild(new HorizontalSpacer());
            buttonContainer.AddChild(closeButton);
            topToBottom.AddChild(new VerticalSpacer());
            topToBottom.AddChild(buttonContainer);
            this.AddChild(topToBottom);

            additionalInfoContainer.Visible = false;
            this.Title = "Check for Update".Localize();
            this.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
            this.ShowAsSystemWindow();

        }

        public static void Show()
        {
            if (checkUpdate == null)
            {
                checkUpdate = new CheckForUpdateWindow();
                checkUpdate.Closed += (parentSender, e) =>
                {
                    checkUpdate = null;
                };
            }
            else
            {
                checkUpdate.BringToFront();
            }
        }

        private void ReleaseOptionsDropList_SelectionChanged(object sender, EventArgs e)
        {
            //getAdditionalFeedInfo();
            
            string releaseCode = ((StyledDropDownList)sender).SelectedValue;
            if (releaseCode != UserSettings.Instance.get("UpdateFeedType"))
            {    
                UserSettings.Instance.set("UpdateFeedType", releaseCode);
            }
           
        }

        private void FixTabDot(object sender, EventArgs e)
        {
            UpdateControlData.Instance.CheckForUpdateUserRequested();
        }


       /*private void getAdditionalFeedInfo()
        {
            

            if (releaseOptionsDropList.SelectedLabel == "release")
            {
                stableInfoLabel.Visible = true;
                betaInfoLabel.Visible = false;
                alphaInfoLabel.Visible = false;
            }
            else if (releaseOptionsDropList.SelectedLabel == "pre-release")
            {
                stableInfoLabel.Visible = false;
                betaInfoLabel.Visible = true;
                alphaInfoLabel.Visible = false;
            }
            else
            {
                stableInfoLabel.Visible = false;
                betaInfoLabel.Visible = false;
                alphaInfoLabel.Visible = true;
            }
        }*/

    }
}
