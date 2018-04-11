/*
Copyright (c) 2018, Lars Brubaker
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
using MatterHackers.MatterControl.PrinterCommunication.Io;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MatterHackers.MatterControl.ConfigurationPage.PrintLeveling
{
	public class FindBedHeight : InstructionsPage
	{
		private Vector3 lastReportedPosition;
		private List<ProbePosition> probePositions;
		int probePositionsBeingEditedIndex;
		private double moveAmount;

		protected JogControls.MoveButton zPlusControl;
		protected JogControls.MoveButton zMinusControl;
		protected WizardControl container;

		public FindBedHeight(PrinterConfig printer, WizardControl container, string pageDescription, string setZHeightCoarseInstruction1, string setZHeightCoarseInstruction2, double moveDistance, 
			List<ProbePosition> probePositions, int probePositionsBeingEditedIndex, ThemeConfig theme)
			: base(printer, pageDescription, setZHeightCoarseInstruction1, theme)
		{
			this.container = container;
			this.probePositions = probePositions;
			this.moveAmount = moveDistance;
			this.lastReportedPosition = printer.Connection.LastReportedPosition;
			this.probePositionsBeingEditedIndex = probePositionsBeingEditedIndex;

			GuiWidget spacer = new GuiWidget(15, 15);
			topToBottomControls.AddChild(spacer);

			FlowLayoutWidget zButtonsAndInfo = new FlowLayoutWidget();
			zButtonsAndInfo.HAnchor |= Agg.UI.HAnchor.Center;
			FlowLayoutWidget zButtons = CreateZButtons();
			zButtonsAndInfo.AddChild(zButtons);

			zButtonsAndInfo.AddChild(new GuiWidget(15, 10));

			//textFields
			TextWidget zPosition = new TextWidget("Z: 0.0      ", pointSize: 12, textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				VAnchor = VAnchor.Center,
				Margin = new BorderDouble(10, 0),
			};

			UiThread.SetInterval(() =>
			{
				Vector3 destinationPosition = printer.Connection.CurrentDestination;
				zPosition.Text = "Z: {0:0.00}".FormatWith(destinationPosition.Z);
			}, .3, () => !HasBeenClosed);

			zButtonsAndInfo.AddChild(zPosition);

			topToBottomControls.AddChild(zButtonsAndInfo);

			AddTextField(setZHeightCoarseInstruction2, 10, theme);
		}

		public override void PageIsBecomingActive()
		{
			// always make sure we don't have print leveling turned on
			PrintLevelingStream.AllowLeveling = false;

			base.PageIsBecomingActive();
			this.Parents<SystemWindow>().First().KeyDown += TopWindowKeyDown;

			container.nextButton.ToolTipText = "[Right Arrow]".Localize();
		}

		public override void PageIsBecomingInactive()
		{
			this.Parents<SystemWindow>().First().KeyDown -= TopWindowKeyDown;
			probePositions[probePositionsBeingEditedIndex].position = printer.Connection.LastReportedPosition;
			base.PageIsBecomingInactive();

			container.nextButton.ToolTipText = "";
		}

		private FlowLayoutWidget CreateZButtons()
		{
			FlowLayoutWidget zButtons = JogControls.CreateZButtons(printer, Color.White, 4, out zPlusControl, out zMinusControl, true);
			// set these to 0 so the button does not do any movements by default (we will handle the movement on our click callback)
			zPlusControl.MoveAmount = 0;
			zPlusControl.ToolTipText += " [Up Arrow]".Localize();
			zPlusControl.Click += zPlusControl_Click;

			zMinusControl.MoveAmount = 0;
			zMinusControl.ToolTipText += " [Down Arrow]".Localize();
			zMinusControl.Click += zMinusControl_Click;
			return zButtons;
		}

		public void TopWindowKeyDown(object s, KeyEventArgs keyEvent)
		{
			switch(keyEvent.KeyCode)
			{
				case Keys.Up:
					zPlusControl_Click(null, null);
					container.nextButton.Enabled = true;
					break;

				case Keys.Down:
					zMinusControl_Click(null, null);
					container.nextButton.Enabled = true;
					break;

				case Keys.Right:
					if (container.nextButton.Enabled)
					{
						UiThread.RunOnIdle(() => container.nextButton.OnClick(null));
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