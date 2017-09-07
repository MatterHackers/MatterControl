/*
Copyright (c) 2014, Lars Brubaker
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
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.PrinterControls
{
	public class MacroControls : ControlWidgetBase
	{
		public MacroControls(PrinterConnection printerConnection, int headingPointSize)
		{
			this.AddChild(new MacroControlsWidget(printerConnection, headingPointSize));
		}
	}

	public class MacroControlsWidget : FlowLayoutWidget
	{
		PrinterConnection printerConnection;
		public MacroControlsWidget(PrinterConnection printerConnection, int headingPointSize)
					: base(FlowDirection.TopToBottom)
		{
			this.printerConnection = printerConnection;
			var buttonFactory = ApplicationController.Instance.Theme.HomingButtons;

			this.HAnchor = HAnchor.Stretch;

			// add the widgets to this window
			Button editButton;
			AltGroupBox groupBox = new AltGroupBox(buttonFactory.GenerateGroupBoxLabelWithEdit(new TextWidget("Macros".Localize(), pointSize: headingPointSize, textColor: ActiveTheme.Instance.SecondaryAccentColor), out editButton));
			editButton.Click += (sender, e) =>
			{
				EditMacrosWindow.Show();
			};

			groupBox.BorderColor = ActiveTheme.Instance.PrimaryTextColor;
			groupBox.HAnchor |= Agg.UI.HAnchor.Stretch;
			// make sure the client area will get smaller when the contents get smaller
			groupBox.ClientArea.VAnchor = Agg.UI.VAnchor.Fit;

			FlowLayoutWidget controlRow = new FlowLayoutWidget(Agg.UI.FlowDirection.TopToBottom);
			controlRow.Margin = new BorderDouble(top: 5);
			controlRow.HAnchor = HAnchor.Stretch;
			{
				controlRow.AddChild(GetMacroButtonContainer(buttonFactory));
			}

			groupBox.AddChild(controlRow);
			this.AddChild(groupBox);
		}

		private FlowLayoutWidget GetMacroButtonContainer(TextImageButtonFactory buttonFactory)
		{
			FlowLeftRightWithWrapping macroContainer = new FlowLeftRightWithWrapping();

			TextWidget noMacrosFound = new TextWidget("No macros are currently set up for this printer.".Localize(), pointSize: 10);
			noMacrosFound.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			macroContainer.AddChild(noMacrosFound);
			noMacrosFound.Visible = false;

			if (printerConnection.PrinterSettings?.GetMacros(MacroUiLocation.Controls).Any() != true)
			{
				noMacrosFound.Visible = true;
				return macroContainer;
			}

			foreach (GCodeMacro macro in printerConnection.PrinterSettings.GetMacros(MacroUiLocation.Controls))
			{
				Button macroButton = buttonFactory.Generate(GCodeMacro.FixMacroName(macro.Name));
				macroButton.Margin = new BorderDouble(right: 5);
				macroButton.Click += (s, e) => macro.Run(printerConnection);

				macroContainer.AddChild(macroButton);
			}

			macroContainer.Children.CollectionChanged += (s, e) =>
			{
				if (!this.HasBeenClosed)
				{
					noMacrosFound.Visible = macroContainer.Children.Count == 0;
				}
			};
			
			return macroContainer;
		}
	}
}