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
		private TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();
		private MHTextEditWidget textToAddWidget;
		LibrarySelectorWidget librarySelectorWidget;
		Button saveAsButton;

        public SaveAsWindow(Action<SaveAsReturnInfo> functionToCallOnSaveAs, List<ProviderLocatorNode> providerLocator, bool showQueue, bool getNewName)
			: base(480, 500)
		{
			textImageButtonFactory.normalTextColor = ActiveTheme.Instance.PrimaryTextColor;
			textImageButtonFactory.hoverTextColor = ActiveTheme.Instance.PrimaryTextColor;
			textImageButtonFactory.pressedTextColor = ActiveTheme.Instance.PrimaryTextColor;
			textImageButtonFactory.disabledTextColor = ActiveTheme.Instance.TabLabelUnselected;
			textImageButtonFactory.disabledFillColor = new RGBA_Bytes();

			Title = "MatterControl - " + "Save As".Localize();
			AlwaysOnTopOfMain = true;
			this.Name = "Save As Window";

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
			}

			//Creates container in the middle of window
			FlowLayoutWidget middleRowContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
			{
				middleRowContainer.HAnchor = HAnchor.ParentLeftRight;
				middleRowContainer.VAnchor = VAnchor.ParentBottomTop;
				middleRowContainer.Padding = new BorderDouble(5);
				middleRowContainer.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;
			}

			librarySelectorWidget = new LibrarySelectorWidget(showQueue);

			// put in the bread crumb widget
			FolderBreadCrumbWidget breadCrumbWidget = new FolderBreadCrumbWidget(librarySelectorWidget.SetCurrentLibraryProvider, librarySelectorWidget.CurrentLibraryProvider);
			middleRowContainer.AddChild(breadCrumbWidget);

			librarySelectorWidget.ChangedCurrentLibraryProvider += breadCrumbWidget.SetBreadCrumbs;

			// put in the area to pick the provider to save to
			{
				//Adds text box and check box to the above container
				GuiWidget chooseWindow = new GuiWidget(10, 30);
				chooseWindow.HAnchor = HAnchor.ParentLeftRight;
				chooseWindow.VAnchor = VAnchor.ParentBottomTop;
				chooseWindow.Margin = new BorderDouble(5);
				chooseWindow.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
				chooseWindow.Padding = new BorderDouble(3);
				chooseWindow.AddChild(librarySelectorWidget);

				middleRowContainer.AddChild(chooseWindow);
			}

			// put in the area to type in the new name
			if(getNewName)
			{
				string fileNameLabel = "Design Name".Localize();
				TextWidget fileNameHeader = new TextWidget(fileNameLabel, pointSize: 12);
				fileNameHeader.TextColor = ActiveTheme.Instance.PrimaryTextColor;
				fileNameHeader.Margin = new BorderDouble(5);
				fileNameHeader.HAnchor = HAnchor.ParentLeft;

				//Adds text box and check box to the above container
				textToAddWidget = new MHTextEditWidget("", pixelWidth: 300, messageWhenEmptyAndNotSelected: "Enter a Design Name Here".Localize());
				textToAddWidget.HAnchor = HAnchor.ParentLeftRight;
				textToAddWidget.Margin = new BorderDouble(5);
				textToAddWidget.ActualTextEditWidget.EnterPressed += new KeyEventHandler(ActualTextEditWidget_EnterPressed);

				middleRowContainer.AddChild(fileNameHeader);
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

			saveAsButton = textImageButtonFactory.Generate("Save".Localize(), centerText: true);
			saveAsButton.Name = "Save As Save Button";
			// Disable the save as button until the user actually selects a provider
			saveAsButton.Enabled = false;
			saveAsButton.Cursor = Cursors.Hand;
			buttonRow.AddChild(saveAsButton);

			librarySelectorWidget.ChangedCurrentLibraryProvider += EnableSaveAsButtonOnChangedLibraryProvider;

			saveAsButton.Click += saveAsButton_Click;

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

			this.AddChild(topToBottom);

			ShowAsSystemWindow();
		}

		private void EnableSaveAsButtonOnChangedLibraryProvider(LibraryProvider arg1, LibraryProvider arg2)
		{
			// Once we have navigated to any provider enable the ability to click the save as button.
			saveAsButton.Enabled = true;
		}

		public override void OnLoad(EventArgs args)
		{
			if (textToAddWidget != null
				&& !UserSettings.Instance.IsTouchScreen)
			{
				UiThread.RunOnIdle(textToAddWidget.Focus);
			}
			base.OnLoad(args);
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
			string newName = "none";
			if (textToAddWidget != null)
			{
				newName = textToAddWidget.ActualTextEditWidget.Text;
			}

			if (newName != "")
			{
				string fileName = Path.ChangeExtension(Path.GetRandomFileName(), ".amf");
				string fileNameAndPath = Path.Combine(ApplicationDataStorage.Instance.ApplicationLibraryDataPath, fileName);

				SaveAsReturnInfo returnInfo = new SaveAsReturnInfo(newName, fileNameAndPath, librarySelectorWidget.CurrentLibraryProvider);
				functionToCallOnSaveAs(returnInfo);
				CloseOnIdle();
			}
		}

		public class SaveAsReturnInfo
		{
			public string fileNameAndPath;
			public string newName;
			public LibraryProvider destinationLibraryProvider;

			public SaveAsReturnInfo(string newName, string fileNameAndPath, LibraryProvider destinationLibraryProvider)
			{
				this.destinationLibraryProvider = destinationLibraryProvider;
				this.newName = newName;
				this.fileNameAndPath = fileNameAndPath;
			}
		}
	}
}