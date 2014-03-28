using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;
using MatterHackers.Agg.Image;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.Localizations;

namespace MatterHackers.MatterControl 
{
	public class SaveAsWindow : SystemWindow
	{
		Button saveAsButton;
		Button cancelSaveButton;
		CheckBox addToLibraryOption;
		protected TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory ();

		public SaveAsWindow()
			: base (480, 250)
		{
			Title = "Save As Window";

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
				string saveAsLabel = "Save As:";
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

			string fileNameLabel = "Design Name*";
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

			string checkBoxHelpText = "Design will be saved to MatterControl library unless 'Add to Queue' box is checked.";
			TextWidget checkBoxHelpFull = new TextWidget(checkBoxHelpText, pointSize: 9);
			checkBoxHelpFull.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			checkBoxHelpFull.Margin = new BorderDouble(5);
			checkBoxHelpFull.HAnchor = HAnchor.ParentLeftRight;

			addToLibraryOption = new CheckBox("Add to Queue",RGBA_Bytes.White);
			addToLibraryOption.Margin = new BorderDouble (5);
			addToLibraryOption.HAnchor = HAnchor.ParentLeftRight;

			presetsFormContainer.AddChild(textBoxHeader);
			presetsFormContainer.AddChild (textBoxHeaderFull);
			presetsFormContainer.AddChild(textToAddWidget);
			presetsFormContainer.AddChild(cTSpacer);
			presetsFormContainer.AddChild(checkBoxHelpFull);
			presetsFormContainer.AddChild(addToLibraryOption);
			topToBottom.AddChild(presetsFormContainer);


			//Creates button container on the bottom of window 
			FlowLayoutWidget buttonRow = new FlowLayoutWidget(FlowDirection.LeftToRight);
			{
				BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
				buttonRow.HAnchor = HAnchor.ParentLeftRight;
				buttonRow.Padding = new BorderDouble(0,3);
			}
				
			saveAsButton = textImageButtonFactory.Generate("Save As".Localize(), centerText: true);
			saveAsButton.Visible = true;
			saveAsButton.Cursor = Cursors.Hand;

			//Adds SaveAs and Close Button to button container
			GuiWidget hButtonSpacer = new GuiWidget();
			hButtonSpacer.HAnchor = HAnchor.ParentLeftRight;

			cancelSaveButton = textImageButtonFactory.Generate ("Cancel", centerText: true);
			cancelSaveButton.Visible = true;
			cancelSaveButton.Cursor = Cursors.Hand;

			buttonRow.AddChild(saveAsButton);
			buttonRow.AddChild(hButtonSpacer);
			buttonRow.AddChild(cancelSaveButton);
			topToBottom.AddChild(buttonRow);

			ShowAsSystemWindow ();

		}
			
	}
}

