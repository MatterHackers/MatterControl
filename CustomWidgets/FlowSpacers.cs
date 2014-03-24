using MatterHackers.Agg;
using MatterHackers.Agg.UI;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MatterHackers.MatterControl.CustomWidgets
{
    public class HorizontalSpacer : GuiWidget
    {
        public HorizontalSpacer()
        {
            HAnchor = Agg.UI.HAnchor.ParentLeftRight;
        }
    }

    public class VerticalSpacer : GuiWidget
    {
        public VerticalSpacer()
        {
            VAnchor = Agg.UI.VAnchor.ParentBottomTop;
        }
    }
}
