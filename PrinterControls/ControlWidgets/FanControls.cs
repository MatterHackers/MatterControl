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
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterCommunication;
using System;

namespace MatterHackers.MatterControl.PrinterControls
{
	public class FanControls : ControlWidgetBase
	{
		private EventHandler unregisterEvents;

		private EditableNumberDisplay fanSpeedDisplay;

		private CheckBox toggleSwitch;

		public FanControls(int headingPointSize)
		{
			AltGroupBox fanControlsGroupBox = new AltGroupBox(new TextWidget("Fan".Localize(), pointSize: headingPointSize, textColor: ActiveTheme.Instance.SecondaryAccentColor));

			fanControlsGroupBox.Margin = new BorderDouble(0);
			fanControlsGroupBox.BorderColor = ActiveTheme.Instance.PrimaryTextColor;
			fanControlsGroupBox.HAnchor |= Agg.UI.HAnchor.ParentLeftRight;
			fanControlsGroupBox.VAnchor = Agg.UI.VAnchor.FitToChildren;

			this.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
			this.AddChild(fanControlsGroupBox);

			FlowLayoutWidget leftToRight = new FlowLayoutWidget();

			FlowLayoutWidget fanControlsLayout = new FlowLayoutWidget(FlowDirection.TopToBottom);
			fanControlsLayout.Padding = new BorderDouble(3, 5, 3, 0);
			{
				fanControlsLayout.AddChild(CreateFanControls());
			}

			leftToRight.AddChild(fanControlsLayout);

			this.HAnchor = HAnchor.ParentLeftRight;

			fanSpeedDisplay = new EditableNumberDisplay("{0}%".FormatWith(PrinterConnection.Instance.FanSpeed0To255.ToString()), "100%");
			fanSpeedDisplay.EditComplete += (sender, e) =>
			{
				PrinterConnection.Instance.FanSpeed0To255 = (int)(fanSpeedDisplay.GetValue() * 255.5 / 100);
			};
			leftToRight.AddChild(fanSpeedDisplay);

			fanControlsGroupBox.AddChild(leftToRight);
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}

		private GuiWidget CreateFanControls()
		{
			PrinterConnection.Instance.FanSpeedSet.RegisterEvent(FanSpeedChanged_Event, ref unregisterEvents);

			FlowLayoutWidget leftToRight = new FlowLayoutWidget();
			leftToRight.Padding = new BorderDouble(3, 0, 0, 5);

			//Matt's test editing to add a on/off toggle switch
			bool fanActive = PrinterConnection.Instance.FanSpeed0To255 != 0;

			toggleSwitch = ImageButtonFactory.CreateToggleSwitch(fanActive);
			toggleSwitch.VAnchor = VAnchor.ParentCenter;
			toggleSwitch.CheckedStateChanged += new EventHandler(ToggleSwitch_Click);
			toggleSwitch.Margin = new BorderDouble(5, 0);

			leftToRight.AddChild(toggleSwitch);

			return leftToRight;
		}

		private bool doingDisplayUpdateFromPrinter = false;

		private void FanSpeedChanged_Event(object sender, EventArgs e)
		{
			int printerFanSpeed = PrinterConnection.Instance.FanSpeed0To255;

			fanSpeedDisplay.SetDisplayString("{0}%".FormatWith((int)(printerFanSpeed * 100.5 / 255)));

			doingDisplayUpdateFromPrinter = true;

			if (printerFanSpeed > 0)
			{
				toggleSwitch.Checked = true;
			}
			else
			{
				toggleSwitch.Checked = false;
			}

			doingDisplayUpdateFromPrinter = false;
		}

		private void ToggleSwitch_Click(object sender, EventArgs e)
		{
			if (!doingDisplayUpdateFromPrinter)
			{
				CheckBox toggleSwitch = (CheckBox)sender;
				if (toggleSwitch.Checked)
				{
					PrinterConnection.Instance.FanSpeed0To255 = 255;
				}
				else
				{
					PrinterConnection.Instance.FanSpeed0To255 = 0;
				}
			}
		}
	}
}