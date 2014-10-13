using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;
using MatterHackers.Agg.Image;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrintLibrary;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.CustomWidgets;

namespace MatterHackers.MatterControl 
{
	public class SaveAsWindow : SystemWindow
	{
		TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory ();
        MHTextEditWidget textToAddWidget;
        CheckBox addToLibraryOption;        

        public delegate void SetPrintItemWrapperAndSave(PrintItemWrapper printItemWrapper);
        SetPrintItemWrapperAndSave functionToCallOnSaveAs;

        public SaveAsWindow(SetPrintItemWrapperAndSave functionToCallOnSaveAs)
			: base (480, 250)
		{
			Title = "MatterControl - Save As";

            this.functionToCallOnSaveAs = functionToCallOnSaveAs;

			FlowLayoutWidget topToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
			topToBottom.AnchorAll();
			topToBottom.Padding = new BorderDouble(3, 0, 3, 5);

			// Creates Header
			FlowLayoutWidget headerRow = new FlowLayoutWidget(FlowDirection.LeftToRight);
			headerRow.HAnchor = HAnchor.ParentLeftRight;
			headerRow.Margin = new BorderDouble(0, 3, 0, 0);
			headerRow.Padding = new BorderDouble(0, 3, 0, 3);
			BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

			//Creates Text and adds into header 
			{
				string saveAsLabel = "Save New Design to Queue:";
				TextWidget elementHeader = new TextWidget (saveAsLabel, pointSize: 14);
				elementHeader.TextColor = ActiveTheme.Instance.PrimaryTextColor;
				elementHeader.HAnchor = HAnchor.ParentLeftRight;
				elementHeader.VAnchor = Agg.UI.VAnchor.ParentBottom;

				headerRow.AddChild (elementHeader);
				topToBottom.AddChild (headerRow);
				this.AddChild (topToBottom);
			}

			//Creates container in the middle of window
			FlowLayoutWidget middleRowContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
			{
				middleRowContainer.HAnchor = HAnchor.ParentLeftRight;
				middleRowContainer.VAnchor = VAnchor.ParentBottomTop;
				middleRowContainer.Padding = new BorderDouble(5);
				middleRowContainer.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;
			}

			string fileNameLabel = "Design Name";
			TextWidget textBoxHeader = new TextWidget(fileNameLabel, pointSize: 12);
			textBoxHeader.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			textBoxHeader.Margin = new BorderDouble (5);
			textBoxHeader.HAnchor = HAnchor.ParentLeft;

			string fileNameLabelFull = "Enter the name of your design.";
			TextWidget textBoxHeaderFull = new TextWidget(fileNameLabelFull, pointSize: 9);
			textBoxHeaderFull.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			textBoxHeaderFull.Margin = new BorderDouble (5);
			textBoxHeaderFull.HAnchor = HAnchor.ParentLeftRight;

			//Adds text box and check box to the above container
			textToAddWidget = new MHTextEditWidget("", pixelWidth: 300, messageWhenEmptyAndNotSelected: "Enter a Design Name Here");
			textToAddWidget.HAnchor = HAnchor.ParentLeftRight;
			textToAddWidget.Margin = new BorderDouble(5);

            addToLibraryOption = new CheckBox("Also save to Library", ActiveTheme.Instance.PrimaryTextColor);
			addToLibraryOption.Margin = new BorderDouble (5);
			addToLibraryOption.HAnchor = HAnchor.ParentLeftRight;

			middleRowContainer.AddChild(textBoxHeader);
			middleRowContainer.AddChild (textBoxHeaderFull);
			middleRowContainer.AddChild(textToAddWidget);
			middleRowContainer.AddChild(new HorizontalSpacer());
			middleRowContainer.AddChild(addToLibraryOption);
			topToBottom.AddChild(middleRowContainer);

			//Creates button container on the bottom of window 
			FlowLayoutWidget buttonRow = new FlowLayoutWidget(FlowDirection.LeftToRight);
			{
				BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
				buttonRow.HAnchor = HAnchor.ParentLeftRight;
				buttonRow.Padding = new BorderDouble(0,3);
			}
				
			Button saveAsButton = textImageButtonFactory.Generate("Save As".Localize(), centerText: true);
			saveAsButton.Visible = true;
			saveAsButton.Cursor = Cursors.Hand;
            buttonRow.AddChild(saveAsButton);

            saveAsButton.Click += new ButtonBase.ButtonEventHandler(saveAsButton_Click);
            textToAddWidget.ActualTextEditWidget.EnterPressed += new KeyEventHandler(ActualTextEditWidget_EnterPressed);

			//Adds SaveAs and Close Button to button container
            buttonRow.AddChild(new HorizontalSpacer());

			Button cancelButton = textImageButtonFactory.Generate ("Cancel", centerText: true);
			cancelButton.Visible = true;
			cancelButton.Cursor = Cursors.Hand;
            buttonRow.AddChild(cancelButton);
            cancelButton.Click += (sender, e) =>
            {
                CloseOnIdle();
            };

			topToBottom.AddChild(buttonRow);

			ShowAsSystemWindow ();
		}

        void ActualTextEditWidget_EnterPressed(object sender, KeyEventArgs keyEvent)
        {
            SubmitForm();
        }

        void saveAsButton_Click(object sender, MouseEventArgs mouseEvent)
        {
            SubmitForm();
        }

        private void SubmitForm()
        {
            string newName = textToAddWidget.ActualTextEditWidget.Text;
            if (newName != "")
            {
                string fileName = Path.ChangeExtension(Path.GetRandomFileName(), ".stl");
                string fileNameAndPath = Path.Combine(ApplicationDataStorage.Instance.ApplicationLibraryDataPath, fileName);

                PrintItem printItem = new PrintItem();
                printItem.Name = newName;
                printItem.FileLocation = Path.GetFullPath(fileNameAndPath);
                printItem.PrintItemCollectionID = LibraryData.Instance.LibraryCollection.Id;
                printItem.Commit();

                PrintItemWrapper printItemWrapper = new PrintItemWrapper(printItem);
                QueueData.Instance.AddItem(printItemWrapper);

                if (addToLibraryOption.Checked)
                {
                    LibraryData.Instance.AddItem(printItemWrapper);
                }

                functionToCallOnSaveAs(printItemWrapper);
                CloseOnIdle();
            }
        }
	}
}

