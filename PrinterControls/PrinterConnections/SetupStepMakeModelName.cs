using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using System;
using MatterHackers.MatterControl.SettingsManagement;
using System.Collections.Generic;
using System.Linq;
using MatterHackers.MatterControl.CustomWidgets;

namespace MatterHackers.MatterControl.PrinterControls.PrinterConnections
{
	//Normally step one of the setup process
	public class SetupStepMakeModelName : SetupConnectionWidgetBase
	{
		private FlowLayoutWidget printerModelContainer;
		private FlowLayoutWidget printerMakeContainer;
		private FlowLayoutWidget printerNameContainer;

		private MHTextEditWidget printerNameInput;

		private List<CustomCommands> printerCustomCommands;

		private TextWidget printerNameError;
		private TextWidget printerModelError;
		private TextWidget printerMakeError;

		private PrinterChooser printerManufacturerSelector;

		private Button nextButton;

		private bool usingDefaultName;

		public SetupStepMakeModelName(ConnectionWindow windowController, GuiWidget containerWindowToClose, PrinterSetupStatus printerSetupStatus = null)
			: base(windowController, containerWindowToClose, printerSetupStatus)
		{
			//Construct inputs
			printerNameContainer = createPrinterNameContainer();
			printerMakeContainer = createPrinterMakeContainer();

			if (printerManufacturerSelector.CountOfMakes == 1)
			{
				ActivePrinter.Make = printerManufacturerSelector.ManufacturerDropList.SelectedValue;

				printerMakeContainer.Visible = false;
				printerModelContainer = createPrinterModelContainer(printerManufacturerSelector.ManufacturerDropList.SelectedValue);
				printerModelContainer.Visible = true;
			}
			else
			{
				printerModelContainer = createPrinterModelContainer();
			}

			//Add inputs to main container
			contentRow.AddChild(printerNameContainer);
			contentRow.AddChild(printerMakeContainer);
			contentRow.AddChild(printerModelContainer);

			//Construct buttons
			nextButton = textImageButtonFactory.Generate(LocalizedString.Get("Save & Continue"));
			nextButton.Name = "Save & Continue Button";
			nextButton.Click += new EventHandler(NextButton_Click);

			//Add buttons to buttonContainer
			footerRow.AddChild(nextButton);
			footerRow.AddChild(new HorizontalSpacer());
			footerRow.AddChild(cancelButton);

			usingDefaultName = true;

			SetElementState();
		}

		private void SetElementState()
		{
			printerModelContainer.Visible = (this.ActivePrinter.Make != null);
			nextButton.Visible = (this.ActivePrinter.Model != null && this.ActivePrinter.Make != null);
		}

		private FlowLayoutWidget createPrinterNameContainer()
		{
			FlowLayoutWidget container = new FlowLayoutWidget(FlowDirection.TopToBottom);
			container.Margin = new BorderDouble(0, 5);
			BorderDouble elementMargin = new BorderDouble(top: 3);

			string printerNameLabelTxt = LocalizedString.Get("Name");
			string printerNameLabelTxtFull = string.Format("{0}:", printerNameLabelTxt);
			TextWidget printerNameLabel = new TextWidget(printerNameLabelTxtFull, 0, 0, 12);
			printerNameLabel.TextColor = this.defaultTextColor;
			printerNameLabel.HAnchor = HAnchor.ParentLeftRight;
			printerNameLabel.Margin = new BorderDouble(0, 0, 0, 1);

			printerNameInput = new MHTextEditWidget(this.ActivePrinter.Name);
			printerNameInput.HAnchor = HAnchor.ParentLeftRight;
			printerNameInput.KeyPressed += PrinterNameInput_KeyPressed;

			printerNameError = new TextWidget(LocalizedString.Get("Give your printer a name."), 0, 0, 10);
			printerNameError.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			printerNameError.HAnchor = HAnchor.ParentLeftRight;
			printerNameError.Margin = elementMargin;

			container.AddChild(printerNameLabel);
			container.AddChild(printerNameInput);
			container.AddChild(printerNameError);
			container.HAnchor = HAnchor.ParentLeftRight;
			return container;
		}

