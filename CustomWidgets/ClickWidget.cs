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
		private event EventHandler PrivateClick;

		private int borderWidth = 0;
		private RGBA_Bytes borderColor = RGBA_Bytes.Black;

		public ClickWidget()
			: base()
		{
		}

		public ClickWidget(double width, double height)
			: base(width, height)
		{
		}

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

		private List<EventHandler> ClickEventDelegates = new List<EventHandler>();

		public event EventHandler Click
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
			foreach (EventHandler eh in ClickEventDelegates)
			{
				PrivateClick -= eh;
			}
			ClickEventDelegates.Clear();
		}

		public void ClickButton(MouseEventArgs mouseEvent)
		{
			if (PrivateClick != null)
			{
				UiThread.RunOnIdle(() => PrivateClick(this, mouseEvent));
			}
		}

		public bool GetChildClicks = false;

		override public void OnMouseUp(MouseEventArgs mouseEvent)
		{
			if (PositionWithinLocalBounds(mouseEvent.X, mouseEvent.Y))
			{
				if (GetChildClicks || this.MouseCaptured == true)
				{
					ClickButton(mouseEvent);
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