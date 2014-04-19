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

namespace MatterHackers.MatterControl 
{
	public class SaveAsWindow : SystemWindow
	{
		protected TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory ();

        public delegate void SetPrintItemWrapperAndSave(PrintItemWrapper printItemWrapper);
        public SaveAsWindow(SetPrintItemWrapperAndSave functionToCallOnSaveAs)
			: base (480, 250)
		{
			Title = "MatterControl - Save As";

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
			FlowLayoutWidget presetsFormContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
			{
				presetsFormContainer.HAnchor = HAnchor.ParentLeftRight;
				presetsFormContainer.VAnchor = VAnchor.ParentBottomTop;
				presetsFormContainer.Padding = new BorderDouble(5);
				presetsFormContainer.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;
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
			MHTextEditWidget textToAddWidget = new MHTextEditWidget("", pixelWidth: 300, messageWhenEmptyAndNotSelected: "Enter a Design Name Here");
			textToAddWidget.HAnchor = HAnchor.ParentLeftRight;
			textToAddWidget.Margin = new BorderDouble(5);

			GuiWidget cTSpacer = new GuiWidget();
			cTSpacer.HAnchor = HAnchor.ParentLeftRight;

            CheckBox addToLibraryOption = new CheckBox("Also save to Library", ActiveTheme.Instance.PrimaryTextColor);
			addToLibraryOption.Margin = new BorderDouble (5);
			addToLibraryOption.HAnchor = HAnchor.ParentLeftRight;

			presetsFormContainer.AddChild(textBoxHeader);
			presetsFormContainer.AddChild (textBoxHeaderFull);
			presetsFormContainer.AddChild(textToAddWidget);
			presetsFormContainer.AddChild(cTSpacer);
			presetsFormContainer.AddChild(addToLibraryOption);
			topToBottom.AddChild(presetsFormContainer);

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
            saveAsButton.Click += (sender, e) =>
            {
                string newName = textToAddWidget.ActualTextEditWidget.Text;
                if (newName != "")
                {
                    string fileName = "{0}.stl".FormatWith(Path.GetRandomFileName());
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
            };

			//Adds SaveAs and Close Button to button container
			GuiWidget hButtonSpacer = new GuiWidget();
			hButtonSpacer.HAnchor = HAnchor.ParentLeftRight;
            buttonRow.AddChild(hButtonSpacer);

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
	}
}

