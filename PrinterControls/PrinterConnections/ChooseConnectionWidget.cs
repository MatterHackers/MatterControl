using System.Collections.Generic;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;

namespace MatterHackers.MatterControl.PrinterControls.PrinterConnections
{
    public class ChooseConnectionWidget : ConnectionWidgetBase
    {
        FlowLayoutWidget ConnectionControlContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
        
        List<GuiWidget> radioButtonsOfKnownPrinters = new List<GuiWidget>();
        TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();
		TextImageButtonFactory editButtonFactory = new TextImageButtonFactory();
        Button closeButton;
		Button enterEditModeButton;
		Button leaveEditModeButton;

        bool editMode;

        public ChooseConnectionWidget(ConnectionWindow windowController, SystemWindow container, bool editMode = false)
            : base(windowController, container)
        {
            {
                this.editMode = editMode;

				textImageButtonFactory.normalTextColor = ActiveTheme.Instance.PrimaryTextColor;
				textImageButtonFactory.hoverTextColor = ActiveTheme.Instance.PrimaryTextColor;
				textImageButtonFactory.disabledTextColor = ActiveTheme.Instance.PrimaryTextColor;
				textImageButtonFactory.pressedTextColor = ActiveTheme.Instance.PrimaryTextColor;
                textImageButtonFactory.borderWidth = 0;

				editButtonFactory.normalTextColor = ActiveTheme.Instance.SecondaryAccentColor;
				editButtonFactory.hoverTextColor = RGBA_Bytes.White;
				editButtonFactory.disabledTextColor = ActiveTheme.Instance.SecondaryAccentColor;
				editButtonFactory.pressedTextColor = RGBA_Bytes.White;
				editButtonFactory.borderWidth = 0;
				editButtonFactory.FixedWidth = 50;
                
                this.AnchorAll();
                this.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;
                this.Padding = new BorderDouble(0); //To be re-enabled once native borders are turned off

                GuiWidget mainContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
                mainContainer.AnchorAll();
                mainContainer.Padding = new BorderDouble(3, 0, 3, 5);
                mainContainer.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

                FlowLayoutWidget headerRow = new FlowLayoutWidget(FlowDirection.LeftToRight);
                headerRow.HAnchor = HAnchor.ParentLeftRight;
                headerRow.Margin = new BorderDouble(0, 3, 0, 0);
                headerRow.Padding = new BorderDouble(0, 3, 0, 0);
                
                {
                    string chooseThreeDPrinterConfigLabel = LocalizedString.Get("Choose a 3D Printer Configuration");
					string chooseThreeDPrinterConfigFull = string.Format ("{0}:", chooseThreeDPrinterConfigLabel);

					TextWidget elementHeader = new TextWidget(string.Format(chooseThreeDPrinterConfigFull), pointSize: 14);
                    elementHeader.TextColor = this.defaultTextColor;
                    elementHeader.HAnchor = HAnchor.ParentLeftRight;
                    elementHeader.VAnchor = Agg.UI.VAnchor.ParentCenter;

                   // ActionLink editModeLink;

					enterEditModeButton = editButtonFactory.Generate (LocalizedString.Get ("Edit"), centerText: true);
					enterEditModeButton.Click += EditModeOnLink_Click;
					leaveEditModeButton = editButtonFactory.Generate (LocalizedString.Get ("Done"), centerText: true);
					leaveEditModeButton.Click += EditModeOffLink_Click;

					if (!this.editMode)
					{
						leaveEditModeButton.Visible = false;
						enterEditModeButton.Visible = true;
					} 
					else
					{
						leaveEditModeButton.Visible = true;
						enterEditModeButton.Visible = false;
					}



					headerRow.AddChild(enterEditModeButton);
					headerRow.AddChild (leaveEditModeButton);
					headerRow.AddChild(elementHeader);
                }

                //To do - replace with scrollable widget
                FlowLayoutWidget printerListContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
                //ListBox printerListContainer = new ListBox();
                {                                   
                    printerListContainer.HAnchor = HAnchor.ParentLeftRight;
                    printerListContainer.VAnchor = VAnchor.ParentBottomTop;
					printerListContainer.Padding = new BorderDouble(3);
                    printerListContainer.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;

                    //Get a list of printer records and add them to radio button list
                    foreach (Printer printer in GetAllPrinters())
                    {
                        PrinterListItem printerListItem;
                        if (this.editMode)
                        {
                            printerListItem = new PrinterListItemEdit(printer, this.windowController);
                        }
                        else
                        {
                            printerListItem = new PrinterListItemView(printer, this.windowController);
                        }

                        printerListContainer.AddChild(printerListItem);
                    }
                }

                FlowLayoutWidget buttonContainer = new FlowLayoutWidget(FlowDirection.LeftToRight);
                buttonContainer.HAnchor = HAnchor.ParentLeft | HAnchor.ParentRight;
                buttonContainer.Margin = new BorderDouble(0, 3);
                {
                    closeButton = textImageButtonFactory.Generate(LocalizedString.Get("Close"));

                    Button addPrinterButton = textImageButtonFactory.Generate(LocalizedString.Get("Add"), "icon_circle_plus.png");
                    addPrinterButton.Click += new ButtonBase.ButtonEventHandler(AddConnectionLink_Click);

                    Button refreshListButton = textImageButtonFactory.Generate(LocalizedString.Get("Refresh"));
                    refreshListButton.Click += new ButtonBase.ButtonEventHandler(EditModeOffLink_Click);

                    GuiWidget spacer = new GuiWidget();
                    spacer.HAnchor = HAnchor.ParentLeftRight;

                    //Add buttons to ButtonContainer
                    buttonContainer.AddChild(addPrinterButton);

                    if (!this.editMode)
                    {
                        buttonContainer.AddChild(refreshListButton);
                    }


                    buttonContainer.AddChild(spacer);
                    buttonContainer.AddChild(closeButton);
                }

                mainContainer.AddChild(headerRow);
                mainContainer.AddChild(printerListContainer);
                mainContainer.AddChild(buttonContainer);

                this.AddChild(mainContainer);

                BindCloseButtonClick();
            }
        }

