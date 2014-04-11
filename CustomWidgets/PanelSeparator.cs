using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MatterHackers.Agg;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.Font;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.CustomWidgets
{
    class PanelSeparator : ClickWidget
    {
        RGBA_Bytes defaultBackgroundColor;
        RGBA_Bytes hoverBackgroundColor;

        public bool Hidden { get; set; }
        
        public PanelSeparator()
            : base(4, 1)
        {
            AddHandlers();

            defaultBackgroundColor = new RGBA_Bytes(200, 200, 200);
            hoverBackgroundColor = new RGBA_Bytes(100, 100, 100);

            this.Hidden = false;
            this.BackgroundColor = defaultBackgroundColor;
            this.VAnchor = VAnchor.ParentBottomTop;
            this.Margin = new BorderDouble(8, 0);
            this.Cursor = Cursors.Hand;
        }

        void AddHandlers()
        {
            this.MouseEnterBounds += new EventHandler(PanelSeparator_MouseEnterBounds);
            this.MouseLeaveBounds += new EventHandler(PanelSeparator_MouseLeaveBounds);
        }

        void PanelSeparator_MouseLeaveBounds(object sender, EventArgs e)
        {
            this.BackgroundColor = defaultBackgroundColor;
        }

        void PanelSeparator_MouseEnterBounds(object sender, EventArgs e)
        {
            this.BackgroundColor = hoverBackgroundColor;
        }
    }
}
