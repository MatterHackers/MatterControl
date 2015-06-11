using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.VectorMath;
using System;

namespace MatterHackers.MatterControl.ActionBar
{
	//Base widget for ActionBarRows
	public abstract class ActionRowBase : FlowLayoutWidget
	{
		public ActionRowBase()
			: base(FlowDirection.LeftToRight)
		{
			Initialize();
			SetDisplayAttributes();
			AddChildElements();
			AddHandlers();
		}

		protected virtual void Initialize()
		{
			//Placeholder for row-specific initialization
		}

		protected void SetDisplayAttributes()
		{
			this.HAnchor = HAnchor.ParentLeftRight;
		}

		protected abstract void AddChildElements();

		protected virtual void AddHandlers()
		{
			//Placeholder for row-specific handlers
		}
	}

	//Base widget for use in ButtonViewStates
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

	//Base widget for use in ButtonViewStates
	public class PrinterSelectViewBase : GuiWidget
	{
		protected RGBA_Bytes fillColor;
		protected RGBA_Bytes borderColor;
		protected double borderWidth;
		protected double borderRadius;
		protected double padding;
		protected double statusTextHeight = 8;
		private TextWidget printerStatusText;
		private TextWidget printerNameText;

		private event EventHandler unregisterEvents;

		public PrinterSelectViewBase(
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
			this.Padding = new BorderDouble(10, 5);
			this.HAnchor = HAnchor.ParentLeftRight;

			FlowLayoutWidget textContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
			textContainer.VAnchor = VAnchor.ParentBottomTop;
			textContainer.HAnchor = HAnchor.ParentLeftRight;

			printerNameText = new TextWidget("", pointSize: textHeight);
			printerNameText.AutoExpandBoundsToText = true;
			printerNameText.HAnchor = HAnchor.ParentCenter;
			printerNameText.TextColor = textColor;

			string printerStatusTextBeg = LocalizedString.Get("Status");
			string printerStatusTextEnd = LocalizedString.Get("Connected");
			string printerStatusTextFull = string.Format("{0}: {1}", printerStatusTextBeg, printerStatusTextEnd);
			printerStatusText = new TextWidget(printerStatusTextFull, pointSize: statusTextHeight);
			printerStatusText.AutoExpandBoundsToText = true;
			printerStatusText.HAnchor = HAnchor.ParentCenter;
			printerStatusText.TextColor = textColor;

			textContainer.AddChild(printerNameText);
			textContainer.AddChild(printerStatusText);

			SetButtonText();

			//this.AnchorAll();
			this.AddChild(textContainer);

			ActivePrinterProfile.Instance.ActivePrinterChanged.RegisterEvent(onActivePrinterChanged, ref unregisterEvents);
			PrinterConnectionAndCommunication.Instance.CommunicationStateChanged.RegisterEvent(onActivePrinterChanged, ref unregisterEvents);
		}

		public override void OnClosed(EventArgs e)
		{
			if (unregisterEvents != null)
			{
				unregisterEvents(this, null);
			}
			base.OnClosed(e);
		}

		private void onActivePrinterChanged(object sender, EventArgs e)
		{
			UiThread.RunOnIdle(() =>
			{
				SetButtonText();
			});
		}

		private int GetPrinterRecordCount()
		{
			return DataStorage.Datastore.Instance.RecordCount("Printer");
		}

