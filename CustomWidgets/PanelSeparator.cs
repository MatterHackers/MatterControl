using System;
using System.IO;
using MatterHackers.Agg;
using MatterHackers.Agg.PlatfromAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.DataStorage;

namespace MatterHackers.MatterControl.CustomWidgets
{
    class PanelSeparator : ClickWidget
    {
        RGBA_Bytes defaultBackgroundColor;
        RGBA_Bytes hoverBackgroundColor;
        bool hidden;
        ImageWidget arrowIndicator;

        public bool Hidden 
        { 
            get { return hidden; }
            set { hidden = value; }
        }

        public void SetDisplayState(object state = null)
        {
            if (hidden)
            {
                this.Width = 24;
                arrowIndicator.Visible = true;
            }
            else
            {
                this.Width = 4;
                arrowIndicator.Visible = false;
            }
        }
        
        public PanelSeparator()
            : base(4, 1)
        {
            AddHandlers();

            defaultBackgroundColor = new RGBA_Bytes(200, 200, 200);
            hoverBackgroundColor = new RGBA_Bytes(100, 100, 100);
            
            Agg.Image.ImageBuffer arrowImage = new Agg.Image.ImageBuffer();
            ImageIO.LoadImageData(Path.Combine(ApplicationDataStorage.Instance.ApplicationStaticDataPath, "Icons", "icon_arrow_left_16x16.png"), arrowImage);
            arrowIndicator = new ImageWidget(arrowImage);
            arrowIndicator.HAnchor = Agg.UI.HAnchor.ParentCenter;
            arrowIndicator.VAnchor = Agg.UI.VAnchor.ParentCenter;
            arrowIndicator.Visible = true;

            this.AddChild(arrowIndicator);

            this.Hidden = false;
            this.BackgroundColor = defaultBackgroundColor;
            this.VAnchor = VAnchor.ParentBottomTop;
            this.Margin = new BorderDouble(8, 0);
            this.Cursor = Cursors.Hand;

            SetDisplayState();
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
