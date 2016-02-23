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

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DataStorage;
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
			importPresetButton.Click += new EventHandler(importPresets_Click);
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

		private FlowLayoutWidget addSettingsContainer;
		private PresetListControl settingsRowContainer;
		private FlowLayoutWidget errorMessageContainer;

		private FlowLayoutWidget GetMiddleRow()
		{
			FlowLayoutWidget container = new FlowLayoutWidget();
			container.HAnchor = HAnchor.ParentLeftRight;
			container.VAnchor = Agg.UI.VAnchor.ParentBottomTop;
			container.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;

			FlowLayoutWidget topBottomContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
			topBottomContainer.AnchorAll();

			FlowLayoutWidget addContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
			addContainer.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
			addContainer.HAnchor = Agg.UI.HAnchor.ParentLeftRight;

			TextWidget errorMessage = new TextWidget("Oops! Please select a setting first.", pointSize: 10);
			errorMessage.TextColor = ActiveTheme.Instance.SecondaryAccentColor;

			errorMessageContainer = new FlowLayoutWidget();
			errorMessageContainer.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
			errorMessageContainer.BackgroundColor = ActiveTheme.Instance.TransparentDarkOverlay;
			errorMessageContainer.Visible = false;
			errorMessageContainer.Padding = new BorderDouble(3);

			errorMessageContainer.AddChild(new HorizontalSpacer());
			errorMessageContainer.AddChild(errorMessage);
			errorMessageContainer.AddChild(new HorizontalSpacer());

			addSettingsContainer = new FlowLayoutWidget();
			addSettingsContainer.Padding = new BorderDouble(3);
			addSettingsContainer.BackgroundColor = ActiveTheme.Instance.TransparentDarkOverlay;
			addSettingsContainer.HAnchor = HAnchor.ParentLeftRight;

			PopulateAddSettingRow();

			addContainer.AddChild(addSettingsContainer);
			addContainer.AddChild(errorMessageContainer);

			settingsRowContainer = new PresetListControl();
			settingsRowContainer.HAnchor = HAnchor.ParentLeftRight;

			LoadSettingsRows();

			topBottomContainer.AddChild(addContainer);
			topBottomContainer.AddChild(settingsRowContainer);

			container.AddChild(topBottomContainer);
			return container;
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
				UiThread.RunOnIdle(windowController.ChangeToSlicePresetList);
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

		private void PopulateAddSettingRow(int categoryDefaultIndex = -1, int groupDefaultIndex = -1, string settingDefaultConfigName = "-1")
		{
			errorMessageContainer.Visible = false;

			categoryDropDownList = new SettingsDropDownList("- Select Category -");
			categoryDropDownList.Margin = new BorderDouble(right: 3);
			categoryDropDownList.MinimumSize = new Vector2(categoryDropDownList.LocalBounds.Width, categoryDropDownList.LocalBounds.Height);
			categoryDropDownList.VAnchor = Agg.UI.VAnchor.ParentCenter;
			categoryDropDownList.Height = 24;

			groupDropDownList = new SettingsDropDownList("- Select Group -");
			groupDropDownList.Margin = new BorderDouble(right: 3);
			groupDropDownList.MinimumSize = new Vector2(groupDropDownList.LocalBounds.Width, groupDropDownList.LocalBounds.Height);
			groupDropDownList.VAnchor = Agg.UI.VAnchor.ParentCenter;
			groupDropDownList.Height = 24;

			settingDropDownList = new SettingsDropDownList("- Select Setting -");
			settingDropDownList.Margin = new BorderDouble(right: 3);
			settingDropDownList.MinimumSize = new Vector2(settingDropDownList.LocalBounds.Width, settingDropDownList.LocalBounds.Height);
			settingDropDownList.VAnchor = Agg.UI.VAnchor.ParentCenter;
			settingDropDownList.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
			settingDropDownList.AlignToRightEdge = true;
			settingDropDownList.Height = 24;

			string selectedCategoryValue = "{0}:-1:-1".FormatWith(categoryDefaultIndex);
			string selectedGroupValue = "{0}:{1}:-1".FormatWith(categoryDefaultIndex, groupDefaultIndex);
			string selectedSettingValue = "{0}:{1}:{2}".FormatWith(categoryDefaultIndex, groupDefaultIndex, settingDefaultConfigName);

			string UserLevel = "Advanced"; //Show all settings
			for (int categoryIndex = 0; categoryIndex < SliceSettingsOrganizer.Instance.UserLevels[UserLevel].CategoriesList.Count; categoryIndex++)
			{
				OrganizerCategory category = SliceSettingsOrganizer.Instance.UserLevels[UserLevel].CategoriesList[categoryIndex];

				//Always add all categories
				MenuItem categoryMenuItem = categoryDropDownList.AddItem(category.Name, "{0}:-1:-1".FormatWith(categoryIndex));
				categoryMenuItem.Selected += new EventHandler(OnItemSelected);

				for (int groupIndex = 0; groupIndex < category.GroupsList.Count; groupIndex++)
				{
					OrganizerGroup group = category.GroupsList[groupIndex];
					string groupValue = "{0}:{1}:-1".FormatWith(categoryIndex, groupIndex);

					//Add groups if within selected category or no category selected
					if (categoryIndex == categoryDefaultIndex || categoryDefaultIndex == -1)
					{
						MenuItem groupMenuItem = groupDropDownList.AddItem(group.Name, groupValue);
						groupMenuItem.Selected += new EventHandler(OnItemSelected);
					}

					for (int subGroupIndex = 0; subGroupIndex < group.SubGroupsList.Count; subGroupIndex++)
					{
						OrganizerSubGroup subgroup = group.SubGroupsList[subGroupIndex];
						for (int settingIndex = 0; settingIndex < subgroup.SettingDataList.Count; settingIndex++)
						{
							//Add settings if within selected category and group or no category selected
							if (selectedGroupValue == groupValue || (groupDefaultIndex == -1 && categoryIndex == categoryDefaultIndex) || categoryDefaultIndex == -1)
							{
								OrganizerSettingsData setting = subgroup.SettingDataList[settingIndex];

								if (setting.DataEditType != OrganizerSettingsData.DataEditTypes.HARDWARE_PRESENT)
								{
									string itemValue = "{0}:{1}:{2}".FormatWith(categoryIndex, groupIndex, setting.SlicerConfigName);
									string itemName = setting.PresentationName.Replace("\\n", "").Replace(":", "");
									if (setting.ExtraSettings.Trim() != "" && setting.DataEditType != OrganizerSettingsData.DataEditTypes.LIST)
									{
										itemName = "{0} ({1})".FormatWith(itemName, setting.ExtraSettings.Replace("\\n", " "));
									}
									if (ActivePrinterProfile.Instance.ActiveSliceEngine.MapContains(setting.SlicerConfigName))
									{
										MenuItem settingMenuItem = settingDropDownList.AddItem(itemName, itemValue);
										settingMenuItem.Selected += new EventHandler(OnItemSelected);
										settingMenuItem.Selected += new EventHandler(OnSettingSelected);
									}
								}
							}
						}
					}
				}
			}

			if (categoryDefaultIndex != -1)
			{
				categoryDropDownList.SelectedValue = selectedCategoryValue;
			}
			if (groupDefaultIndex != -1)
			{
				groupDropDownList.SelectedValue = selectedGroupValue;
			}
			if (settingDefaultConfigName != "-1")
			{
				settingDropDownList.SelectedValue = selectedSettingValue;
			}

			addSettingsContainer.RemoveAllChildren();
			addSettingsContainer.AddChild(categoryDropDownList);
			addSettingsContainer.AddChild(groupDropDownList);
			addSettingsContainer.AddChild(settingDropDownList);
			//addSettingsContainer.AddChild(addButton);
		}

		private void OnItemSelected(object sender, EventArgs e)
		{
			MenuItem item = (MenuItem)sender;
			string[] valueArray = item.Value.Split(':');
			UiThread.RunOnIdle(() => PopulateAddSettingRow(Int32.Parse(valueArray[0]), Int32.Parse(valueArray[1]), valueArray[2]));
		}

		private void LoadSettingsRows()
		{
			settingsRowContainer.RemoveScrollChildren();
			UiThread.RunOnIdle(() =>
			{
				foreach (KeyValuePair<String, SliceSetting> item in this.windowController.ActivePresetLayer.settingsDictionary)
				{
					OrganizerSettingsData settingData = SliceSettingsOrganizer.Instance.GetSettingsData(item.Key);

					// Dont add row if there is no entry
					if (settingData != null && ActivePrinterProfile.Instance.ActiveSliceEngine.MapContains(settingData.SlicerConfigName))
					{
						FlowLayoutWidget row = GetSettingsRow(settingData, item.Value.Value);
						row.Padding = new BorderDouble(3, 3, 3, 6);
						settingsRowContainer.AddChild(row);
						HorizontalLine horizontalLine = new HorizontalLine();
						horizontalLine.BackgroundColor = ActiveTheme.Instance.SecondaryTextColor;
						settingsRowContainer.AddChild(horizontalLine);
					}
				}
			});
		}

		private OrganizerSettingsData addRowSettingData;

		private void OnSettingSelected(object sender, EventArgs e)
		{
			MenuItem item = (MenuItem)sender;
			string[] valueArray = item.Value.Split(':');
			string configName = valueArray[2];
			addRowSettingData = SliceSettingsOrganizer.Instance.GetSettingsData(configName);

			AddSettingToPreset();
		}

		private void AddSettingToPreset()
		{
			UiThread.RunOnIdle(() =>
			{
				if (addRowSettingData != null)
				{
					if (!this.windowController.ActivePresetLayer.settingsDictionary.ContainsKey(addRowSettingData.SlicerConfigName))
					{
						SliceSetting sliceSetting = new SliceSetting();
						sliceSetting.Name = addRowSettingData.SlicerConfigName;
						sliceSetting.Value = ActiveSliceSettings.Instance.GetActiveValue(addRowSettingData.SlicerConfigName); //populate with current
						sliceSetting.SettingsCollectionId = this.windowController.ActivePresetLayer.settingsCollectionData.Id;

						this.windowController.ActivePresetLayer.settingsDictionary[addRowSettingData.SlicerConfigName] = sliceSetting;

						OnSettingsChanged();
					}
					addRowSettingData = null;
					PopulateAddSettingRow(-1, -1, "-1");
					LoadSettingsRows();

					duplicatePresetButton.Visible = false;
					exportPresetButton.Visible = false;
				}
				else
				{
					errorMessageContainer.Visible = true;
				}
			});
		}

		private void OnSettingsChanged()
		{
			SettingsChanged.CallEvents(this, null);
		}

		private void SaveSetting(string slicerConfigName, string value)
		{
			SaveValue(slicerConfigName, value);
		}

		private List<SliceSetting> SliceSettingsToRemoveOnCommit = new List<SliceSetting>();

		private void RemoveSetting(string configName)
		{
			UiThread.RunOnIdle(() =>
			{
				if (this.windowController.ActivePresetLayer.settingsDictionary.ContainsKey(configName))
				{
					SliceSetting item = this.windowController.ActivePresetLayer.settingsDictionary[configName];
					SliceSettingsToRemoveOnCommit.Add(item);

					this.windowController.ActivePresetLayer.settingsDictionary.Remove(configName);
					OnSettingsChanged();
				}

				LoadSettingsRows();
			});
		}

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

		private FlowLayoutWidget GetSettingsRow(OrganizerSettingsData settingData, string sliceSettingValue = null)
		{
			FlowLayoutWidget container = new FlowLayoutWidget();
			container.HAnchor = HAnchor.ParentLeftRight;

			double minSettingNameWidth = 400;

			if (ActiveSliceSettings.Instance.Contains(settingData.SlicerConfigName))
			{
				int intEditWidth = 60;
				int doubleEditWidth = 60;
				int vectorXYEditWidth = 60;
				int multiLineEditHeight = 60;

				//Get the current value as a default value
				if (sliceSettingValue == null)
				{
					sliceSettingValue = ActiveSliceSettings.Instance.GetActiveValue(settingData.SlicerConfigName);
				}

				//Get the presentation name from the SettingNameLookup
				TextWidget settingName = new TextWidget(SettingNameLookup[settingData.SlicerConfigName], pointSize: 10);
				settingName.AutoExpandBoundsToText = false;
				settingName.EllipsisIfClipped = true;
				settingName.TextColor = ActiveTheme.Instance.PrimaryTextColor;
				settingName.VAnchor = Agg.UI.VAnchor.ParentCenter;
				settingName.MinimumSize = new Vector2(minSettingNameWidth, settingName.Height);
				settingName.Width = minSettingNameWidth;

				container.AddChild(settingName);

				switch (settingData.DataEditType)
				{
					case OrganizerSettingsData.DataEditTypes.INT:
						{
							int currentValue = 0;
							int.TryParse(sliceSettingValue, out currentValue);
							MHNumberEdit intEditWidget = new MHNumberEdit(currentValue, pixelWidth: intEditWidth, tabIndex: tabIndexForItem++);
							intEditWidget.ActuallNumberEdit.EditComplete += (sender, e) => { SaveSetting(settingData.SlicerConfigName, ((NumberEdit)sender).Value.ToString()); };
							container.AddChild(intEditWidget);
							container.AddChild(getSettingInfoData(settingData));
						}
						break;

					case OrganizerSettingsData.DataEditTypes.DOUBLE:
						{
							double currentValue = 0;
							double.TryParse(sliceSettingValue, out currentValue);
							MHNumberEdit doubleEditWidget = new MHNumberEdit(currentValue, allowNegatives: true, allowDecimals: true, pixelWidth: doubleEditWidth, tabIndex: tabIndexForItem++);
							doubleEditWidget.ActuallNumberEdit.EditComplete += (sender, e) => { SaveSetting(settingData.SlicerConfigName, ((NumberEdit)sender).Value.ToString()); };
							container.AddChild(doubleEditWidget);
							container.AddChild(getSettingInfoData(settingData));
						}
						break;

					case OrganizerSettingsData.DataEditTypes.POSITIVE_DOUBLE:
						{
							double currentValue = 0;
							double.TryParse(sliceSettingValue, out currentValue);
							MHNumberEdit doubleEditWidget = new MHNumberEdit(currentValue, allowDecimals: true, pixelWidth: doubleEditWidth, tabIndex: tabIndexForItem++);
							doubleEditWidget.ActuallNumberEdit.EditComplete += (sender, e) => { SaveSetting(settingData.SlicerConfigName, ((NumberEdit)sender).Value.ToString()); };
							container.AddChild(doubleEditWidget);
							container.AddChild(getSettingInfoData(settingData));
						}
						break;

					case OrganizerSettingsData.DataEditTypes.OFFSET:
						{
							double currentValue = 0;
							double.TryParse(sliceSettingValue, out currentValue);
							MHNumberEdit doubleEditWidget = new MHNumberEdit(currentValue, allowDecimals: true, allowNegatives: true, pixelWidth: doubleEditWidth, tabIndex: tabIndexForItem++);
							doubleEditWidget.ActuallNumberEdit.EditComplete += (sender, e) => { SaveSetting(settingData.SlicerConfigName, ((NumberEdit)sender).Value.ToString()); };
							container.AddChild(doubleEditWidget);
							container.AddChild(getSettingInfoData(settingData));
						}
						break;

					case OrganizerSettingsData.DataEditTypes.DOUBLE_OR_PERCENT:
						{
							FlowLayoutWidget content = new FlowLayoutWidget();

							MHTextEditWidget stringEdit = new MHTextEditWidget(sliceSettingValue, pixelWidth: doubleEditWidth - 2, tabIndex: tabIndexForItem++);
							stringEdit.ActualTextEditWidget.EditComplete += (sender, e) =>
							{
								TextEditWidget textEditWidget = (TextEditWidget)sender;
								string text = textEditWidget.Text;
								text = text.Trim();
								bool isPercent = text.Contains("%");
								if (isPercent)
								{
									text = text.Substring(0, text.IndexOf("%"));
								}
								double result;
								double.TryParse(text, out result);
								text = result.ToString();
								if (isPercent)
								{
									text += "%";
								}
								textEditWidget.Text = text;
								SaveSetting(settingData.SlicerConfigName, textEditWidget.Text);
							};
							stringEdit.SelectAllOnFocus = true;

							stringEdit.ActualTextEditWidget.InternalTextEditWidget.AllSelected += (sender, e) =>
							{
								// select evrything up to the % (if present)
								InternalTextEditWidget textEditWidget = (InternalTextEditWidget)sender;
								int percentIndex = textEditWidget.Text.IndexOf("%");
								if (percentIndex != -1)
								{
									textEditWidget.SetSelection(0, percentIndex - 1);
								}
							};

							content.AddChild(stringEdit);
							content.AddChild(getSettingInfoData(settingData));

							container.AddChild(content);
						}
						break;

					case OrganizerSettingsData.DataEditTypes.INT_OR_MM:
						{
							FlowLayoutWidget content = new FlowLayoutWidget();

							MHTextEditWidget stringEdit = new MHTextEditWidget(sliceSettingValue, pixelWidth: doubleEditWidth - 2, tabIndex: tabIndexForItem++);

							string startingText = stringEdit.Text;
							stringEdit.ActualTextEditWidget.EditComplete += (sender, e) =>
							{
								TextEditWidget textEditWidget = (TextEditWidget)sender;
								// only validate when we lose focus
								if (!textEditWidget.ContainsFocus)
								{
									string text = textEditWidget.Text;
									text = text.Trim();
									bool isMm = text.Contains("mm");
									if (isMm)
									{
										text = text.Substring(0, text.IndexOf("mm"));
									}
									double result;
									double.TryParse(text, out result);
									text = result.ToString();
									if (isMm)
									{
										text += "mm";
									}
									else
									{
										result = (int)result;
										text = result.ToString();
									}
									textEditWidget.Text = text;
									startingText = stringEdit.Text;
								}

								SaveSetting(settingData.SlicerConfigName, textEditWidget.Text);

								// make sure we are still looking for the final validation before saving.
								if (textEditWidget.ContainsFocus)
								{
									UiThread.RunOnIdle(() =>
									{
										string currentText = textEditWidget.Text;
										int cursorIndex = textEditWidget.InternalTextEditWidget.CharIndexToInsertBefore;
										textEditWidget.Text = startingText;
										textEditWidget.InternalTextEditWidget.MarkAsStartingState();
										textEditWidget.Text = currentText;
										textEditWidget.InternalTextEditWidget.CharIndexToInsertBefore = cursorIndex;
									});
								}
							};
							stringEdit.SelectAllOnFocus = true;

							stringEdit.ActualTextEditWidget.InternalTextEditWidget.AllSelected += (sender, e) =>
							{
								// select evrything up to the mm (if present)
								InternalTextEditWidget textEditWidget = (InternalTextEditWidget)sender;
								int mMIndex = textEditWidget.Text.IndexOf("mm");
								if (mMIndex != -1)
								{
									textEditWidget.SetSelection(0, mMIndex - 1);
								}
							};

							content.AddChild(stringEdit);
							content.AddChild(getSettingInfoData(settingData));

							container.AddChild(content);
						}
						break;

					case OrganizerSettingsData.DataEditTypes.CHECK_BOX:
						{
							CheckBox checkBoxWidget = new CheckBox("");
							checkBoxWidget.VAnchor = Agg.UI.VAnchor.ParentBottom;
							checkBoxWidget.TextColor = ActiveTheme.Instance.PrimaryTextColor;
							checkBoxWidget.Checked = (sliceSettingValue == "1");
							checkBoxWidget.CheckedStateChanged += (sender, e) =>
							{
								if (((CheckBox)sender).Checked)
								{
									SaveSetting(settingData.SlicerConfigName, "1");
								}
								else
								{
									SaveSetting(settingData.SlicerConfigName, "0");
								}
							};
							container.AddChild(checkBoxWidget);
						}
						break;

					case OrganizerSettingsData.DataEditTypes.STRING:
						{
							MHTextEditWidget stringEdit = new MHTextEditWidget(sliceSettingValue, pixelWidth: 120, tabIndex: tabIndexForItem++);
							stringEdit.ActualTextEditWidget.EditComplete += (sender, e) => { SaveSetting(settingData.SlicerConfigName, ((TextEditWidget)sender).Text); };
							container.AddChild(stringEdit);
						}
						break;

					case OrganizerSettingsData.DataEditTypes.MULTI_LINE_TEXT:
						{
							string convertedNewLines = sliceSettingValue.Replace("\\n", "\n");
							MHTextEditWidget stringEdit = new MHTextEditWidget(convertedNewLines, pointSize: 10, pixelWidth: 280, pixelHeight: multiLineEditHeight, multiLine: true, tabIndex: tabIndexForItem++);
							stringEdit.ActualTextEditWidget.EditComplete += (sender, e) => { SaveSetting(settingData.SlicerConfigName, ((TextEditWidget)sender).Text.Replace("\n", "\\n")); };
							container.AddChild(stringEdit);
						}
						break;

					case OrganizerSettingsData.DataEditTypes.LIST:
						{
							StyledDropDownList selectableOptions = new StyledDropDownList("None", Direction.Down);
							selectableOptions.Margin = new BorderDouble();

							string[] listItems = settingData.ExtraSettings.Split(',');
							foreach (string listItem in listItems)
							{
								MenuItem newItem = selectableOptions.AddItem(listItem);
								if (newItem.Text == sliceSettingValue)
								{
									selectableOptions.SelectedLabel = sliceSettingValue;
								}

								newItem.Selected += (sender, e) =>
								{
									MenuItem menuItem = ((MenuItem)sender);
									SaveSetting(settingData.SlicerConfigName, menuItem.Text);
								};
							}
							container.AddChild(selectableOptions);
						}
						break;

					case OrganizerSettingsData.DataEditTypes.HARDWARE_PRESENT:
						// this is not editable for a detail setting (quality or material)
						break;

					case OrganizerSettingsData.DataEditTypes.VECTOR2:
						{
							string[] xyValueStrings = sliceSettingValue.Split(',');
							if (xyValueStrings.Length != 2)
							{
								xyValueStrings = new string[] { "0", "0" };
							}
							double currentXValue = 0;
							double.TryParse(xyValueStrings[0], out currentXValue);
							MHNumberEdit xEditWidget = new MHNumberEdit(currentXValue, allowDecimals: true, pixelWidth: vectorXYEditWidth, tabIndex: tabIndexForItem++);

							double currentYValue = 0;
							double.TryParse(xyValueStrings[1], out currentYValue);
							MHNumberEdit yEditWidget = new MHNumberEdit(currentYValue, allowDecimals: true, pixelWidth: vectorXYEditWidth, tabIndex: tabIndexForItem++);
							{
								xEditWidget.ActuallNumberEdit.EditComplete += (sender, e) => { SaveSetting(settingData.SlicerConfigName, xEditWidget.ActuallNumberEdit.Value.ToString() + "," + yEditWidget.ActuallNumberEdit.Value.ToString()); };
								container.AddChild(xEditWidget);
								TextWidget xText = new TextWidget("x");
								xText.TextColor = ActiveTheme.Instance.PrimaryTextColor;
								xText.Margin = new BorderDouble(5, 0);
								container.AddChild(xText);
							}
							{
								yEditWidget.ActuallNumberEdit.EditComplete += (sender, e) => { SaveSetting(settingData.SlicerConfigName, xEditWidget.ActuallNumberEdit.Value.ToString() + "," + yEditWidget.ActuallNumberEdit.Value.ToString()); };
								container.AddChild(yEditWidget);
								TextWidget yText = new TextWidget("y");
								yText.TextColor = ActiveTheme.Instance.PrimaryTextColor;
								yText.Margin = new BorderDouble(5, 0);
								container.AddChild(yText);
							}
						}
						break;

					case OrganizerSettingsData.DataEditTypes.OFFSET2:
						{
							string[] xyValueStrings = sliceSettingValue.Split('x');
							if (xyValueStrings.Length != 2)
							{
								xyValueStrings = new string[] { "0", "0" };
							}
							double currentXValue = 0;
							double.TryParse(xyValueStrings[0], out currentXValue);
							MHNumberEdit xEditWidget = new MHNumberEdit(currentXValue, allowDecimals: true, allowNegatives: true, pixelWidth: vectorXYEditWidth, tabIndex: tabIndexForItem++);

							double currentYValue = 0;
							double.TryParse(xyValueStrings[1], out currentYValue);
							MHNumberEdit yEditWidget = new MHNumberEdit(currentYValue, allowDecimals: true, allowNegatives: true, pixelWidth: vectorXYEditWidth, tabIndex: tabIndexForItem++);
							{
								xEditWidget.ActuallNumberEdit.EditComplete += (sender, e) => { SaveSetting(settingData.SlicerConfigName, xEditWidget.ActuallNumberEdit.Value.ToString() + "x" + yEditWidget.ActuallNumberEdit.Value.ToString()); };
								container.AddChild(xEditWidget);
								TextWidget xText = new TextWidget("x");
								xText.TextColor = ActiveTheme.Instance.PrimaryTextColor;
								xText.Margin = new BorderDouble(5, 0);
								container.AddChild(xText);
							}
							{
								yEditWidget.ActuallNumberEdit.EditComplete += (sender, e) => { SaveSetting(settingData.SlicerConfigName, xEditWidget.ActuallNumberEdit.Value.ToString() + "x" + yEditWidget.ActuallNumberEdit.Value.ToString()); };
								container.AddChild(yEditWidget);
								TextWidget yText = new TextWidget("y");
								yText.TextColor = ActiveTheme.Instance.PrimaryTextColor;
								yText.Margin = new BorderDouble(5, 0);
								container.AddChild(yText);
							}
						}
						break;

					default:
						TextWidget missingSetting = new TextWidget(String.Format("Missing the setting for '{0}'.", settingData.DataEditType.ToString()));
						missingSetting.TextColor = ActiveTheme.Instance.PrimaryTextColor;
						missingSetting.BackgroundColor = RGBA_Bytes.Red;
						container.AddChild(missingSetting);
						break;
				}
			}
			else // the setting we think we are adding is not in the config.ini it may have been depricated
			{
				TextWidget settingName = new TextWidget(String.Format("Setting '{0}' not found in config.ini", settingData.SlicerConfigName));
				settingName.TextColor = ActiveTheme.Instance.PrimaryTextColor;
				settingName.MinimumSize = new Vector2(minSettingNameWidth, settingName.MinimumSize.y);
				container.AddChild(settingName);
				container.BackgroundColor = RGBA_Bytes.Red;
			}
			container.AddChild(new HorizontalSpacer());
			Button removeLink = linkButtonFactory.Generate("remove");
			removeLink.Click += (sender, e) =>
			{
				RemoveSetting(settingData.SlicerConfigName);
			};
			removeLink.VAnchor = Agg.UI.VAnchor.ParentCenter;
			container.AddChild(removeLink);

			return container;
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

			if (ActivePrinterProfile.Instance.ActivePrinter != null)
			{
				collection.Name = string.Format("{0} ({1})", windowController.filterLabel, noExistingPresets.ToString());
				collection.Tag = windowController.filterTag;
				collection.PrinterId = ActivePrinterProfile.Instance.ActivePrinter.Id;
			}

			windowController.ActivePresetLayer = new SettingsLayer(collection, settingsDictionary);
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
					ActiveSliceSettings.Instance.LoadAllSettings();
					// Dissabled this as the pannel is already reloaded from LoadAllSettings LBB 2015 01 03.
					//ApplicationController.Instance.ReloadAdvancedControlsPanel();
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

				SettingsLayer duplicateLayer = new SettingsLayer(duplicateCollection, settingsDictionary);
				windowController.ActivePresetLayer = duplicateLayer;
				windowController.ChangeToSlicePresetDetail();
			});
		}

		private string configFileExtension = "slice";

		private void importPresets_Click(object sender, EventArgs mouseEvent)
		{
			OpenFileDialogParams openParams = new OpenFileDialogParams("Load Slice Preset|*.slice;*.ini");
			openParams.ActionButtonLabel = "Load Slice Preset";
			openParams.Title = "MatterControl: Select A File";

			FileDialog.OpenFileDialog(openParams, onLoadPreset);
		}

		private void onLoadPreset(OpenFileDialogParams openParams)
		{
			if (openParams.FileNames != null)
			{
				Dictionary<string, SliceSetting> settingsDictionary = new Dictionary<string, SliceSetting>();
				try
				{
					if (File.Exists(openParams.FileName))
					{
						string[] lines = System.IO.File.ReadAllLines(openParams.FileName);
						foreach (string line in lines)
						{
							//Ignore commented lines
							if (!line.StartsWith("#"))
							{
								string[] settingLine = line.Split('=');
								string keyName = settingLine[0].Trim();
								string settingDefaultValue = settingLine[1].Trim();

								SliceSetting sliceSetting = new SliceSetting();
								sliceSetting.Name = keyName;
								sliceSetting.Value = settingDefaultValue;
								sliceSetting.SettingsCollectionId = windowController.ActivePresetLayer.settingsCollectionData.Id;

								settingsDictionary.Add(keyName, sliceSetting);
							}
						}
						windowController.ActivePresetLayer.settingsDictionary = settingsDictionary;
						LoadSettingsRows();
					}
				}
				catch (Exception)
				{
					// Error loading configuration
				}
			}
		}

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
			this.BorderWidth = 1;
			this.BorderColor = ActiveTheme.Instance.SecondaryTextColor;
			this.HoverColor = whiteSemiTransparent;
			this.BackgroundColor = new RGBA_Bytes(255, 255, 255, 0);

			mainControlText.VAnchor = VAnchor.ParentCenter;
		}
	}
}