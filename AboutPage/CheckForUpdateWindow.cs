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
        StyledDropDownList releaseOptionsDropList;
        String additionalFeedbackInformation;
        TextWidget stableInfoLabel;
        TextWidget alphaInfoLabel;
        TextWidget betaInfoLabel;

        public CheckForUpdateWindow()
            : base (500,300)
        {


            FlowLayoutWidget topToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
			topToBottom.AnchorAll();

            FlowLayoutWidget mainLabelContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
            

            FlowLayoutWidget currentFeedAndDropDownContainer = new FlowLayoutWidget(FlowDirection.LeftToRight);
            currentFeedAndDropDownContainer.VAnchor = VAnchor.FitToChildren;
            currentFeedAndDropDownContainer.HAnchor = HAnchor.ParentLeftRight;
            currentFeedAndDropDownContainer.Margin = new BorderDouble(0,0,10,0);
           

            TextWidget checkUpdateLabel = new TextWidget("Update Status".Localize(), pointSize: 20);
            checkUpdateLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
            checkUpdateLabel.Margin = new BorderDouble(5, 10, 10, 5);


            HorizontalLine topLine = new HorizontalLine(ActiveTheme.Instance.PrimaryAccentColor);
            UpdateControlView updateStatusWidget = new UpdateControlView();
            HorizontalLine horizontalLine = new HorizontalLine(ActiveTheme.Instance.PrimaryAccentColor);


            String fullCurrentFeedLabel = "Select Update Notification Feed: ";
            TextWidget feedLabel = new TextWidget(fullCurrentFeedLabel, pointSize: 12);
            feedLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
            feedLabel.VAnchor = VAnchor.ParentCenter;

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


            
            TextWidget additionalInfoHeader = new TextWidget("Additional Feed Info: ", 12);
            additionalInfoHeader.TextColor = ActiveTheme.Instance.PrimaryTextColor;
            

            string stableFeedInfoText = "Access to the release feed and current stable build";
            stableInfoLabel = new TextWidget(stableFeedInfoText);
            stableInfoLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;

            string alphaFeedInfoText = "Alpha feed provides accessto updates that add new features but may not be reliable for everyday use";
            alphaInfoLabel = new TextWidget(alphaFeedInfoText);
            alphaInfoLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;

            string betaFeedInfoText = "Beta feed grants access to updates and features that are candidates for release in the Stable feed";
            betaInfoLabel = new TextWidget(betaFeedInfoText);
            betaInfoLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
            

            FlowLayoutWidget buttonContainer = new FlowLayoutWidget(FlowDirection.LeftToRight);
            buttonContainer.HAnchor = HAnchor.ParentLeftRight;
            buttonContainer.VAnchor = VAnchor.FitToChildren;


            Button closeButton = textImageButtonFactory.Generate("Close".Localize(), centerText: true);
            closeButton.Click += (sender, e) =>
            {
                CloseOnIdle();
            };

           
            topToBottom.AddChild(mainLabelContainer);
            topToBottom.AddChild(topLine);
            topToBottom.AddChild(updateStatusWidget);
            topToBottom.AddChild(horizontalLine);
            currentFeedAndDropDownContainer.AddChild(feedLabel);
            currentFeedAndDropDownContainer.AddChild(new HorizontalSpacer());
            currentFeedAndDropDownContainer.AddChild(releaseOptionsDropList);
            topToBottom.AddChild(currentFeedAndDropDownContainer);
            topToBottom.AddChild(additionalInfoHeader);
            topToBottom.AddChild(stableInfoLabel);
            topToBottom.AddChild(alphaInfoLabel);
            topToBottom.AddChild(betaInfoLabel);
            buttonContainer.AddChild(new HorizontalSpacer());
            buttonContainer.AddChild(closeButton);
            topToBottom.AddChild(new VerticalSpacer());
            topToBottom.AddChild(buttonContainer);
            mainLabelContainer.AddChild(checkUpdateLabel);

            getAdditionalFeedInfo();

            this.AddChild(topToBottom);
            this.Title = "Check For Update".Localize();
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
            getAdditionalFeedInfo();
            
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

        private void getAdditionalFeedInfo()
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
        }

    }
}
