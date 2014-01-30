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
		public ThemeColorSelectorWidget ()
		{	
			//TextWidget colorText = new TextWidget("Accent Color", color: RGBA_Bytes.White);
			//colorText.VAnchor = Agg.UI.VAnchor.ParentCenter;
			//this.AddChild(colorText);
			//Temporary theme changer button
			GuiWidget themeButtons = new GuiWidget(96, 42);
			themeButtons.BackgroundColor = RGBA_Bytes.White;
			int index = 0;
			for (int x = 0; x < 5; x++)
			{
				for (int y = 0; y < 2; y++)
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
					index++;
					themeButtons.AddChild(colorButton);
				}
			}
			themeButtons.Margin = new BorderDouble(5);
			this.AddChild(themeButtons);
            this.VAnchor = VAnchor.ParentCenter;
		}

        public override void OnDraw(Graphics2D graphics2D)
        {
            base.OnDraw(graphics2D);
            RectangleDouble border = LocalBounds;
            border.Deflate(new BorderDouble(1));
            graphics2D.Rectangle(border, ActiveTheme.Instance.SecondaryBackgroundColor, 4);
        }
	}
}

