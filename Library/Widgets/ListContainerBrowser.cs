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

using MatterHackers.Agg;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.Library;

namespace MatterHackers.MatterControl.PrintLibrary
{
	public class ListContainerBrowser : FlowLayoutWidget, IIgnoredPopupChild
	{
		private FolderBreadCrumbWidget breadCrumbWidget;
		private GuiWidget searchInput;
		private ILibraryContainer searchContainer;

		private ILibraryContext libraryContext;

		public ListContainerBrowser(ListView libraryView, ILibraryContext libraryContext)
			: base(FlowDirection.TopToBottom)
		{
			this.libraryContext = libraryContext;

			var navBar = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.ParentLeftRight
			};

			this.AddChild(navBar);

			breadCrumbWidget = new FolderBreadCrumbWidget(libraryView);
			navBar.AddChild(breadCrumbWidget);

			var icon = StaticData.Instance.LoadIcon("icon_search_24x24.png", 16, 16);

			var buttonFactory = ApplicationController.Instance.Theme.ButtonFactory;
			var initialMargin = buttonFactory.Margin;
			buttonFactory.Options.Margin = new BorderDouble(8, 0);

			var searchPanel = new SearchInputBox()
			{
				Visible = false,
				Margin = new BorderDouble(10, 0, 5, 0)
			};
			searchPanel.searchInput.ActualTextEditWidget.EnterPressed += (s, e) =>
			{
				this.PerformSearch();
			};
			searchPanel.resetButton.Click += (s, e) =>
			{
				breadCrumbWidget.Visible = true;
				searchPanel.Visible = false;

				searchPanel.searchInput.Text = "";

				this.ClearSearch();
			};

			// Store a reference to the input field
			this.searchInput = searchPanel.searchInput;

			navBar.AddChild(searchPanel);

			Button searchButton = buttonFactory.Generate("", icon);
			searchButton.ToolTipText = "Search".Localize();
			searchButton.Name = "Search Library Button";
			searchButton.Margin = 0;
			searchButton.Click += (s, e) =>
			{
				if (searchPanel.Visible)
				{
					PerformSearch();
				}
				else
				{
					searchContainer = libraryContext.ActiveContainer;

					breadCrumbWidget.Visible = false;
					searchPanel.Visible = true;
					searchInput.Focus();
				}
			};
			buttonFactory.Options.Margin = initialMargin;
			navBar.AddChild(searchButton);

			var libraryContainerView = new ListView(libraryContext)
			{
				HAnchor = HAnchor.ParentLeftRight,
				ShowItems = false
			};
			this.AddChild(libraryContainerView);
		}

		private void PerformSearch()
		{
			UiThread.RunOnIdle(() =>
			{
				libraryContext.ActiveContainer.KeywordFilter = searchInput.Text.Trim();
			});
		}

		private void ClearSearch()
		{
			UiThread.RunOnIdle(() =>
			{
				searchContainer.KeywordFilter = "";

				// Restore the original ActiveContainer before search started - some containers may change context
				ApplicationController.Instance.Library.ActiveContainer = searchContainer;

				searchContainer = null;
			});
		}

		private class SearchInputBox : GuiWidget
		{
			internal MHTextEditWidget searchInput;
			internal Button resetButton;

			public SearchInputBox()
			{
				this.VAnchor = VAnchor.ParentCenter | VAnchor.FitToChildren;
				this.HAnchor = HAnchor.ParentLeftRight;

				searchInput = new MHTextEditWidget(messageWhenEmptyAndNotSelected: "Search Library".Localize())
				{
					Name = "Search Library Edit",
					HAnchor = HAnchor.ParentLeftRight,
					VAnchor = VAnchor.ParentCenter
				};
				this.AddChild(searchInput);

				resetButton = ApplicationController.Instance.Theme.CreateSmallResetButton();
				resetButton.HAnchor = HAnchor.ParentRight | HAnchor.FitToChildren;
				resetButton.VAnchor = VAnchor.ParentCenter | VAnchor.FitToChildren;
				resetButton.Name = "Close Search";
				resetButton.ToolTipText = "Clear".Localize();

				this.AddChild(resetButton);
			}
		}

	}
}
