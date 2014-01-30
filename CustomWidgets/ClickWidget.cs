using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.OpenGlGui;
using MatterHackers.PolygonMesh;
using MatterHackers.RenderOpenGl;
using MatterHackers.VectorMath;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.MatterControl
{
    // A clickable GuiWidget
    public class ClickWidget : GuiWidget
    {
        public delegate void ButtonEventHandler(object sender, MouseEventArgs mouseEvent);
        private event ButtonEventHandler PrivateClick;
        private int borderWidth = 0;
        private RGBA_Bytes borderColor = RGBA_Bytes.Black;

        public int BorderWidth 
        {
            get { return borderWidth; }
            set 
            {
                this.borderWidth = value;
                this.Invalidate(); 
            }
        }

        public RGBA_Bytes BorderColor
        {
            get { return borderColor; }
            set
            {
                this.borderColor = value;
                this.Invalidate();
            }
        }

        List<ButtonEventHandler> ClickEventDelegates = new List<ButtonEventHandler>(); 

        public event ButtonEventHandler Click
        {
            //Wraps the PrivateClick event delegate so that we can track which events have been added and clear them if necessary            
            add
            {
                PrivateClick += value;
                ClickEventDelegates.Add(value);
            }

            remove
            {
                PrivateClick -= value;
                ClickEventDelegates.Remove(value);
            }
        }

        public void UnbindClickEvents()
        {
            //Clears all event handlers from the Click event
            foreach (ButtonEventHandler eh in ClickEventDelegates)
            {
                PrivateClick -= eh;
            }
            ClickEventDelegates.Clear();
        }

        public void ClickButton(MouseEventArgs mouseEvent)
        {
            if (PrivateClick != null)
            {
                PrivateClick(this, mouseEvent);
            }
        }

        override public void OnMouseUp(MouseEventArgs mouseEvent)
        {
            if (PositionWithinLocalBounds(mouseEvent.X, mouseEvent.Y))
            {
                ClickButton(mouseEvent);
            }

            base.OnMouseUp(mouseEvent);
        }

        public override void OnDraw(Graphics2D graphics2D)
        {
            RectangleDouble borderRectangle = LocalBounds;
            RoundedRect rectBorder = new RoundedRect(borderRectangle, 0);

            graphics2D.Render(new Stroke(rectBorder, BorderWidth), BorderColor);
            base.OnDraw(graphics2D);
        }
    }
}
