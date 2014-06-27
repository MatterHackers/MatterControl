using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;

namespace MatterHackers.MatterControl
{
    public class WizardPage : GuiWidget
    {
        string stepDescription = "";
        public string StepDescription { get { return stepDescription; } set { stepDescription = value; } }
        public WizardPage(string stepDescription)
        {
            StepDescription = stepDescription;
        }

        public virtual void PageIsBecomingActive()
        {
        }

        public virtual void PageIsBecomingInactive()
        {
        }
    }

    public class WizardControl : GuiWidget
    {
        protected TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();

        FlowLayoutWidget bottomToTopLayout;
        List<WizardPage> pages = new List<WizardPage>();
        int pageIndex = 0;
        public Button backButton;
        public Button nextButton;
        Button doneButton;
        Button cancelButton;

        TextWidget stepDescriptionWidget;

        public string StepDescription
        {
            get { return stepDescriptionWidget.Text; }
            set { stepDescriptionWidget.Text = value; }
        }

        public WizardControl()
        {
            FlowLayoutWidget topToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
            topToBottom.AnchorAll();
            topToBottom.Padding = new BorderDouble(3, 0, 3, 5);

            FlowLayoutWidget headerRow = new FlowLayoutWidget(FlowDirection.LeftToRight);
            headerRow.HAnchor = HAnchor.ParentLeftRight;
            headerRow.Margin = new BorderDouble(0, 3, 0, 0);
            headerRow.Padding = new BorderDouble(0, 3, 0, 3);

            {
                string titleString = LocalizedString.Get("Title Stuff".Localize());
                stepDescriptionWidget = new TextWidget(titleString, pointSize: 14);
                stepDescriptionWidget.AutoExpandBoundsToText = true;
                stepDescriptionWidget.TextColor = ActiveTheme.Instance.PrimaryTextColor;
                stepDescriptionWidget.HAnchor = HAnchor.ParentLeftRight;
                stepDescriptionWidget.VAnchor = Agg.UI.VAnchor.ParentBottom;

                headerRow.AddChild(stepDescriptionWidget);
            }

            topToBottom.AddChild(headerRow);

            textImageButtonFactory.normalTextColor = ActiveTheme.Instance.PrimaryTextColor;
            textImageButtonFactory.hoverTextColor = ActiveTheme.Instance.PrimaryTextColor;
            textImageButtonFactory.disabledTextColor = new RGBA_Bytes(200, 200, 200);
            textImageButtonFactory.disabledFillColor = new RGBA_Bytes(0, 0, 0, 0);
            textImageButtonFactory.pressedTextColor = ActiveTheme.Instance.PrimaryTextColor;

            AnchorAll();
            BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

            bottomToTopLayout = new FlowLayoutWidget(FlowDirection.BottomToTop);
            bottomToTopLayout.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;
            bottomToTopLayout.Padding = new BorderDouble(3);

            topToBottom.AddChild(bottomToTopLayout);

            {
                FlowLayoutWidget buttonBar = new FlowLayoutWidget();
                buttonBar.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
                buttonBar.Padding = new BorderDouble(0, 3);

                textImageButtonFactory.FixedWidth = 60;
                backButton = textImageButtonFactory.Generate(LocalizedString.Get("Back"), centerText: true);
                backButton.Click += new ButtonBase.ButtonEventHandler(back_Click);

                nextButton = textImageButtonFactory.Generate(LocalizedString.Get("Next"), centerText: true);
                nextButton.Click += new ButtonBase.ButtonEventHandler(next_Click);

                doneButton = textImageButtonFactory.Generate(LocalizedString.Get("Done"), centerText: true);
                doneButton.Click += done_Click;

                cancelButton = textImageButtonFactory.Generate("Cancel".Localize(), centerText: true);
                cancelButton.Click += done_Click;

                buttonBar.AddChild(backButton);
                buttonBar.AddChild(nextButton);
                buttonBar.AddChild(new HorizontalSpacer());
                buttonBar.AddChild(doneButton);
                buttonBar.AddChild(cancelButton);

                topToBottom.AddChild(buttonBar);
            }

            bottomToTopLayout.AnchorAll();

            AddChild(topToBottom);
        }

        void done_Click(object sender, MouseEventArgs mouseEvent)
        {
            GuiWidget windowToClose = this;
            while (windowToClose != null && windowToClose as SystemWindow == null)
            {
                windowToClose = windowToClose.Parent;
            }

            SystemWindow topSystemWindow = windowToClose as SystemWindow;
            if (topSystemWindow != null)
            {
                topSystemWindow.CloseOnIdle();
            }
        }

        void next_Click(object sender, MouseEventArgs mouseEvent)
        {
            pageIndex = Math.Min(pages.Count - 1, pageIndex + 1);
            SetPageVisibility();
        }

        void back_Click(object sender, MouseEventArgs mouseEvent)
        {
            pageIndex = Math.Max(0, pageIndex - 1);
            SetPageVisibility();
        }

        void SetPageVisibility()
        {
            // we set these before we call becoming active or inactive so that they can override these if needed.
            {
                // if the first page
                if (pageIndex == 0)
                {
                    backButton.Enabled = false;
                    nextButton.Enabled = true;

                    doneButton.Visible = false;
                    cancelButton.Visible = true;
                }
                // if the last page
                else if (pageIndex >= pages.Count - 1)
                {
                    backButton.Enabled = true;
                    nextButton.Enabled = false;

                    doneButton.Visible = true;
                    cancelButton.Visible = false;
                }
                else // in the middle
                {
                    backButton.Enabled = true;
                    nextButton.Enabled = true;

                    doneButton.Visible = false;
                    cancelButton.Visible = true;
                }
            }

            for (int i = 0; i < pages.Count; i++)
            {
                if (i == pageIndex)
                {
                    pages[i].Visible = true;
                    pages[i].PageIsBecomingActive();
                    StepDescription = pages[i].StepDescription;
                }
                else
                {
                    if (pages[i].Visible)
                    {
                        pages[i].Visible = false;
                        pages[i].PageIsBecomingInactive();
                    }
                }
            }
        }

        public void AddPage(WizardPage widgetForPage)
        {
            pages.Add(widgetForPage);
            bottomToTopLayout.AddChild(widgetForPage);
            SetPageVisibility();
        }
    }
}
