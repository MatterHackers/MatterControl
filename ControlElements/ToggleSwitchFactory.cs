using MatterHackers.Agg;
using MatterHackers.Agg.UI;

namespace MatterHackers.MatterControl
{
	public class ToggleSwitchFactory
	{
		public RGBA_Bytes defaultBackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
		public RGBA_Bytes defaultInteriorColor = new RGBA_Bytes(220, 220, 220);
		public RGBA_Bytes defaultThumbColor = ActiveTheme.Instance.PrimaryAccentColor;
		public RGBA_Bytes defaultExteriorColor = ActiveTheme.Instance.PrimaryTextColor;

		public double defaultSwitchHeight = 24;
		public double defaultSwitchWidth = 60;

		public const string defaultTrueText = "On";
		public const string defaultFalseText = "Off";

		public ToggleSwitchFactory()
		{
		}

		public ToggleSwitch Generate(bool value = false)
		{
			ToggleSwitch toggleSwitch = new ToggleSwitch(defaultSwitchWidth, defaultSwitchHeight, value, defaultBackgroundColor, defaultInteriorColor, defaultThumbColor, defaultExteriorColor);
			return toggleSwitch;
		}

		public ToggleSwitch GenerateGivenTextWidget(TextWidget assocTextWidget, string trueText = defaultTrueText, string falseText = defaultFalseText, bool value = false)
		{
			ToggleSwitch toggleSwitch = new ToggleSwitch(assocTextWidget, trueText, falseText, defaultSwitchWidth, defaultSwitchHeight, value, defaultBackgroundColor, defaultInteriorColor, defaultThumbColor, defaultExteriorColor);
			return toggleSwitch;
		}

		public FlowLayoutWidget GenerateToggleSwitchAndTextWidget(string trueText = defaultTrueText, string falseText = defaultFalseText, bool value = false)
		{
			FlowLayoutWidget leftToRight = new FlowLayoutWidget();
			//	leftToRight.Padding = new BorderDouble(3, 0, 0, 5) * TextWidget.GlobalPointSizeScaleRatio;

			TextWidget textWidget = new TextWidget(defaultFalseText, pointSize: 10, textColor: ActiveTheme.Instance.PrimaryTextColor);
			textWidget.VAnchor = VAnchor.ParentCenter;

			ToggleSwitch toggleSwitch = new ToggleSwitch(textWidget, trueText, falseText, defaultSwitchWidth, defaultSwitchHeight, value, defaultBackgroundColor, defaultInteriorColor, defaultThumbColor, defaultExteriorColor);
			toggleSwitch.VAnchor = VAnchor.ParentCenter;
			setToggleSwitchColors(toggleSwitch);

			leftToRight.AddChild(toggleSwitch);
			leftToRight.AddChild(textWidget);

			return leftToRight;
		}

		private void setToggleSwitchColors(ToggleSwitch toggleSwitch)
		{
			toggleSwitch.BackgroundColor = defaultBackgroundColor;
			toggleSwitch.InteriorColor = defaultInteriorColor;
			toggleSwitch.ThumbColor = defaultThumbColor;
			toggleSwitch.ExteriorColor = defaultExteriorColor;
		}
	}
}