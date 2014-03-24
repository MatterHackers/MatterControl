using MatterHackers.Agg;
using MatterHackers.Agg.UI;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MatterHackers.MatterControl.CustomWidgets
{
    public class DisableableWidget : GuiWidget
    {
        public GuiWidget disableOverlay;

        public DisableableWidget()
        {
            HAnchor = Agg.UI.HAnchor.ParentLeftRight;
            VAnchor = Agg.UI.VAnchor.FitToChildren;

            disableOverlay = new GuiWidget(HAnchor.ParentLeftRight, VAnchor.ParentBottomTop);
            disableOverlay.Visible = false;
            base.AddChild(disableOverlay);
        }

        public enum EnableLevel { Disabled, ConfigOnly, Enabled };

        public void SetEnableLevel(EnableLevel enabledLevel)
        {
            disableOverlay.BackgroundColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryBackgroundColor, 160);

            switch (enabledLevel)
            {
                case EnableLevel.Disabled:
                    disableOverlay.Margin = new BorderDouble(0);
                    disableOverlay.Visible = true;
                    break;

                case EnableLevel.ConfigOnly:
                    disableOverlay.Margin = new BorderDouble(10, 10, 10, 15);
                    disableOverlay.Visible = true;
                    break;

                case EnableLevel.Enabled:
                    disableOverlay.Visible = false;
                    break;
            }
        }

        public override void AddChild(GuiWidget childToAdd, int indexInChildrenList = -1)
        {
            if (indexInChildrenList == -1)
            {
                // put it under the disableOverlay
                base.AddChild(childToAdd, Children.Count - 1);
            }
            else
            {
                base.AddChild(childToAdd, indexInChildrenList);
            }
        }
    }
}
