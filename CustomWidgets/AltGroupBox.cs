using MatterHackers.Agg;
using MatterHackers.Agg.UI;

namespace MatterHackers.MatterControl
{
	public class AltGroupBox : FlowLayoutWidget
	{
		private GuiWidget groupBoxLabel;
		private RGBA_Bytes borderColor = RGBA_Bytes.Black;
		private GuiWidget clientArea;

		public RGBA_Bytes TextColor
		{
			get
			{
				TextWidget textBox = groupBoxLabel as TextWidget;
				if (textBox != null)
				{
					return textBox.TextColor;
				}
				return RGBA_Bytes.White;
			}
			set
			{
				TextWidget textBox = groupBoxLabel as TextWidget;
				if (textBox != null)
				{
					textBox.TextColor = value;
				}
			}
		}

		public RGBA_Bytes BorderColor
		{
			get
			{
				return this.borderColor;
			}
			set
			{
				this.borderColor = value;
			}
		}

		public GuiWidget ClientArea
		{
			get
			{
				return clientArea;
			}
		}

		public AltGroupBox()
			: this("")
		{
		}

		public AltGroupBox(GuiWidget groupBoxLabel)
			: base(FlowDirection.TopToBottom)
		{
			this.Padding = new BorderDouble(5);
			this.Margin = new BorderDouble(0);
			this.groupBoxLabel = groupBoxLabel;
			this.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
			this.BackgroundColor = ActiveTheme.Instance.TertiaryBackgroundColor;

			if (groupBoxLabel != null)
			{
				groupBoxLabel.Margin = new BorderDouble(0);
				groupBoxLabel.HAnchor = HAnchor.ParentLeftRight;
				base.AddChild(groupBoxLabel);
			}

			clientArea = new GuiWidget(HAnchor.ParentLeftRight, VAnchor.FitToChildren);
			base.AddChild(clientArea);
		}

		public AltGroupBox(string title)
			: this(new TextWidget(title, pointSize: 12))
		{
		}

		public override void AddChild(GuiWidget childToAdd, int indexInChildrenList = -1)
		{
			clientArea.AddChild(childToAdd, indexInChildrenList);
		}

		public override string Text
		{
			get
			{
				if (groupBoxLabel != null)
				{
					return groupBoxLabel.Text;
				}

				return "";
			}
			set
			{
				if (groupBoxLabel != null)
				{
					groupBoxLabel.Text = value;
				}
			}
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			RectangleDouble localBounds = LocalBounds;
			//// bottom
			//graphics2D.Line(localBounds.Left + lineInset, localBounds.Bottom + lineInset, localBounds.Left + Width - lineInset, localBounds.Bottom + lineInset, this.borderColor);
			//// left
			//graphics2D.Line(localBounds.Left + lineInset, localBounds.Bottom + lineInset, localBounds.Left + lineInset, localBounds.Bottom + Height - lineInset, this.borderColor);
			//// right
			//graphics2D.Line(localBounds.Left + Width - lineInset, localBounds.Bottom + lineInset, localBounds.Left + Width - lineInset, localBounds.Bottom + Height - lineInset, this.borderColor);
			//// top
			//graphics2D.Line(localBounds.Left + lineInset, localBounds.Bottom + Height - lineInset, groupBoxLabel.BoundsRelativeToParent.Left - 2, localBounds.Bottom + Height - lineInset, this.borderColor);
			//graphics2D.Line(groupBoxLabel.BoundsRelativeToParent.Right + 2, localBounds.Bottom + Height - lineInset, localBounds.Left + Width - lineInset, localBounds.Bottom + Height - lineInset, this.borderColor);

			base.OnDraw(graphics2D);
		}
	}
}