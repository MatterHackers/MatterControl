using System;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.MatterControl
{
	public class DropDownButtonBase : GuiWidget
	{

		RGBA_Bytes fillColor;
		RGBA_Bytes borderColor;


		double borderWidth;

		//Set Defaults
		public DropDownButtonBase(string label, RGBA_Bytes fillColor, RGBA_Bytes borderColor, RGBA_Bytes textColor, double borderWidth, BorderDouble margin, int fontSize = 12, FlowDirection flowDirection = FlowDirection.LeftToRight, double height = 40)
		{
			FlowLayoutWidget container = new FlowLayoutWidget ();
			container.Padding = new BorderDouble (0);
			container.Margin = new BorderDouble (0);
			if(label != "")
			{
				TextWidget text = new TextWidget (label ,pointSize: fontSize, textColor: textColor);
				text.VAnchor = VAnchor.ParentCenter;
				text.Padding = new BorderDouble (0, 0);
				container.AddChild (text);
			}

			GuiWidget arrow = new GuiWidget (20, height);
			arrow.VAnchor = VAnchor.ParentCenter;
			container.AddChild (arrow);
			this.AddChild (container);

			this.Padding = new BorderDouble (0, 0);
			this.fillColor = fillColor;
			this.borderColor = borderColor;
			this.borderWidth = borderWidth;
			this.HAnchor = HAnchor.FitToChildren;
		}

		public override void OnDraw (Graphics2D graphics2D)
		{
			DrawBorder (graphics2D);
			DrawBackground (graphics2D);

			base.OnDraw (graphics2D);
		}

		private void DrawBorder(Graphics2D graphics2D)
		{
			if (borderColor.Alpha0To255 > 0)
			{
				RectangleDouble boarderRectangle = LocalBounds;

				RoundedRect rectBorder = new RoundedRect(boarderRectangle,0);

				graphics2D.Render(new Stroke(rectBorder, borderWidth), borderColor);
			}
		}

		private void DrawBackground(Graphics2D graphics2D)
		{
			if (this.fillColor.Alpha0To255 > 0)
			{
				RectangleDouble insideBounds = LocalBounds;
				insideBounds.Inflate(-this.borderWidth);
				RoundedRect rectInside = new RoundedRect(insideBounds,0);

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
		FlowDirection flowDirection;

		public double FixedWidth = 10;
		public double FixedHeight = 30;

		public DropDownMenuFactory ()
		{

		}



		public DynamicDropDownMenu Generate(string label = "", TupleList<string,Func<bool>> optionList = null, Direction direction = Direction.Down)
		{

			DynamicDropDownMenu menu = new DynamicDropDownMenu (label, CreateButtonViewStates(label),direction);
			menu.VAnchor = VAnchor.ParentCenter;
			menu.HAnchor = HAnchor.FitToChildren;
			menu.MenuAsWideAsItems = false;
			menu.AlignToRightEdge = true;
			menu.NormalColor = normalFillColor;
			menu.HoverColor = hoverFillColor;
			menu.BorderColor = normalBorderColor;
			menu.BackgroundColor = menu.NormalColor;

			if(optionList != null)
			{
				foreach(Tuple<string,Func<bool>> option in optionList)
				{
					menu.addItem (option.Item1, option.Item2);
				}
			}

			return menu;
		}

		private ButtonViewStates CreateButtonViewStates(string label)
		{
			//Create the multi-state button view
			ButtonViewStates buttonViewWidget = new ButtonViewStates(
				new DropDownButtonBase(label, normalFillColor, normalBorderColor, normalTextColor, borderWidth, Margin,  fontSize, flowDirection ,  FixedHeight),
				new DropDownButtonBase(label, hoverFillColor, hoverBorderColor, hoverTextColor, borderWidth, Margin,  fontSize, flowDirection ,  FixedHeight),
				new DropDownButtonBase(label, pressedFillColor, pressedBorderColor, pressedTextColor, borderWidth, Margin,  fontSize, flowDirection ,  FixedHeight),
				new DropDownButtonBase(label, disabledFillColor, disabledBorderColor, disabledTextColor, borderWidth, Margin,  fontSize, flowDirection ,  FixedHeight)
			);

			buttonViewWidget.Padding = new BorderDouble (0, 0);

			return buttonViewWidget;
		}
	}
}

