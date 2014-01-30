using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO.Ports;
using System.Diagnostics;

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.OpenGlGui;
using MatterHackers.PolygonMesh;
using MatterHackers.RenderOpenGl;
using MatterHackers.VectorMath;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.Localizations;

namespace MatterHackers.MatterControl.PrinterControls.PrinterConnections
{
    public class ChooseConnectionWidget : ConnectionWidgetBase
    {
        FlowLayoutWidget ConnectionControlContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
        
        List<GuiWidget> radioButtonsOfKnownPrinters = new List<GuiWidget>();
        TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();
        Button closeButton;
        bool editMode;

        public ChooseConnectionWidget(ConnectionWindow windowController, SystemWindow container, bool editMode = false)
            : base(windowController, container)
        {
            {
                this.editMode = editMode;

                textImageButtonFactory.normalTextColor = RGBA_Bytes.White;
                textImageButtonFactory.hoverTextColor = RGBA_Bytes.White;
                textImageButtonFactory.disabledTextColor = RGBA_Bytes.White;
                textImageButtonFactory.pressedTextColor = RGBA_Bytes.White;
                textImageButtonFactory.borderWidth = 0;
                
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
                headerRow.Padding = new BorderDouble(0, 3, 0, 3);
                
                {
					string chooseThreeDPrinterConfigLabel = new LocalizedString("Choose a 3D Printer Configuration").Translated;
					string chooseThreeDPrinterConfigFull = string.Format ("{0}:", chooseThreeDPrinterConfigLabel);

					TextWidget elementHeader = new TextWidget(string.Format(chooseThreeDPrinterConfigFull), pointSize: 14);
                    elementHeader.TextColor = this.defaultTextColor;
                    elementHeader.HAnchor = HAnchor.ParentLeftRight;
                    elementHeader.VAnchor = Agg.UI.VAnchor.ParentBottom;

                    ActionLink editModeLink;
                    if (!this.editMode)
                    {
						editModeLink = actionLinkFactory.Generate(new LocalizedString("Edit").Translated, 12, EditModeOnLink_Click);
                    }
                    else
                    {
						editModeLink = actionLinkFactory.Generate(new LocalizedString("Done").Translated, 12, EditModeOffLink_Click);
                    }

                    editModeLink.TextColor = new RGBA_Bytes(250, 250, 250);
                    editModeLink.VAnchor = Agg.UI.VAnchor.ParentBottom;

                    headerRow.AddChild(elementHeader);
                    headerRow.AddChild(editModeLink);
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
					closeButton = textImageButtonFactory.Generate(new LocalizedString("Close").Translated);

					Button addPrinterButton = textImageButtonFactory.Generate(new LocalizedString("Add").Translated, "icon_circle_plus.png");
                    addPrinterButton.Click += new ButtonBase.ButtonEventHandler(AddConnectionLink_Click);

					Button refreshListButton = textImageButtonFactory.Generate(new LocalizedString("Refresh").Translated);
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
        }

        void EditModeOffLink_Click(object sender, MouseEventArgs mouseEvent)
        {
            this.windowController.ChangeToChoosePrinter(false);
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
