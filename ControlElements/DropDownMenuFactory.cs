/*
Copyright (c) 2015, Kevin Pope
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
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using System;
using System.Collections.Generic;

namespace MatterHackers.MatterControl
{
	public class DropDownButtonBase : GuiWidget
	{
		private RGBA_Bytes fillColor;
		private RGBA_Bytes borderColor;

		private double borderWidth;

		//Set Defaults
		public DropDownButtonBase(string label, RGBA_Bytes fillColor, RGBA_Bytes borderColor, RGBA_Bytes textColor, double borderWidth, BorderDouble margin, int fontSize = 12, FlowDirection flowDirection = FlowDirection.LeftToRight, double height = 40)
		{
			FlowLayoutWidget container = new FlowLayoutWidget();
			container.Padding = new BorderDouble(0);
			container.Margin = new BorderDouble(0);
			if (label != "")
			{
				TextWidget text = new TextWidget(label, pointSize: fontSize, textColor: textColor);
				text.VAnchor = VAnchor.Center;
				text.Padding = new BorderDouble(0, 0);
				container.AddChild(text);
			}

			GuiWidget arrow = new GuiWidget(20, height);
			arrow.VAnchor = VAnchor.Center;
			container.AddChild(arrow);
			this.AddChild(container);

			this.Padding = new BorderDouble(0, 0);
			this.fillColor = fillColor;
			this.borderColor = borderColor;
			this.borderWidth = borderWidth;
			this.HAnchor = HAnchor.Fit;
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			DrawBorder(graphics2D);
			DrawBackground(graphics2D);

			base.OnDraw(graphics2D);
		}

		private void DrawBorder(Graphics2D graphics2D)
		{
			if (borderColor.Alpha0To255 > 0)
			{
				RectangleDouble boarderRectangle = LocalBounds;

				RoundedRect rectBorder = new RoundedRect(boarderRectangle, 0);

				graphics2D.Render(new Stroke(rectBorder, borderWidth), borderColor);
			}
		}

		private void DrawBackground(Graphics2D graphics2D)
		{
			if (this.fillColor.Alpha0To255 > 0)
			{
				RectangleDouble insideBounds = LocalBounds;
				insideBounds.Inflate(-this.borderWidth);
				RoundedRect rectInside = new RoundedRect(insideBounds, 0);

				graphics2D.Render(rectInside, this.fillColor);
			}
		}
	}

	public class DropDownMenuFactory
	{
		public BorderDouble Margin = new BorderDouble(0, 0);
		public RGBA_Bytes normalFillColor = new RGBA_Bytes(0, 0, 0, 0);
		public RGBA_Bytes hoverFillColor = new RGBA_Bytes(0, 0, 0, 50);
		public RGBA_Bytes pressedFillColor = new RGBA_Bytes(0, 0, 0, 0);
		public RGBA_Bytes disabledFillColor = new RGBA_Bytes(255, 255, 255, 50);

		public RGBA_Bytes normalBorderColor = new RGBA_Bytes(255, 255, 255, 0);
		public RGBA_Bytes hoverBorderColor = new RGBA_Bytes(0, 0, 0, 0);
		public RGBA_Bytes pressedBorderColor = new RGBA_Bytes(0, 0, 0, 0);
		public RGBA_Bytes disabledBorderColor = new RGBA_Bytes(0, 0, 0, 0);
		public RGBA_Bytes checkedBorderColor = new RGBA_Bytes(255, 255, 255, 0);

		public RGBA_Bytes normalTextColor = ActiveTheme.Instance.PrimaryTextColor;
		public RGBA_Bytes hoverTextColor = ActiveTheme.Instance.PrimaryTextColor;
		public RGBA_Bytes pressedTextColor = ActiveTheme.Instance.PrimaryTextColor;
		public RGBA_Bytes disabledTextColor = ActiveTheme.Instance.PrimaryTextColor;

		public int fontSize = 12;
		public double borderWidth = 1;

		public double FixedWidth = 10;
		public double FixedHeight = 30;

		public DropDownMenu Generate(string label = "", List<NamedAction> actions = null, Direction direction = Direction.Down)
		{
			return new DropDownMenu(CreateButtonViewStates(label), direction)
			{
				VAnchor = VAnchor.Center,
				HAnchor = HAnchor.Fit,
				MenuAsWideAsItems = false,
				AlignToRightEdge = true,
				//Width = this.FixedWidth,
				NormalColor = normalFillColor,
				BackgroundColor = normalFillColor,
				HoverColor = hoverFillColor,
				BorderColor = normalBorderColor,
				MenuActions = actions
			};
		}

		private ButtonViewStates CreateButtonViewStates(string label)
		{
			//Create the multi-state button view
			var buttonViewWidget = new ButtonViewStates(
				new DropDownButtonBase(label, normalFillColor, normalBorderColor, normalTextColor, borderWidth, Margin, fontSize, FlowDirection.LeftToRight, FixedHeight),
				new DropDownButtonBase(label, hoverFillColor, hoverBorderColor, hoverTextColor, borderWidth, Margin, fontSize, FlowDirection.LeftToRight, FixedHeight),
				new DropDownButtonBase(label, pressedFillColor, pressedBorderColor, pressedTextColor, borderWidth, Margin, fontSize, FlowDirection.LeftToRight, FixedHeight),
				new DropDownButtonBase(label, disabledFillColor, disabledBorderColor, disabledTextColor, borderWidth, Margin, fontSize, FlowDirection.LeftToRight, FixedHeight)
			);

			buttonViewWidget.Padding = new BorderDouble(0, 0);

			return buttonViewWidget;
		}
	}
}