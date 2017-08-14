using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DataStorage;
using System;
using System.IO;

namespace MatterHackers.MatterControl
{
	public class RenameItemWindow : SystemWindow
	{
		private Action<string> functionToCallToCreateNamedFolder;
		private TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();
		private MHTextEditWidget saveAsNameWidget;
		TextWidget elementHeader;
		Button renameItemButton;

		public string ElementHeader
		{
			get { return elementHeader.Text; }
			set { elementHeader.Text = value; }
		}

		public RenameItemWindow(string windowTitle, string currentItemName, Action<string> functionToCallToRenameItem)
			: base(480, 180)
		{
			Title = "MatterControl - Rename Item";
			AlwaysOnTopOfMain = true;

			this.functionToCallToCreateNamedFolder = functionToCallToRenameItem;

			FlowLayoutWidget topToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
			topToBottom.AnchorAll();
			topToBottom.Padding = new BorderDouble(3, 0, 3, 5);

			// Creates Header
			FlowLayoutWidget headerRow = new FlowLayoutWidget(FlowDirection.LeftToRight);
			headerRow.HAnchor = HAnchor.Stretch;
			headerRow.Margin = new BorderDouble(0, 3, 0, 0);
			headerRow.Padding = new BorderDouble(0, 3, 0, 3);
			BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

			//Creates Text and adds into header
			{
				elementHeader = new TextWidget(windowTitle, pointSize: 14);
				elementHeader.TextColor = ActiveTheme.Instance.PrimaryTextColor;
				elementHeader.HAnchor = HAnchor.Stretch;
				elementHeader.VAnchor = Agg.UI.VAnchor.Bottom;

				headerRow.AddChild(elementHeader);
				topToBottom.AddChild(headerRow);
				this.AddChild(topToBottom);
			}

			//Creates container in the middle of window
			FlowLayoutWidget middleRowContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
			{
				middleRowContainer.HAnchor = HAnchor.Stretch;
				middleRowContainer.VAnchor = VAnchor.Stretch;
				middleRowContainer.Padding = new BorderDouble(5);
				middleRowContainer.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;
			}

			string fileNameLabel = "New Name".Localize();
			TextWidget textBoxHeader = new TextWidget(fileNameLabel, pointSize: 12);
			textBoxHeader.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			textBoxHeader.Margin = new BorderDouble(5);
			textBoxHeader.HAnchor = HAnchor.Left;

			//Adds text box and check box to the above container
			saveAsNameWidget = new MHTextEditWidget(currentItemName, pixelWidth: 300, messageWhenEmptyAndNotSelected: "Enter New Name Here".Localize());
			saveAsNameWidget.HAnchor = HAnchor.Stretch;
			saveAsNameWidget.Margin = new BorderDouble(5);

			middleRowContainer.AddChild(textBoxHeader);
			middleRowContainer.AddChild(saveAsNameWidget);
			middleRowContainer.AddChild(new HorizontalSpacer());
			topToBottom.AddChild(middleRowContainer);

			//Creates button container on the bottom of window
			FlowLayoutWidget buttonRow = new FlowLayoutWidget(FlowDirection.LeftToRight);
			{
				BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
				buttonRow.HAnchor = HAnchor.Stretch;
				buttonRow.Padding = new BorderDouble(0, 3);
			}

			renameItemButton = textImageButtonFactory.Generate("Rename".Localize());
			renameItemButton.Name = "Rename Button";
			renameItemButton.Visible = true;
			renameItemButton.Cursor = Cursors.Hand;
			buttonRow.AddChild(renameItemButton);

			renameItemButton.Click += renameItemButton_Click;
			saveAsNameWidget.ActualTextEditWidget.EnterPressed += new KeyEventHandler(ActualTextEditWidget_EnterPressed);

			//Adds Create and Close Button to button container
			buttonRow.AddChild(new HorizontalSpacer());

			Button cancelButton = textImageButtonFactory.Generate("Cancel".Localize());
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
			UiThread.RunOnIdle(() =>
			{
				saveAsNameWidget.Focus();
				saveAsNameWidget.ActualTextEditWidget.InternalTextEditWidget.SelectAll();
			});
			base.OnLoad(args);
		}

		private void ActualTextEditWidget_EnterPressed(object sender, KeyEventArgs keyEvent)
		{
			SubmitForm();
		}

		private void renameItemButton_Click(object sender, EventArgs mouseEvent)
		{
			SubmitForm();
		}

		private void SubmitForm()
		{
			string newName = saveAsNameWidget.ActualTextEditWidget.Text;
			if (newName != "")
			{
				string fileName = Path.ChangeExtension(Path.GetRandomFileName(), ".amf");
				string fileNameAndPath = Path.Combine(ApplicationDataStorage.Instance.ApplicationLibraryDataPath, fileName);

				functionToCallToCreateNamedFolder(newName);
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