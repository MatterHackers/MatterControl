using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.SlicerConfiguration;
using System;

namespace MatterHackers.MatterControl.PrinterControls.PrinterConnections
{
	public class SetupConnectionWidgetBase : ConnectionWidgetBase
	{
		//private GuiWidget mainContainer;

		protected FlowLayoutWidget headerRow;
		protected FlowLayoutWidget contentRow;
		protected FlowLayoutWidget footerRow;
		protected TextWidget headerLabel;
		protected Button cancelButton;
		protected TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();
		protected LinkButtonFactory linkButtonFactory = new LinkButtonFactory();

		public SetupConnectionWidgetBase(ConnectionWizard wizard) : base(wizard)
		{
			SetDisplayAttributes();
			
			cancelButton = textImageButtonFactory.Generate("Cancel".Localize());
			cancelButton.Name = "Setup Connection Cancel Button";
			cancelButton.Click += CancelButton_Click;

			//Create the main container
			GuiWidget mainContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
			mainContainer.AnchorAll();
			mainContainer.Padding = new BorderDouble(3, 5, 3, 5);
			mainContainer.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

			//Create the header row for the widget
			headerRow = new FlowLayoutWidget(FlowDirection.LeftToRight);
			headerRow.Margin = new BorderDouble(0, 3, 0, 0);
			headerRow.Padding = new BorderDouble(0, 3, 0, 3);
			headerRow.HAnchor = HAnchor.ParentLeftRight;
			{
				headerLabel = new TextWidget("3D Printer Setup".Localize(), pointSize: 14);
				headerLabel.AutoExpandBoundsToText = true;
				headerLabel.TextColor = ActiveTheme.Instance.PrimaryTextColor;
				headerRow.AddChild(headerLabel);
			}

			//Create the main control container
			contentRow = new FlowLayoutWidget(FlowDirection.TopToBottom);
			contentRow.Padding = new BorderDouble(5);
			contentRow.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;
			contentRow.HAnchor = HAnchor.ParentLeftRight;
			contentRow.VAnchor = VAnchor.ParentBottomTop;

			//Create the footer (button) container
			footerRow = new FlowLayoutWidget(FlowDirection.LeftToRight);
			footerRow.HAnchor = HAnchor.ParentLeft | HAnchor.ParentRight;
			footerRow.Margin = new BorderDouble(0, 3);

			mainContainer.AddChild(headerRow);
			mainContainer.AddChild(contentRow);
			mainContainer.AddChild(footerRow);
			this.AddChild(mainContainer);
		}

		protected void SaveAndExit()
		{
			ActiveSliceSettings.Instance.RunInTransaction(settings =>
			{
				settings.SetAutoConnect(ActivePrinter.AutoConnectFlag);
				settings.SetBaudRate(ActivePrinter.BaudRate);
				settings.SetComPort(ActivePrinter.ComPort);
				settings.SetSlicingEngine(ActivePrinter.CurrentSlicingEngine);
				settings.SetDriverType(ActivePrinter.DriverType);
				settings.SetId(ActivePrinter.Id);
				settings.SetName(ActivePrinter.Name);
			});

			UiThread.RunOnIdle(connectionWizard.Close);
		}

		private void SetDisplayAttributes()
		{
			textImageButtonFactory.normalTextColor = ActiveTheme.Instance.PrimaryTextColor;
			textImageButtonFactory.hoverTextColor = ActiveTheme.Instance.PrimaryTextColor;
			textImageButtonFactory.disabledTextColor = ActiveTheme.Instance.PrimaryTextColor;
			textImageButtonFactory.pressedTextColor = ActiveTheme.Instance.PrimaryTextColor;
			textImageButtonFactory.borderWidth = 0;

			linkButtonFactory.textColor = ActiveTheme.Instance.PrimaryTextColor;
			linkButtonFactory.fontSize = 10;

			this.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;
			this.AnchorAll();
			this.Padding = new BorderDouble(0); //To be re-enabled once native borders are turned off
		}

		private void CancelButton_Click(object sender, EventArgs mouseEvent)
		{
			PrinterConnectionAndCommunication.Instance.HaltConnectionThread();
			UiThread.RunOnIdle(connectionWizard.Close);
		}
	}
}