		private FlowLayoutWidget createPrinterMakeContainer()
		{
			FlowLayoutWidget container = new FlowLayoutWidget(FlowDirection.TopToBottom);
			container.Margin = new BorderDouble(0, 5);
			BorderDouble elementMargin = new BorderDouble(top: 3);

			string printerManufacturerLabelTxt = LocalizedString.Get("Make");
			string printerManufacturerLabelTxtFull = string.Format("{0}:", printerManufacturerLabelTxt);
			TextWidget printerManufacturerLabel = new TextWidget(printerManufacturerLabelTxtFull, 0, 0, 12);
			printerManufacturerLabel.TextColor = this.defaultTextColor;
			printerManufacturerLabel.HAnchor = HAnchor.ParentLeftRight;
			printerManufacturerLabel.Margin = elementMargin;

			printerManufacturerSelector = new PrinterChooser();
			printerManufacturerSelector.HAnchor = HAnchor.ParentLeftRight;
			printerManufacturerSelector.Margin = elementMargin;
			printerManufacturerSelector.ManufacturerDropList.SelectionChanged += new EventHandler(ManufacturerDropList_SelectionChanged);

			printerMakeError = new TextWidget(LocalizedString.Get("Select the printer manufacturer"), 0, 0, 10);
			printerMakeError.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			printerMakeError.HAnchor = HAnchor.ParentLeftRight;
			printerMakeError.Margin = elementMargin;

			container.AddChild(printerManufacturerLabel);
			container.AddChild(printerManufacturerSelector);
			container.AddChild(printerMakeError);

			container.HAnchor = HAnchor.ParentLeftRight;
			return container;
		}

		private FlowLayoutWidget createPrinterModelContainer(string make = "Other")
		{
			FlowLayoutWidget container = new FlowLayoutWidget(FlowDirection.TopToBottom);
			container.Margin = new BorderDouble(0, 5);
			BorderDouble elementMargin = new BorderDouble(top: 3);

			string printerModelLabelTxt = LocalizedString.Get("Model");
			string printerModelLabelTxtFull = string.Format("{0}:", printerModelLabelTxt);
			TextWidget printerModelLabel = new TextWidget(printerModelLabelTxtFull, 0, 0, 12);
			printerModelLabel.TextColor = this.defaultTextColor;
			printerModelLabel.HAnchor = HAnchor.ParentLeftRight;
			printerModelLabel.Margin = elementMargin;

			ModelChooser printerModelSelector = new ModelChooser(make);
			printerModelSelector.HAnchor = HAnchor.ParentLeftRight;
			printerModelSelector.Margin = elementMargin;
			printerModelSelector.ModelDropList.SelectionChanged += new EventHandler(ModelDropList_SelectionChanged);
			printerModelSelector.SelectIfOnlyOneModel();

			printerModelError = new TextWidget(LocalizedString.Get("Select the printer model"), 0, 0, 10);
			printerModelError.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			printerModelError.HAnchor = HAnchor.ParentLeftRight;
			printerModelError.Margin = elementMargin;

			container.AddChild(printerModelLabel);
			container.AddChild(printerModelSelector);
			container.AddChild(printerModelError);

			container.HAnchor = HAnchor.ParentLeftRight;
			return container;
		}

		private void ManufacturerDropList_SelectionChanged(object sender, EventArgs e)
		{
			ActivePrinter.Make = ((DropDownList)sender).SelectedValue;
			ActivePrinter.Model = null;
			ReconstructModelSelector();
			SetElementState();
		}

