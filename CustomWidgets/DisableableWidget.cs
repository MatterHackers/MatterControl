using MatterHackers.Agg;
using MatterHackers.Agg.UI;

namespace MatterHackers.MatterControl.CustomWidgets
{
	public class DisableableWidget : GuiWidget
	{
		public GuiWidget disableOverlay;

		public DisableableWidget()
		{
			HAnchor = HAnchor.ParentLeftRight;
			VAnchor = VAnchor.FitToChildren;
			this.Margin = new BorderDouble(3);
			disableOverlay = new GuiWidget(0, 0);

			this.BoundsChanged += (s, e) =>
			{
				if (Parent != null
				&& Parent.Visible && Parent.Width > 0
				&& Parent.Height > 0
				&& Parent.Children.Count > 1)
				{

					var childBounds = GetChildrenBoundsIncludingMargins(considerChild: (parent, child) =>
				{
					if (child == disableOverlay)
					{
						return false;
					}

					return true;
				});

					disableOverlay.LocalBounds = new RectangleDouble(childBounds.Left,
						childBounds.Bottom,
						childBounds.Right,
						childBounds.Top - disableOverlay.Margin.Top);
				}
			};

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