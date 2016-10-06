/*
Copyright (c) 2014, Kevin Pope
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
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.Agg.VertexSource;
using MatterHackers.MatterControl.PrintLibrary.Provider;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.CustomWidgets.LibrarySelector
{
	public class FolderBreadCrumbWidget : FlowLayoutWidget
	{
		private static TextImageButtonFactory navigationButtonFactory = new TextImageButtonFactory();

		private Action<LibraryProvider> SwitchToLibraryProvider;

		public FolderBreadCrumbWidget(Action<LibraryProvider> SwitchToLibraryProvider, LibraryProvider currentLibraryProvider)
		{
			this.Name = "FolderBreadCrumbWidget";
			this.SwitchToLibraryProvider = SwitchToLibraryProvider;
			UiThread.RunOnIdle(() => SetBreadCrumbs(null, currentLibraryProvider));

			navigationButtonFactory.normalTextColor = ActiveTheme.Instance.PrimaryTextColor;
			navigationButtonFactory.hoverTextColor = ActiveTheme.Instance.PrimaryTextColor;
			navigationButtonFactory.pressedTextColor = ActiveTheme.Instance.PrimaryTextColor;
			navigationButtonFactory.disabledTextColor = ActiveTheme.Instance.PrimaryTextColor;
			navigationButtonFactory.disabledFillColor = navigationButtonFactory.normalFillColor;
			navigationButtonFactory.Margin = new BorderDouble(10, 0);
			navigationButtonFactory.borderWidth = 0;

			HAnchor = HAnchor.ParentLeftRight;
		}

		public void SetBreadCrumbs(LibraryProvider previousLibraryProvider, LibraryProvider currentLibraryProvider)
		{
			LibraryProvider displayingProvider = currentLibraryProvider;

			this.CloseAllChildren();

			List<LibraryProvider> parentProviderList = new List<LibraryProvider>();
			while (currentLibraryProvider != null)
			{
				parentProviderList.Add(currentLibraryProvider);
				currentLibraryProvider = currentLibraryProvider.ParentLibraryProvider;
			}

			bool haveFilterRunning = !string.IsNullOrEmpty(displayingProvider.KeywordFilter);

			bool first = true;
			for (int i = parentProviderList.Count - 1; i >= 0; i--)
			{
				LibraryProvider parentLibraryProvider = parentProviderList[i];
				if (!first)
				{
					GuiWidget separator = new TextWidget(">", textColor: ActiveTheme.Instance.PrimaryTextColor);
					separator.VAnchor = VAnchor.ParentCenter;
					separator.Margin = new BorderDouble(0);
					this.AddChild(separator);
				}

				Button gotoProviderButton = navigationButtonFactory.Generate(parentLibraryProvider.Name);
				gotoProviderButton.Name = "Bread Crumb Button " + parentLibraryProvider.Name;
				if (first)
				{
					gotoProviderButton.Margin = new BorderDouble(0, 0, 3, 0);
				}
				else
				{
					gotoProviderButton.Margin = new BorderDouble(3, 0);
				}
				gotoProviderButton.Click += (sender2, e2) =>
				{
					UiThread.RunOnIdle(() =>
					{
						SwitchToLibraryProvider(parentLibraryProvider);
					});
				};
				this.AddChild(gotoProviderButton);
				first = false;
			}

			if (haveFilterRunning)
			{
				GuiWidget separator = new TextWidget(">", textColor: ActiveTheme.Instance.PrimaryTextColor);
				separator.VAnchor = VAnchor.ParentCenter;
				separator.Margin = new BorderDouble(0);
				this.AddChild(separator);

				Button searchResultsButton = null;
				if (UserSettings.Instance.IsTouchScreen)
				{
					searchResultsButton = navigationButtonFactory.Generate("Search Results".Localize(), "icon_search_32x32.png");
				}
				else
				{
					searchResultsButton = navigationButtonFactory.Generate("Search Results".Localize(), "icon_search_24x24.png");
				}
				searchResultsButton.Name = "Bread Crumb Button " + "Search Results";
				searchResultsButton.Margin = new BorderDouble(3, 0);
				this.AddChild(searchResultsButton);
			}

			// while all the buttons don't fit in the control
			if (this.Parent != null
				&& this.Width > 0
				&& this.Children.Count > 4
				&& this.GetChildrenBoundsIncludingMargins().Width > (this.Width - 20))
			{
				// lets take out the > and put in a ...
				this.RemoveChild(1);
				GuiWidget separator = new TextWidget("...", textColor: ActiveTheme.Instance.PrimaryTextColor);
				separator.VAnchor = VAnchor.ParentCenter;
				separator.Margin = new BorderDouble(3, 0);
				this.AddChild(separator, 1);

				while (this.GetChildrenBoundsIncludingMargins().Width > this.Width - 20
					&& this.Children.Count > 4)
				{
					this.RemoveChild(3);
					this.RemoveChild(2);
				}
			}
		}
	}
}