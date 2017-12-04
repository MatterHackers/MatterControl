/*
Copyright (c) 2017, Lars Brubaker, John Lewin
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
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl
{
	public class MacroListPage : DialogPage
	{
		public MacroListPage(PrinterSettings printerSettings)
			: base ("Close".Localize())
		{
			this.WindowTitle = "Macro Editor".Localize();
			this.HeaderText = "Macro Presets".Localize() + ":";

			var theme = ApplicationController.Instance.Theme;
			var linkButtonFactory = theme.LinkButtonFactory;

			this.RebuildList(printerSettings, linkButtonFactory);

			Button addMacroButton = textImageButtonFactory.Generate("Add".Localize(), AggContext.StaticData.LoadIcon("fa-plus_16.png", IconColor.Theme));
			addMacroButton.ToolTipText = "Add a new Macro".Localize();
			addMacroButton.Click += (s, e) =>
			{
				this.WizardWindow.ChangeToPage(
					new MacroDetailPage(
						new GCodeMacro()
						{
							Name = "Home All",
							GCode = "G28 ; Home All Axes"
						},
						printerSettings));
			};

			this.AddPageAction(addMacroButton);
		}

		private void RebuildList(PrinterSettings printerSettings, LinkButtonFactory linkButtonFactory)
		{
			this.contentRow.CloseAllChildren();

			if (printerSettings?.Macros != null)
			{
				foreach (GCodeMacro macro in printerSettings.Macros)
				{
					var macroRow = new FlowLayoutWidget
					{
						Margin = new BorderDouble(3, 0, 3, 3),
						HAnchor = HAnchor.Stretch,
						Padding = new BorderDouble(3),
						BackgroundColor = Color.White
					};

					macroRow.AddChild(new TextWidget(GCodeMacro.FixMacroName(macro.Name)));

					macroRow.AddChild(new HorizontalSpacer());

					// You can't use the foreach variable inside the lambda functions directly or it will always be the last item.
					// We make a local variable to create a closure around it to ensure we get the correct instance
					var localMacroReference = macro;

					Button editLink = linkButtonFactory.Generate("edit".Localize());
					editLink.Margin = new BorderDouble(right: 5);
					editLink.Click += (s, e) =>
					{
						this.WizardWindow.ChangeToPage(
							new MacroDetailPage(localMacroReference, printerSettings));
					};
					macroRow.AddChild(editLink);

					Button removeLink = linkButtonFactory.Generate("remove".Localize());
					removeLink.Click += (sender, e) =>
					{
						printerSettings.Macros.Remove(localMacroReference);
						this.RebuildList(printerSettings, linkButtonFactory);
					};
					macroRow.AddChild(removeLink);

					contentRow.AddChild(macroRow);
				}
			}
		}
	}
}