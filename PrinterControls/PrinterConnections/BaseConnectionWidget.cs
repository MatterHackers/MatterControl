using MatterHackers.Agg;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
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

	public class BaudRateRadioButton : RadioButton
	{
		public int BaudRate;

		public BaudRateRadioButton(string label)
			: base(label)
		{
			BaudRate = int.Parse(label);
		}
	}

	public class PrinterSelectRadioButton : RadioButton
	{
		public Printer printer;

		public PrinterSelectRadioButton(Printer printer)
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
		public ActionLink Generate(string linkText, int fontSize, GuiWidget.MouseEventHandler clickEvent)
		{
			ActionLink actionLink = new ActionLink(linkText, fontSize);
			if (clickEvent != null)
			{
				actionLink.MouseUp += new GuiWidget.MouseEventHandler(clickEvent);
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
			this.VAnchor = VAnchor.ParentBottomTop;
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
		public Printer LinkedPrinter;

		public PrinterActionLink(string text, Printer printer, int fontSize = 10)
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
			buttonText.VAnchor = VAnchor.ParentCenter;
			buttonText.HAnchor = HAnchor.ParentCenter;
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

	public class ConnectionWidgetBase : GuiWidget
	{
		private static Regex linuxDefaultUIFilter = new Regex("/dev/ttyS*\\d+", RegexOptions.CultureInvariant | RegexOptions.Compiled);
		private List<SerialPortIndexRadioButton> SerialPortButtonsList = new List<SerialPortIndexRadioButton>();
		private bool printerComPortIsAvailable = false;

		protected GuiWidget containerWindowToClose;
		protected RGBA_Bytes defaultTextColor = ActiveTheme.Instance.PrimaryTextColor;
		protected RGBA_Bytes defaultBackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
		protected RGBA_Bytes subContainerTextColor = ActiveTheme.Instance.PrimaryTextColor;
		protected RGBA_Bytes labelBackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
		protected RGBA_Bytes linkTextColor = ActiveTheme.Instance.SecondaryAccentColor;
		protected ConnectionWindow windowController;
		public ActionLinkFactory actionLinkFactory = new ActionLinkFactory();

		private event EventHandler unregisterEvents;

		public ConnectionWidgetBase(ConnectionWindow windowController, GuiWidget containerWindowToClose)
			: base()
		{
			this.windowController = windowController;
			this.containerWindowToClose = containerWindowToClose;
			ActiveTheme.Instance.ThemeChanged.RegisterEvent(ThemeChanged, ref unregisterEvents);
		}

		public int GetPrinterRecordCount()
		{
			return Datastore.Instance.RecordCount("Printer");
		}

		public void ThemeChanged(object sender, EventArgs e)
		{
			this.linkTextColor = ActiveTheme.Instance.PrimaryAccentColor;
			this.Invalidate();
		}

		public override void OnClosed(EventArgs e)
		{
			if (unregisterEvents != null)
			{
				unregisterEvents(this, null);
			}
			base.OnClosed(e);
		}

		private SerialPortIndexRadioButton createComPortOption(string portName, bool isActivePrinterPort)
		{
			SerialPortIndexRadioButton comPortOption = new SerialPortIndexRadioButton(portName, portName)
			{
				HAnchor = HAnchor.ParentLeft,
				Margin = new BorderDouble(3, 3, 5, 3),
				TextColor = this.subContainerTextColor,
				Checked = isActivePrinterPort
			};
			return comPortOption;
		}

		protected string GetSelectedSerialPort()
		{
			foreach (SerialPortIndexRadioButton button in SerialPortButtonsList)
			{
				if (button.Checked)
				{
					return button.PortValue;
				}
			}

			throw new Exception(LocalizedString.Get("Could not find a selected button."));
		}

		protected void CreateSerialPortControls(FlowLayoutWidget comPortContainer, string activePrinterSerialPort)
		{
			int portIndex = 0;
			string[] allPorts = FrostedSerialPort.GetPortNames();
			IEnumerable<string> filteredPorts;

			if (OsInformation.OperatingSystem == OSType.X11)
			{
				// A default and naive filter that works well on Ubuntu 14
				filteredPorts = allPorts.Where(portName => portName != "/dev/tty" && !linuxDefaultUIFilter.Match(portName).Success);
			}
			else
			{
				// looks_like_mac -- serialPort.StartsWith("/dev/tty."); looks_like_pc -- serialPort.StartsWith("COM")
				filteredPorts = allPorts.Where(portName => portName.StartsWith("/dev/tty.") || portName.StartsWith("COM"));
			}

			IEnumerable<string> portsToCreate = filteredPorts.Any() ? filteredPorts : allPorts;

			// Add a radio button for each filtered port
			foreach (string portName in portsToCreate)
			{
				SerialPortIndexRadioButton comPortOption = createComPortOption(portName, activePrinterSerialPort == portName);
				if (comPortOption.Checked)
				{
					printerComPortIsAvailable = true;
				}

				SerialPortButtonsList.Add(comPortOption);
				comPortContainer.AddChild(comPortOption);

				portIndex++;
			}

			// Add a virtual entry for serial ports that were previously configured but are not currently connected
			if (!printerComPortIsAvailable && activePrinterSerialPort != null)
			{
				SerialPortIndexRadioButton comPortOption = createComPortOption(activePrinterSerialPort, true);
				comPortOption.Enabled = false;

				comPortContainer.AddChild(comPortOption);
				SerialPortButtonsList.Add(comPortOption);
				portIndex++;
			}

			//If there are still no com ports show a message to that effect
			if (portIndex == 0)
			{
				TextWidget comPortOption = new TextWidget(LocalizedString.Get("No COM ports available"));
				comPortOption.Margin = new BorderDouble(3, 6, 5, 6);
				comPortOption.TextColor = this.subContainerTextColor;
				comPortContainer.AddChild(comPortOption);
			}
		}
	}
}