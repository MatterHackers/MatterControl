using System;
using System.IO;
using MatterHackers.Agg.Image;
using MatterHackers.Agg;
using MatterHackers.VectorMath;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.DataStorage;

namespace MatterHackers.MatterControl.CustomWidgets
{
    class PanelSeparator : ClickWidget
    {
        RGBA_Bytes defaultBackgroundColor;
        RGBA_Bytes hoverBackgroundColor;
        bool pushedRight;
        ImageBuffer rightArrowIndicator;
        ImageBuffer leftArrowIndicator;

        public bool PushedRight 
        { 
            get { return pushedRight; }
            set 
            {
                if (pushedRight != value)
                {
                    pushedRight = value;
                }
            }
        }

        public override void OnDraw(Graphics2D graphics2D)
        {
            BackBuffer.SetRecieveBlender(new BlenderBGRA());
            RectangleDouble bounds = LocalBounds;
            TypeFacePrinter printer;
            Vector2 center = new Vector2((bounds.Right - bounds.Left) / 2, (bounds.Top - bounds.Bottom) / 2);
            int textMargin = 20;
            if (pushedRight)
            {
                printer = new TypeFacePrinter("Advanced Controls", justification: Justification.Center, baseline: Baseline.BoundsCenter);
                Vector2 textSize = printer.GetSize();
                graphics2D.Render(leftArrowIndicator, new Vector2(center.x - leftArrowIndicator.Width / 2, center.y - leftArrowIndicator.Height / 2 - (textSize.x / 2 + textMargin)));
                graphics2D.Render(leftArrowIndicator, new Vector2(center.x - leftArrowIndicator.Width / 2, center.y - leftArrowIndicator.Height / 2 + (textSize.x / 2 + textMargin)));
            }
            else
            {
                printer = new TypeFacePrinter("Part View", justification: Justification.Center, baseline: Baseline.BoundsCenter);
                Vector2 textSize = printer.GetSize();
                graphics2D.Render(rightArrowIndicator, new Vector2(center.x - rightArrowIndicator.Width / 2, center.y - rightArrowIndicator.Height / 2 - (textSize.x / 2 + textMargin)));
                graphics2D.Render(rightArrowIndicator, new Vector2(center.x - rightArrowIndicator.Width / 2, center.y - rightArrowIndicator.Height / 2 + (textSize.x / 2 + textMargin)));
            }
            VertexSourceApplyTransform rotated = new VertexSourceApplyTransform(printer, Affine.NewRotation(MathHelper.Tau / 4));
            VertexSourceApplyTransform positioned = new VertexSourceApplyTransform(rotated, Affine.NewTranslation(center.x, center.y));
            graphics2D.Render(positioned, RGBA_Bytes.White);

            base.OnDraw(graphics2D);
        }
        
        public PanelSeparator()
            : base(24, 1)
        {
            AddHandlers();

            hoverBackgroundColor = new RGBA_Bytes(100, 100, 100);
            defaultBackgroundColor = new RGBA_Bytes(160, 160, 160);

            rightArrowIndicator = StaticData.Instance.LoadIcon("icon_arrow_right_16x16.png");
            leftArrowIndicator = StaticData.Instance.LoadIcon("icon_arrow_left_16x16.png");

            this.PushedRight = false;
            this.BackgroundColor = defaultBackgroundColor;
            this.VAnchor = VAnchor.ParentBottomTop;
            this.Margin = new BorderDouble(8, 0);
            this.Cursor = Cursors.Hand;

            DoubleBuffer = true;
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
