using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using MatterHackers.Agg;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ConfigurationPage.PrintLeveling;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl
{
	public class WizardPanel : GuiWidget
	{
		protected FlowLayoutWidget headerRow;
		protected FlowLayoutWidget contentRow;
		protected FlowLayoutWidget footerRow;

		protected TextWidget headerLabel;
		protected Button cancelButton;

		protected TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();
		protected TextImageButtonFactory whiteImageButtonFactory;
		protected LinkButtonFactory linkButtonFactory = new LinkButtonFactory();

		protected double labelFontSize = 12 * GuiWidget.DeviceScale;
		protected double errorFontSize = 10 * GuiWidget.DeviceScale;

		protected WizardWindow wizardWindow;

		protected GuiWidget mainContainer;

		private event EventHandler unregisterEvents;

		public WizardPanel(WizardWindow wizardWindow, string unlocalizedTextForCancelButton = "Cancel", TextImageButtonFactory textButtonFactory = null)
			: base()
		{
			this.wizardWindow = wizardWindow;

			// Use either the requested button factory or a default with fontSize == 16
			this.textImageButtonFactory = textButtonFactory ??  new TextImageButtonFactory() { fontSize = 16 };

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

			this.AnchorAll();

			ActiveTheme.Instance.ThemeChanged.RegisterEvent(ThemeChanged, ref unregisterEvents);

			cancelButton = textImageButtonFactory.Generate(unlocalizedTextForCancelButton.Localize());
			cancelButton.Name = unlocalizedTextForCancelButton;
			cancelButton.Click += (s, e) =>
			{
				UiThread.RunOnIdle(() => this.wizardWindow?.Close());
			};

			//Create the main container
			mainContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
			mainContainer.AnchorAll();
			mainContainer.Padding = new BorderDouble(12, 12, 12, 0);
			mainContainer.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

			//Create the header row for the widget
			headerRow = new FlowLayoutWidget(FlowDirection.LeftToRight);
			headerRow.Margin = new BorderDouble(0, 3, 0, 0);
			headerRow.Padding = new BorderDouble(0, 12);
			headerRow.HAnchor = HAnchor.ParentLeftRight;
			{
				headerLabel = new TextWidget("Setup Wizard".Localize(), pointSize: 24, textColor: ActiveTheme.Instance.SecondaryAccentColor);
				headerLabel.AutoExpandBoundsToText = true;
				headerRow.AddChild(headerLabel);
			}

			//Create the main control container
			contentRow = new FlowLayoutWidget(FlowDirection.TopToBottom);
			contentRow.Padding = new BorderDouble(10);
			contentRow.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;
			contentRow.HAnchor = HAnchor.ParentLeftRight;
			contentRow.VAnchor = VAnchor.ParentBottomTop;

			//Create the footer (button) container
			footerRow = new FlowLayoutWidget(FlowDirection.LeftToRight);
			footerRow.HAnchor = HAnchor.ParentLeft | HAnchor.ParentRight;
			footerRow.Margin = new BorderDouble(0, 6);

			mainContainer.AddChild(headerRow);
			mainContainer.AddChild(contentRow);
			mainContainer.AddChild(footerRow);
			this.AddChild(mainContainer);
		}

		public void ThemeChanged(object sender, EventArgs e)
		{
			this.Invalidate();
		}

		public override void OnClosed(EventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}
	}
}