/*
Copyright (c) 2017, Lars Brubaker, John Lewin
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
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.Library;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl
{
	public class LibraryBrowserPage : DialogPage
	{
		protected GuiWidget acceptButton = null;
		protected ThemedTextEditWidget itemNameWidget;
		protected ILibraryContext libraryNavContext;
		protected LibraryListView librarySelectorWidget;
		private FolderBreadCrumbWidget breadCrumbWidget = null;

		public static bool AllowDragToBed { get; internal set; } = true;

		public LibraryBrowserPage(Action<ILibraryWritableContainer, string> acceptCallback, string acceptButtonText)
		{
			AllowDragToBed = false;

			Closed += (s, e) =>
			{
				AllowDragToBed = true;
			};

			this.WindowSize = new Vector2(480, 500);

			contentRow.Padding = 0;

			// Create a new library context for the SaveAs view
			libraryNavContext = new LibraryConfig()
			{
				ActiveContainer = ApplicationController.Instance.Library.RootLibaryContainer
			};
			libraryNavContext.ContainerChanged += (s, e) =>
			{
				acceptButton.Enabled = libraryNavContext.ActiveContainer is ILibraryWritableContainer;
				breadCrumbWidget.SetContainer(libraryNavContext.ActiveContainer);
			};

			librarySelectorWidget = new LibraryListView(libraryNavContext, new IconListView(theme, 75), theme)
			{
				BackgroundColor = theme.MinimalShade,
				ContainerFilter = (container) => !container.IsReadOnly,
			};

			// put in the bread crumb widget
			breadCrumbWidget = new FolderBreadCrumbWidget(libraryNavContext, theme);
			breadCrumbWidget.BackgroundColor = theme.MinimalShade;
			contentRow.AddChild(breadCrumbWidget);
			contentRow.BackgroundColor = Color.Transparent;

			contentRow.AddChild(librarySelectorWidget);

			acceptButton = theme.CreateDialogButton(acceptButtonText);
			acceptButton.Name = "Accept Button";
			// Disable the save as button until the user actually selects a provider
			acceptButton.Enabled = false;
			acceptButton.Cursor = Cursors.Hand;
			acceptButton.Click += (s, e) =>
			{
				var closeAfterSave = true;
				if (librarySelectorWidget.ActiveContainer is ILibraryWritableContainer writableContainer)
				{
					var fileName = ApplicationController.Instance.SanitizeFileName(itemNameWidget?.ActualTextEditWidget.Text ?? "none");
					var outputName = Path.ChangeExtension(fileName, ".mcx");

					if (writableContainer is FileSystemContainer fileSystemContainer)
					{
						if (File.Exists(Path.Combine(fileSystemContainer.FullPath, outputName)))
                        {
							closeAfterSave = false;
							// ask about overwriting the exisitng file
							StyledMessageBox.ShowMessageBox(
							(overwriteFile) =>
							{
								if (overwriteFile)
								{
									acceptCallback(writableContainer, outputName);
									this.DialogWindow.CloseOnIdle();
								}
								else
                                {
									// turn the accept button back on
									acceptButton.Enabled = true;
								}
							},
							"\"{0}\" already exists.\nDo you want to replace it?".Localize().FormatWith(outputName),
							"Confirm Save As".Localize(),
							StyledMessageBox.MessageType.YES_NO,
							"Replace".Localize(),
							"Cancel".Localize());
						}
						else
                        {
							// save and exit normaly
							acceptCallback(writableContainer, outputName);
						}
					}
					else
					{
						acceptCallback(writableContainer, outputName);
					}
				}

				if (closeAfterSave)
				{
					this.DialogWindow.CloseOnIdle();
				}
			};

			this.AddPageAction(acceptButton);
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			if (mouseEvent.Button == MouseButtons.XButton1)
			{
				// user pressed the back button
				NavigateBack();
			}

			base.OnMouseDown(mouseEvent);
		}

		public void NavigateBack()
		{
			breadCrumbWidget.NavigateBack();
		}

		public override void OnLoad(EventArgs args)
		{
			if (itemNameWidget != null
				&& !GuiWidget.TouchScreenMode)
			{
				UiThread.RunOnIdle(itemNameWidget.Focus);
			}
			base.OnLoad(args);
		}
	}
}