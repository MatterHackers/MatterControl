/*
Copyright (c) 2016, Lars Brubaker, John Lewin
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
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.DataStorage.ClassicDB;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using MatterHackers.MatterControl.PrinterCommunication;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class PresetsContext
	{
		public ObservableCollection<PrinterSettingsLayer> PresetLayers { get; }
		public PrinterSettingsLayer PersistenceLayer { get; set; }
		public Action<string> SetAsActive { get; set; }
		public Action DeleteLayer { get; set; }

		public NamedSettingsLayers LayerType { get; set; }

		public PresetsContext(ObservableCollection<PrinterSettingsLayer> settingsLayers, PrinterSettingsLayer activeLayer)
		{
			this.PersistenceLayer = activeLayer;
			this.PresetLayers = settingsLayers;
		}
	}

	public class SlicePresetsWindow : SystemWindow
	{
		private static Regex numberMatch = new Regex("\\s*\\(\\d+\\)", RegexOptions.Compiled);

		private PresetsContext presetsContext;
		private PrinterConfig printer;
		private GuiWidget middleRow;
		private InlineTitleEdit inlineTitleEdit;

		public SlicePresetsWindow(PrinterConfig printer, PresetsContext presetsContext)
				: base(641, 481)
		{
			var theme = ApplicationController.Instance.Theme;
			this.presetsContext = presetsContext;
			this.printer = printer;
			this.AlwaysOnTopOfMain = true;
			this.Title = "Slice Presets Editor".Localize();
			this.MinimumSize = new Vector2(640, 480);
			this.AnchorAll();

			var linkButtonFactory = new LinkButtonFactory()
			{
				fontSize = 8,
				textColor = ActiveTheme.Instance.SecondaryAccentColor
			};

			var buttonFactory = ApplicationController.Instance.Theme.ButtonFactory;

			FlowLayoutWidget mainContainer = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				Padding = new BorderDouble(3)
			};
			mainContainer.AnchorAll();

			middleRow = new GuiWidget();
			middleRow.AnchorAll();
			middleRow.AddChild(CreateSliceSettingsWidget(printer, presetsContext.PersistenceLayer));

			inlineTitleEdit = new InlineTitleEdit(presetsContext.PersistenceLayer.Name, theme, boldFont: true);
			inlineTitleEdit.TitleChanged += (s, e) =>
			{
				printer.Settings.SetValue(SettingsKey.layer_name, inlineTitleEdit.Text, presetsContext.PersistenceLayer);
				//ActiveSliceSettings.SettingChanged.CallEvents(null, new StringEventArgs(SettingsKey.layer_name));
			};
			mainContainer.AddChild(inlineTitleEdit);

			mainContainer.AddChild(middleRow);
			mainContainer.AddChild(GetBottomRow(buttonFactory));

			this.AddChild(mainContainer);

			BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
		}

		private GuiWidget CreateSliceSettingsWidget(PrinterConfig printer, PrinterSettingsLayer persistenceLayer)
		{
			var settingsContext = new SettingsContext(
				printer,
				new List<PrinterSettingsLayer>
				{
					persistenceLayer,
					ActiveSliceSettings.Instance.OemLayer,
					ActiveSliceSettings.Instance.BaseLayer
				},
				presetsContext.LayerType);

			return new SliceSettingsWidget(printer, settingsContext, ApplicationController.Instance.Theme)
			{
				ShowControlBar = false
			};
		}

		private FlowLayoutWidget GetBottomRow(TextImageButtonFactory buttonFactory)
		{
			var container = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Stretch,
				Margin = new BorderDouble(top: 3)
			};

			Button duplicateButton = buttonFactory.Generate("Duplicate".Localize());
			duplicateButton.Click += (s, e) =>
			{
				UiThread.RunOnIdle(() =>
				{
					string sanitizedName = numberMatch.Replace(inlineTitleEdit.Text, "").Trim();
					string newProfileName = agg_basics.GetNonCollidingName(sanitizedName, presetsContext.PresetLayers.Select(preset => preset.ValueOrDefault(SettingsKey.layer_name)));

					var clonedLayer = presetsContext.PersistenceLayer.Clone();
					clonedLayer.Name = newProfileName;
					presetsContext.PresetLayers.Add(clonedLayer);

					presetsContext.SetAsActive(clonedLayer.LayerID);
					presetsContext.PersistenceLayer = clonedLayer;

					middleRow.CloseAllChildren();
					middleRow.AddChild(CreateSliceSettingsWidget(printer, clonedLayer));

					inlineTitleEdit.Text = newProfileName;
				});
			};

			Button deleteButton = buttonFactory.Generate("Delete".Localize());
			deleteButton.Click += (s, e) =>
			{
				UiThread.RunOnIdle(() =>
				{
					presetsContext.DeleteLayer();
					this.Close();
				});
			};

			Button closeButton = buttonFactory.Generate("Close".Localize());
			closeButton.Click += (sender, e) =>
			{
				this.CloseOnIdle();
			};

			container.AddChild(duplicateButton);
			container.AddChild(deleteButton);
			container.AddChild(new HorizontalSpacer());
			container.AddChild(closeButton);

			return container;
		}
	}
}
