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
using MatterHackers.MatterControl.FieldValidation;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.IO;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class SlicePresetDetailWidget : GuiWidget
	{
		private TextImageButtonFactory buttonFactory = new TextImageButtonFactory();
		private LinkButtonFactory linkButtonFactory = new LinkButtonFactory();
		private SlicePresetsWindow windowController;
		private TextWidget presetNameError;
		private MHTextEditWidget presetNameInput;

		private Button savePresetButton;
		private Button duplicatePresetButton;
		private Button importPresetButton;
		private Button exportPresetButton;

		private int tabIndexForItem = 0;

		public SlicePresetDetailWidget(SlicePresetsWindow windowController)
		{
			this.windowController = windowController;
			this.AnchorAll();
			if (this.windowController.ActivePresetLayer == null)
			{
				initSlicePreset();
			}

			linkButtonFactory.fontSize = 8;
			linkButtonFactory.textColor = ActiveTheme.Instance.SecondaryAccentColor;

			buttonFactory = new TextImageButtonFactory();
			buttonFactory.normalTextColor = ActiveTheme.Instance.PrimaryTextColor;
			buttonFactory.hoverTextColor = ActiveTheme.Instance.PrimaryTextColor;
			buttonFactory.disabledTextColor = ActiveTheme.Instance.PrimaryTextColor;
			buttonFactory.pressedTextColor = ActiveTheme.Instance.PrimaryTextColor;
			buttonFactory.borderWidth = 0;

			AddElements();
			AddHandlers();
		}

		private void AddHandlers()
		{
			savePresetButton.Click += new EventHandler(savePresets_Click);
			duplicatePresetButton.Click += new EventHandler(duplicatePresets_Click);
			exportPresetButton.Click += new EventHandler(exportPresets_Click);
		}

		private void AddElements()
		{
			FlowLayoutWidget mainContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
			mainContainer.Padding = new BorderDouble(3);
			mainContainer.AnchorAll();

			mainContainer.AddChild(GetTopRow());
			mainContainer.AddChild(GetMiddleRow());
			mainContainer.AddChild(GetBottomRow());

			this.AddChild(mainContainer);
		}

		private FlowLayoutWidget GetTopRow()
		{
			FlowLayoutWidget metaContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
			metaContainer.HAnchor = HAnchor.ParentLeftRight;
			metaContainer.Padding = new BorderDouble(0, 3);

			FlowLayoutWidget firstRow = new FlowLayoutWidget();
			firstRow.HAnchor = HAnchor.ParentLeftRight;

			TextWidget labelText = new TextWidget("Edit Preset:".FormatWith(windowController.filterLabel.Localize()), pointSize: 14);
			labelText.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			labelText.VAnchor = VAnchor.ParentCenter;
			labelText.Margin = new BorderDouble(right: 4);

			presetNameInput = new MHTextEditWidget(windowController.ActivePresetLayer.settingsCollectionData.Name);
			presetNameInput.HAnchor = HAnchor.ParentLeftRight;

			firstRow.AddChild(labelText);
			firstRow.AddChild(presetNameInput);

			presetNameError = new TextWidget("This is an error message", 0, 0, 10);
			presetNameError.TextColor = RGBA_Bytes.Red;
			presetNameError.HAnchor = HAnchor.ParentLeftRight;
			presetNameError.Margin = new BorderDouble(top: 3);
			presetNameError.Visible = false;

			FlowLayoutWidget secondRow = new FlowLayoutWidget();
			secondRow.HAnchor = HAnchor.ParentLeftRight;

			secondRow.AddChild(new GuiWidget(labelText.Width + 4, 1));
			secondRow.AddChild(presetNameError);

			metaContainer.AddChild(firstRow);
			metaContainer.AddChild(secondRow);

			return metaContainer;
		}

		private SettingsDropDownList categoryDropDownList;
		private SettingsDropDownList groupDropDownList;
		private SettingsDropDownList settingDropDownList;

		private GuiWidget GetMiddleRow()
		{
			NamedSettingsLayers layerFilter = NamedSettingsLayers.Material;
			List<SettingsLayer> layerFilters = null;

			if (layerFilter != NamedSettingsLayers.All)
			{
				var settings = ActiveSliceSettings.Instance;

				// TODO: The editing context needs to provide the key
				System.Diagnostics.Debugger.Break();
				string layerKey = settings.ActiveMaterialKey;

				layerFilters = new List<SettingsLayer> { settings.BaseLayer, settings.OemLayer };

				switch (layerFilter)
				{
					case NamedSettingsLayers.Material:
						layerFilters.Add(settings.MaterialLayer(layerKey));
						break;

					case NamedSettingsLayers.Quality:
						layerFilters.Add(settings.QualityLayer(layerKey));
						break;
				}
			}

			var settingsWidget = new SliceSettingsWidget(layerFilters, NamedSettingsLayers.Material);
			settingsWidget.settingsControlBar.Visible = false;

			return settingsWidget;
		}

		private FlowLayoutWidget GetBottomRow()
		{
			FlowLayoutWidget container = new FlowLayoutWidget();
			container.HAnchor = HAnchor.ParentLeftRight;
			container.Margin = new BorderDouble(top: 3);

			savePresetButton = buttonFactory.Generate(LocalizedString.Get("Save"));
			duplicatePresetButton = buttonFactory.Generate(LocalizedString.Get("Duplicate"));
			importPresetButton = buttonFactory.Generate(LocalizedString.Get("Import"));
			exportPresetButton = buttonFactory.Generate(LocalizedString.Get("Export"));

			Button cancelButton = buttonFactory.Generate(LocalizedString.Get("Cancel"));
			cancelButton.Click += (sender, e) =>
			{
				UiThread.RunOnIdle(windowController.Close);
			};

			container.AddChild(savePresetButton);
			//Only show duplicate/import/export buttons if setting has been saved.
			if (windowController.ActivePresetLayer.settingsCollectionData.Id != 0)
			{
				container.AddChild(duplicatePresetButton);
				container.AddChild(importPresetButton);
				container.AddChild(exportPresetButton);
			}
			container.AddChild(new HorizontalSpacer());
			container.AddChild(cancelButton);
			return container;
		}

		private Dictionary<string, string> settingLayoutData = new Dictionary<string, string>(); //Setting config name, setting 'full' display name (with category/group)

		private Dictionary<string, string> SettingNameLookup
		{
			get
			{
				if (settingLayoutData.Count == 0)
				{
					PopulateLayoutDictionary();
				}
				return settingLayoutData;
			}
		}

		private void PopulateLayoutDictionary()
		{
			// Show all settings
			var advancedSettings = SliceSettingsOrganizer.Instance.UserLevels["Advanced"];

			foreach (OrganizerCategory category in advancedSettings.CategoriesList)
			{
				foreach (OrganizerGroup group in category.GroupsList)
				{
					foreach (OrganizerSubGroup subgroup in group.SubGroupsList)
					{
						foreach (OrganizerSettingsData setting in subgroup.SettingDataList)
						{
							string settingDisplayName = "{0} > {1} > {2}".FormatWith(category.Name, group.Name, setting.PresentationName).Replace("\\n", "").Replace(":", "");
							settingLayoutData[setting.SlicerConfigName] = settingDisplayName;
						}
					}
				}
			}
		}

		private OrganizerSettingsData addRowSettingData;

		private void OnSettingsChanged()
		{
			SettingsChanged.CallEvents(this, null);
		}

		private void SaveSetting(string slicerConfigName, string value)
		{
			SaveValue(slicerConfigName, value);
		}

		private List<SliceSetting> SliceSettingsToRemoveOnCommit = new List<SliceSetting>();

		public RootedObjectEventHandler CommitStatusChanged = new RootedObjectEventHandler();
		public RootedObjectEventHandler SettingsChanged = new RootedObjectEventHandler();

		public void SaveValue(string keyName, string keyValue)
		{
			if (this.windowController.ActivePresetLayer.settingsDictionary.ContainsKey(keyName)
				&& this.windowController.ActivePresetLayer.settingsDictionary[keyName].Value != keyValue)
			{
				this.windowController.ActivePresetLayer.settingsDictionary[keyName].Value = keyValue;

				OnSettingsChanged();
			}
			else
			{
				SliceSetting sliceSetting = new SliceSetting();
				sliceSetting.Name = keyName;
				sliceSetting.Value = keyValue;
				sliceSetting.SettingsCollectionId = this.windowController.ActivePresetLayer.settingsCollectionData.Id;

				this.windowController.ActivePresetLayer.settingsDictionary[keyName] = sliceSetting;

				OnSettingsChanged();
			}
		}

		public void CommitChanges()
		{
			foreach (KeyValuePair<String, SliceSetting> item in this.windowController.ActivePresetLayer.settingsDictionary)
			{
				//Ensure that each setting's collection id matches current collection id (ie for new presets)
				if (item.Value.SettingsCollectionId != windowController.ActivePresetLayer.settingsCollectionData.Id)
				{
					item.Value.SettingsCollectionId = windowController.ActivePresetLayer.settingsCollectionData.Id;
				}
				item.Value.Commit();
			}
			foreach (SliceSetting item in SliceSettingsToRemoveOnCommit)
			{
				item.Delete();
			}
		}

		private TextWidget getSettingInfoData(OrganizerSettingsData settingData)
		{
			string extraSettings = settingData.ExtraSettings;
			extraSettings = extraSettings.Replace("\\n", "\n");
			TextWidget dataTypeInfo = new TextWidget(extraSettings, pointSize: 10);
			dataTypeInfo.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			dataTypeInfo.Margin = new BorderDouble(5, 0);
			return dataTypeInfo;
		}

		private bool ValidatePresetsForm()
		{
			ValidationMethods validationMethods = new ValidationMethods();

			List<FormField> formFields = new List<FormField> { };
			FormField.ValidationHandler[] stringValidationHandlers = new FormField.ValidationHandler[] { validationMethods.StringIsNotEmpty };
			FormField.ValidationHandler[] nameValidationHandlers = new FormField.ValidationHandler[] { validationMethods.StringIsNotEmpty, validationMethods.StringHasNoSpecialChars };

			formFields.Add(new FormField(presetNameInput, presetNameError, stringValidationHandlers));

			bool formIsValid = true;
			foreach (FormField formField in formFields)
			{
				formField.FieldErrorMessageWidget.Visible = false;
				bool fieldIsValid = formField.Validate();
				if (!fieldIsValid)
				{
					formIsValid = false;
				}
			}
			return formIsValid;
		}

		private void initSlicePreset()
		{
			int noExistingPresets = ExistingPresetsCount() + 1;

			Dictionary<string, SliceSetting> settingsDictionary = new Dictionary<string, SliceSetting>();
			SliceSettingsCollection collection = new SliceSettingsCollection();

			if (ActiveSliceSettings.Instance != null)
			{
				// TODO: Review bindings to int printerID
				int printerID;
				int.TryParse(ActiveSliceSettings.Instance.Id(), out printerID);

				collection.Name = string.Format("{0} ({1})", windowController.filterLabel, noExistingPresets.ToString());
				collection.Tag = windowController.filterTag;
				collection.PrinterId = printerID;
			}

			windowController.ActivePresetLayer = new ClassicSqlitePrinterProfiles.ClassicSettingsLayer(collection, settingsDictionary);
		}

		public int ExistingPresetsCount()
		{
			string query = string.Format("SELECT COUNT(*) FROM SliceSettingsCollection WHERE Tag = '{0}';", windowController.filterTag);
			string result = Datastore.Instance.dbSQLite.ExecuteScalar<string>(query);
			return Convert.ToInt32(result);
		}

		private void savePresets_Click(object sender, EventArgs mouseEvent)
		{
			UiThread.RunOnIdle(() =>
			{
				if (ValidatePresetsForm())
				{
					saveActivePresets();
					windowController.functionToCallOnSave(this, null);
					windowController.ChangeToSlicePresetList();
				}
			});
		}

		private void duplicatePresets_Click(object sender, EventArgs mouseEvent)
		{
			UiThread.RunOnIdle(() =>
			{
				SliceSettingsCollection duplicateCollection = new SliceSettingsCollection();
				duplicateCollection.Name = string.Format("{0} (copy)".FormatWith(windowController.ActivePresetLayer.settingsCollectionData.Name));
				duplicateCollection.Tag = windowController.ActivePresetLayer.settingsCollectionData.Tag;
				duplicateCollection.PrinterId = windowController.ActivePresetLayer.settingsCollectionData.PrinterId;

				Dictionary<string, SliceSetting> settingsDictionary = new Dictionary<string, SliceSetting>();
				IEnumerable<SliceSetting> settingsList = this.windowController.GetCollectionSettings(windowController.ActivePresetLayer.settingsCollectionData.Id);
				foreach (SliceSetting s in settingsList)
				{
					SliceSetting newSetting = new SliceSetting();
					newSetting.Name = s.Name;
					newSetting.Value = s.Value;

					settingsDictionary.Add(s.Name, newSetting);
				}

				var duplicateLayer = new ClassicSqlitePrinterProfiles.ClassicSettingsLayer(duplicateCollection, settingsDictionary);
				windowController.ActivePresetLayer = duplicateLayer;
				windowController.ChangeToSlicePresetDetail();
			});
		}

		private string configFileExtension = "slice";

		private void exportPresets_Click(object sender, EventArgs mouseEvent)
		{
			UiThread.RunOnIdle(SaveAs);
		}

		private void SaveAs()
		{
			SaveFileDialogParams saveParams = new SaveFileDialogParams("Save Slice Preset|*." + configFileExtension);
			saveParams.FileName = presetNameInput.Text;

			FileDialog.SaveFileDialog(saveParams, onSaveFileSelected);
		}

		private void onSaveFileSelected(SaveFileDialogParams saveParams)
		{
			if (!string.IsNullOrEmpty(saveParams.FileName))
			{
				GenerateConfigFile(saveParams.FileName);
			}
		}

		public void GenerateConfigFile(string fileName)
		{
			List<string> configFileAsList = new List<string>();

			foreach (KeyValuePair<String, SliceSetting> setting in windowController.ActivePresetLayer.settingsDictionary)
			{
				string settingString = string.Format("{0} = {1}", setting.Value.Name, setting.Value.Value);
				configFileAsList.Add(settingString);
			}
			string configFileAsString = string.Join("\n", configFileAsList.ToArray());

			FileStream fs = new FileStream(fileName, FileMode.Create);
			StreamWriter sw = new System.IO.StreamWriter(fs);
			sw.Write(configFileAsString);
			sw.Close();
		}

		private void saveActivePresets()
		{
			windowController.ActivePresetLayer.settingsCollectionData.Name = presetNameInput.Text;
			windowController.ActivePresetLayer.settingsCollectionData.Commit();
			CommitChanges();
		}
	}

	internal class PresetListControl : ScrollableWidget
	{
		private FlowLayoutWidget topToBottomItemList;

		public PresetListControl()
		{
			this.AnchorAll();
			this.AutoScroll = true;
			this.ScrollArea.HAnchor |= Agg.UI.HAnchor.ParentLeftRight;

			topToBottomItemList = new FlowLayoutWidget(FlowDirection.TopToBottom);
			topToBottomItemList.HAnchor = Agg.UI.HAnchor.Max_FitToChildren_ParentWidth;
			topToBottomItemList.Margin = new BorderDouble(top: 3);

			base.AddChild(topToBottomItemList);
		}

		public void RemoveScrollChildren()
		{
			topToBottomItemList.RemoveAllChildren();
		}

		public override void AddChild(GuiWidget child, int indexInChildrenList = -1)
		{
			FlowLayoutWidget itemHolder = new FlowLayoutWidget();
			itemHolder.Margin = new BorderDouble(0, 0, 0, 0);
			itemHolder.HAnchor = Agg.UI.HAnchor.Max_FitToChildren_ParentWidth;
			itemHolder.AddChild(child);
			itemHolder.VAnchor = VAnchor.FitToChildren;

			topToBottomItemList.AddChild(itemHolder, indexInChildrenList);
		}
	}

	public class SettingsDropDownList : DropDownList
	{
		private static RGBA_Bytes whiteSemiTransparent = new RGBA_Bytes(255, 255, 255, 100);
		private static RGBA_Bytes whiteTransparent = new RGBA_Bytes(255, 255, 255, 0);

		public SettingsDropDownList(string noSelectionString, Direction direction = Direction.Down)
			: base(noSelectionString, whiteTransparent, whiteSemiTransparent, direction, maxHeight: 300)
		{
			//this.HAnchor = HAnchor.ParentLeftRight;
			this.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			this.MenuItemsBorderWidth = 1;
			this.MenuItemsBackgroundColor = RGBA_Bytes.White;
			this.MenuItemsBorderColor = ActiveTheme.Instance.SecondaryTextColor;
			this.MenuItemsPadding = new BorderDouble(10, 4, 10, 6);
			this.MenuItemsBackgroundHoverColor = ActiveTheme.Instance.PrimaryAccentColor;
			this.MenuItemsTextHoverColor = ActiveTheme.Instance.PrimaryTextColor;
			this.MenuItemsTextColor = RGBA_Bytes.Black;
			this.BorderWidth = 1;
			this.BorderColor = ActiveTheme.Instance.SecondaryTextColor;
			this.HoverColor = whiteSemiTransparent;
			this.BackgroundColor = new RGBA_Bytes(255, 255, 255, 0);

			mainControlText.VAnchor = VAnchor.ParentCenter;
		}
	}
}