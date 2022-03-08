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
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.VectorMath;
using Newtonsoft.Json;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class SlicePresetsPage : DialogPage
	{
		private static Regex numberMatch = new Regex("\\s*\\(\\d+\\)", RegexOptions.Compiled);

		private PresetsContext presetsContext;
		private PrinterConfig printer;

		public SlicePresetsPage(PrinterConfig printer, PresetsContext presetsContext, bool showExport)
			: base("Close".Localize())
		{
			this.presetsContext = presetsContext;
			this.printer = printer;
			this.AlwaysOnTopOfMain = true;

			this.WindowTitle = "Slice Presets Editor".Localize();
			this.WindowSize = new Vector2(640 * DeviceScale, 480 * DeviceScale);
			this.AnchorAll();

			this.headerRow.Visible = false;
			this.contentRow.Padding = 0;

			contentRow.BackgroundColor = Color.Transparent;

			var inlineNameEdit = new InlineStringEdit(presetsContext.PersistenceLayer.Name,
				theme,
				presetsContext.LayerType.ToString() + " Name",
				boldFont: true,
				emptyText: "Setting Name".Localize());

			inlineNameEdit.ValueChanged += (s, e) =>
			{
				printer.Settings.SetValue(SettingsKey.layer_name, inlineNameEdit.Text, presetsContext.PersistenceLayer);
			};
			inlineNameEdit.Closed += (s, e) =>
			{
				printer.Settings.SetValue(SettingsKey.layer_name, inlineNameEdit.Text, presetsContext.PersistenceLayer);
			};
			contentRow.AddChild(inlineNameEdit);

			var sliceSettingsWidget = CreateSliceSettingsWidget(printer, presetsContext.PersistenceLayer);
			contentRow.AddChild(sliceSettingsWidget);

			var duplicateButton = theme.CreateDialogButton("Duplicate".Localize());
			duplicateButton.Click += (s, e) =>
			{
				string sanitizedName = numberMatch.Replace(inlineNameEdit.Text, "").Trim();
				string newProfileName = agg_basics.GetNonCollidingName(sanitizedName, new HashSet<string>(presetsContext.PresetLayers.Select(preset => preset.ValueOrDefault(SettingsKey.layer_name))));

				var clonedLayer = presetsContext.PersistenceLayer.Clone();
				clonedLayer.Name = newProfileName;
				presetsContext.PresetLayers.Add(clonedLayer);

				presetsContext.SetAsActive(clonedLayer.LayerID);
				presetsContext.PersistenceLayer = clonedLayer;

				sliceSettingsWidget.Close();
				sliceSettingsWidget = CreateSliceSettingsWidget(printer, clonedLayer);
				contentRow.AddChild(sliceSettingsWidget);

				inlineNameEdit.Text = newProfileName;
			};

			this.AddPageAction(duplicateButton);

			if (showExport)
			{
				var exportButton = theme.CreateDialogButton("Export".Localize());
				exportButton.Click += (s, e) =>
				{
					// show a system save dialog
					AggContext.FileDialogs.SaveFileDialog(
							new SaveFileDialogParams("MatterControl Settings Export|*.material", title: "Export Material Setting")
							{
								FileName = presetsContext.PersistenceLayer.Name
							},
							(saveParams) =>
							{
								// save these settings to a .printer file
								try
								{
									if (!string.IsNullOrWhiteSpace(saveParams.FileName))
									{
										// create an empyt profile
										var materialSettings = new PrinterSettings();
										// copy just this material setting to it
										materialSettings.MaterialLayers.Add(presetsContext.PersistenceLayer.Clone());
										// save it
										File.WriteAllText(saveParams.FileName, JsonConvert.SerializeObject(materialSettings, Formatting.Indented));
									}
								}
								catch (Exception e2)
								{
									UiThread.RunOnIdle(() =>
									{
										StyledMessageBox.ShowMessageBox(e2.Message, "Couldn't save file".Localize());
									});
								}
							});
				};
			
				this.AddPageAction(exportButton);
			}

			var deleteButton = theme.CreateDialogButton("Delete".Localize());
			deleteButton.Click += (s, e) =>
			{
				presetsContext.DeleteLayer();
				this.DialogWindow.Close();
			};
			this.AddPageAction(deleteButton);
		}

		private GuiWidget CreateSliceSettingsWidget(PrinterConfig printer, PrinterSettingsLayer persistenceLayer)
		{
			var settingsContext = new SettingsContext(
				printer,
				new List<PrinterSettingsLayer>
				{
					persistenceLayer,
					printer.Settings.OemLayer,
					printer.Settings.BaseLayer
				},
				presetsContext.LayerType);

			return new SliceSettingsWidget(printer, settingsContext, theme)
			{
				ShowControlBar = false
			};
		}

		public override void OnLoad(EventArgs args)
		{
			this.DialogWindow.Padding = 0;
			footerRow.Padding = theme.DefaultContainerPadding;
			footerRow.Margin = 0;
			footerRow.Border = new BorderDouble(top: 1);
			footerRow.BorderColor = theme.TabBarBackground;
			base.OnLoad(args);
		}
	}
}
