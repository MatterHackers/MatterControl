using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DataStorage;
using System;
using System.IO;

namespace MatterHackers.MatterControl
{
	public class CreateFolderWindow : SystemWindow
	{
		private Action<CreateFolderReturnInfo> functionToCallToCreateNamedFolder;
		private TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();
		private MHTextEditWidget folderNameWidget;

		public CreateFolderWindow(Action<CreateFolderReturnInfo> functionToCallToCreateNamedFolder)
			: base(480, 180)
		{
			Title = "MatterControl - Create Folder";
			AlwaysOnTopOfMain = true;

			this.functionToCallToCreateNamedFolder = functionToCallToCreateNamedFolder;

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
				string createFolderLabel = "Create New Folder:".Localize();
				TextWidget elementHeader = new TextWidget(createFolderLabel, pointSize: 14);
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

			string fileNameLabel = "Folder Name".Localize();
			TextWidget textBoxHeader = new TextWidget(fileNameLabel, pointSize: 12);
			textBoxHeader.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			textBoxHeader.Margin = new BorderDouble(5);
			textBoxHeader.HAnchor = HAnchor.ParentLeft;

			//Adds text box and check box to the above container
			folderNameWidget = new MHTextEditWidget("", pixelWidth: 300, messageWhenEmptyAndNotSelected: "Enter a Folder Name Here".Localize());
			folderNameWidget.Name = "Create Folder - Text Input";
			folderNameWidget.HAnchor = HAnchor.ParentLeftRight;
			folderNameWidget.Margin = new BorderDouble(5);

			middleRowContainer.AddChild(textBoxHeader);
			middleRowContainer.AddChild(folderNameWidget);
			middleRowContainer.AddChild(new HorizontalSpacer());
			topToBottom.AddChild(middleRowContainer);

			//Creates button container on the bottom of window
			FlowLayoutWidget buttonRow = new FlowLayoutWidget(FlowDirection.LeftToRight);
			{
				BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
				buttonRow.HAnchor = HAnchor.ParentLeftRight;
				buttonRow.Padding = new BorderDouble(0, 3);
			}

			Button createFolderButton = textImageButtonFactory.Generate("Create".Localize(), centerText: true);
			createFolderButton.Name = "Create Folder Button";
			createFolderButton.Visible = true;
			createFolderButton.Cursor = Cursors.Hand;
			buttonRow.AddChild(createFolderButton);

			createFolderButton.Click += createFolderButton_Click;
			folderNameWidget.ActualTextEditWidget.EnterPressed += new KeyEventHandler(ActualTextEditWidget_EnterPressed);

			//Adds Create and Close Button to button container
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
		}

		public override void OnLoad(EventArgs args)
		{
			UiThread.RunOnIdle(folderNameWidget.Focus);
			base.OnLoad(args);
		}

		private void ActualTextEditWidget_EnterPressed(object sender, KeyEventArgs keyEvent)
		{
			SubmitForm();
		}

		private void createFolderButton_Click(object sender, EventArgs mouseEvent)
		{
			SubmitForm();
		}

		private void SubmitForm()
		{
			string newName = folderNameWidget.ActualTextEditWidget.Text;
			if (newName != "")
			{
				string fileName = Path.ChangeExtension(Path.GetRandomFileName(), ".amf");
				string fileNameAndPath = Path.Combine(ApplicationDataStorage.Instance.ApplicationLibraryDataPath, fileName);

				CreateFolderReturnInfo returnInfo = new CreateFolderReturnInfo(newName);
				functionToCallToCreateNamedFolder(returnInfo);
				CloseOnIdle();
			}
		}

		public class CreateFolderReturnInfo
		{
			public string newName;

			public CreateFolderReturnInfo(string newName)
			{
				this.newName = newName;
			}
		}
	}
}