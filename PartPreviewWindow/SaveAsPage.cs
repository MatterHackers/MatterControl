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
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.Library;

namespace MatterHackers.MatterControl
{
	public class SaveAsPage : DialogPage
	{
		private MHTextEditWidget textToAddWidget;
		private ILibraryContext libraryNavContext;

		public SaveAsPage(Action<string, ILibraryContainer> functionToCallOnSaveAs, bool allowNameChange = true)
		{
			Button saveAsButton = null;
			FolderBreadCrumbWidget breadCrumbWidget = null;
			var buttonFactory = ApplicationController.Instance.Theme.ButtonFactory;

			this.WindowTitle = "MatterControl - " + "Save As".Localize();
			this.Name = "Save As Window";
			this.WindowSize = new VectorMath.Vector2(480, 500);
			this.HeaderText = "Save New Design".Localize() + ":";

			contentRow.Padding = 0;
			ListView librarySelectorWidget;
		
			// Create a new library context for the SaveAs view
			libraryNavContext = new LibraryConfig()
			{
				ActiveContainer = ApplicationController.Instance.Library.RootLibaryContainer
			};
			libraryNavContext.ContainerChanged += (s, e) =>
			{
				saveAsButton.Enabled = libraryNavContext.ActiveContainer is ILibraryWritableContainer;
				breadCrumbWidget.SetBreadCrumbs(libraryNavContext.ActiveContainer);
			};

			librarySelectorWidget = new ListView(libraryNavContext, new IconListView(75))
			{
				BackgroundColor = ActiveTheme.Instance.TertiaryBackgroundColor,
				ShowItems = false,
				ContainerFilter = (container) => !container.IsReadOnly,
			};

			// put in the bread crumb widget
			breadCrumbWidget = new FolderBreadCrumbWidget(librarySelectorWidget);
			contentRow.AddChild(breadCrumbWidget);

			// put in the area to pick the provider to save to
			// Adds text box and check box to the above container
			GuiWidget chooseWindow = new GuiWidget(10, 30)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
				BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor,
			};
			chooseWindow.AddChild(librarySelectorWidget);
			contentRow.AddChild(chooseWindow);

			// put in the area to type in the new name
			if (allowNameChange)
			{
				TextWidget fileNameHeader = new TextWidget("Design Name".Localize(), pointSize: 12)
				{
					TextColor = ActiveTheme.Instance.PrimaryTextColor,
					Margin = new BorderDouble(5),
					HAnchor = HAnchor.Left
				};
				contentRow.AddChild(fileNameHeader);

				//Adds text box and check box to the above container
				textToAddWidget = new MHTextEditWidget("", pixelWidth: 300, messageWhenEmptyAndNotSelected: "Enter a Design Name Here".Localize())
				{
					HAnchor = HAnchor.Stretch,
					Margin = new BorderDouble(5)
				};
				textToAddWidget.ActualTextEditWidget.EnterPressed += (s, e) =>
				{
					saveAsButton.OnClick(new MouseEventArgs(MouseButtons.Left, 1, 1, 1, -1));
				};
				contentRow.AddChild(textToAddWidget);
			}

			saveAsButton = buttonFactory.Generate("Save".Localize());
			saveAsButton.Name = "Save As Save Button";
			// Disable the save as button until the user actually selects a provider
			saveAsButton.Enabled = false;
			saveAsButton.Cursor = Cursors.Hand;
			saveAsButton.Click += (s, e) =>
			{
				functionToCallOnSaveAs(
					textToAddWidget?.ActualTextEditWidget.Text ?? "none",
					librarySelectorWidget.ActiveContainer);

				this.WizardWindow.CloseOnIdle();
			};

			this.AddPageAction(saveAsButton);
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
	}
}