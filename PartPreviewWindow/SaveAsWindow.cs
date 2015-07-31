using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.CustomWidgets.LibrarySelector;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrintLibrary;
using MatterHackers.MatterControl.PrintLibrary.Provider;
using MatterHackers.MatterControl.PrintQueue;
using System;
using System.Collections.Generic;
using System.IO;

namespace MatterHackers.MatterControl
{
	public class SaveAsWindow : SystemWindow
	{
		private Action<SaveAsReturnInfo> functionToCallOnSaveAs;
		private LibraryProvider selectedLibraryProvider;
		private TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();
		private MHTextEditWidget textToAddWidget;

		public SaveAsWindow(Action<SaveAsReturnInfo> functionToCallOnSaveAs, List<ProviderLocatorNode> providerLocator)
			: base(480, 450)
		{
			Title = "MatterControl - " + "Save As".Localize();
			AlwaysOnTopOfMain = true;

			selectedLibraryProvider = new LibraryProviderSelector(ChangeLibraryProvider);

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
				string saveAsLabel = "Save New Design".Localize() + ":";
				TextWidget elementHeader = new TextWidget(saveAsLabel, pointSize: 14);
				elementHeader.TextColor = ActiveTheme.Instance.PrimaryTextColor;
				elementHeader.HAnchor = HAnchor.ParentLeftRight;
				elementHeader.VAnchor = Agg.UI.VAnchor.ParentBottom;

				headerRow.AddChild(elementHeader);
				topToBottom.AddChild(headerRow);
				this.AddChild(topToBottom);
			}

			//Creates container in the middle of window
			FlowLayoutWidget middleRowContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
			{
				middleRowContainer.HAnchor = HAnchor.ParentLeftRight;
				middleRowContainer.VAnchor = VAnchor.ParentBottomTop;
				middleRowContainer.Padding = new BorderDouble(5);
				middleRowContainer.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;
			}

			// put in the bread crumb widget
			FolderBreadCrumbWidget breadCrumbWidget = new FolderBreadCrumbWidget(ChangeLibraryProvider, selectedLibraryProvider);
			middleRowContainer.AddChild(breadCrumbWidget);

			// put in the area to pick the provider to save to
			{
				string providerToSaveToLabel = "Save Location".Localize();
				TextWidget textBoxHeader = new TextWidget(providerToSaveToLabel, pointSize: 12);
				textBoxHeader.TextColor = ActiveTheme.Instance.PrimaryTextColor;
				textBoxHeader.Margin = new BorderDouble(5);
				textBoxHeader.HAnchor = HAnchor.ParentLeft;

				string chooseLocationLabelFull = "Choose the location to save to.".Localize(); ;
				TextWidget textBoxHeaderFull = new TextWidget(chooseLocationLabelFull, pointSize: 9);
				textBoxHeaderFull.TextColor = ActiveTheme.Instance.PrimaryTextColor;
				textBoxHeaderFull.Margin = new BorderDouble(5);
				textBoxHeaderFull.HAnchor = HAnchor.ParentLeftRight;

				//Adds text box and check box to the above container
				GuiWidget chooseWindow = new GuiWidget(10, 30);
				chooseWindow.HAnchor = HAnchor.ParentLeftRight;
				chooseWindow.VAnchor = VAnchor.ParentBottomTop;
				chooseWindow.Margin = new BorderDouble(5);
				chooseWindow.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
				chooseWindow.Padding = new BorderDouble(3);
				chooseWindow.AddChild(new LibrarySelectorWidget());

				middleRowContainer.AddChild(textBoxHeader);
				middleRowContainer.AddChild(textBoxHeaderFull);
				middleRowContainer.AddChild(chooseWindow);
			}

