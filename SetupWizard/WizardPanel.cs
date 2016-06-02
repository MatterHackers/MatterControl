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
		protected TextImageButtonFactory whiteImageButtonFactory = new TextImageButtonFactory();
		protected LinkButtonFactory linkButtonFactory = new LinkButtonFactory();
		protected GuiWidget containerWindowToClose;
		protected RGBA_Bytes defaultTextColor = ActiveTheme.Instance.PrimaryTextColor;
		protected RGBA_Bytes defaultBackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
		protected RGBA_Bytes subContainerTextColor = ActiveTheme.Instance.PrimaryTextColor;
		protected RGBA_Bytes labelBackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
		protected RGBA_Bytes linkTextColor = ActiveTheme.Instance.SecondaryAccentColor;
		protected double labelFontSize = 12 * GuiWidget.DeviceScale;
		protected double errorFontSize = 10 * GuiWidget.DeviceScale;
		protected WizardWindow wizardWindow;

		event EventHandler unregisterEvents;

		public void ThemeChanged(object sender, EventArgs e)
		{
			this.linkTextColor = ActiveTheme.Instance.PrimaryAccentColor;
			this.Invalidate();
		}

		public override void OnClosed(EventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}

		PrinterInfo activePrinter;
		public PrinterInfo ActivePrinter
		{
			get
			{
				if (activePrinter == null)
				{
					var settings = ActiveSliceSettings.Instance;
					activePrinter = new PrinterInfo
					{
						AutoConnect = settings.DoAutoConnect(),
						BaudRate = settings.BaudRate(),
						ComPort = settings.ComPort(),
						DriverType = settings.DriverType(),
						Id = settings.ID,
						Name = settings.Name()
					};
				}

				return activePrinter;
			}
		}

		public WizardPanel(WizardWindow windowController, string unlocalizedTextForCancelButton = "Cancel")
			: base()
		{
			this.wizardWindow = windowController;
			this.textImageButtonFactory.fontSize = 16;

			SetWhiteButtonAttributes();

			this.AnchorAll();

			ActiveTheme.Instance.ThemeChanged.RegisterEvent(ThemeChanged, ref unregisterEvents);

			cancelButton = textImageButtonFactory.Generate(unlocalizedTextForCancelButton.Localize());
			cancelButton.Name = unlocalizedTextForCancelButton;
			cancelButton.Click += new EventHandler(CancelButton_Click);

			//Create the main container
			GuiWidget mainContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
			mainContainer.AnchorAll();
			mainContainer.Padding = new BorderDouble(12, 12, 12, 0);
			mainContainer.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

			//Create the header row for the widget
			headerRow = new FlowLayoutWidget(FlowDirection.LeftToRight);
			headerRow.Margin = new BorderDouble(0, 3, 0, 0);
			headerRow.Padding = new BorderDouble(0, 12);
			headerRow.HAnchor = HAnchor.ParentLeftRight;
			{
				string defaultHeaderTitle = "Setup Wizard".Localize();
				headerLabel = new TextWidget(defaultHeaderTitle, pointSize: 24, textColor: ActiveTheme.Instance.SecondaryAccentColor);
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

		protected void SaveAndExit()
		{
			throw new NotImplementedException();
			/*
			this.ActivePrinter.Commit();
			ActivePrinterProfile.Instance.ActivePrinter = this.ActivePrinter;
			this.containerWindowToClose.Close(); */

		}

		void SetWhiteButtonAttributes()
		{
			whiteImageButtonFactory.normalFillColor = RGBA_Bytes.White;
			whiteImageButtonFactory.disabledFillColor = RGBA_Bytes.White;
			whiteImageButtonFactory.fontSize = 16;
			whiteImageButtonFactory.borderWidth = 1;

			whiteImageButtonFactory.normalBorderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 200);
			whiteImageButtonFactory.hoverBorderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 200);

			whiteImageButtonFactory.disabledTextColor = RGBA_Bytes.DarkGray;
			whiteImageButtonFactory.hoverTextColor = ActiveTheme.Instance.PrimaryTextColor;
			whiteImageButtonFactory.normalTextColor = RGBA_Bytes.Black;
			whiteImageButtonFactory.pressedTextColor = ActiveTheme.Instance.PrimaryTextColor;
			whiteImageButtonFactory.FixedWidth = 200;
		}

		void CloseWindow(object o, EventArgs e)
		{
			PrinterConnectionAndCommunication.Instance.HaltConnectionThread();
			this.wizardWindow.Close();
		}

		void CancelButton_Click(object sender, EventArgs mouseEvent)
		{
			UiThread.RunOnIdle(() =>
			{
				if (Parent != null)
				{
					Parent.Close();
				}
			});
		}
	}
}