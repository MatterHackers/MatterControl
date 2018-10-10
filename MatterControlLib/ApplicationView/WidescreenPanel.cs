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
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.ConfigurationPage;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.PrintLibrary;
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
			this.BackgroundColor = theme.Colors.PrimaryBackgroundColor;

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

	public class BrandMenuButton : PopupButton
	{
		public BrandMenuButton(ThemeConfig theme)
		{
			this.Name = "MatterControl BrandMenuButton";
			this.VAnchor = VAnchor.Fit;
			this.HAnchor = HAnchor.Fit;
			this.Margin = 0;
			this.PopupContent = new ApplicationSettingsWidget(ApplicationController.Instance.MenuTheme)
			{
				HAnchor = HAnchor.Absolute,
				VAnchor = VAnchor.Fit,
				Width = 500,
			};

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
	}
}
