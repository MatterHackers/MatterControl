/*
Copyright (c) 2018 Lars Brubaker, John Lewin
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
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.ConfigurationPage.PrintLeveling
{
	public class SelectMaterialPage : WizardPage
	{
		public SelectMaterialPage(ISetupWizard setupWizard, string headerText, string instructionsText, string nextButtonText, int extruderIndex, bool showLoadFilamentButton, bool showAlreadyLoadedButton)
			: base(setupWizard, headerText, instructionsText)
		{
			contentRow.AddChild(
				new PresetSelectorWidget(printer, "Material".Localize(), Color.Transparent, NamedSettingsLayers.Material, extruderIndex, theme)
				{
					BackgroundColor = Color.Transparent,
					Margin = new BorderDouble(0, 0, 0, 15)
				});

			NextButton.Text = nextButtonText;

			if (showLoadFilamentButton)
			{
				NextButton.Visible = false;

				var loadFilamentButton = new TextButton("Load Filament".Localize(), theme)
				{
					Name = "Load Filament",
					BackgroundColor = theme.MinimalShade,
				};
				loadFilamentButton.Click += (s, e) =>
				{
					base.setupWizard.MoveNext();
					if(base.setupWizard.Current is WizardPage wizardPage)
					{
						this.DialogWindow.ChangeToPage(wizardPage);
					}
				};

				this.AddPageAction(loadFilamentButton);
			}

			if (showAlreadyLoadedButton)
			{
				NextButton.Visible = false;

				var alreadyLoadedButton = new TextButton("Already Loaded".Localize(), theme)
				{
					Name = "Already Loaded Button",
					BackgroundColor = theme.MinimalShade
				};

				alreadyLoadedButton.Click += (s, e) =>
				{
					switch (extruderIndex)
					{
						case 0:
							printer.Settings.SetValue(SettingsKey.filament_has_been_loaded, "1");
							break;

						case 1:
							printer.Settings.SetValue(SettingsKey.filament_1_has_been_loaded, "1");
							break;
					}

					this.FinishWizard();
				};

				this.AddPageAction(alreadyLoadedButton);
			}
		}
	}
}