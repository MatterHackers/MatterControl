/*
Copyright (c) 2018, John Lewin
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ConfigurationPage;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.SettingsManagement;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl
{
	public partial class UpdateSettingsPage : DialogPage
	{
		private PrinterConfig printer;

		public UpdateSettingsPage(PrinterConfig printer)
			: base("Close".Localize())
		{
			this.printer = printer;
			this.AlwaysOnTopOfMain = true;
			this.WindowTitle = this.HeaderText = "Update Settings".Localize();
			this.WindowSize = new Vector2(700 * GuiWidget.DeviceScale, 600 * GuiWidget.DeviceScale);

			contentRow.Padding = theme.DefaultContainerPadding;
			contentRow.Padding = 0;
			contentRow.BackgroundColor = Color.Transparent;
			GuiWidget settingsColumn;

			var settingsAreaScrollBox = new ScrollableWidget(true);
			settingsAreaScrollBox.ScrollArea.HAnchor |= HAnchor.Stretch;
			settingsAreaScrollBox.AnchorAll();
			settingsAreaScrollBox.BackgroundColor = theme.MinimalShade;
			contentRow.AddChild(settingsAreaScrollBox);

			settingsColumn = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.MaxFitOrStretch
			};

			settingsAreaScrollBox.AddChild(settingsColumn);

			if (ProfileManager.GetOemSettingsNeedingUpdate(printer).Any())
			{
				AddUpgradeInfoPannel(settingsColumn);
			}
			else
			{
				settingsColumn.AddChild(new WrappedTextWidget("No setting currently need to be updated.".Localize(), pointSize: 11)
				{
					Margin = new BorderDouble(0, 5),
					TextColor = theme.TextColor
				});
			}

			// Enforce consistent SectionWidget spacing and last child borders
			foreach (var section in settingsColumn.Children<SectionWidget>())
			{
				section.Margin = new BorderDouble(0, 10, 0, 0);

				if (section.ContentPanel.Children.LastOrDefault() is SettingsItem lastRow)
				{
					// If we're in a contentPanel that has SettingsItems...

					// Clear the last items bottom border
					lastRow.Border = lastRow.Border.Clone(bottom: 0);

					// Set a common margin on the parent container
					section.ContentPanel.Margin = new BorderDouble(2, 0);
				}
			}
		}

		private async void AddUpgradeInfoPannel(GuiWidget generalPanel)
		{
			generalPanel.AddChild(new WrappedTextWidget(@"The following settings have had their default values changed and should be updated.
Updating the default will not change any other overrides that you may have applied.".Localize(), pointSize: 11)
			{
				Margin = new BorderDouble(5, 15),
				TextColor = theme.TextColor
			});

			int tabIndex = 0;

			var make = printer.Settings.GetValue(SettingsKey.make);
			var model = printer.Settings.GetValue(SettingsKey.model);
			var serverOemSettings = await ProfileManager.LoadOemSettingsAsync(OemSettings.Instance.OemProfiles[make][model],
				make,
				model);

			var oemPrinter = new PrinterConfig(serverOemSettings);

			foreach (var setting in ProfileManager.GetOemSettingsNeedingUpdate(printer))
			{
				void AddSetting(PrinterConfig printer, string description, string key, Color overlay)
				{
					var oldUnder = new GuiWidget()
					{
						HAnchor = HAnchor.Stretch,
						VAnchor = VAnchor.Fit
					};
					var oldTopToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom)
					{
						HAnchor = HAnchor.Stretch
					};

					var settingsContext = new SettingsContext(printer, null, NamedSettingsLayers.OEMSettings);

					oldTopToBottom.AddChild(SliceSettingsTabView.CreateItemRow(
						PrinterSettings.SettingsData[key],
						settingsContext,
						printer,
						theme,
						ref tabIndex));
					var oldCover = new GuiWidget()
					{
						BackgroundColor = overlay,
						HAnchor = HAnchor.Stretch,
						VAnchor = VAnchor.Stretch
					};
					oldCover.AddChild(new TextWidget(description, pointSize: 11)
					{
						HAnchor = HAnchor.Center,
						VAnchor = VAnchor.Center,
						BackgroundColor = theme.BackgroundColor,
						Margin = new BorderDouble(0, 5),
						TextColor = theme.TextColor
					});
					generalPanel.AddChild(oldUnder).AddChild(oldTopToBottom);
					oldUnder.AddChild(oldCover);
				}

				AddSetting(printer, "Current Default".Localize(), setting.key, theme.SlightShade);
				AddSetting(oemPrinter, "New Default".Localize(), setting.key, Color.Transparent);

				var buttonContainer = new FlowLayoutWidget(FlowDirection.RightToLeft)
				{
					HAnchor = HAnchor.Stretch,
					Margin = new BorderDouble(0, 25, 0, 3),
					Border = new BorderDouble(0, 1, 0, 0),
					BorderColor = theme.MinimalShade,
				};

				generalPanel.AddChild(buttonContainer);
				buttonContainer.AddChild(new TextButton("Update Setting".Localize(), theme)
				{
					Margin = new BorderDouble(0, 3, 20, 0),
				});
			}
		}

		private void AddSettingsRow(GuiWidget widget, GuiWidget container)
		{
			container.AddChild(widget);
			widget.Padding = widget.Padding.Clone(right: 10);
		}
	}
}
