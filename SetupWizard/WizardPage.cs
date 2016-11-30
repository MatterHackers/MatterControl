
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;

namespace MatterHackers.MatterControl
{
	public class WizardPage : GuiWidget
	{
		protected FlowLayoutWidget headerRow;
		protected FlowLayoutWidget contentRow;
		protected FlowLayoutWidget footerRow;

		protected TextWidget headerLabel;
		protected Button cancelButton;

		protected TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory() { fontSize = 16 };
		protected TextImageButtonFactory whiteImageButtonFactory;
		protected LinkButtonFactory linkButtonFactory = new LinkButtonFactory();

		protected double labelFontSize = 12 * GuiWidget.DeviceScale;
		protected double errorFontSize = 10 * GuiWidget.DeviceScale;

		public WizardWindow WizardWindow;

		protected GuiWidget mainContainer;

		public WizardPage(string unlocalizedTextForCancelButton = "Cancel", string unlocalizedTextForTitle = "Setup Wizard")
		{
			whiteImageButtonFactory = new TextImageButtonFactory()
			{
				normalFillColor = RGBA_Bytes.White,
				disabledFillColor = RGBA_Bytes.White,
				fontSize = 16,
				borderWidth = 1,

				normalBorderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 200),
				hoverBorderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 200),

				disabledTextColor = RGBA_Bytes.DarkGray,
				hoverTextColor = ActiveTheme.Instance.PrimaryTextColor,
				normalTextColor = RGBA_Bytes.Black,
				pressedTextColor = ActiveTheme.Instance.PrimaryTextColor,
				FixedWidth = 200
			};

			if (!UserSettings.Instance.IsTouchScreen)
			{
				textImageButtonFactory = new TextImageButtonFactory()
				{
					normalTextColor = ActiveTheme.Instance.PrimaryTextColor,
					hoverTextColor = ActiveTheme.Instance.PrimaryTextColor,
					disabledTextColor = ActiveTheme.Instance.PrimaryTextColor,
					pressedTextColor = ActiveTheme.Instance.PrimaryTextColor,
					borderWidth = 0
				};

				linkButtonFactory.textColor = ActiveTheme.Instance.PrimaryTextColor;
				linkButtonFactory.fontSize = 10;

				this.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;
				this.Padding = new BorderDouble(0); //To be re-enabled once native borders are turned off
			}

			this.AnchorAll();

			cancelButton = textImageButtonFactory.Generate(unlocalizedTextForCancelButton.Localize());
			cancelButton.Name = "Cancel Wizard Button";
			cancelButton.Click += (s, e) =>
			{
				UiThread.RunOnIdle(() => WizardWindow?.Close());
			};

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
				HAnchor = HAnchor.ParentLeftRight
			};

			headerLabel = new TextWidget(unlocalizedTextForTitle.Localize(), pointSize: 24, textColor: ActiveTheme.Instance.SecondaryAccentColor)
			{
				AutoExpandBoundsToText = true
			};
			headerRow.AddChild(headerLabel);

			// Create the main control container
			contentRow = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				Padding = new BorderDouble(10),
				BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor,
				HAnchor = HAnchor.ParentLeftRight,
				VAnchor = VAnchor.ParentBottomTop
			};

			// Create the footer (button) container
			footerRow = new FlowLayoutWidget(FlowDirection.LeftToRight)
			{
				HAnchor = HAnchor.ParentLeft | HAnchor.ParentRight,
				Margin = new BorderDouble(0, 6)
			};

			mainContainer.AddChild(headerRow);
			mainContainer.AddChild(contentRow);
			mainContainer.AddChild(footerRow);

			if (!UserSettings.Instance.IsTouchScreen)
			{
				mainContainer.Padding = new BorderDouble(3, 5, 3, 5);
				headerRow.Padding = new BorderDouble(0, 3, 0, 3);

				headerLabel.PointSize = 14;
				headerLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
				contentRow.Padding = new BorderDouble(5);
				footerRow.Margin = new BorderDouble(0, 3);
			}

			this.AddChild(mainContainer);
		}
	}
}