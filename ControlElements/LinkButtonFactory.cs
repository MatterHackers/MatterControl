using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using System;

namespace MatterHackers.MatterControl
{
	public class ChangeTextColorEventArgs : EventArgs
	{
		public RGBA_Bytes color;

		public ChangeTextColorEventArgs(RGBA_Bytes color)
		{
			this.color = color;
		}
	}

	//Base widget for use in ButtonStatesViewWidget
	public class LinkButtonViewBase : GuiWidget
	{
		protected RGBA_Bytes fillColor = new RGBA_Bytes(0, 0, 0, 0);
		protected RGBA_Bytes borderColor = new RGBA_Bytes(0, 0, 0, 0);
		protected double borderWidth = 0;
		protected double borderRadius;
		protected double padding;
		protected bool isUnderlined = false;

		public RGBA_Bytes TextColor { get; set; }

		private TextWidget buttonText;

		public LinkButtonViewBase(string label,
									 double textHeight,
									 double padding,
									 RGBA_Bytes textColor,
									 bool isUnderlined = false)
			: base()
		{
			this.padding = padding;
			this.TextColor = textColor;
			this.isUnderlined = isUnderlined;

			buttonText = new TextWidget(label, pointSize: textHeight);
			buttonText.VAnchor = VAnchor.Center;
			buttonText.HAnchor = HAnchor.Center;
			buttonText.TextColor = this.TextColor;

			//this.AnchorAll();
			this.AddChild(buttonText);
			HAnchor = HAnchor.Fit;
			VAnchor = VAnchor.Fit;
		}

		public override void SendToChildren(object objectToRoute)
		{
			var changeColorEvent = objectToRoute as ChangeTextColorEventArgs;
			if (changeColorEvent != null)
			{
				buttonText.TextColor = changeColorEvent.color;
			}
			base.SendToChildren(objectToRoute);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			RectangleDouble Bounds = LocalBounds;
			RoundedRect rectBorder = new RoundedRect(Bounds, this.borderRadius);

			graphics2D.Render(rectBorder, borderColor);

			RectangleDouble insideBounds = Bounds;
			insideBounds.Inflate(-this.borderWidth);
			RoundedRect rectInside = new RoundedRect(insideBounds, Math.Max(this.borderRadius - this.borderWidth, 0));

			graphics2D.Render(rectInside, this.fillColor);

			if (this.isUnderlined)
			{
				//Printer.TypeFaceStyle.DoUnderline = true;
				RectangleDouble underline = new RectangleDouble(LocalBounds.Left, LocalBounds.Bottom, LocalBounds.Right, LocalBounds.Bottom);
				graphics2D.Rectangle(underline, buttonText.TextColor);
			}

			base.OnDraw(graphics2D);
		}
	}

	public class LinkButtonFactory
	{
		public double fontSize = 14;
		public double padding = 3;
		public RGBA_Bytes fillColor = new RGBA_Bytes(63, 63, 70, 0);
		public RGBA_Bytes borderColor = new RGBA_Bytes(37, 37, 38, 0);
		public RGBA_Bytes textColor = ActiveTheme.Instance.PrimaryAccentColor;
		public BorderDouble margin = new BorderDouble(0, 3);

		public Button Generate(string buttonText)
		{
			//Widgets to show during the four button states
			LinkButtonViewBase buttonWidgetPressed = getButtonWidgetPressed(buttonText);
			LinkButtonViewBase buttonWidgetHover = getButtonWidgetHover(buttonText);
			LinkButtonViewBase buttonWidgetNormal = getButtonWidgetNormal(buttonText);
			LinkButtonViewBase buttonWidgetDisabled = getButtonWidgetDisabled(buttonText);

			//Create container for the three state widgets for the button
			ButtonViewStates buttonViewWidget = new ButtonViewStates(buttonWidgetNormal, buttonWidgetHover, buttonWidgetPressed, buttonWidgetDisabled);

			//Create button based on view container widget
			return new Button(0, 0, buttonViewWidget)
			{
				Margin = margin,
				Cursor = Cursors.Hand,
			};
		}

		private LinkButtonViewBase getButtonWidgetPressed(string buttonText)
		{
			LinkButtonViewBase widget = new LinkButtonViewBase(buttonText,
															   this.fontSize,
															   this.padding,
															   this.textColor);
			return widget;
		}

		private LinkButtonViewBase getButtonWidgetHover(string buttonText)
		{
			LinkButtonViewBase widget = new LinkButtonViewBase(buttonText,
															   this.fontSize,
															   this.padding,
															   this.textColor);
			return widget;
		}

		public LinkButtonViewBase getButtonWidgetNormal(string buttonText)
		{
			LinkButtonViewBase widget = new LinkButtonViewBase(buttonText,
															   this.fontSize,
															   this.padding,
															   this.textColor,
															   true);
			return widget;
		}

		private LinkButtonViewBase getButtonWidgetDisabled(string buttonText)
		{
			LinkButtonViewBase widget = new LinkButtonViewBase(buttonText,
															   this.fontSize,
															   this.padding,
															   this.textColor);
			return widget;
		}
	}
}