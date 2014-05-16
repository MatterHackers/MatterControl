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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.IO;
using System.Diagnostics;
using System.Threading;

using MatterHackers.Agg.Image;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;
using MatterHackers.MatterControl.DataStorage;

namespace MatterHackers.MatterControl
{
	public class ThemeColorSelectorWidget : FlowLayoutWidget
	{
		GuiWidget colorToChangeTo;

		public ThemeColorSelectorWidget (GuiWidget colorToChangeTo)
		{	

			this.colorToChangeTo = colorToChangeTo;
			//TextWidget colorText = new TextWidget("Accent Color", color: RGBA_Bytes.White);
			//colorText.VAnchor = Agg.UI.VAnchor.ParentCenter;
			//this.AddChild(colorText);
			//Temporary theme changer button

			GuiWidget themeButtons = new GuiWidget(186, 42);

            int themeCount = ActiveTheme.Instance.AvailableThemes.Count;

			themeButtons.BackgroundColor = RGBA_Bytes.White;
			int index = 0;
            for (int x = 0; x < themeCount/2; x++)
			{
                Button buttonOne = getThemeButton(index, x, 0);
                Button buttonTwo = getThemeButton(index + themeCount/2, x, 1);

                themeButtons.AddChild(buttonOne);
                themeButtons.AddChild(buttonTwo);
                index++;
			}

			this.AddChild (themeButtons);
			this.VAnchor = VAnchor.ParentCenter;
		}

        public Button getThemeButton(int index, int x, int y)
        {
            GuiWidget normal = new GuiWidget(16, 16);
			normal.BackgroundColor = ActiveTheme.Instance.AvailableThemes[index].primaryAccentColor;
			GuiWidget hover = new GuiWidget(16, 16);
			hover.BackgroundColor = ActiveTheme.Instance.AvailableThemes[index].secondaryAccentColor;
			GuiWidget pressed = new GuiWidget(16, 16);
			pressed.BackgroundColor = ActiveTheme.Instance.AvailableThemes[index].secondaryAccentColor;
			GuiWidget disabled = new GuiWidget(16, 16);
			new GuiWidget(16, 16);

            Button colorButton = new Button(4 + x * 18, 4 + y * 18, new ButtonViewStates(normal, hover, pressed, disabled));
			colorButton.Name = index.ToString();
			colorButton.Click += (sender, mouseEvent) => 
			{                                
				UserSettings.Instance.set("ActiveThemeIndex",((GuiWidget)sender).Name);
				ActiveTheme.Instance.LoadThemeSettings(int.Parse(((GuiWidget)sender).Name));
			};

			colorButton.MouseEnterBounds += (sender, mouseEvent) =>
			{

				colorToChangeTo.BackgroundColor = ActiveTheme.Instance.AvailableThemes[index].primaryAccentColor;

			};

			colorButton.MouseLeaveBounds += (sender, mouseEvent) => 
			{
				colorToChangeTo.BackgroundColor = ActiveTheme.Instance.PrimaryAccentColor;
			};

            return colorButton;
        }

        public override void OnDraw(Graphics2D graphics2D)
        {
            base.OnDraw(graphics2D);
            RectangleDouble border = LocalBounds;
            border.Deflate(new BorderDouble(1));
            //graphics2D.Rectangle(border, ActiveTheme.Instance.SecondaryBackgroundColor, 4);
        }
	}
}

