/*
Copyright (c) 2017, John Lewin
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

using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl
{
	public class ConfigurePrinterWidget : FlowLayoutWidget
	{
		public ConfigurePrinterWidget(SliceSettingsWidget sliceSettingsWidget, ThemeConfig theme)
			: base(FlowDirection.TopToBottom)
		{
			int tabIndex = 0;
			var rowItem = sliceSettingsWidget.CreateItemRow(SliceSettingsOrganizer.SettingsData["printer_name"],ref tabIndex, theme);

			var firstChild = rowItem.Children.FirstOrDefault();
			firstChild.HAnchor = HAnchor.Absolute;
			firstChild.Width = 100;
			firstChild.Margin = firstChild.Margin.Clone(right: 0);

			var nextChild = rowItem.Children.Skip(1).FirstOrDefault();
			nextChild.HAnchor = HAnchor.Stretch;
			nextChild.Children.FirstOrDefault().HAnchor = HAnchor.Stretch;

			this.AddChild(rowItem);

			var primaryTabControl = new SimpleTabs(new GuiWidget())
			{
				Margin = new BorderDouble(top: 8),
				VAnchor = VAnchor.Stretch,
				HAnchor = HAnchor.Stretch,
				MinimumSize = new Vector2(200, 200)
			};
			primaryTabControl.TabBar.BackgroundColor = theme.ActiveTabBarBackground;
			this.AddChild(primaryTabControl);

			foreach (var section in SliceSettingsOrganizer.Instance.UserLevels["Printer"].Categories)
			{
				var scrollable = new ScrollableWidget(true)
				{
					VAnchor = VAnchor.Stretch,
					HAnchor = HAnchor.Stretch,
				};
				scrollable.ScrollArea.HAnchor = HAnchor.Stretch;
				scrollable.AddChild(
					sliceSettingsWidget.CreateGroupContent(
						section.Groups.FirstOrDefault(), 
						sliceSettingsWidget.settingsContext, 
						sliceSettingsWidget.ShowHelpControls,
						ActiveTheme.Instance.PrimaryTextColor, scrollable.ScrollArea));

				primaryTabControl.AddTab(
					new ToolTab(
						section.Name.Localize(),
						primaryTabControl,
						scrollable,
						theme,
						hasClose: false,
						pointSize: theme.DefaultFontSize)
					{
						Name = section.Name + " Tab",
						InactiveTabColor = Color.Transparent,
						ActiveTabColor = theme.ActiveTabColor
					});
			}

			primaryTabControl.SelectedTabIndex = 0;

		}
	}
}