		private void SetButtonText()
		{
			if (PrinterConnectionAndCommunication.Instance.CommunicationState == PrinterConnectionAndCommunication.CommunicationStates.FailedToConnect && PrinterConnectionAndCommunication.Instance.ConnectionFailureMessage != "")
			{
				printerStatusText.Text = "Status: " + PrinterConnectionAndCommunication.Instance.ConnectionFailureMessage;
			}
			else
			{
				string statusStringBeg = LocalizedString.Get("Status").ToUpper();
				string statusString = string.Format("{1}: {0}", PrinterConnectionAndCommunication.Instance.PrinterConnectionStatusVerbose, statusStringBeg);
				printerStatusText.Text = string.Format(statusString, PrinterConnectionAndCommunication.Instance.PrinterConnectionStatusVerbose);
			}
			if (ActivePrinterProfile.Instance.ActivePrinter != null)
			{
				printerNameText.Text = ActivePrinterProfile.Instance.ActivePrinter.Name;
			}
			else
			{
				if (GetPrinterRecordCount() > 0)
				{
					string printerNameLabel = LocalizedString.Get("Select Printer");
					string printerNameLabelFull = string.Format("- {0} -", printerNameLabel);
					printerNameText.Text = (printerNameLabelFull);
				}
				else
				{
					string addPrinterLabel = LocalizedString.Get("Add Printer");
					string addPrinterLabelFull = string.Format("- {0} -", addPrinterLabel);
					printerNameText.Text = (addPrinterLabelFull);
				}
			}
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

	public class PrinterSelectButton : Button
	{
		private double width = 180;
		private double height = 40;
		private double borderRadius = 0;
		private double borderWidth = 1;
		private double fontSize = 14;
		private double padding = 3;
		private BorderDouble margin = new BorderDouble(0, 0);

		public PrinterSelectButton()
		{
			this.HAnchor = HAnchor.ParentLeftRight;

			//Widgets to show during the four button states
			PrinterSelectViewBase buttonWidgetPressed = getButtonWidgetNormal();
			PrinterSelectViewBase buttonWidgetHover = getButtonWidgetHover();
			PrinterSelectViewBase buttonWidgetNormal = getButtonWidgetNormal();
			PrinterSelectViewBase buttonWidgetDisabled = getButtonWidgetNormal();

			//Create container for the three state widgets for the button
			ButtonViewStates buttonView = new ButtonViewStates(buttonWidgetNormal, buttonWidgetHover, buttonWidgetPressed, buttonWidgetDisabled);
			buttonView.HAnchor = HAnchor.ParentLeftRight;

			Margin = DefaultMargin;

			OriginRelativeParent = new Vector2(0, 0);

			if (buttonView != null)
			{
				buttonView.Selectable = false;

				AddChild(buttonView);

				HAnchor = HAnchor.FitToChildren;
				VAnchor = VAnchor.FitToChildren;

				if (LocalBounds.Left != 0 || LocalBounds.Bottom != 0)
				{
					// let's make sure that a button has 0, 0 at the lower left
					// move the children so they will fit with 0, 0 at the lower left
					foreach (GuiWidget child in Children)
					{
						child.OriginRelativeParent = child.OriginRelativeParent + new Vector2(-LocalBounds.Left, -LocalBounds.Bottom);
					}

					HAnchor = HAnchor.FitToChildren;
					VAnchor = VAnchor.FitToChildren;
				}

				MinimumSize = new Vector2(Width, Height);
			}
		}

		private PrinterSelectViewBase getButtonWidgetHover()
		{
			RGBA_Bytes borderColor;
			RGBA_Bytes fillColor = ActiveTheme.Instance.PrimaryBackgroundColor;
			if (ActiveTheme.Instance.IsDarkTheme)
			{
				borderColor = new RGBA_Bytes(128, 128, 128);
			}
			else
			{
				borderColor = new RGBA_Bytes(128, 128, 128);
			}
			RGBA_Bytes textColor = ActiveTheme.Instance.PrimaryTextColor;
			PrinterSelectViewBase widget = new PrinterSelectViewBase(
															   this.width,
															   this.height,
															   this.fontSize,
															   this.borderWidth,
															   this.borderRadius,
															   this.padding,
															   textColor,
															   fillColor,
															   borderColor);
			return widget;
		}

		public PrinterSelectViewBase getButtonWidgetNormal()
		{
			RGBA_Bytes fillColor = ActiveTheme.Instance.PrimaryBackgroundColor;
			RGBA_Bytes borderColor;
			if (ActiveTheme.Instance.IsDarkTheme)
			{
				borderColor = new RGBA_Bytes(77, 77, 77);
			}
			else
			{
				borderColor = new RGBA_Bytes(190, 190, 190);
			}

			RGBA_Bytes textColor = ActiveTheme.Instance.PrimaryTextColor;
			PrinterSelectViewBase widget = new PrinterSelectViewBase(
															   this.width,
															   this.height,
															   this.fontSize,
															   this.borderWidth,
															   this.borderRadius,
															   this.padding,
															   textColor,
															   fillColor,
															   borderColor);
			return widget;
		}
	}

	public class ActionBarControlButtonFactory
	{
		private double width = 75;
		private double height = 30;
		private double borderRadius = 3;
		private double borderWidth = 1;
		private double fontSize = 14;
		private double padding = 3;
		private BorderDouble margin = new BorderDouble(5, 0);

		public Button Generate(string buttonText)
		{
			//Widgets to show during the four button states
			ControlButtonViewBase buttonWidgetPressed = getButtonWidgetPressed(buttonText);
			ControlButtonViewBase buttonWidgetHover = getButtonWidgetHover(buttonText);
			ControlButtonViewBase buttonWidgetNormal = getButtonWidgetNormal(buttonText);
			ControlButtonViewBase buttonWidgetDisabled = getButtonWidgetDisabled(buttonText);

			//Create container for the three state widgets for the button
			ButtonViewStates buttonViewWidget = new ButtonViewStates(buttonWidgetNormal, buttonWidgetHover, buttonWidgetPressed, buttonWidgetDisabled);

			//Create button based on view container widget
			Button controlButton = new Button(0, 0, buttonViewWidget);
			controlButton.Margin = margin;

			return controlButton;
		}

		private ControlButtonViewBase getButtonWidgetPressed(string buttonText)
		{
			RGBA_Bytes fillColor = new RGBA_Bytes(63, 63, 70);
			RGBA_Bytes borderColor = new RGBA_Bytes(37, 37, 38);
			RGBA_Bytes textColor = new RGBA_Bytes(230, 230, 230);
			ControlButtonViewBase widget = new ControlButtonViewBase(buttonText,
															   this.width,
															   this.height,
															   this.fontSize,
															   this.borderWidth,
															   this.borderRadius,
															   this.padding,
															   textColor,
															   fillColor,
															   borderColor);
			return widget;
		}

		private ControlButtonViewBase getButtonWidgetHover(string buttonText)
		{
			RGBA_Bytes fillColor = new RGBA_Bytes(63, 63, 70);
			RGBA_Bytes borderColor = RGBA_Bytes.LightGray;
			RGBA_Bytes textColor = new RGBA_Bytes(230, 230, 230);
			ControlButtonViewBase widget = new ControlButtonViewBase(buttonText,
															   this.width,
															   this.height,
															   this.fontSize,
															   this.borderWidth,
															   this.borderRadius,
															   this.padding,
															   textColor,
															   fillColor,
															   borderColor);
			return widget;
		}

		public ControlButtonViewBase getButtonWidgetNormal(string buttonText)
		{
			RGBA_Bytes fillColor = new RGBA_Bytes(245, 245, 245);
			RGBA_Bytes borderColor = new RGBA_Bytes(204, 204, 204);
			RGBA_Bytes textColor = new RGBA_Bytes(69, 69, 69);
			ControlButtonViewBase widget = new ControlButtonViewBase(buttonText,
															   this.width,
															   this.height,
															   this.fontSize,
															   this.borderWidth,
															   this.borderRadius,
															   this.padding,
															   textColor,
															   fillColor,
															   borderColor);
			return widget;
		}

		private ControlButtonViewBase getButtonWidgetDisabled(string buttonText)
		{
			RGBA_Bytes fillColor = new RGBA_Bytes(245, 245, 245);
			RGBA_Bytes borderColor = new RGBA_Bytes(204, 204, 204);
			RGBA_Bytes textColor = new RGBA_Bytes(153, 153, 153);
			ControlButtonViewBase widget = new ControlButtonViewBase(buttonText,
															   this.width,
															   this.height,
															   this.fontSize,
															   this.borderWidth,
															   this.borderRadius,
															   this.padding,
															   textColor,
															   fillColor,
															   borderColor);
			return widget;
		}
	}
}