/*
Copyright (c) 2014, Kevin Pope
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using System;
using System.Collections.Generic;

namespace MatterHackers.MatterControl.PrinterControls.PrinterConnections
{
	public class ChooseConnectionWidget : ConnectionWidgetBase
	{
		private FlowLayoutWidget ConnectionControlContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);

		private List<GuiWidget> radioButtonsOfKnownPrinters = new List<GuiWidget>();
		private TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();
		private TextImageButtonFactory editButtonFactory = new TextImageButtonFactory();
		private Button closeButton;

		private bool editMode;

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
				editButtonFactory.FixedWidth = 60 * TextWidget.GlobalPointSizeScaleRatio;

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
					string chooseThreeDPrinterConfigFull = string.Format("{0}:", chooseThreeDPrinterConfigLabel);

					TextWidget elementHeader = new TextWidget(string.Format(chooseThreeDPrinterConfigFull), pointSize: 14);
					elementHeader.TextColor = this.defaultTextColor;
					elementHeader.HAnchor = HAnchor.ParentLeftRight;
					elementHeader.VAnchor = Agg.UI.VAnchor.ParentCenter;

					headerRow.AddChild(elementHeader);
				}

				FlowLayoutWidget editButtonRow = new FlowLayoutWidget(FlowDirection.LeftToRight);
				editButtonRow.BackgroundColor = ActiveTheme.Instance.TransparentDarkOverlay;
				editButtonRow.HAnchor = HAnchor.ParentLeftRight;
				editButtonRow.Margin = new BorderDouble(0, 3, 0, 0);
				editButtonRow.Padding = new BorderDouble(0, 3, 0, 0);

				Button enterLeaveEditModeButton;
				if (!this.editMode)
				{
					enterLeaveEditModeButton = editButtonFactory.Generate(LocalizedString.Get("Edit"), centerText: true);
					enterLeaveEditModeButton.Click += EditModeOnLink_Click;
				}
				else
				{
					enterLeaveEditModeButton = editButtonFactory.Generate(LocalizedString.Get("Done"), centerText: true);
					enterLeaveEditModeButton.Click += EditModeOffLink_Click;
				}

				editButtonRow.AddChild(enterLeaveEditModeButton);

				//To do - replace with scrollable widget
				FlowLayoutWidget printerListContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
				//ListBox printerListContainer = new ListBox();
				{
					printerListContainer.HAnchor = HAnchor.ParentLeftRight;
					printerListContainer.VAnchor = VAnchor.FitToChildren;
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
					addPrinterButton.Click += new EventHandler(AddConnectionLink_Click);

					Button refreshListButton = textImageButtonFactory.Generate(LocalizedString.Get("Refresh"));
					refreshListButton.Click += new EventHandler(EditModeOffLink_Click);

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

                ScrollableWidget printerListScrollArea = new ScrollableWidget(true);
                printerListScrollArea.ScrollArea.HAnchor |= Agg.UI.HAnchor.ParentLeftRight;
                printerListScrollArea.AnchorAll();
                printerListScrollArea.AddChild(printerListContainer);

				mainContainer.AddChild(headerRow);
				mainContainer.AddChild(editButtonRow);
                mainContainer.AddChild(printerListScrollArea);
				mainContainer.AddChild(buttonContainer);

				this.AddChild(mainContainer);

				BindCloseButtonClick();
			}
		}

		private void BindCloseButtonClick()
		{
			closeButton.UnbindClickEvents();
			closeButton.Click += new EventHandler(CloseWindow);
		}

		private void EditModeOnLink_Click(object sender, EventArgs mouseEvent)
		{
			this.windowController.ChangeToChoosePrinter(true);
		}

		private void EditModeOffLink_Click(object sender, EventArgs mouseEvent)
		{
			this.windowController.ChangeToChoosePrinter(false);
		}

		private void AddConnectionLink_Click(object sender, EventArgs mouseEvent)
		{
			this.windowController.ChangeToAddPrinter();
		}

		private void EditConnectionLink_Click(object sender, EventArgs mouseEvent)
		{
			PrinterActionLink actionLink = (PrinterActionLink)sender;
			this.windowController.ChangedToEditPrinter(actionLink.LinkedPrinter);
		}

		private void CloseWindow(object o, EventArgs e)
		{
			//Stop listening for connection events (if set) and close window
			UiThread.RunOnIdle(containerWindowToClose.Close);
		}

		private IEnumerable<Printer> GetAllPrinters()
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