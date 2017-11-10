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
using MatterHackers.Agg.UI;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class PopupMenu : FlowLayoutWidget
	{
		public static BorderDouble MenuPadding { get; set; } = new BorderDouble(40, 8, 20, 8);

		public PopupMenu()
			: base(FlowDirection.TopToBottom)
		{

		}

		public MenuItem CreateHorizontalLine()
		{
			var menuItem = new MenuItem(new GuiWidget()
			{
				HAnchor = HAnchor.Stretch,
				Height = 1,
				BackgroundColor = Color.LightGray,
				Margin = new BorderDouble(10, 1),
				VAnchor = VAnchor.Center,
			}, "HorizontalLine");

			this.AddChild(menuItem);

			return menuItem;
		}

		public MenuItem CreateMenuItem(string name, string value = null, double pointSize = 12)
		{
			var menuStatesView = new MenuItemColorStatesView(name)
			{
				NormalBackgroundColor = Color.White,
				OverBackgroundColor = Color.Gray,
				NormalTextColor = Color.Black,
				OverTextColor = Color.Black,
				DisabledTextColor = Color.Gray,
				PointSize = pointSize,
				Padding = MenuPadding,
			};

			var menuItem = new MenuItem(menuStatesView, value ?? name)
			{
				Text = name,
				Name = name + " Menu Item"
			};

			this.AddChild(menuItem);

			return menuItem;
		}

		public MenuItem CreateMenuItem(GuiWidget guiWidget, string name, string value = null)
		{
			guiWidget.Padding = MenuPadding;

			var menuItem = new MenuItem(guiWidget, value ?? name)
			{
				Text = name,
				Name = name + " Menu Item"
			};

			this.AddChild(menuItem);

			return menuItem;
		}
	}
}