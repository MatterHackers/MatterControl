﻿using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PrinterCommunication;
using System;

namespace MatterHackers.MatterControl.PrinterControls.PrinterConnections
{
	public class SetupStepComPortOne : SetupConnectionWidgetBase
	{
		private Button nextButton;

		public SetupStepComPortOne(ConnectionWindow windowController, GuiWidget containerWindowToClose, PrinterSetupStatus setupPrinter)
			: base(windowController, containerWindowToClose, setupPrinter)
		{
			contentRow.AddChild(createPrinterConnectionMessageContainer());
			{
				//Construct buttons
				nextButton = textImageButtonFactory.Generate(LocalizedString.Get("Continue"));
				nextButton.Click += new EventHandler(NextButton_Click);

				//Add buttons to buttonContainer
				footerRow.AddChild(nextButton);
				footerRow.AddChild(new HorizontalSpacer());
				footerRow.AddChild(cancelButton);
			}
		}

		public FlowLayoutWidget createPrinterConnectionMessageContainer()
		{
			FlowLayoutWidget container = new FlowLayoutWidget(FlowDirection.TopToBottom);
			container.VAnchor = VAnchor.ParentBottomTop;
			container.Margin = new BorderDouble(5);
			BorderDouble elementMargin = new BorderDouble(top: 5);

			TextWidget printerMessageOne = new TextWidget(LocalizedString.Get("MatterControl will now attempt to auto-detect printer."), 0, 0, 10);
			printerMessageOne.Margin = new BorderDouble(0, 10, 0, 5);
			printerMessageOne.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			printerMessageOne.HAnchor = HAnchor.ParentLeftRight;
			printerMessageOne.Margin = elementMargin;

			string printerMessageTwoTxt = LocalizedString.Get("Disconnect printer");
			string printerMessageTwoTxtEnd = LocalizedString.Get("if currently connected");
			string printerMessageTwoTxtFull = string.Format("1.) {0} ({1}).", printerMessageTwoTxt, printerMessageTwoTxtEnd);
			TextWidget printerMessageTwo = new TextWidget(printerMessageTwoTxtFull, 0, 0, 12);
			printerMessageTwo.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			printerMessageTwo.HAnchor = HAnchor.ParentLeftRight;
			printerMessageTwo.Margin = elementMargin;

			string printerMessageThreeTxt = LocalizedString.Get("Press");
			string printerMessageThreeTxtEnd = LocalizedString.Get("Continue");
			string printerMessageThreeFull = string.Format("2.) {0} '{1}'.", printerMessageThreeTxt, printerMessageThreeTxtEnd);
			TextWidget printerMessageThree = new TextWidget(printerMessageThreeFull, 0, 0, 12);
			printerMessageThree.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			printerMessageThree.HAnchor = HAnchor.ParentLeftRight;
			printerMessageThree.Margin = elementMargin;

			GuiWidget vSpacer = new GuiWidget();
			vSpacer.VAnchor = VAnchor.ParentBottomTop;

			string setupManualConfigurationOrSkipConnectionText = LocalizedString.Get(("You can also"));
			string setupManualConfigurationOrSkipConnectionTextFull = String.Format("{0}:", setupManualConfigurationOrSkipConnectionText);
			TextWidget setupManualConfigurationOrSkipConnectionWidget = new TextWidget(setupManualConfigurationOrSkipConnectionTextFull, 0, 0, 10);
			setupManualConfigurationOrSkipConnectionWidget.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			setupManualConfigurationOrSkipConnectionWidget.HAnchor = HAnchor.ParentLeftRight;
			setupManualConfigurationOrSkipConnectionWidget.Margin = elementMargin;

			Button manualLink = linkButtonFactory.Generate(LocalizedString.Get("Manually Configure Connection"));
			manualLink.Margin = new BorderDouble(0, 5);
			manualLink.Click += new EventHandler(ManualLink_Click);

			string printerMessageFourText = LocalizedString.Get("or");
			TextWidget printerMessageFour = new TextWidget(printerMessageFourText, 0, 0, 10);
			printerMessageFour.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			printerMessageFour.HAnchor = HAnchor.ParentLeftRight;
			printerMessageFour.Margin = elementMargin;

			Button skipConnectionLink = linkButtonFactory.Generate(LocalizedString.Get("Skip Connection Setup"));
			skipConnectionLink.Margin = new BorderDouble(0, 8);
			skipConnectionLink.Click += new EventHandler(SkipConnectionLink_Click);

			container.AddChild(printerMessageOne);
			container.AddChild(printerMessageTwo);
			container.AddChild(printerMessageThree);
			container.AddChild(vSpacer);
			container.AddChild(setupManualConfigurationOrSkipConnectionWidget);
			container.AddChild(manualLink);
			container.AddChild(printerMessageFour);
			container.AddChild(skipConnectionLink);

			container.HAnchor = HAnchor.ParentLeftRight;
			return container;
		}

		private void ManualLink_Click(object sender, EventArgs mouseEvent)
		{
			UiThread.RunOnIdle(MoveToManualConfiguration);
		}

		private void MoveToManualConfiguration()
		{
			Parent.AddChild(new SetupStepComPortManual((ConnectionWindow)Parent, Parent, this.currentPrinterSetupStatus));
			Parent.RemoveChild(this);
		}

		private void NextButton_Click(object sender, EventArgs mouseEvent)
		{
			UiThread.RunOnIdle(MoveToNextWidget);
		}

		private void MoveToNextWidget()
		{
			Parent.AddChild(new SetupStepComPortTwo((ConnectionWindow)Parent, Parent, this.currentPrinterSetupStatus));
			Parent.RemoveChild(this);
		}

		private void SkipConnectionLink_Click(object sender, EventArgs mouseEvent)
		{
			PrinterConnectionAndCommunication.Instance.HaltConnectionThread();
			if (GetPrinterRecordCount() > 0)
			{
				this.windowController.ChangeToChoosePrinter();
			}
			else
			{
				Parent.Close();
			}
		}
	}
}