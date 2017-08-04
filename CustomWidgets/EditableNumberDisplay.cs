using System;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;

namespace MatterHackers.MatterControl
{
	public class EditableNumberDisplay : FlowLayoutWidget
	{
		protected ClickWidget clickableValueContainer;
		protected MHNumberEdit numberInputField;
		protected TextWidget valueDisplay;

		public event EventHandler EditComplete;

		public event EventHandler EditEnabled;

		public EditableNumberDisplay(string startingValue, string largestPossibleValue)
			: base(Agg.UI.FlowDirection.LeftToRight)
		{
			this.Margin = new BorderDouble(3, 0);
			this.VAnchor = VAnchor.ParentCenter;

			clickableValueContainer = new ClickWidget();
			clickableValueContainer.VAnchor = VAnchor.ParentBottomTop;
			clickableValueContainer.Cursor = Cursors.Hand;
			clickableValueContainer.BorderWidth = 1;
			clickableValueContainer.BorderColor = new RGBA_Bytes(255, 255, 255, 140);

			clickableValueContainer.MouseEnterBounds += (sender, e) =>
			{
				clickableValueContainer.BorderWidth = 2;
				clickableValueContainer.BorderColor = new RGBA_Bytes(255, 255, 255, 255);
			};

			clickableValueContainer.MouseLeaveBounds += (sender, e) =>
			{
				clickableValueContainer.BorderWidth = 1;
				clickableValueContainer.BorderColor = new RGBA_Bytes(255, 255, 255, 140);
			};

			valueDisplay = new TextWidget(largestPossibleValue, pointSize: 12);
			valueDisplay.VAnchor = VAnchor.ParentCenter;
			valueDisplay.HAnchor = HAnchor.ParentLeft;
			valueDisplay.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			valueDisplay.Margin = new BorderDouble(6);

			clickableValueContainer.Click += editField_Click;

			clickableValueContainer.AddChild(valueDisplay);
			clickableValueContainer.SetBoundsToEncloseChildren();
			valueDisplay.Text = startingValue;

			numberInputField = new MHNumberEdit(0, pixelWidth: 40, allowDecimals: true);
			numberInputField.VAnchor = VAnchor.ParentCenter;
			numberInputField.Margin = new BorderDouble(left: 6);
			numberInputField.Visible = false;

			// This is a hack to make sure the control is tall enough.
			// TODO: This hack needs a unit test and then pass and then remove this line.
			this.MinimumSize = new VectorMath.Vector2(0, numberInputField.Height);

			numberInputField.ActuallNumberEdit.EnterPressed += new KeyEventHandler(ActuallNumberEdit_EnterPressed);

			numberInputField.KeyDown += (sender, e) =>
			{
				if (e.KeyCode == Keys.Escape)
				{
					clickableValueContainer.Visible = true;
					numberInputField.Visible = false;
				}
			};

			this.AddChild(clickableValueContainer);
			this.AddChild(numberInputField);
		}

		private void editField_Click(object sender, EventArgs mouseEvent)
		{
			clickableValueContainer.Visible = false;
			numberInputField.Visible = true;

			// This is trying to get all the numbers to the left of the decimal. We could do a better
			// job of finding the number. 6546431321654
			string displayString = valueDisplay.Text.Split('.')[0];
			if (displayString != null && displayString != "")
			{
				double displayStringAsValue;
				double.TryParse(displayString, out displayStringAsValue);
				numberInputField.ActuallNumberEdit.Value = displayStringAsValue;
			}

			numberInputField.ActuallNumberEdit.InternalNumberEdit.Focus();
			numberInputField.ActuallNumberEdit.InternalNumberEdit.SelectAll();
			OnEditEnabled();
		}

		public void OnEditEnabled()
		{
			EditEnabled?.Invoke(this, null);
		}

		public void OnEditComplete()
		{
			EditComplete?.Invoke(this, null);
		}

		private void setButton_Click(object sender, EventArgs mouseEvent)
		{
			OnEditComplete();
		}

		private void ActuallNumberEdit_EnterPressed(object sender, KeyEventArgs keyEvent)
		{
			OnEditComplete();
		}

		public void SetDisplayString(string displayString)
		{
			valueDisplay.Text = displayString;
			clickableValueContainer.Visible = true;
			numberInputField.Visible = false;
		}

		public double GetValue()
		{
			return numberInputField.ActuallNumberEdit.Value;
		}
	}
}