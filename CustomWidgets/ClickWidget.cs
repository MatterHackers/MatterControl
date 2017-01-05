using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using System;
using System.Collections.Generic;

namespace MatterHackers.MatterControl
{
	// A clickable GuiWidget
	public class ClickWidget : GuiWidget
	{
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

		public event EventHandler Click;

		public bool GetChildClicks = false;

		override public void OnMouseUp(MouseEventArgs mouseEvent)
		{
			if (PositionWithinLocalBounds(mouseEvent.X, mouseEvent.Y))
			{
				if (Click != null && (GetChildClicks || this.MouseCaptured))
				{
					UiThread.RunOnIdle(() => Click(this, mouseEvent));
				}
			}

			base.OnMouseUp(mouseEvent);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			RectangleDouble borderRectangle = LocalBounds;

			if (BorderWidth > 0)
			{
				if (BorderWidth == 1)
				{
					graphics2D.Rectangle(borderRectangle, BorderColor);
				}
				else
				{
					RoundedRect rectBorder = new RoundedRect(borderRectangle, 0);

					graphics2D.Render(new Stroke(rectBorder, BorderWidth), BorderColor);
				}
			}
			base.OnDraw(graphics2D);
		}
	}
}