        void BindCloseButtonClick()
        {
            closeButton.UnbindClickEvents();
            closeButton.Click += new ButtonBase.ButtonEventHandler(CloseWindow);
        }
			

        void EditModeOnLink_Click(object sender, MouseEventArgs mouseEvent)
        {

			this.windowController.ChangeToChoosePrinter(true);
			enterEditModeButton.Visible = false;
			leaveEditModeButton.Visible = true;
        }

        void EditModeOffLink_Click(object sender, MouseEventArgs mouseEvent)
        {

			this.windowController.ChangeToChoosePrinter(false);
			enterEditModeButton.Visible = true;
			leaveEditModeButton.Visible = false;
        }

        void AddConnectionLink_Click(object sender, MouseEventArgs mouseEvent)
        {
            this.windowController.ChangeToAddPrinter();
        }        

        void EditConnectionLink_Click(object sender, MouseEventArgs mouseEvent)
        {
            PrinterActionLink actionLink = (PrinterActionLink)sender;
            this.windowController.ChangedToEditPrinter(actionLink.LinkedPrinter);
        }

        void CloseWindow(object o, MouseEventArgs e)
        {
            //Stop listening for connection events (if set) and close window
            UiThread.RunOnIdle(CloseOnIdle);
        }

        void CloseOnIdle(object state)
        {
            this.containerWindowToClose.Close();
        }

        IEnumerable<Printer> GetAllPrinters()
        {
            //Retrieve a list of saved printers from the Datastore
//            return (IEnumerable<Printer>)from s in Datastore.Instance.dbSQLite.Table<Printer>()
//                                         orderby s.Name
//                                         select s;

			string query = string.Format("SELECT * FROM Printer;");
			IEnumerable<Printer> result = (IEnumerable<Printer>)Datastore.Instance.dbSQLite.Query<Printer>(query);
			return result;
        }
    }

}
