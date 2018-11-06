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
	public class SelectMaterialPage : PrinterSetupWizardPage
	{
		public SelectMaterialPage(PrinterSetupWizard context, string headerText, string instructionsText, string nextButtonText, bool onlyLoad)
			: base(context, headerText, instructionsText)
		{
			contentRow.AddChild(
				new PresetSelectorWidget(printer, "Material".Localize(), Color.Transparent, NamedSettingsLayers.Material, theme)
				{
					BackgroundColor = Color.Transparent,
					Margin = new BorderDouble(0, 0, 0, 15)
				});

			NextButton.Text = nextButtonText;

			if (onlyLoad)
			{
			}
			else
			{
				NextButton.Visible = false;

				contentRow.AddChild(this.CreateTextField("Optionally, click below to get help loading this material".Localize() + ":"));

				var loadFilamentButton = new TextButton("Load Filament".Localize(), theme)
				{
					Name = "Load Filament",
					BackgroundColor = theme.MinimalShade,
					VAnchor = Agg.UI.VAnchor.Absolute,
					HAnchor = Agg.UI.HAnchor.Fit | Agg.UI.HAnchor.Left,
					Margin = new BorderDouble(10, 0, 0, 15)
				};
				loadFilamentButton.Click += (s, e) =>
				{
					wizardContext.ShowNextPage(this.DialogWindow);
				};

				contentRow.AddChild(loadFilamentButton);

				var selectButton = new TextButton("Select".Localize(), theme)
				{
					Name = "Already Loaded Button",
					BackgroundColor = theme.MinimalShade
				};

				selectButton.Click += (s, e) =>
				{
					this.DialogWindow.CloseOnIdle();
					printer.Settings.SetValue(SettingsKey.filament_has_been_loaded, "1");
				};

				this.AddPageAction(selectButton);
			}
		}
	}
}