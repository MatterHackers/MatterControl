using System;
using System.Collections.Generic;
using System.Linq;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;

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
				normalFillColor = this.Options.NormalFillColor,
				hoverFillColor = this.Options.HoverFillColor,
				pressedFillColor = this.Options.PressedFillColor,
				normalBorderColor = this.Options.NormalBorderColor,
				hoverBorderColor = this.Options.HoverBorderColor,
				pressedBorderColor = this.Options.PressedBorderColor,
				disabledBorderColor = this.Options.DisabledBorderColor,
				normalTextColor = this.Options.NormalTextColor,
				hoverTextColor = this.Options.HoverTextColor,
				pressedTextColor = this.Options.PressedTextColor,
				disabledTextColor = this.Options.DisabledTextColor,
				FixedWidth = 20,
			};

			DropDownMenu menu = menuFactory.Generate(actions: actions.Skip(1).ToList(), direction: direction);
			menu.Height = FixedHeight;
			menu.BorderColor = this.Options.NormalBorderColor;
			menu.HoverArrowColor = this.Options.HoverTextColor;
			menu.NormalArrowColor = this.Options.NormalTextColor;
			menu.BackgroundColor = this.Options.NormalFillColor;

			// TODO: Why?
			if (actions.Count > 1)
			{
				menu.Name = actions[1].Title + " Menu";
			}

			var primaryAction = actions[0];

			var buttonFactory = ApplicationController.Instance.Theme.SmallMarginButtonFactory;

			Button button = buttonFactory.Generate(primaryAction.Title, AggContext.StaticData.LoadIcon(imageName, 24, 24));
			button.Name = $"{primaryAction.Title} Button";
			button.Click += (s, e) =>
			{
				primaryAction.Action();
			};

			return new SplitButton(button, menu);
		}
	}
}