/*
Copyright (c) 2018, Kevin Pope, John Lewin
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
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ConfigurationPage;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl
{
	public class WidescreenPanel : FlowLayoutWidget
	{
		private ThemeConfig theme;

		public WidescreenPanel(ThemeConfig theme)
		{
			this.theme = theme;
		}

		public override void Initialize()
		{
			base.Initialize();

			this.AnchorAll();
			this.Name = "WidescreenPanel";
			this.BackgroundColor = theme.ActiveTabColor;

			// Push TouchScreenMode into GuiWidget
			GuiWidget.TouchScreenMode = UserSettings.Instance.IsTouchScreen;

			// put in the right column
			var partPreviewContent = new PartPreviewContent(theme)
			{
				VAnchor = VAnchor.Bottom | VAnchor.Top,
				HAnchor = HAnchor.Left | HAnchor.Right
			};

			this.AddChild(partPreviewContent);
		}
	}

	public class BrandMenuButton : PopupMenuButton
	{
		public BrandMenuButton(ThemeConfig theme)
			: base (theme)
		{
			this.Name = "MatterControl BrandMenuButton";
			this.VAnchor = VAnchor.Fit;
			this.HAnchor = HAnchor.Fit;
			this.Margin = 0;

			this.DynamicPopupContent = () => BrandMenuButton.CreatePopupMenu();

			var row = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Fit,
				VAnchor = VAnchor.Fit,
			};
			this.AddChild(row);

			row.AddChild(new IconButton(AggContext.StaticData.LoadIcon("mh-app-logo.png", theme.InvertIcons), theme)
			{
				VAnchor = VAnchor.Center,
				Margin = theme.ButtonSpacing,
				Selectable = false
			});

			row.AddChild(new TextWidget(ApplicationController.Instance.ShortProductName, textColor: theme.Colors.PrimaryTextColor)
			{
				VAnchor = VAnchor.Center
			});

			foreach (var child in this.Children)
			{
				child.Selectable = false;
			}
		}

		private static PopupMenu CreatePopupMenu()
		{
			var menuTheme = ApplicationController.Instance.MenuTheme;

			var popupMenu = new PopupMenu(menuTheme)
			{
				MinimumSize = new Vector2(300, 0)
			};

			var linkIcon = AggContext.StaticData.LoadIcon("fa-link_16.png", 16, 16, menuTheme.InvertIcons);

			PopupMenu.MenuItem menuItem;

			menuItem = popupMenu.CreateMenuItem("Help".Localize(), AggContext.StaticData.LoadIcon("help_page.png", 16, 16, menuTheme.InvertIcons));
			menuItem.Click += (s, e) => ApplicationController.Instance.ShowApplicationHelp();

			popupMenu.CreateHorizontalLine();

			menuItem = popupMenu.CreateMenuItem("Check For Update".Localize(), AggContext.StaticData.LoadIcon("update.png", 16, 16, menuTheme.InvertIcons));
			menuItem.Click += (s, e) => UiThread.RunOnIdle(() =>
			{
				UpdateControlData.Instance.CheckForUpdate();
				DialogWindow.Show<CheckForUpdatesPage>();
			});

			popupMenu.CreateHorizontalLine();

			menuItem = popupMenu.CreateMenuItem("Settings".Localize(), AggContext.StaticData.LoadIcon("fa-cog_16.png", 16, 16, menuTheme.InvertIcons));
			menuItem.Click += (s, e) => DialogWindow.Show<ApplicationSettingsPage>();
			menuItem.Name = "Settings MenuItem";

			popupMenu.CreateHorizontalLine();

			ImageBuffer indicatorIcon = null;

			if (IntPtr.Size == 8)
			{
				indicatorIcon = AggContext.StaticData.LoadIcon("x64.png", 16, 16, menuTheme.InvertIcons);
			}

			popupMenu.CreateSubMenu("Community".Localize(), menuTheme, (modifyMenu) =>
			{
				menuItem = modifyMenu.CreateMenuItem("Forums".Localize(), linkIcon);
				menuItem.Click += (s, e) => ApplicationController.Instance.LaunchBrowser("https://forums.matterhackers.com/category/20/mattercontrol");

				menuItem = modifyMenu.CreateMenuItem("Guides and Articles".Localize(), linkIcon);
				menuItem.Click += (s, e) => ApplicationController.Instance.LaunchBrowser("http://www.matterhackers.com/topic/mattercontrol");

				menuItem = modifyMenu.CreateMenuItem("Support".Localize(), linkIcon);
				menuItem.Click += (s, e) => ApplicationController.Instance.LaunchBrowser("http://https://www.matterhackers.com/mattercontrol/support");

				menuItem = modifyMenu.CreateMenuItem("Release Notes".Localize(), linkIcon);
				menuItem.Click += (s, e) => ApplicationController.Instance.LaunchBrowser("http://wiki.mattercontrol.com/Release_Notes");

				modifyMenu.CreateHorizontalLine();

				menuItem = modifyMenu.CreateMenuItem("Report a Bug".Localize(), AggContext.StaticData.LoadIcon("feedback.png", 16, 16, menuTheme.InvertIcons));
				menuItem.Click += (s, e) => ApplicationController.Instance.LaunchBrowser("https://github.com/MatterHackers/MatterControl/issues");
			}, AggContext.StaticData.LoadIcon("feedback.png", 16, 16, menuTheme.InvertIcons));

			popupMenu.CreateHorizontalLine();

			var themeRow = new GuiWidget()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
			};

			themeRow.AddChild(new TextWidget("Theme".Localize(), pointSize: menuTheme.DefaultFontSize, textColor: menuTheme.Colors.PrimaryTextColor)
			{
				VAnchor = VAnchor.Center,
			});

			themeRow.AddChild(new ThemeColorPanel.AccentColorsWidget(AppContext.ThemeSet, 16, 2)
			{
				HAnchor = HAnchor.Right
			});

			menuItem = popupMenu.CreateMenuItem(themeRow, "Theme", AggContext.StaticData.LoadIcon("theme.png", 16, 16, menuTheme.InvertIcons));
			menuItem.Padding = menuItem.Padding.Clone(right: 5);

			popupMenu.CreateHorizontalLine();

			var imageBuffer = new ImageBuffer(18, 18);

			// x64 indicator icon
			if (IntPtr.Size == 8)
			{
				var graphics = imageBuffer.NewGraphics2D();
				graphics.Clear(menuTheme.ActiveTabColor);
				graphics.Rectangle(imageBuffer.GetBoundingRect(), menuTheme.PrimaryAccentColor);
				graphics.DrawString("64", imageBuffer.Width / 2, imageBuffer.Height / 2, 8, Agg.Font.Justification.Center, Agg.Font.Baseline.BoundsCenter, color: menuTheme.PrimaryAccentColor);
			}

			menuItem = popupMenu.CreateMenuItem("About".Localize() + " MatterControl", imageBuffer);
			menuItem.Click += (s, e) => ApplicationController.Instance.ShowAboutPage();
			return popupMenu;
		}
	}
}
