using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;

namespace MatterHackers.MatterControl
{
    public class WizardPage : GuiWidget
    {
        public WizardPage()
        {
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
        Button backButton;
        public Button nextButton;
        Button doneButton;

        public Button DoneButton
        {
            get { return doneButton; }
        }

        public WizardControl()
        {
            Padding = new BorderDouble(10);
            textImageButtonFactory.normalTextColor = RGBA_Bytes.White;
            textImageButtonFactory.hoverTextColor = RGBA_Bytes.White;
            textImageButtonFactory.disabledTextColor = new RGBA_Bytes(200, 200, 200);
            textImageButtonFactory.disabledFillColor = new RGBA_Bytes(0, 0, 0, 0);
            textImageButtonFactory.pressedTextColor = RGBA_Bytes.White;

            AnchorAll();
            BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

            bottomToTopLayout = new FlowLayoutWidget(FlowDirection.BottomToTop);
            FlowLayoutWidget buttonBar = new FlowLayoutWidget();

            textImageButtonFactory.FixedWidth = 60;
			backButton = textImageButtonFactory.Generate(LocalizedString.Get("Back"), centerText: true);
            backButton.Click += new ButtonBase.ButtonEventHandler(back_Click);

			nextButton = textImageButtonFactory.Generate(LocalizedString.Get("Next"), centerText: true);
            nextButton.Click += new ButtonBase.ButtonEventHandler(next_Click);

			doneButton = textImageButtonFactory.Generate(LocalizedString.Get("Done"), centerText: true);
            doneButton.Click += new ButtonBase.ButtonEventHandler(done_Click);

            textImageButtonFactory.FixedWidth = 0;

            buttonBar.AddChild(backButton);
            buttonBar.AddChild(nextButton);
            buttonBar.AddChild(doneButton);

            bottomToTopLayout.AddChild(buttonBar);
            bottomToTopLayout.AnchorAll();

            AddChild(bottomToTopLayout);
        }

        void done_Click(object sender, MouseEventArgs mouseEvent)
        {
            UiThread.RunOnIdle(CloseOnIdle);
        }

		void CloseOnIdle(object state)
        {
            Close();
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
            backButton.Enabled = true;
            nextButton.Visible = true;
            doneButton.Visible = false;

            for (int i = 0; i < pages.Count; i++)
            {
                if (i == pageIndex)
                {
                    pages[i].Visible = true;
                    pages[i].PageIsBecomingActive();
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

            if (pageIndex == 0)
            {
                backButton.Enabled = false;
            }
            if (pageIndex >= pages.Count -1)
            {
                nextButton.Visible = false;
                doneButton.Visible = true;
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
