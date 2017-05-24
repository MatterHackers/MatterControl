using MatterHackers.Agg;
using MatterHackers.Agg.UI;

namespace MatterHackers.MatterControl.ConfigurationPage
{
	public class SettingsViewBase : AltGroupBox
	{
		protected readonly int TallButtonHeight = (int)(25 * GuiWidget.DeviceScale + .5);
		protected TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();
		protected LinkButtonFactory linkButtonFactory = new LinkButtonFactory();
		protected FlowLayoutWidget mainContainer;

		public SettingsViewBase(string title)
			: base(new TextWidget(title, pointSize: 18, textColor: ActiveTheme.Instance.SecondaryAccentColor))
		{
			SetDisplayAttributes();
			mainContainer = new FlowLayoutWidget(Agg.UI.FlowDirection.TopToBottom);
			mainContainer.HAnchor = HAnchor.ParentLeftRight;
			mainContainer.Margin = new BorderDouble(left: 6);
		}

		private void SetDisplayAttributes()
		{
			this.Margin = new BorderDouble(2, 4, 2, 0);

			// colors
			this.textImageButtonFactory.normalFillColor = RGBA_Bytes.Transparent;
			this.textImageButtonFactory.normalBorderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 200);
			this.textImageButtonFactory.normalTextColor = ActiveTheme.Instance.SecondaryTextColor;

			this.textImageButtonFactory.pressedTextColor = ActiveTheme.Instance.PrimaryTextColor;

			this.textImageButtonFactory.hoverTextColor = ActiveTheme.Instance.PrimaryTextColor;
			this.textImageButtonFactory.hoverBorderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 200);

			this.textImageButtonFactory.disabledFillColor = RGBA_Bytes.Transparent;
			this.textImageButtonFactory.disabledBorderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 100);
			this.textImageButtonFactory.disabledTextColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 100);

			// other settings
			this.textImageButtonFactory.FixedHeight = TallButtonHeight;
			this.textImageButtonFactory.fontSize = 11;
			this.textImageButtonFactory.borderWidth = 1;

			this.linkButtonFactory.fontSize = 11;
		}
	}
}