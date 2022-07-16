/*
Copyright (c) 2022, Lars Brubaker, John Lewin
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

using System;
using System.IO;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.ImageProcessing;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.Library;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl
{
	public class SaveAsPage : LibraryBrowserPage
	{
		public SaveAsPage(Action<ILibraryContainer, string> itemSaver)
			: base(itemSaver, "Save".Localize())
		{
			this.WindowTitle = "MatterControl - " + "Save As".Localize();
			this.Name = "Save As Window";
			this.WindowSize = new Vector2(480 * GuiWidget.DeviceScale, 500 * GuiWidget.DeviceScale);
			this.HeaderText = "Save New Design".Localize() + ":";

			// put in the area to type in the new name
			var fileNameHeader = new TextWidget("Design Name".Localize(), pointSize: 12)
			{
				TextColor = theme.TextColor,
				Margin = new BorderDouble(5),
				HAnchor = HAnchor.Left
			};
			contentRow.AddChild(fileNameHeader);

			// Adds text box and check box to the above container
			itemNameWidget = new ThemedTextEditWidget("", theme, pixelWidth: 300, messageWhenEmptyAndNotSelected: "Enter a Design Name Here".Localize())
			{
				HAnchor = HAnchor.Stretch,
				Margin = new BorderDouble(5),
				Name = "Design Name Edit Field"
			};

			this.librarySelectorWidget.ClickItemEvent += (s, e) =>
			{
				if (s is ListViewItem listViewItem
					&& this.AcceptButton.Enabled)
				{
					itemNameWidget.ActualTextEditWidget.Text = Path.ChangeExtension(listViewItem.Model.Name, ".mcx");
				}
			};

			void ClickAccept()
            {
				if (this.acceptButton.Enabled)
				{
					if (librarySelectorWidget.ActiveContainer is ILibraryWritableContainer)
					{
						acceptButton.InvokeClick();
						// And disable it so there are not multiple fires. No need to re-enable, the dialog is going to close.
						this.AcceptButton.Enabled = false;
					}
				}
			}

			this.librarySelectorWidget.DoubleClickItemEvent += (s, e) =>
			{
				ClickAccept();
			};

			itemNameWidget.ActualTextEditWidget.EnterPressed += (s, e) =>
			{
				ClickAccept();
			};

			itemNameWidget.ActualTextEditWidget.TextChanged += (s, e) =>
			{
				acceptButton.Enabled = libraryNavContext.ActiveContainer is ILibraryWritableContainer
					&& !string.IsNullOrWhiteSpace(itemNameWidget.ActualTextEditWidget.Text);
			};

			contentRow.AddChild(itemNameWidget);

			var icon = StaticData.Instance.LoadIcon("fa-folder-new_16.png", 16, 16).SetToColor(ApplicationController.Instance.MenuTheme.TextColor);
			var isEnabled = false;
			if (librarySelectorWidget.ActiveContainer is ILibraryWritableContainer writableContainer)
			{
				isEnabled = writableContainer?.AllowAction(ContainerActions.AddContainers) == true;
			}

			var folderButtonRow = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Left | HAnchor.Fit,
			};
			contentRow.AddChild(folderButtonRow);

			// add a create folder button
			var createFolderButton = new TextIconButton("Create Folder".Localize(), icon, theme)
			{
				Enabled = isEnabled,
				VAnchor = VAnchor.Absolute,
				DrawIconOverlayOnDisabled = true
			};
			createFolderButton.Name = "Create Folder In Button";
			folderButtonRow.AddChild(createFolderButton);

			var refreshButton = new IconButton(StaticData.Instance.LoadIcon("fa-refresh_14.png", 16, 16).SetToColor(theme.TextColor), theme)
			{
				ToolTipText = "Refresh Folder".Localize(),
				Enabled = isEnabled,
			};
			refreshButton.Click += (s, e) =>
			{
				librarySelectorWidget.ActiveContainer.Load();
			};

			// folderButtonRow.AddChild(refreshButton);

			createFolderButton.Click += CreateFolder_Click;

			// add a message to navigate to a writable folder
			var writableMessage = new TextWidget("Please select a writable folder".Localize(), pointSize: theme.DefaultFontSize)
			{
				TextColor = theme.TextColor,
				Margin = new BorderDouble(5, 0),
				VAnchor = VAnchor.Center
			};
			footerRow.AddChild(writableMessage, 0);

			footerRow.AddChild(new HorizontalSpacer(), 1);

			// change footer in this context
			footerRow.HAnchor = HAnchor.Stretch;
			footerRow.Margin = 0;

			libraryNavContext.ContainerChanged += (s, e) =>
			{
				var writable = libraryNavContext.ActiveContainer is ILibraryWritableContainer;
				createFolderButton.Enabled = writable;
				refreshButton.Enabled = writable;
				writableMessage.Visible = !writable;
			};
		}

		public override void OnKeyDown(KeyEventArgs keyEvent)
		{
			if (keyEvent.KeyCode == Keys.F5)
			{
				librarySelectorWidget.ActiveContainer.Load();
			}

			base.OnKeyDown(keyEvent);
		}

		private void CreateFolder_Click(object sender, MouseEventArgs e)
		{
			DialogWindow.Show(
				new InputBoxPage(
					"Create Folder".Localize(),
					"Folder Name".Localize(),
					"",
					"Enter New Name Here".Localize(),
					"Create".Localize(),
					(newName) =>
					{
						if (librarySelectorWidget.ActiveContainer is ILibraryWritableContainer writableContainer)
						{
							if (!string.IsNullOrEmpty(newName)
								&& writableContainer != null)
							{
								writableContainer.Add(new[]
								{
									new CreateFolderItem() { Name = newName }
								});
							}
						}
					}));
		}
	}
}