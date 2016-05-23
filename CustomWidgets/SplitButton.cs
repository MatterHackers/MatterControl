using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using System;

namespace MatterHackers.MatterControl
{
	public class SplitButton : FlowLayoutWidget
	{
		private Button defaultButton;
		private DynamicDropDownMenu altChoices;

		private Button DefaultButton { get { return defaultButton; } }

		public SplitButton(string buttonText, Direction direction = Direction.Down)
			: base(FlowDirection.LeftToRight, HAnchor.FitToChildren, VAnchor.FitToChildren)
		{
			defaultButton = CreateDefaultButton(buttonText);
			altChoices = CreateDropDown(direction);

			defaultButton.VAnchor = VAnchor.ParentCenter;

			AddChild(defaultButton);
			AddChild(altChoices);
		}

		public SplitButton(Button button, DynamicDropDownMenu menu)
			: base(FlowDirection.LeftToRight, HAnchor.FitToChildren, VAnchor.FitToChildren)
		{
			defaultButton = button;
			altChoices = menu;

			defaultButton.VAnchor = VAnchor.ParentCenter;

			AddChild(defaultButton);
			AddChild(altChoices);
		}

		public void AddItem(string name, Func<bool> clickFunction)
		{
			altChoices.addItem(name, clickFunction);
		}

		private DynamicDropDownMenu CreateDropDown(Direction direction)
		{
			DynamicDropDownMenu menu = new DynamicDropDownMenu("", direction);
			menu.VAnchor = VAnchor.ParentCenter;
			menu.MenuAsWideAsItems = false;
			menu.AlignToRightEdge = true;
			menu.Height = defaultButton.Height;

			return menu;
		}

		private Button CreateDefaultButton(string buttonText)
		{
			TextImageButtonFactory buttonFactory = new TextImageButtonFactory();
			buttonFactory.FixedHeight = 30 * GuiWidget.DeviceScale;
			buttonFactory.normalFillColor = RGBA_Bytes.White;
			buttonFactory.normalTextColor = RGBA_Bytes.Black;
			buttonFactory.hoverTextColor = RGBA_Bytes.Black;
			buttonFactory.hoverFillColor = new RGBA_Bytes(255, 255, 255, 200);
			buttonFactory.borderWidth = 1;
			buttonFactory.normalBorderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 200);
			buttonFactory.hoverBorderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 200);

			return buttonFactory.Generate(buttonText, centerText: true);
		}
	}
}