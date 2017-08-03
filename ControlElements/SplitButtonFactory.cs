using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using System;
using System.Collections.Generic;
using System.Linq;

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

		public double FixedHeight = 30 * GuiWidget.DeviceScale;

		public SplitButton Generate(List<NamedAction> actions, Direction direction = Direction.Down, string imageName = null)
		{
			var menuFactory = new DropDownMenuFactory()
			{
				normalFillColor = this.normalFillColor,
				hoverFillColor = this.hoverFillColor,
				pressedFillColor = this.pressedFillColor,
				normalBorderColor = this.normalBorderColor,
				hoverBorderColor = this.hoverBorderColor,
				pressedBorderColor = this.pressedBorderColor,
				disabledBorderColor = this.disabledBorderColor,
				normalTextColor = this.normalTextColor,
				hoverTextColor = this.hoverTextColor,
				pressedTextColor = this.pressedTextColor,
				disabledTextColor = this.disabledTextColor,
				FixedWidth = 20,
			};

			DropDownMenu menu = menuFactory.Generate(actions: actions.Skip(1).ToList(), direction: direction);
			menu.Height = FixedHeight;
			menu.BorderColor = normalBorderColor;
			menu.HoverArrowColor = this.hoverTextColor;
			menu.NormalArrowColor = this.normalTextColor;
			menu.BackgroundColor = normalFillColor;
			menu.Margin = new BorderDouble();

			// TODO: Why?
			if (actions.Count > 1)
			{
				menu.Name = actions[1].Title + " Menu";
			}

			var primaryAction = actions[0];

			var buttonFactory = new TextImageButtonFactory(new ButtonFactoryOptions()
			{
				FixedHeight = this.FixedHeight,

				Normal = new ButtonOptionSection()
				{

					FillColor = this.normalFillColor,
					TextColor = this.normalTextColor,
					BorderColor = this.normalBorderColor,
				},
				Hover = new ButtonOptionSection()
				{
					TextColor = this.hoverTextColor,
					FillColor = this.hoverFillColor,
					BorderColor = this.hoverBorderColor
				},

				BorderWidth = 1,
			});

			Button button = buttonFactory.Generate(primaryAction.Title, normalImageName: imageName, centerText: true);
			button.Name = $"{primaryAction.Title} Button";
			button.Click += (s, e) =>
			{
				primaryAction.Action();
			};

			return new SplitButton(button, menu);
		}
	}
}