		private void ReconstructModelSelector()
		{
			//reconstruct model selector
			int currentIndex = contentRow.GetChildIndex(printerModelContainer);
			contentRow.RemoveChild(printerModelContainer);



			printerModelContainer = createPrinterModelContainer(ActivePrinter.Make);
			contentRow.AddChild(printerModelContainer, currentIndex);
			contentRow.Invalidate();

			printerMakeError.Visible = false;
		}

		private void ModelDropList_SelectionChanged(object sender, EventArgs e)
		{
			UiThread.RunOnIdle(() =>
			{
				ActivePrinter.Model = ((DropDownList)sender).SelectedLabel;
				currentPrinterSetupStatus.LoadSetupSettings(ActivePrinter.Make, ActivePrinter.Model);
				printerModelError.Visible = false;
				SetElementState();
				if (usingDefaultName)
				{
					// Use ManufacturerDropList.SelectedLabel instead of ActivePrinter.Make to ensure the mapped Unicode values are picked up
					string mappedMakeText = printerManufacturerSelector.ManufacturerDropList.SelectedLabel;

                    string printerInputName = String.Format("{0} {1}", mappedMakeText, this.ActivePrinter.Model);
                    string query = "SELECT Name FROM Printer WHERE Name LIKE @printerName;";
                    var names = Datastore.Instance.dbSQLite.Query<sqlName>(query, printerInputName + "%").Select(item => item.Name).ToList();

                    if (!names.Contains(printerInputName))
                    {
                        printerNameInput.Text = printerInputName;
                    }
                    else
                    {
                        
                        int printerModelCount = 0; //Used to keep track of how many of the printer models we run into before and empty one
                        string possiblePrinterName;

                        do
                        {
                            printerModelCount++;
                            possiblePrinterName = String.Format("{0} ({1})", printerInputName, printerModelCount);
                        } while (names.Contains(possiblePrinterName));


                        printerNameInput.Text = possiblePrinterName;
                    }

				}
			});
		}

		private void PrinterNameInput_KeyPressed(object sender, KeyPressEventArgs e)
		{
			this.usingDefaultName = false;
		}

		private void MoveToNextWidget()
		{
			if (Parent != null) // if it hasn't been closed
			{
				if (this.ActivePrinter.BaudRate == null)
				{
					Parent.AddChild(new SetupStepBaudRate((ConnectionWindow)Parent, Parent, this.currentPrinterSetupStatus));
					Parent.RemoveChild(this);
				}
				else if (this.currentPrinterSetupStatus.DriversToInstall.Count > 0)
				{
					Parent.AddChild(new SetupStepInstallDriver((ConnectionWindow)Parent, Parent, this.currentPrinterSetupStatus));
					Parent.RemoveChild(this);
				}
				else
				{
					Parent.AddChild(new SetupStepComPortOne((ConnectionWindow)Parent, Parent, this.currentPrinterSetupStatus));
					Parent.RemoveChild(this);
				}
			}
		}

		private void NextButton_Click(object sender, EventArgs mouseEvent)
		{
			bool canContinue = this.OnSave();
			if (canContinue)
			{
				this.currentPrinterSetupStatus.LoadCalibrationPrints();
				UiThread.RunOnIdle(MoveToNextWidget);
			}
		}

		public int ExistingPrinterCount()
		{
			return Datastore.Instance.RecordCount("Printer");
		}

		private class sqlName
		{
			public string Name { get; set; }
		}

		private bool OnSave()
		{
			if (printerNameInput.Text != "")
			{
				this.ActivePrinter.Name = printerNameInput.Text;
				if (this.ActivePrinter.Make == null || this.ActivePrinter.Model == null)
				{
					return false;
				}
				else
				{
					Datastore.Instance.dbSQLite.RunInTransaction(currentPrinterSetupStatus.Save);
					return true;
				}
			}
			else
			{
				this.printerNameError.TextColor = RGBA_Bytes.Red;
				this.printerNameError.Text = "Printer name cannot be blank";
				this.printerNameError.Visible = true;
				return false;
			}
		}
	}
}