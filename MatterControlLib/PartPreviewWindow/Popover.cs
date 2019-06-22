/*
Copyright (c) 2019, Lars Brubaker, John Lewin
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

using System;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class Popover : FlowLayoutWidget
	{
		protected IVertexSource tabShape = null;
		protected Stroke tabStroke;
		private Color _tagColor;
		private BorderDouble originalPadding;

		/// <summary>
		/// Initializes a new instance of the <see cref="Popover"/> class.
		/// </summary>
		/// <param name="arrowDirection">The direction the popover arrow should point</param>
		/// <param name="padding">The padding of the control, adjusted internally to account for arrow region</param>
		/// <param name="notchSize">The size of the arrow notch</param>
		/// <param name="arrowOffset">The arrow offset in x or y given the specified arrow</param>
		public Popover(ArrowDirection arrowDirection, BorderDouble padding, int notchSize, int arrowOffset, bool autoBorderColor = true)
			: base (FlowDirection.TopToBottom)
		{
			this.originalPadding = padding;
			this.NotchSize = notchSize;
			this.ArrowDirection = arrowDirection;
			this.autoBorderColor = autoBorderColor;

			_arrowOffset = arrowOffset;
		}

		public virtual Color TagColor
		{
			get => _tagColor;
			set
			{
				_tagColor = value;

				if (autoBorderColor)
				{
					this.BorderColor = TagColor.AdjustLightness(AppContext.Theme.IsDarkTheme ? 1.3 : .8).ToColor();
				}
			}
		}

		public ArrowDirection ArrowDirection
		{
			get => _arrow;
			set
			{
				_arrow = value;

				this.RebuildShape();

				switch (_arrow)
				{
					case ArrowDirection.Up:
						this.Padding = originalPadding.Clone(top: originalPadding.Top + this.NotchSize);
						break;

					case ArrowDirection.Down:
						this.Padding = originalPadding.Clone(bottom: originalPadding.Bottom + this.NotchSize);
						break;

					case ArrowDirection.Left:
						this.Padding = originalPadding.Clone(left: originalPadding.Left + this.NotchSize);
						break;

					default: // ArrowDirection.Right:
						this.Padding = originalPadding.Clone(right: originalPadding.Right + this.NotchSize);
						break;
				}
			}
		}

		public override void OnBoundsChanged(EventArgs e)
		{
			base.OnBoundsChanged(e);

			this.RebuildShape();
		}

		protected virtual void RebuildShape()
		{
			tabShape = Popover.GetShape(this.ArrowDirection, this.LocalBounds, this.NotchSize, this.ArrowOffset);
			tabStroke = new Stroke(tabShape);
		}

		private int _arrowOffset;

		/// <summary>
		/// Notch offset. See https://photos.app.goo.gl/YdTiehf6ih7fSoDA9 for point diagram
		/// </summary>
		public int ArrowOffset
		{
			get => _arrowOffset;
			set
			{
				if (_arrowOffset != value)
				{
					_arrowOffset = value;
					this.RebuildShape();
				}
			}
		}

		private bool autoBorderColor;
		private ArrowDirection _arrow;

		public int NotchSize { get; }

		protected static IVertexSource GetShape(ArrowDirection arrowDirection, RectangleDouble rect, double notchSize, double p2)
		{
			// See https://photos.app.goo.gl/YdTiehf6ih7fSoDA9 for point diagram

			notchSize += 0.5;

			var tabShape = new VertexStorage();
			var centerY = rect.YCenter;

			// Tab - core
			var radius = 4.0;

			double x0 = rect.Left;
			double x1 = x0 + (arrowDirection == ArrowDirection.Left ? notchSize : 0);
			double x2 = x1 + radius;

			double x5 = rect.Right;
			double x4 = x5 - (arrowDirection == ArrowDirection.Right ? notchSize : 0);
			double x3 = x4 - radius;

			double y0 = rect.Top;
			double y1 = y0 - (arrowDirection == ArrowDirection.Up ? notchSize : 0);
			double y2 = y1 - radius;

			double y5 = rect.Bottom;
			double y4 = y5 + (arrowDirection == ArrowDirection.Down ? notchSize : 0);
			double y3 = y4 + radius;

			int p1, p3;

			switch (arrowDirection)
			{
				case ArrowDirection.Down:
					p2 = x1 + p2;
					p1 = (int)(p2 + notchSize);
					p3 = (int)(p2 - notchSize);
					break;

				case ArrowDirection.Right:
					p2 = y1 - p2;
					p1 = (int)(p2 + notchSize);
					p3 = (int)(p2 - notchSize);
					break;

				case ArrowDirection.Up:
					p2 = x1 + p2;
					p1 = (int)(p2 - notchSize);
					p3 = (int)(p2 + notchSize);
					break;

				case ArrowDirection.Left:
				default:
					p2 = y1 - p2;
					p1 = (int)(p2 - notchSize);
					p3 = (int)(p2 + notchSize);
					break;
			}

			int notchX = (int)(p2 - notchSize);

			tabShape.MoveTo(x2, y4); // A
			tabShape.curve3(x1, y4, x1, y3); // A -> B

			if (arrowDirection != ArrowDirection.Left)
			{
				// B -> C
				tabShape.LineTo(x1, y2);
			}
			else
			{
				// Left Notch (B -> C through P1:P4)
				tabShape.LineTo(x1, p1); // B -> P1

				// Notch
				tabShape.LineTo(x0, p2); // P1 -> P2
				tabShape.LineTo(x1, p3); // P2 -> P3
				tabShape.LineTo(x1, y2); // P3 -> P4
			}

			tabShape.curve3(x1, y1, x2, y1); // C -> D

			if (arrowDirection != ArrowDirection.Up)
			{
				// D -> E
				tabShape.LineTo(x3, y1);
			}
			else
			{
				// D -> E through (P1:P3)
				tabShape.LineTo(p1, y1); // F -> P1

				// Notch
				tabShape.LineTo(p2, y0); // P1 -> P2
				tabShape.LineTo(p3, y1); // P2 -> P3
				tabShape.LineTo(x3, y1); // P3 -> E
			}

			tabShape.curve3(x4, y1, x4, y2); // E -> F

			if (arrowDirection != ArrowDirection.Right)
			{
				// F -> G
				tabShape.LineTo(x4, y3);
			}
			else
			{
				// F -> G through P1-P3
				tabShape.LineTo(x4, p1); // F -> P1

				// Notch
				tabShape.LineTo(x5, p2); // P1 -> P2
				tabShape.LineTo(x4, p3); // P2 -> P3
				tabShape.LineTo(x4, y3); // P3 -> G
			}

			tabShape.curve3(x4, y4, x3, y4); // G -> H

			if (arrowDirection != ArrowDirection.Down)
			{
				// H -> A
				tabShape.LineTo(x2, y4);
			}
			else
			{
				// H -> A (through P1:P3)
				tabShape.LineTo(notchX + (notchSize * 2), y4); // F -> P1

				// Notch
				tabShape.LineTo(p2, y5); // P1 -> P2
				tabShape.LineTo(notchX, y4); // P2 -> P3
				tabShape.LineTo(x3, y4); // P3 -> A
			}

			return new FlattenCurves(tabShape);
		}

		public override void OnDrawBackground(Graphics2D graphics2D)
		{
			base.OnDrawBackground(graphics2D);

			if (tabShape != null)
			{
				graphics2D.Render(tabShape, this.TagColor);
			}

			if (this.BorderColor != Color.Transparent)
			{
				graphics2D.Render(tabStroke, this.BorderColor);
			}
		}
	}
}
