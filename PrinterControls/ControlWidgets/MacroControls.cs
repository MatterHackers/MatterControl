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

using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.PrinterControls
{
	public class MacroControls : ControlWidgetBase
	{
		public MacroControls(PrinterConfig printer, int headingPointSize)
		{
			this.AddChild(new MacroControlsWidget(printer, headingPointSize));
		}
	}

	public class MacroControlsWidget : FlowLayoutWidget
	{
		private PrinterConfig printer;
		public MacroControlsWidget(PrinterConfig printer, int headingPointSize)
					: base(FlowDirection.TopToBottom)
		{
			this.printer = printer;
			this.HAnchor = HAnchor.Stretch;

			var buttonFactory = ApplicationController.Instance.Theme.HomingButtons;

			// add the widgets to this window
			Button editButton = buttonFactory.GenerateIconButton(AggContext.StaticData.LoadIcon("icon_edit.png", 16, 16, IconColor.Theme));
			editButton.Click += (s, e) =>
			{
				DialogWindow.Show(new MacroListPage(printer.Settings));
			};

			this.AddChild(
				new SectionWidget(
					"Macros".Localize(),
					ActiveTheme.Instance.PrimaryAccentColor,
					GetMacroButtonContainer(buttonFactory),
					editButton));
		}

		private FlowLayoutWidget GetMacroButtonContainer(TextImageButtonFactory buttonFactory)
		{
			var macroContainer = new FlowLeftRightWithWrapping();

			var noMacrosFound = new TextWidget("No macros are currently set up for this printer.".Localize(), pointSize: 10)
			{
				TextColor = ActiveTheme.Instance.PrimaryTextColor,
			};
			macroContainer.AddChild(noMacrosFound);

			if (printer.Settings?.GetMacros(MacroUiLocation.Controls).Any() != true)
			{
				noMacrosFound.Visible = true;
				return macroContainer;
			}

			foreach (GCodeMacro macro in printer.Settings.GetMacros(MacroUiLocation.Controls))
			{
				Button macroButton = buttonFactory.Generate(GCodeMacro.FixMacroName(macro.Name));
				macroButton.Margin = new BorderDouble(right: 5);
				macroButton.Click += (s, e) => macro.Run(printer.Connection);

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