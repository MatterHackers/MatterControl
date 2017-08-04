using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MatterHackers.MatterControl
{
	public class SplitButtonFactory
	{
		public int fontSize = 12;
		public double borderWidth = 1;
		public bool invertImageLocation = false;
		public bool AllowThemeToAdjustImage = true;

		public double FixedHeight = 30 * GuiWidget.DeviceScale;

		public ButtonFactoryOptions Options { get; set; } = ApplicationController.Instance.Theme.ButtonFactory.Options;

		public SplitButton Generate(List<NamedAction> actions, Direction direction = Direction.Down, string imageName = null)
		{
			var menuFactory = new DropDownMenuFactory()
			{
				normalFillColor = this.Options.Normal.FillColor,
				hoverFillColor = this.Options.Hover.FillColor,
				pressedFillColor = this.Options.Pressed.FillColor,
				normalBorderColor = this.Options.Normal.BorderColor,
				hoverBorderColor = this.Options.Hover.BorderColor,
				pressedBorderColor = this.Options.Pressed.BorderColor,
				disabledBorderColor = this.Options.Disabled.BorderColor,
				normalTextColor = this.Options.Normal.TextColor,
				hoverTextColor = this.Options.Hover.TextColor,
				pressedTextColor = this.Options.Pressed.TextColor,
				disabledTextColor = this.Options.Disabled.TextColor,
				FixedWidth = 20,
			};

			DropDownMenu menu = menuFactory.Generate(actions: actions.Skip(1).ToList(), direction: direction);
			menu.Height = FixedHeight;
			menu.BorderColor = this.Options.Normal.BorderColor;
			menu.HoverArrowColor = this.Options.Hover.TextColor;
			menu.NormalArrowColor = this.Options.Normal.TextColor;
			menu.BackgroundColor = this.Options.Normal.FillColor;
			menu.Margin = new BorderDouble();

			// TODO: Why?
			if (actions.Count > 1)
			{
				menu.Name = actions[1].Title + " Menu";
			}

			var primaryAction = actions[0];

			var buttonFactory = ApplicationController.Instance.Theme.SmallMarginButtonFactory;

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