
using System;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;

namespace MatterHackers.MatterControl
{
	public class WizardPage : GuiWidget
	{
		private FlowLayoutWidget headerRow;
		protected FlowLayoutWidget contentRow;
		private FlowLayoutWidget footerRow;

		private WrappedTextWidget headerLabel;
		protected Button cancelButton { get; }

		protected TextImageButtonFactory textImageButtonFactory { get; } = ApplicationController.Instance.Theme.WizardButtons;
		protected TextImageButtonFactory whiteImageButtonFactory { get; } = ApplicationController.Instance.Theme.WhiteButtonFactory;
		protected LinkButtonFactory linkButtonFactory = ApplicationController.Instance.Theme.LinkButtonFactory;

		protected double labelFontSize = 12 * GuiWidget.DeviceScale;
		protected double errorFontSize = 10 * GuiWidget.DeviceScale;

		public WizardWindow WizardWindow;

		protected GuiWidget mainContainer;

		protected bool abortCancel = false;

		public WizardPage(string unlocalizedTextForCancelButton = "Cancel", string unlocalizedTextForTitle = "Setup Wizard")
		{
			if (!UserSettings.Instance.IsTouchScreen)
			{
				this.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;
				this.Padding = new BorderDouble(0); //To be re-enabled once native borders are turned off
			}

			this.AnchorAll();

			cancelButton = textImageButtonFactory.Generate(unlocalizedTextForCancelButton.Localize());
			cancelButton.Name = "Cancel Wizard Button";

			// Create the main container
			mainContainer = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				Padding = new BorderDouble(12, 12, 12, 0),
				BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor
			};
			mainContainer.AnchorAll();

			// Create the header row for the widget
			headerRow = new FlowLayoutWidget(FlowDirection.LeftToRight)
			{
				Margin = new BorderDouble(0, 3, 0, 0),
				Padding = new BorderDouble(0, 12),
				HAnchor = HAnchor.Stretch
			};

			headerLabel = new WrappedTextWidget(unlocalizedTextForTitle.Localize(), pointSize: 24, textColor: ActiveTheme.Instance.SecondaryAccentColor)
			{
				HAnchor = HAnchor.Stretch
			};
			headerRow.AddChild(headerLabel);

			// Create the main control container
			contentRow = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				Padding = new BorderDouble(10),
				BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor,
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch
			};

			// Create the footer (button) container
			footerRow = new FlowLayoutWidget(FlowDirection.LeftToRight)
			{
				HAnchor = HAnchor.Left | HAnchor.Right,
				Margin = new BorderDouble(0, 6)
			};

			mainContainer.AddChild(headerRow);
			mainContainer.AddChild(contentRow);
			mainContainer.AddChild(footerRow);

			if (!UserSettings.Instance.IsTouchScreen)
			{
				mainContainer.Padding = new BorderDouble(3, 5, 3, 5);
				headerRow.Padding = new BorderDouble(0, 3, 0, 3);

				headerLabel.TextWidget.PointSize = 14;
				headerLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
				contentRow.Padding = new BorderDouble(5);
				footerRow.Margin = new BorderDouble(0, 3);
			}

			this.AddChild(mainContainer);
		}

		public string WindowTitle { get; set; }

		public void AddPageAction(Button button)
		{
			footerRow.AddChild(button);
		}

		public override void OnLoad(EventArgs args)
		{
			// Add 'Close' event listener after derived types have had a chance to register event listeners
			cancelButton.Click += (s, e) =>
			{
				if (!abortCancel)
				{
					UiThread.RunOnIdle(() => WizardWindow?.Close());
				}
			};

			footerRow.AddChild(new HorizontalSpacer());
			footerRow.AddChild(cancelButton);

			base.OnLoad(args);
		}

		public virtual void PageIsBecomingActive()
		{
		}

		public virtual void PageIsBecomingInactive()
		{
		}
	}
}