/*
Copyright (c) 2018, Lars Brubaker, John Lewin
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
using System.Collections.Generic;
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrinterCommunication.Io;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.ConfigurationPage.PrintLeveling
{
	public class FindBedHeight : PrinterSetupWizardPage
	{
		private Vector3 lastReportedPosition;
		private List<ProbePosition> probePositions;
		private int probePositionsBeingEditedIndex;
		private double moveAmount;

		protected JogControls.MoveButton zPlusControl;
		protected JogControls.MoveButton zMinusControl;
		private RunningInterval runningInterval;

		public FindBedHeight(PrinterSetupWizard context, string pageDescription, string setZHeightCoarseInstruction1, string setZHeightCoarseInstruction2, double moveDistance,
			List<ProbePosition> probePositions, int probePositionsBeingEditedIndex)
			: base(context, pageDescription, setZHeightCoarseInstruction1)
		{
			this.probePositions = probePositions;
			this.moveAmount = moveDistance;
			this.lastReportedPosition = printer.Connection.LastReportedPosition;
			this.probePositionsBeingEditedIndex = probePositionsBeingEditedIndex;

			GuiWidget spacer = new GuiWidget(15, 15);
			contentRow.AddChild(spacer);

			FlowLayoutWidget zButtonsAndInfo = new FlowLayoutWidget();
			zButtonsAndInfo.HAnchor |= Agg.UI.HAnchor.Center;
			FlowLayoutWidget zButtons = CreateZButtons();
			zButtonsAndInfo.AddChild(zButtons);

			zButtonsAndInfo.AddChild(new GuiWidget(15, 10));

			//textFields
			TextWidget zPosition = new TextWidget("Z: 0.0      ", pointSize: 12, textColor: theme.TextColor)
			{
				VAnchor = VAnchor.Center,
				Margin = new BorderDouble(10, 0),
			};

			runningInterval = UiThread.SetInterval(() =>
			{
				Vector3 destinationPosition = printer.Connection.CurrentDestination;
				zPosition.Text = "Z: {0:0.00}".FormatWith(destinationPosition.Z);
			}, .3);

			zButtonsAndInfo.AddChild(zPosition);

			contentRow.AddChild(zButtonsAndInfo);

			contentRow.AddChild(
				this.CreateTextField(setZHeightCoarseInstruction2));
		}

		public override void PageIsBecomingActive()
		{
			// always make sure we don't have print leveling turned on
			PrintLevelingStream.AllowLeveling = false;
			NextButton.ToolTipText = string.Format("[{0}]", "Right Arrow".Localize());

			base.PageIsBecomingActive();
		}

		public override void OnLoad(EventArgs args)
		{
			this.DialogWindow.KeyDown += TopWindowKeyDown;
			base.OnLoad(args);
		}

		public override void OnClosed(EventArgs e)
		{
			// Unregister listeners
			UiThread.ClearInterval(runningInterval);
			this.DialogWindow.KeyDown -= TopWindowKeyDown;

			base.OnClosed(e);
		}

		public override void PageIsBecomingInactive()
		{
			this.Parents<SystemWindow>().First().KeyDown -= TopWindowKeyDown;
			probePositions[probePositionsBeingEditedIndex].position = printer.Connection.LastReportedPosition;
			base.PageIsBecomingInactive();

			NextButton.ToolTipText = "";
		}

		private FlowLayoutWidget CreateZButtons()
		{
			FlowLayoutWidget zButtons = JogControls.CreateZButtons(printer, 4, out zPlusControl, out zMinusControl, new PrinterControls.XYZColors(theme), theme, true);

			// set these to 0 so the button does not do any movements by default (we will handle the movement on our click callback)
			zPlusControl.MoveAmount = 0;
			zPlusControl.ToolTipText += string.Format(" [{0}]", "Up Arrow".Localize());
			zPlusControl.Click += zPlusControl_Click;

			zMinusControl.MoveAmount = 0;
			zMinusControl.ToolTipText += string.Format(" [{0}]", "Down Arrow".Localize());
			zMinusControl.Click += zMinusControl_Click;
			return zButtons;
		}

		public void TopWindowKeyDown(object s, KeyEventArgs keyEvent)
		{
			switch(keyEvent.KeyCode)
			{
				case Keys.Up:
					zPlusControl_Click(null, null);
					NextButton.Enabled = true;
					break;

				case Keys.Down:
					zMinusControl_Click(null, null);
					NextButton.Enabled = true;
					break;

				case Keys.Right:
					if (NextButton.Enabled)
					{
						UiThread.RunOnIdle(() => NextButton.InvokeClick());
					}
					break;
			}

			base.OnKeyDown(keyEvent);
		}

		private void zMinusControl_Click(object sender, EventArgs mouseEvent)
		{
			printer.Connection.MoveRelative(PrinterConnection.Axis.Z, -moveAmount, printer.Settings.Helpers.ManualMovementSpeeds().Z);
			printer.Connection.ReadPosition();
		}

		private void zPlusControl_Click(object sender, EventArgs mouseEvent)
		{
			printer.Connection.MoveRelative(PrinterConnection.Axis.Z, moveAmount, printer.Settings.Helpers.ManualMovementSpeeds().Z);
			printer.Connection.ReadPosition();
		}
	}
}