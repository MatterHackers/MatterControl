using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using System;
using System.Collections.Generic;

namespace MatterHackers.MatterControl
{
	public class WizardControlPage : GuiWidget
	{
		private string stepDescription = "";

		public string StepDescription { get { return stepDescription; } set { stepDescription = value; } }

		public WizardControlPage(string stepDescription)
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
		double extraTextScaling = 1;
		protected TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();

		private FlowLayoutWidget bottomToTopLayout;
		private List<WizardControlPage> pages = new List<WizardControlPage>();
		private int pageIndex = 0;
		public Button backButton;
		public Button nextButton;
		private Button doneButton;
		private Button cancelButton;

		private TextWidget stepDescriptionWidget;

		public string StepDescription
		{
			get { return stepDescriptionWidget.Text; }
			set { stepDescriptionWidget.Text = value; }
		}

		public WizardControl()
		{
			if (UserSettings.Instance.IsTouchScreen)
			{
				extraTextScaling = 1.33333;
			}
			textImageButtonFactory.fontSize = extraTextScaling * textImageButtonFactory.fontSize;

			FlowLayoutWidget topToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
			topToBottom.AnchorAll();

			if (UserSettings.Instance.IsTouchScreen)
			{
				topToBottom.Padding = new BorderDouble(12);
			}
			else
			{
				topToBottom.Padding = new BorderDouble(3, 0, 3, 5);
			}

			FlowLayoutWidget headerRow = new FlowLayoutWidget(FlowDirection.LeftToRight);
			headerRow.HAnchor = HAnchor.ParentLeftRight;
			headerRow.Margin = new BorderDouble(0, 3, 0, 0);
			headerRow.Padding = new BorderDouble(0, 3, 0, 3);

			{
				string titleString = LocalizedString.Get("Title Stuff".Localize());
				stepDescriptionWidget = new TextWidget(titleString, pointSize: 14 * extraTextScaling);
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

				backButton = textImageButtonFactory.Generate(LocalizedString.Get("Back"), centerText: true);
				backButton.Click += new EventHandler(back_Click);

				nextButton = textImageButtonFactory.Generate(LocalizedString.Get("Next"), centerText: true);
				nextButton.Name = "Next Button";
				nextButton.Click += new EventHandler(next_Click);

				doneButton = textImageButtonFactory.Generate(LocalizedString.Get("Done"), centerText: true);
				doneButton.Name = "Done Button";
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

		private void done_Click(object sender, EventArgs mouseEvent)
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

		public override void OnClosed(EventArgs e)
		{
			ApplicationController.Instance.ReloadAll(null, null);
			base.OnClosed(e);
		}

		private void next_Click(object sender, EventArgs mouseEvent)
		{
			pageIndex = Math.Min(pages.Count - 1, pageIndex + 1);
			SetPageVisibility();
		}

		private void back_Click(object sender, EventArgs mouseEvent)
		{
			pageIndex = Math.Max(0, pageIndex - 1);
			SetPageVisibility();
		}

		private void SetPageVisibility()
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

		public void AddPage(WizardControlPage widgetForPage)
		{
			pages.Add(widgetForPage);
			bottomToTopLayout.AddChild(widgetForPage);
			SetPageVisibility();
		}
	}
}