/*
Copyright (c) 2014, Kevin Pope
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using MatterHackers.Agg;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;
using System;

namespace MatterHackers.MatterControl
{
	public class SolidSlideView
	{
		private SolidSlider sliderAttachedTo;

		public RGBA_Bytes BackgroundColor { get; set; }

		public RGBA_Bytes FillColor { get; set; }

		public RGBA_Bytes TrackColor { get; set; }

		public double TrackHeight { get; set; }

		public TickPlacement TextPlacement { get; set; }

		public RGBA_Bytes TextColor { get; set; }

		public StyledTypeFace TextStyle { get; set; }

		public RGBA_Bytes ThumbColor { get; set; }

		public TickPlacement TickPlacement { get; set; }

		public RGBA_Bytes TickColor { get; set; }

		public SolidSlideView(SolidSlider sliderWidget)
		{
			sliderAttachedTo = sliderWidget;

			TrackHeight = 10;

			TextColor = RGBA_Bytes.Black;
			TrackColor = new RGBA_Bytes(220, 220, 220);
			ThumbColor = ActiveTheme.Instance.SecondaryAccentColor;
		}

		private RectangleDouble GetTrackBounds()
		{
			RectangleDouble trackBounds;
			if (sliderAttachedTo.Orientation == Orientation.Horizontal)
			{
				trackBounds = new RectangleDouble(0, -TrackHeight / 2, sliderAttachedTo.TotalWidthInPixels, TrackHeight / 2);
			}
			else
			{
				trackBounds = new RectangleDouble(-TrackHeight / 2, 0, TrackHeight / 2, sliderAttachedTo.TotalWidthInPixels);
			}
			return trackBounds;
		}

		private RectangleDouble GetThumbBounds()
		{
			RectangleDouble thumbBounds = sliderAttachedTo.GetThumbHitBounds();
			return thumbBounds;
		}

		public RectangleDouble GetTotalBounds()
		{
			RectangleDouble totalBounds = GetTrackBounds();
			totalBounds.ExpandToInclude(GetThumbBounds());
			return totalBounds;
		}

		public void DoDrawBeforeChildren(Graphics2D graphics2D)
		{
			// erase to the background color
			graphics2D.FillRectangle(GetTotalBounds(), BackgroundColor);
		}

		public void DoDrawAfterChildren(Graphics2D graphics2D)
		{
			RoundedRect track = new RoundedRect(GetTrackBounds(), 0);
			Vector2 ValuePrintPosition;
			if (sliderAttachedTo.Orientation == Orientation.Horizontal)
			{
				ValuePrintPosition = new Vector2(sliderAttachedTo.TotalWidthInPixels / 2, -TrackHeight - 12);
			}
			else
			{
				ValuePrintPosition = new Vector2(0, -TrackHeight - 12);
			}

			// draw the track
			graphics2D.Render(track, TrackColor);

			// now do the thumb
			RectangleDouble thumbBounds = sliderAttachedTo.GetThumbHitBounds();
			RoundedRect thumbOutside = new RoundedRect(thumbBounds, 0);
			graphics2D.Render(thumbOutside, RGBA_Floats.GetTweenColor(ThumbColor.GetAsRGBA_Floats(), RGBA_Floats.Black.GetAsRGBA_Floats(), .2).GetAsRGBA_Bytes());
		}
	}

	public class SolidSlider : GuiWidget
	{
		public event EventHandler ValueChanged;

		public event EventHandler SliderReleased;

		public SolidSlideView View { get; set; }

		private double mouseDownOffsetFromThumbCenter;
		private bool downOnThumb = false;

		private double position0To1;
		private double thumbHeight;
		private int numTicks = 0;

		public double Position0To1
		{
			get
			{
				return position0To1;
			}

			set
			{
				position0To1 = Math.Max(0, Math.Min(value, 1));
			}
		}

		public double Value
		{
			get
			{
				return Minimum + (Maximum - Minimum) * Position0To1;
			}
			set
			{
				double newPosition0To1 = Minimum;
				if (Maximum - Minimum != 0)
				{
					newPosition0To1 = Math.Max(0, Math.Min((value - Minimum) / (Maximum - Minimum), 1));
				}
				if (newPosition0To1 != Position0To1)
				{
					Position0To1 = newPosition0To1;
					if (ValueChanged != null)
					{
						ValueChanged(this, null);
					}
					Invalidate();
				}
			}
		}

		public double PositionPixelsFromFirstValue
		{
			get
			{
				return ThumbWidth / 2 + TrackWidth * Position0To1;
			}
			set
			{
				Position0To1 = (value - ThumbWidth / 2) / TrackWidth;
			}
		}

		public Orientation Orientation { get; set; }

		public double ThumbWidth { get; set; }

		public double ThumbHeight
		{
			get
			{
				return Math.Max(thumbHeight, ThumbWidth);
			}
			set
			{
				thumbHeight = value;
			}
		}

		public double TotalWidthInPixels { get; set; }

		public double TrackWidth
		{
			get
			{
				return TotalWidthInPixels - ThumbWidth;
			}
		}

		/// <summary>
		/// There will always be 0 or at least two ticks, one at the start and one at the end.
		/// </summary>
		public int NumTicks
		{
			get
			{
				return numTicks;
			}

			set
			{
				numTicks = value;
				if (numTicks == 1)
				{
					numTicks = 2;
				}
			}
		}

		public bool SnapToTicks { get; set; }

		public double Minimum { get; set; }

		public double Maximum { get; set; }

		public bool SmallChange { get; set; }

		public bool LargeChange { get; set; }

		public SolidSlider(Vector2 positionOfTrackFirstValue, double thumbWidth, double minimum = 0, double maximum = 1, Orientation orientation = Orientation.Horizontal)
		{
			View = new SolidSlideView(this);
			View.TrackHeight = thumbWidth;
			OriginRelativeParent = positionOfTrackFirstValue;
			//TotalWidthInPixels = widthInPixels;
			Orientation = orientation;
			Minimum = minimum;
			Maximum = maximum;
			ThumbWidth = thumbWidth;
			ThumbHeight = thumbWidth * 1.4;

			MinimumSize = new Vector2(Width, Height);
		}

		public SolidSlider(Vector2 lowerLeft, Vector2 upperRight)
			: this(new Vector2(lowerLeft.x, lowerLeft.y + (upperRight.y - lowerLeft.y) / 2), upperRight.x - lowerLeft.x)
		{
		}

		public SolidSlider(double lowerLeftX, double lowerLeftY, double upperRightX, double upperRightY)
			: this(new Vector2(lowerLeftX, lowerLeftY + (upperRightY - lowerLeftY) / 2), upperRightX - lowerLeftX)
		{
		}

		public override RectangleDouble LocalBounds
		{
			get
			{
				return View.GetTotalBounds();
			}
			set
			{
				//OriginRelativeParent = new Vector2(value.Left, value.Bottom - View.GetTotalBounds().Bottom);
				//throw new Exception("Figure out what this should do.");
				if (HAnchor == HAnchor.ParentLeftRight)
				{
					TotalWidthInPixels = value.Right - value.Left;
				}
			}
		}

		public void SetRange(double minimum, double maximum)
		{
			Minimum = minimum;
			Maximum = maximum;
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			View.DoDrawBeforeChildren(graphics2D);
			base.OnDraw(graphics2D);
			View.DoDrawAfterChildren(graphics2D);
		}

		public RectangleDouble GetThumbHitBounds()
		{
			if (Orientation == Orientation.Horizontal)
			{
				return new RectangleDouble(-ThumbWidth / 2 + PositionPixelsFromFirstValue, -ThumbHeight / 2,
					ThumbWidth / 2 + PositionPixelsFromFirstValue, ThumbHeight / 2);
			}
			else
			{
				return new RectangleDouble(-ThumbHeight / 2, -ThumbWidth / 2 + PositionPixelsFromFirstValue,
					ThumbHeight / 2, ThumbWidth / 2 + PositionPixelsFromFirstValue);
			}
		}

		public double GetPosition0To1FromValue(double value)
		{
			return (value - Minimum) / (Maximum - Minimum);
		}

		public double GetPositionPixelsFromValue(double value)
		{
			return ThumbWidth / 2 + TrackWidth * GetPosition0To1FromValue(value);
		}

		public RectangleDouble GetTrackHitBounds()
		{
			if (Orientation == Orientation.Horizontal)
			{
				return new RectangleDouble(0, -ThumbHeight / 2,
					TotalWidthInPixels, ThumbHeight / 2);
			}
			else
			{
				return new RectangleDouble(-ThumbHeight / 2, 0, ThumbHeight / 2, TotalWidthInPixels);
			}
		}

		private double valueOnMouseDown;

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			valueOnMouseDown = Value;
			double oldValue = Value;
			Vector2 mousePos = mouseEvent.Position;
			RectangleDouble thumbBounds = GetThumbHitBounds();
			if (thumbBounds.Contains(mousePos))
			{
				if (Orientation == Orientation.Horizontal)
				{
					mouseDownOffsetFromThumbCenter = mousePos.x - PositionPixelsFromFirstValue;
				}
				else
				{
					mouseDownOffsetFromThumbCenter = mousePos.y - PositionPixelsFromFirstValue;
				}
				downOnThumb = true;
			}
			else // let's check if we are on the track
			{
				RectangleDouble trackHitBounds = GetTrackHitBounds();
				if (trackHitBounds.Contains(mousePos))
				{
					if (Orientation == Orientation.Horizontal)
					{
						PositionPixelsFromFirstValue = mousePos.x;
					}
					else
					{
						PositionPixelsFromFirstValue = mousePos.y;
					}
				}
			}

			if (oldValue != Value)
			{
				ValueChanged?.Invoke(this, mouseEvent);
				Invalidate();
			}

			base.OnMouseDown(mouseEvent);
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			Vector2 mousePos = mouseEvent.Position;
			if (downOnThumb)
			{
				double oldValue = Value;
				if (Orientation == Orientation.Horizontal)
				{
					PositionPixelsFromFirstValue = mousePos.x - mouseDownOffsetFromThumbCenter;
				}
				else
				{
					PositionPixelsFromFirstValue = mousePos.y - mouseDownOffsetFromThumbCenter;
				}
				if (oldValue != Value)
				{
					if (ValueChanged != null)
					{
						ValueChanged(this, mouseEvent);
					}
					Invalidate();
				}
			}
			base.OnMouseMove(mouseEvent);
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			downOnThumb = false;
			base.OnMouseUp(mouseEvent);

			if (valueOnMouseDown != Value)
			{
				SliderReleased?.Invoke(this, mouseEvent);
			}
		}
	}
}