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
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.Library;

namespace MatterHackers.MatterControl
{
	public class LibraryBrowserPage : DialogPage
	{
		protected Button acceptButton = null;
		protected MHTextEditWidget itemNameWidget;
		private ILibraryContext libraryNavContext;

		public LibraryBrowserPage(Action<string, ILibraryContainer> acceptCallback, string acceptButtonText)
		{
			FolderBreadCrumbWidget breadCrumbWidget = null;
			var buttonFactory = ApplicationController.Instance.Theme.ButtonFactory;

			this.WindowSize = new VectorMath.Vector2(480, 500);

			contentRow.Padding = 0;
			ListView librarySelectorWidget;
		
			// Create a new library context for the SaveAs view
			libraryNavContext = new LibraryConfig()
			{
				ActiveContainer = ApplicationController.Instance.Library.RootLibaryContainer
			};
			libraryNavContext.ContainerChanged += (s, e) =>
			{
				acceptButton.Enabled = libraryNavContext.ActiveContainer is ILibraryWritableContainer;
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
			var selectorPanel = new GuiWidget(10, 30)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
				BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor,
			};
			selectorPanel.AddChild(librarySelectorWidget);
			contentRow.AddChild(selectorPanel);

			acceptButton = buttonFactory.Generate(acceptButtonText);
			acceptButton.Name = "Accept Button";
			// Disable the save as button until the user actually selects a provider
			acceptButton.Enabled = false;
			acceptButton.Cursor = Cursors.Hand;
			acceptButton.Click += (s, e) =>
			{
				acceptCallback(
					itemNameWidget?.ActualTextEditWidget.Text ?? "none",
					librarySelectorWidget.ActiveContainer);

				this.WizardWindow.CloseOnIdle();
			};

			this.AddPageAction(acceptButton);
		}

		public override void OnLoad(EventArgs args)
		{
			if (itemNameWidget != null
				&& !UserSettings.Instance.IsTouchScreen)
			{
				UiThread.RunOnIdle(itemNameWidget.Focus);
			}
			base.OnLoad(args);
		}
	}
}