/*
Copyright (c) 2019, Lars Brubaker, John Lewin
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
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ConfigurationPage.PrintLeveling;
using MatterHackers.MatterControl.PrinterControls;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl
{
	public class FilamentSetupWizard : IStagedSetupWizard
	{
		public FilamentSetupWizard(PrinterConfig printer, ThemeConfig theme)
		{
			this.Stages = Enumerable.Range(0, printer.Settings.Helpers.NumberOfTools()).Select(i =>
			{
				return new LoadFilamentWizard(printer, extruderIndex: i, showAlreadyLoadedButton: true);
			}).ToList();

			this.HomePageGenerator = () =>
			{
				var homePage = new WizardSummaryPage()
				{
					HeaderText = "Load Filament".Localize()
				};

				homePage.ContentRow.AddChild(
					new WrappedTextWidget(
						@"Select the hotend on the left to continue".Replace("\r\n", "\n"),
						pointSize: theme.DefaultFontSize,
						textColor: theme.TextColor));

				return homePage;
			};
		}

		public string Title { get; } = "Load Filament".Localize();

		public Vector2 WindowSize { get; } = new Vector2(1200, 700);

		public IEnumerable<ISetupWizard> Stages { get; }

		public Func<DialogPage> HomePageGenerator { get; }

		public static bool SetupRequired(PrinterConfig printer)
		{
			return SetupRequired(printer, 0)
				|| SetupRequired(printer, 1);
		}

		public static bool SetupRequired(PrinterConfig printer, int extruderIndex)
		{
			string filamentKey;

			switch (extruderIndex)
			{
				case 0:
					filamentKey = SettingsKey.filament_has_been_loaded;
					break;

				case 1:
					filamentKey = SettingsKey.filament_1_has_been_loaded;
					break;

				default:
					// TODO: Seems like more than index 0/1 should be supported but SettingsKeys do not exist
					return false;
			}

			return printer.Settings.GetValue<int>(SettingsKey.extruder_count) > 1
				&& !printer.Settings.GetValue<bool>(filamentKey);
		}
	}
}