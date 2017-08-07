using MatterHackers.Agg;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.SerialPortCommunication.FrostedSerial;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace MatterHackers.MatterControl.PrinterControls.PrinterConnections
{
	public class SerialPortIndexRadioButton : RadioButton
	{
		public string PortValue;

		public SerialPortIndexRadioButton(string label, string value)
			: base(label)
		{
			PortValue = value;
			this.EnabledChanged += new EventHandler(onRadioButtonEnabledChanged);
		}

		private void onRadioButtonEnabledChanged(object sender, EventArgs e)
		{
			if (this.Enabled)
			{
				this.TextColor = RGBA_Bytes.White;
			}
			{
				this.TextColor = RGBA_Bytes.Gray;
			}
		}
	}

	public class PrinterSelectRadioButton : RadioButton
	{
		public PrinterInfo printer;

		public PrinterSelectRadioButton(PrinterInfo printer)
			: base(printer.Name)
		{
			this.printer = printer;
		}
	}

	public class OptionContainer : GuiWidget
	{
		private RGBA_Bytes borderColor = new RGBA_Bytes(63, 63, 70);
		private RGBA_Bytes backgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

		public OptionContainer()
			: base()
		{
			this.Margin = new BorderDouble(2, 5, 2, 0);
			this.BackgroundColor = backgroundColor;
		}

		public override void OnDraw(Agg.Graphics2D graphics2D)
		{
			base.OnDraw(graphics2D);
			graphics2D.Rectangle(LocalBounds, borderColor);
		}
	}

	public class ActionLinkFactory
	{
		public ActionLink Generate(string linkText, int fontSize, EventHandler<MouseEventArgs> clickEvent)
		{
			ActionLink actionLink = new ActionLink(linkText, fontSize);
			if (clickEvent != null)
			{
				actionLink.MouseUp += clickEvent;
			}
			return actionLink;
		}
	}

	public class ActionLink : TextWidget
	{
		private bool isUnderlined = true;

		public ActionLink(string text, int fontSize = 10)
			: base(text, 0, 0, fontSize)
		{
			this.Selectable = true;
			this.Margin = new BorderDouble(3, 0, 3, 0);
			this.VAnchor = VAnchor.Stretch;
			this.MouseEnter += new EventHandler(onMouse_Enter);
			this.MouseLeave += new EventHandler(onMouse_Leave);
			this.Cursor = Cursors.Hand;
		}

		public override void OnDraw(Agg.Graphics2D graphics2D)
		{
			base.OnDraw(graphics2D);
			if (this.isUnderlined)
			{
				//Printer.TypeFaceStyle.DoUnderline = true;
				RectangleDouble underline = new RectangleDouble(LocalBounds.Left, LocalBounds.Bottom, LocalBounds.Right, LocalBounds.Bottom);
				graphics2D.Rectangle(underline, this.TextColor);
			}
		}

		private void onMouse_Enter(object sender, EventArgs args)
		{
			this.isUnderlined = false;
			this.Invalidate();
		}

		private void onMouse_Leave(object sender, EventArgs args)
		{
			this.isUnderlined = true;
			this.Invalidate();
		}
	}

	public class PrinterActionLink : ActionLink
	{
		public PrinterInfo LinkedPrinter;

		public PrinterActionLink(string text, PrinterInfo printer, int fontSize = 10)
			: base(text, fontSize)
		{
			this.LinkedPrinter = printer;
		}
	}

	public class DetailRow : FlowLayoutWidget
	{
		public List<GuiWidget> showOnHoverChildren = new List<GuiWidget>();

		public DetailRow()
			: base(FlowDirection.LeftToRight)
		{
			this.MouseEnter += new EventHandler(onMouse_Enter);
			this.MouseLeave += new EventHandler(onMouse_Leave);
		}

		private void onMouse_Enter(object sender, EventArgs args)
		{
			foreach (GuiWidget item in showOnHoverChildren)
			{
				item.Visible = true;
			}
			this.Invalidate();
		}

		private void onMouse_Leave(object sender, EventArgs args)
		{
			foreach (GuiWidget item in showOnHoverChildren)
			{
				item.Visible = false;
			}
			this.Invalidate();
		}
	}

	public class LabelContainer : GuiWidget
	{
		private RGBA_Bytes backgroundColor = new RGBA_Bytes(0, 140, 158);

		public LabelContainer()
			: base()
		{
			this.Margin = new BorderDouble(2, -2, 2, 5);
			this.Padding = new BorderDouble(3);
			this.BackgroundColor = backgroundColor;
		}

		public override void OnDraw(Agg.Graphics2D graphics2D)
		{
			base.OnDraw(graphics2D);
		}
	}

	//Base widget for use in ButtonStatesViewWidget
	public class ControlButtonViewBase : GuiWidget
	{
		protected RGBA_Bytes fillColor;
		protected RGBA_Bytes borderColor;
		protected double borderWidth;
		protected double borderRadius;
		protected double padding;

		public ControlButtonViewBase(string label,
									 double width,
									 double height,
									 double textHeight,
									 double borderWidth,
									 double borderRadius,
									 double padding,
									 RGBA_Bytes textColor,
									 RGBA_Bytes fillColor,
									 RGBA_Bytes borderColor)
			: base(width, height)
		{
			this.borderRadius = borderRadius;
			this.borderWidth = borderWidth;
			this.fillColor = fillColor;
			this.borderColor = borderColor;
			this.padding = padding;

			TextWidget buttonText = new TextWidget(label, textHeight);
			buttonText.VAnchor = VAnchor.Center;
			buttonText.HAnchor = HAnchor.Center;
			buttonText.TextColor = textColor;

			//this.AnchorAll();
			this.AddChild(buttonText);
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

			base.OnDraw(graphics2D);
		}
	}
}