using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using System;
using System.Collections.Generic;

namespace MatterHackers.MatterControl
{
	public class SplitButtonFactory
	{
		public BorderDouble Margin = new BorderDouble(0, 0);
		public RGBA_Bytes normalFillColor = new RGBA_Bytes(0, 0, 0, 0);
		public RGBA_Bytes hoverFillColor = new RGBA_Bytes(0, 0, 0, 50);
		public RGBA_Bytes pressedFillColor = new RGBA_Bytes(0, 0, 0, 0);
		public RGBA_Bytes disabledFillColor = new RGBA_Bytes(255, 255, 255, 50);

		public RGBA_Bytes normalBorderColor = new RGBA_Bytes(255, 255, 255, 0);
		public RGBA_Bytes hoverBorderColor = new RGBA_Bytes(0, 0, 0, 0);
		public RGBA_Bytes pressedBorderColor = new RGBA_Bytes(0, 0, 0, 0);
		public RGBA_Bytes disabledBorderColor = new RGBA_Bytes(0, 0, 0, 0);
		public RGBA_Bytes checkedBorderColor = new RGBA_Bytes(255, 255, 255, 0);

		public RGBA_Bytes normalTextColor = ActiveTheme.Instance.PrimaryTextColor;
		public RGBA_Bytes hoverTextColor = ActiveTheme.Instance.PrimaryTextColor;
		public RGBA_Bytes pressedTextColor = ActiveTheme.Instance.PrimaryTextColor;
		public RGBA_Bytes disabledTextColor = ActiveTheme.Instance.PrimaryTextColor;

		public int fontSize = 12;
		public double borderWidth = 1;
		public bool invertImageLocation = false;
		public bool AllowThemeToAdjustImage = true;
		private string imageName;

		public double FixedHeight = 30 * GuiWidget.DeviceScale;

		public SplitButtonFactory()
		{
		}

		public SplitButton Generate(List<NamedAction> buttonList, Direction direction = Direction.Down, string imageName = null)
		{
			this.imageName = imageName;

			DynamicDropDownMenu menu = CreateMenu(direction);
			if(buttonList.Count > 1)
			{
				menu.Name = buttonList[1].Title + " Menu";
			}

			menu.Margin = new BorderDouble();
			Button button = CreateButton(buttonList[0]);

			foreach (var namedAction in buttonList)
			{
				menu.addItem(namedAction.Title, namedAction.Action);
			}

			SplitButton splitButton = new SplitButton(button, menu);

			return splitButton;
		}

		private Button CreateButton(NamedAction buttonInfo)
		{
			TextImageButtonFactory buttonFactory = new TextImageButtonFactory();

			buttonFactory.FixedHeight = this.FixedHeight;
			buttonFactory.normalFillColor = this.normalFillColor;
			buttonFactory.normalTextColor = this.normalTextColor;
			buttonFactory.hoverTextColor = this.hoverTextColor;
			buttonFactory.hoverFillColor = this.hoverFillColor;
			buttonFactory.borderWidth = 1;
			buttonFactory.normalBorderColor = this.normalBorderColor;
			buttonFactory.hoverBorderColor = this.hoverBorderColor;
			
			Button button = buttonFactory.Generate(buttonInfo.Title, normalImageName: imageName, centerText: true);
			button.Click += (s, e) =>
			{
				buttonInfo.Action();
			};

			return button;
		}

		private DynamicDropDownMenu CreateMenu(Direction direction = Direction.Down)
		{
			DropDownMenuFactory menuFactory = new DropDownMenuFactory();

			menuFactory.normalFillColor = this.normalFillColor;
			menuFactory.hoverFillColor = this.hoverFillColor;
			menuFactory.pressedFillColor = this.pressedFillColor;
			menuFactory.pressedFillColor = this.pressedFillColor;

			menuFactory.normalBorderColor = this.normalBorderColor;
			menuFactory.hoverBorderColor = this.hoverBorderColor;
			menuFactory.pressedBorderColor = this.pressedBorderColor;
			menuFactory.disabledBorderColor = this.disabledBorderColor;

			menuFactory.normalTextColor = this.normalTextColor;
			menuFactory.hoverTextColor = this.hoverTextColor;
			menuFactory.pressedTextColor = this.pressedTextColor;
			menuFactory.disabledTextColor = this.disabledTextColor;

			DynamicDropDownMenu menu = menuFactory.Generate(direction: direction);

			menu.Height = FixedHeight;
			menu.BorderColor = normalBorderColor;
			menu.HoverArrowColor = this.hoverTextColor;
			menu.NormalArrowColor = this.normalTextColor;
			menu.BackgroundColor = normalFillColor;

			return menu;
		}
	}
}