/*
Copyright (c) 2015, Kevin Pope
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

using MatterHackers.Agg.VertexSource;
using MatterHackers.MatterControl;
using System;

namespace MatterHackers.Agg.UI
{
	public class DynamicDropDownMenu : DropDownMenu
	{
		private TupleList<string, Func<bool>> menuItems;

		public DynamicDropDownMenu(GuiWidget buttonView, Direction direction = Direction.Down, double pointSize = 12)
			: base(buttonView, direction, pointSize)
		{
			menuItems = new TupleList<string, Func<bool>>();
			TextColor = RGBA_Bytes.Black;
			NormalArrowColor = RGBA_Bytes.Black;
			HoverArrowColor = RGBA_Bytes.Black;

			BorderWidth = 1;
			BorderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 200);

			this.SelectionChanged += new EventHandler(AltChoices_SelectionChanged);
		}

		public DynamicDropDownMenu(string topMenuText, Direction direction = Direction.Down, double pointSize = 12)
			: base(topMenuText, direction, pointSize)
		{
			menuItems = new TupleList<string, Func<bool>>();

			NormalColor = RGBA_Bytes.White;
			TextColor = RGBA_Bytes.Black;
			NormalArrowColor = RGBA_Bytes.Black;
			HoverArrowColor = RGBA_Bytes.Black;
			HoverColor = new RGBA_Bytes(255, 255, 255, 200);
			BorderWidth = 1;
			BorderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 200);

			this.SelectionChanged += new EventHandler(AltChoices_SelectionChanged);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			base.OnDraw(graphics2D);
		}

		public void addItem(string name, Func<bool> clickFunction)
		{
			this.AddItem(name);
			menuItems.Add(name, clickFunction);
		}

		private void AltChoices_SelectionChanged(object sender, EventArgs e)
		{
			menuItems[((DropDownMenu)sender).SelectedIndex].Item2();
		}
	}
}