			// put in the area to type in the new name
			{
				string fileNameLabel = "Design Name".Localize();
				TextWidget textBoxHeader = new TextWidget(fileNameLabel, pointSize: 12);
				textBoxHeader.TextColor = ActiveTheme.Instance.PrimaryTextColor;
				textBoxHeader.Margin = new BorderDouble(5);
				textBoxHeader.HAnchor = HAnchor.ParentLeft;

				string fileNameLabelFull = "Enter the name of your design.".Localize(); ;
				TextWidget textBoxHeaderFull = new TextWidget(fileNameLabelFull, pointSize: 9);
				textBoxHeaderFull.TextColor = ActiveTheme.Instance.PrimaryTextColor;
				textBoxHeaderFull.Margin = new BorderDouble(5);
				textBoxHeaderFull.HAnchor = HAnchor.ParentLeftRight;

				//Adds text box and check box to the above container
				textToAddWidget = new MHTextEditWidget("", pixelWidth: 300, messageWhenEmptyAndNotSelected: "Enter a Design Name Here".Localize());
				textToAddWidget.HAnchor = HAnchor.ParentLeftRight;
				textToAddWidget.Margin = new BorderDouble(5);
				textToAddWidget.ActualTextEditWidget.EnterPressed += new KeyEventHandler(ActualTextEditWidget_EnterPressed);

				middleRowContainer.AddChild(textBoxHeader);
				middleRowContainer.AddChild(textBoxHeaderFull);
				middleRowContainer.AddChild(textToAddWidget);
			}

			middleRowContainer.AddChild(new HorizontalSpacer());
			topToBottom.AddChild(middleRowContainer);

			//Creates button container on the bottom of window
			FlowLayoutWidget buttonRow = new FlowLayoutWidget(FlowDirection.LeftToRight);
			{
				BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
				buttonRow.HAnchor = HAnchor.ParentLeftRight;
				buttonRow.Padding = new BorderDouble(0, 3);
			}

			Button saveAsButton = textImageButtonFactory.Generate("Save As".Localize(), centerText: true);
			saveAsButton.Visible = true;
			saveAsButton.Cursor = Cursors.Hand;
			buttonRow.AddChild(saveAsButton);

			saveAsButton.Click += new EventHandler(saveAsButton_Click);

			//Adds SaveAs and Close Button to button container
			buttonRow.AddChild(new HorizontalSpacer());

			Button cancelButton = textImageButtonFactory.Generate("Cancel".Localize(), centerText: true);
			cancelButton.Visible = true;
			cancelButton.Cursor = Cursors.Hand;
			buttonRow.AddChild(cancelButton);
			cancelButton.Click += (sender, e) =>
			{
				CloseOnIdle();
			};

			topToBottom.AddChild(buttonRow);

			ShowAsSystemWindow();

			UiThread.RunOnIdle(textToAddWidget.Focus);
		}

		void ChangeLibraryProvider(LibraryProvider libraryProvider)
		{
			selectedLibraryProvider = libraryProvider;
		}

		private void ActualTextEditWidget_EnterPressed(object sender, KeyEventArgs keyEvent)
		{
			SubmitForm();
		}

		private void saveAsButton_Click(object sender, EventArgs mouseEvent)
		{
			SubmitForm();
		}

		private void SubmitForm()
		{
			string newName = textToAddWidget.ActualTextEditWidget.Text;
			if (newName != "")
			{
				string fileName = Path.ChangeExtension(Path.GetRandomFileName(), ".amf");
				string fileNameAndPath = Path.Combine(ApplicationDataStorage.Instance.ApplicationLibraryDataPath, fileName);

				SaveAsReturnInfo returnInfo = new SaveAsReturnInfo(newName, fileNameAndPath, selectedLibraryProvider);
				functionToCallOnSaveAs(returnInfo);
				CloseOnIdle();
			}
		}

		public class SaveAsReturnInfo
		{
			public string fileNameAndPath;
			public string newName;
			public PrintItemWrapper printItemWrapper;

			public SaveAsReturnInfo(string newName, string fileNameAndPath, LibraryProvider destinationLibraryProvider)
			{
				this.newName = newName;
				this.fileNameAndPath = fileNameAndPath;

				PrintItem printItem = new PrintItem();
				printItem.Name = newName;
				printItem.FileLocation = Path.GetFullPath(fileNameAndPath);
				throw new NotImplementedException();

				printItemWrapper = new PrintItemWrapper(printItem, destinationLibraryProvider);
			}
		}
	}
}