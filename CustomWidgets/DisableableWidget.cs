using MatterHackers.Agg;
using MatterHackers.Agg.UI;

namespace MatterHackers.MatterControl.CustomWidgets
{
	public class DisableableWidget : GuiWidget
	{
		public GuiWidget disableOverlay;

		public DisableableWidget()
		{
			HAnchor = Agg.UI.HAnchor.ParentLeftRight;
			VAnchor = Agg.UI.VAnchor.FitToChildren;
			this.Margin = new BorderDouble(3);
			disableOverlay = new GuiWidget(HAnchor.ParentLeftRight, VAnchor.ParentBottomTop);
			disableOverlay.Visible = false;
			base.AddChild(disableOverlay);
		}

		public enum EnableLevel { Disabled, ConfigOnly, Enabled };

		public void SetEnableLevel(EnableLevel enabledLevel)
		{
			disableOverlay.BackgroundColor = new RGBA_Bytes(ActiveTheme.Instance.TertiaryBackgroundColor, 160);

			switch (enabledLevel)
			{
				case EnableLevel.Disabled:
					disableOverlay.Margin = new BorderDouble(0);
					disableOverlay.Visible = true;
					break;

				case EnableLevel.ConfigOnly:
					disableOverlay.Margin = new BorderDouble(0, 0, 0, 26);
					disableOverlay.Visible = true;
					break;

				case EnableLevel.Enabled:
					disableOverlay.Visible = false;
					break;
			}
		}
	}
}