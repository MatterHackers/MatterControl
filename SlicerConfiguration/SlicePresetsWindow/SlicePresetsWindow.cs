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

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class SlicePresetsWindow : SystemWindow
	{
		private string presetsKey;
		private SettingsLayer persistenceLayer;
		private NamedSettingsLayers layerType;

		private TextImageButtonFactory buttonFactory;
		private LinkButtonFactory linkButtonFactory;
		private MHTextEditWidget presetNameInput;

		private string configFileExtension = "slice";

		public SlicePresetsWindow(SettingsLayer persistenceLayer, NamedSettingsLayers layerType, string presetsKey)
				: base(641, 481)
		{
			this.AlwaysOnTopOfMain = true;
			this.Title = LocalizedString.Get("Slice Presets Editor");
			this.persistenceLayer = persistenceLayer;
			this.layerType = layerType;
			this.presetsKey = presetsKey;
			this.MinimumSize = new Vector2(640, 480);
			this.AnchorAll();

			linkButtonFactory = new LinkButtonFactory()
			{
				fontSize = 8,
				textColor = ActiveTheme.Instance.SecondaryAccentColor
			};

			buttonFactory = new TextImageButtonFactory()
			{
				normalTextColor = ActiveTheme.Instance.PrimaryTextColor,
				hoverTextColor = ActiveTheme.Instance.PrimaryTextColor,
				disabledTextColor = ActiveTheme.Instance.PrimaryTextColor,
				pressedTextColor = ActiveTheme.Instance.PrimaryTextColor,
				borderWidth = 0
			};

			FlowLayoutWidget mainContainer = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				Padding = new BorderDouble(3)
			};
			mainContainer.AnchorAll();

			mainContainer.AddChild(GetTopRow());
			mainContainer.AddChild(GetMiddleRow());
			mainContainer.AddChild(GetBottomRow());

			this.AddChild(mainContainer);

			BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

		}

		private FlowLayoutWidget GetTopRow()
		{
			FlowLayoutWidget metaContainer = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.ParentLeftRight,
				Padding = new BorderDouble(0, 3)
			};

			FlowLayoutWidget firstRow = new FlowLayoutWidget(hAnchor: HAnchor.ParentLeftRight);

			TextWidget labelText = new TextWidget("Edit Preset:".Localize(), pointSize: 14)
			{
				TextColor = ActiveTheme.Instance.PrimaryTextColor,
				VAnchor = VAnchor.ParentCenter,
				Margin = new BorderDouble(right: 4)
			};

			presetNameInput = new MHTextEditWidget(this.presetsKey);
			presetNameInput.HAnchor = HAnchor.ParentLeftRight;

			firstRow.AddChild(labelText);
			firstRow.AddChild(presetNameInput);

			FlowLayoutWidget secondRow = new FlowLayoutWidget(hAnchor: HAnchor.ParentLeftRight);

			secondRow.AddChild(new GuiWidget(labelText.Width + 4, 1));

			metaContainer.AddChild(firstRow);
			metaContainer.AddChild(secondRow);

			return metaContainer;
		}

		private GuiWidget GetMiddleRow()
		{
			var settings = ActiveSliceSettings.Instance;
			var layerCascade = new List<SettingsLayer> { settings.BaseLayer, settings.OemLayer, persistenceLayer };

			var settingsWidget = new SliceSettingsWidget(layerCascade, layerType);
			settingsWidget.settingsControlBar.Visible = false;

			return settingsWidget;
		}

		private FlowLayoutWidget GetBottomRow()
		{
			FlowLayoutWidget container = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.ParentLeftRight,
				Margin = new BorderDouble(top: 3)
			};

			Button saveButton = buttonFactory.Generate("Save".Localize());
			saveButton.Click += (s, e) =>
			{
				throw new NotImplementedException();
			};

			Button duplicateButton = buttonFactory.Generate("Duplicate".Localize());
			duplicateButton.Click += (s, e) =>
			{
				UiThread.RunOnIdle(() =>
				{
					// duplicatePresets_Click
					// TODO: copy existing dictionary to new named instance
					throw new NotImplementedException();
				});
			};

			Button importButton = buttonFactory.Generate("Import".Localize());
			importButton.Click += (s, e) =>
			{
				throw new NotImplementedException();
			};

			Button exportButton = buttonFactory.Generate("Export".Localize());
			exportButton.Click += (s, e) => UiThread.RunOnIdle(SaveAs);

			Button closeButton = buttonFactory.Generate("Close".Localize());
			closeButton.Click += (sender, e) =>
			{
				UiThread.RunOnIdle(this.Close);
			};

			container.AddChild(saveButton);

			//Only show duplicate/import/export buttons if setting has been saved.
			if (false)
			{
				container.AddChild(duplicateButton);
				container.AddChild(importButton);
				container.AddChild(exportButton);
			}

			container.AddChild(new HorizontalSpacer());
			container.AddChild(closeButton);

			return container;
		}

		private void SaveAs()
		{
			FileDialog.SaveFileDialog(
				new SaveFileDialogParams("Save Slice Preset|*." + configFileExtension)
				{
					FileName = presetNameInput.Text
				},
				(saveParams) =>
				{
					throw new NotImplementedException();

					if (!string.IsNullOrEmpty(saveParams.FileName))
					{
						// TODO: If we stil want this functionality, it should be moved to a common helper method off of SettingsLayer and resused throughout
						//
						// GenerateConfigFile(saveParams.FileName) ...

						//List<string> configFileAsList = new List<string>();

						//foreach (KeyValuePair<String, SliceSetting> setting in windowController.ActivePresetLayer.settingsDictionary)
						//{
						//	string settingString = string.Format("{0} = {1}", setting.Value.Name, setting.Value.Value);
						//	configFileAsList.Add(settingString);
						//}
						//string configFileAsString = string.Join("\n", configFileAsList.ToArray());

						//FileStream fs = new FileStream(fileName, FileMode.Create);
						//StreamWriter sw = new System.IO.StreamWriter(fs);
						//sw.Write(configFileAsString);
						//sw.Close();
					}
				});
		}
	}
}