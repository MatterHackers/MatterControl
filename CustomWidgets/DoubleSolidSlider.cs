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
	public class DoubleSolidSlideView
	{
		private DoubleSolidSlider sliderAttachedTo;

		public Color BackgroundColor { get; set; }

		public Color FillColor { get; set; }

		public Color TrackColor { get; set; }

		public double TrackHeight { get; set; }

		public TickPlacement TextPlacement { get; set; }

		public Color TextColor { get; set; }

		public StyledTypeFace TextStyle { get; set; }

		public Color ThumbColor { get; set; }

		public TickPlacement TickPlacement { get; set; }

		public Color TickColor { get; set; }

		public DoubleSolidSlideView(DoubleSolidSlider sliderWidget)
		{
			sliderAttachedTo = sliderWidget;

			TrackHeight = 10;

			TextColor = Color.Black;
			TrackColor = new Color(220, 220, 220);
			ThumbColor = ActiveTheme.Instance.PrimaryAccentColor;

			sliderWidget.FirstValueChanged += new EventHandler(sliderWidget_ValueChanged);
			sliderWidget.SecondValueChanged += new EventHandler(sliderWidget_ValueChanged);
		}

		private void sliderWidget_ValueChanged(object sender, EventArgs e)
		{
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
			RectangleDouble thumbBounds = sliderAttachedTo.GetFirstThumbHitBounds();
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

			// draw the first thumb
			RectangleDouble firstThumbBounds = sliderAttachedTo.GetFirstThumbHitBounds();
			RoundedRect firstThumbOutside = new RoundedRect(firstThumbBounds, 0);
			graphics2D.Render(firstThumbOutside, ColorF.GetTweenColor(ThumbColor.ToColorF(), ColorF.Black.ToColorF(), .2).ToColor());

			// draw the second thumb
			RectangleDouble secondThumbBounds = sliderAttachedTo.GetSecondThumbHitBounds();
			RoundedRect secondThumbOutside = new RoundedRect(secondThumbBounds, 0);
			graphics2D.Render(secondThumbOutside, ColorF.GetTweenColor(ThumbColor.ToColorF(), ColorF.Black.ToColorF(), .2).ToColor());
		}
	}

	public class DoubleSolidSlider : GuiWidget
	{
		public event EventHandler FirstValueChanged;

		public event EventHandler SecondValueChanged;

		public event EventHandler FirstSliderReleased;

		public event EventHandler SecondSliderReleased;

		public DoubleSolidSlideView View { get; set; }

		private double mouseDownOffsetFromFirstThumbCenter;
		private double mouseDownOffsetFromSecondThumbCenter;
		private bool downOnFirstThumb = false;
		private bool downOnSecondThumb = false;

		private double firstPosition0To1;
		private double secondPosition0To1;
		private double thumbHeight;
		private int numTicks = 0;

		public double SecondPosition0To1
		{
			get
			{
				return secondPosition0To1;
			}

			set
			{
				secondPosition0To1 = Math.Max(0, Math.Min(value, 1));
			}
		}

		public double FirstPosition0To1
		{
			get
			{
				return firstPosition0To1;
			}

			set
			{
				firstPosition0To1 = Math.Max(0, Math.Min(value, 1));
			}
		}

		public double FirstValue
		{
			get
			{
				return Minimum + (Maximum - Minimum) * FirstPosition0To1;
			}
			set
			{
				double newPosition0To1 = Math.Max(0, Math.Min((value - Minimum) / (Maximum - Minimum), 1));
				if (newPosition0To1 != FirstPosition0To1)
				{
					FirstPosition0To1 = newPosition0To1;
					if (FirstValueChanged != null)
					{
						FirstValueChanged(this, null);
					}
					Invalidate();
				}
			}
		}

		public double SecondValue
		{
			get
			{
				return Minimum + (Maximum - Minimum) * SecondPosition0To1;
			}
			set
			{
				double newPosition0To1 = Math.Max(0, Math.Min((value - Minimum) / (Maximum - Minimum), 1));
				if (newPosition0To1 != SecondPosition0To1)
				{
					SecondPosition0To1 = newPosition0To1;
					if (SecondValueChanged != null)
					{
						SecondValueChanged(this, null);
					}
					Invalidate();
				}
			}
		}

		public double PositionPixelsFromSecondValue
		{
			get
			{
				return ThumbWidth / 2 + TrackWidth * SecondPosition0To1;
			}
			set
			{
				SecondPosition0To1 = (value - ThumbWidth / 2) / TrackWidth;
			}
		}

		public double PositionPixelsFromFirstValue
		{
			get
			{
				return ThumbWidth / 2 + TrackWidth * FirstPosition0To1;
			}
			set
			{
				FirstPosition0To1 = (value - ThumbWidth / 2) / TrackWidth;
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

		public DoubleSolidSlider(Vector2 positionOfTrackFirstValue, double widthInPixels, double minimum = 0, double maximum = 1, Orientation orientation = Orientation.Horizontal)
		{
			View = new DoubleSolidSlideView(this);
			View.TrackHeight = widthInPixels;
			OriginRelativeParent = positionOfTrackFirstValue;
			TotalWidthInPixels = widthInPixels;
			Orientation = orientation;
			Minimum = minimum;
			Maximum = maximum;
			ThumbWidth = widthInPixels;
			ThumbHeight = widthInPixels * 1.4;

			MinimumSize = new Vector2(Width, Height);
		}

		public DoubleSolidSlider(Vector2 lowerLeft, Vector2 upperRight)
			: this(new Vector2(lowerLeft.X, lowerLeft.Y + (upperRight.Y - lowerLeft.Y) / 2), upperRight.X - lowerLeft.X)
		{
		}

		public DoubleSolidSlider(double lowerLeftX, double lowerLeftY, double upperRightX, double upperRightY)
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

		public RectangleDouble GetSecondThumbHitBounds()
		{
			if (Orientation == Orientation.Horizontal)
			{
				return new RectangleDouble(-ThumbWidth / 2 + PositionPixelsFromSecondValue, -ThumbHeight / 2,
					ThumbWidth / 2 + PositionPixelsFromSecondValue, ThumbHeight / 2);
			}
			else
			{
				return new RectangleDouble(-ThumbHeight / 2, -ThumbWidth / 2 + PositionPixelsFromSecondValue,
					ThumbHeight / 2, ThumbWidth / 2 + PositionPixelsFromSecondValue);
			}
		}

		public RectangleDouble GetFirstThumbHitBounds()
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

		public double GetPosition0To1FromFirstValue(double value)
		{
			return (value - Minimum) / (Maximum - Minimum);
		}

		public double GetPositionPixelsFromFirstValue(double value)
		{
			return ThumbWidth / 2 + TrackWidth * GetPosition0To1FromFirstValue(value);
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

		private double firstValueOnMouseDown;
		private double secondValueOnMouseDown;

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			firstValueOnMouseDown = FirstValue;
			secondValueOnMouseDown = SecondValue;
			Vector2 mousePos = mouseEvent.Position;
			RectangleDouble firstThumbBounds = GetFirstThumbHitBounds();
			RectangleDouble secondThumbBounds = GetSecondThumbHitBounds();
			if (firstThumbBounds.Contains(mousePos))
			{
				if (Orientation == Orientation.Horizontal)
				{
					mouseDownOffsetFromFirstThumbCenter = mousePos.X - PositionPixelsFromFirstValue;
				}
				else
				{
					mouseDownOffsetFromFirstThumbCenter = mousePos.Y - PositionPixelsFromFirstValue;
				}
				downOnFirstThumb = true;
			}
			else if (secondThumbBounds.Contains(mousePos))
			{
				if (Orientation == Orientation.Horizontal)
				{
					mouseDownOffsetFromSecondThumbCenter = mousePos.X - PositionPixelsFromSecondValue;
				}
				else
				{
					mouseDownOffsetFromSecondThumbCenter = mousePos.Y - PositionPixelsFromSecondValue;
				}
				downOnSecondThumb = true;
			}
			else // let's check if we are on the track
			{
				//Ignore track hits
			}

			base.OnMouseDown(mouseEvent);
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			Vector2 mousePos = mouseEvent.Position;
			if (downOnFirstThumb)
			{
				double oldValue = FirstValue;
				if (Orientation == Orientation.Horizontal)
				{
					PositionPixelsFromFirstValue = Math.Min(mousePos.X - mouseDownOffsetFromFirstThumbCenter, PositionPixelsFromSecondValue - ThumbWidth - 2);
				}
				else
				{
					PositionPixelsFromFirstValue = Math.Min(mousePos.Y - mouseDownOffsetFromFirstThumbCenter, PositionPixelsFromSecondValue - ThumbWidth - 2);
				}
				if (oldValue != FirstValue)
				{
					if (FirstValueChanged != null)
					{
						FirstValueChanged(this, mouseEvent);
					}
					Invalidate();
				}
			}
			else if (downOnSecondThumb)
			{
				double oldValue = SecondValue;
				if (Orientation == Orientation.Horizontal)
				{
					PositionPixelsFromSecondValue = Math.Max(mousePos.X - mouseDownOffsetFromSecondThumbCenter, PositionPixelsFromFirstValue + ThumbWidth + 2);
				}
				else
				{
					PositionPixelsFromSecondValue = Math.Max(mousePos.Y - mouseDownOffsetFromSecondThumbCenter, PositionPixelsFromFirstValue + ThumbWidth + 2);
				}
				if (oldValue != SecondValue)
				{
					if (SecondValueChanged != null)
					{
						SecondValueChanged(this, mouseEvent);
					}
					Invalidate();
				}
			}
			base.OnMouseMove(mouseEvent);
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			downOnFirstThumb = false;
			downOnSecondThumb = false;
			base.OnMouseUp(mouseEvent);
			if (downOnFirstThumb)
			{
				if (firstValueOnMouseDown != FirstValue && FirstSliderReleased != null)
				{
					FirstSliderReleased(this, mouseEvent);
				}
			}
			else if (downOnSecondThumb)
			{
				if (secondValueOnMouseDown != SecondValue && SecondSliderReleased != null)
				{
					SecondSliderReleased(this, mouseEvent);
				}
			}
		}
	}
}