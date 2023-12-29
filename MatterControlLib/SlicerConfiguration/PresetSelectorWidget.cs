﻿/*
Copyright (c) 2018, Kevin Pope, John Lewin
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
// #define DO_IN_PLACE_EDIT

using System;
using System.Collections.Generic;
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.ImageProcessing;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.Library.Widgets;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class PresetSelectorWidget : FlowLayoutWidget
	{
		private DropDownList dropDownList;
		private string defaultMenuItemText = "- none -".Localize();
		private GuiWidget editButton;
		private int extruderIndex;
		private NamedSettingsLayers layerType;
		private ThemeConfig theme;
		private PrinterConfig printer;
		private GuiWidget pullDownContainer;
		private bool createAsFit;

		public PresetSelectorWidget(PrinterConfig printer, string label, Color accentColor, NamedSettingsLayers layerType, int extruderIndex, ThemeConfig theme, bool createAsFit = false)
			: base(FlowDirection.TopToBottom)
		{
			this.createAsFit = createAsFit;
			this.extruderIndex = extruderIndex;
			this.layerType = layerType;
			this.printer = printer;
			this.Name = label;
			this.theme = theme;
			this.HAnchor = createAsFit ? HAnchor.Fit : HAnchor.Stretch;
			this.VAnchor = VAnchor.Fit;
			this.BackgroundColor = theme.MinimalShade;
			this.Padding = theme.DefaultContainerPadding;

			// Section Label
			this.AddChild(new TextWidget(label, pointSize: theme.DefaultFontSize, textColor: theme.TextColor)
			{
				HAnchor = HAnchor.Left,
				Margin = new BorderDouble(0)
			});

			pullDownContainer = new GuiWidget()
			{
				HAnchor = createAsFit ? HAnchor.Fit : HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
				Border = new BorderDouble(left: 3),
				BorderColor = accentColor,
				Margin = new BorderDouble(top: 6),
				Padding = new BorderDouble(left: (accentColor != Color.Transparent) ? 6 : 0)
			};
			pullDownContainer.AddChild(this.NewPulldownContainer());
			this.AddChild(pullDownContainer);

			// Register listeners
			printer.Settings.SettingChanged += Printer_SettingChanged;
		}

		private FlowLayoutWidget NewPulldownContainer()
		{
			dropDownList = CreateDropdown();

			var container = new FlowLayoutWidget()
			{
				HAnchor = createAsFit ? HAnchor.Fit : HAnchor.MaxFitOrStretch,
				Name = "Preset Pulldown Container"
			};

			editButton = new ThemedIconButton(StaticData.Instance.LoadIcon("icon_edit.png", 16, 16).GrayToColor(theme.TextColor), theme)
			{
				ToolTipText = "Edit Selected Setting".Localize(),
				Enabled = dropDownList.SelectedIndex != -1,
				VAnchor = VAnchor.Center,
				Margin = new BorderDouble(left: 6)
			};

			editButton.Click += (sender, e) =>
			{
				if (layerType == NamedSettingsLayers.Material)
				{
					if (ApplicationController.Instance.AcitveSlicePresetsPage == null)
					{
						string presetsID = printer.Settings.ActiveMaterialKey;
						if (string.IsNullOrEmpty(presetsID))
						{
							return;
						}

						var layerToEdit = printer.Settings.MaterialLayers.Where(layer => layer.LayerID == presetsID).FirstOrDefault();

						var presetsContext = new PresetsContext(printer.Settings.MaterialLayers, layerToEdit)
						{
							LayerType = NamedSettingsLayers.Material,
							SetAsActive = (materialKey) => printer.Settings.ActiveMaterialKey = materialKey,
							DeleteLayer = () =>
							{
								printer.Settings.ActiveMaterialKey = "";
								printer.Settings.MaterialLayers.Remove(layerToEdit);
								printer.Settings.Save();
								RebuildDropDownList(true);
							}
						};

						if (presetsContext.PersistenceLayer == null)
						{
							return;
						}

						var editMaterialPresetsPage = new SlicePresetsPage(printer, presetsContext, true);
						editMaterialPresetsPage.Closed += (s, e2) =>
						{
							ApplicationController.Instance.AcitveSlicePresetsPage = null;
						};

						ApplicationController.Instance.AcitveSlicePresetsPage = editMaterialPresetsPage;
						DialogWindow.Show(editMaterialPresetsPage);
					}
					else
					{
						ApplicationController.Instance.AcitveSlicePresetsPage.DialogWindow.BringToFront();
					}
				}

				if (layerType == NamedSettingsLayers.Quality)
				{
					if (ApplicationController.Instance.AcitveSlicePresetsPage == null)
					{
						string presetsID = printer.Settings.ActiveQualityKey;
						if (string.IsNullOrEmpty(presetsID))
						{
							return;
						}

						var layerToEdit = printer.Settings.QualityLayers.Where(layer => layer.LayerID == presetsID).FirstOrDefault();

						var presetsContext = new PresetsContext(printer.Settings.QualityLayers, layerToEdit)
						{
							LayerType = NamedSettingsLayers.Quality,
							SetAsActive = (qualityKey) => printer.Settings.ActiveQualityKey = qualityKey,
							DeleteLayer = () =>
							{
								printer.Settings.QualityLayers.Remove(layerToEdit);
								printer.Settings.Save();

								// Clear QualityKey after removing layer to ensure listeners see update
								printer.Settings.ActiveQualityKey = "";
								RebuildDropDownList(true);
							}
						};

						if (presetsContext.PersistenceLayer == null)
						{
							return;
						}

						var editQualityPresetsWindow = new SlicePresetsPage(printer, presetsContext, false);
						editQualityPresetsWindow.Closed += (s, e2) =>
						{
							ApplicationController.Instance.AcitveSlicePresetsPage = null;
						};

						ApplicationController.Instance.AcitveSlicePresetsPage = editQualityPresetsWindow;
						DialogWindow.Show(editQualityPresetsWindow);
					}
					else
					{
						ApplicationController.Instance.AcitveSlicePresetsPage.DialogWindow.BringToFront();
					}
				}
			};

			container.AddChild(dropDownList);
			container.AddChild(editButton);

			return container;
		}

		public override void OnDrawBackground(Graphics2D graphics2D)
		{
			// base.OnDrawBackground(graphics2D);
			if (this.BackgroundColor != Color.Transparent)
			{
				graphics2D.Render(new RoundedRect(this.LocalBounds, 5), this.BackgroundColor);
			}
		}

		public override void OnClosed(EventArgs e)
		{
			// Unregister listeners
			printer.Settings.SettingChanged -= Printer_SettingChanged;

			base.OnClosed(e);
		}

		private void Printer_SettingChanged(object s, StringEventArgs stringEvent)
		{
			if (stringEvent != null
				&& (stringEvent.Data == SettingsKey.default_material_presets
					|| (layerType == NamedSettingsLayers.Material && stringEvent.Data == SettingsKey.active_material_key)
					|| (layerType == NamedSettingsLayers.Quality && stringEvent.Data == SettingsKey.active_quality_key)
					|| stringEvent.Data == SettingsKey.layer_name))
			{
				RebuildDropDownList(false);
			}
		}

		private DropDownList CreateDropdown()
		{
			var dropDownList = new MHDropDownList(defaultMenuItemText, theme, maxHeight: 300 * GuiWidget.DeviceScale, useLeftIcons: true)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Center,
				MenuItemsPadding = new BorderDouble(10, 3, 7, 3),
			};

			dropDownList.Name = layerType.ToString() + " DropDown List";
			dropDownList.Margin = 0;
			dropDownList.MinimumSize = new Vector2(dropDownList.LocalBounds.Width, dropDownList.LocalBounds.Height);

			MenuItem defaultMenuItem = dropDownList.AddItem(defaultMenuItemText, "");
			defaultMenuItem.Selected += MenuItem_Selected;

			var listSource = (layerType == NamedSettingsLayers.Material) ? printer.Settings.MaterialLayers : printer.Settings.QualityLayers;
			foreach (var layer in listSource.OrderBy(l => l.Name))
			{
				MenuItem menuItem = dropDownList.AddItem(layer.Name, layer.LayerID);
				menuItem.Name = layer.Name + " Menu";
				menuItem.Selected += MenuItem_Selected;
			}

			var addString = layerType == NamedSettingsLayers.Material ? "New Material".Localize() : "New Quality Setting".Localize();
			
			MenuItem addNewPreset = dropDownList.AddItem(
				StaticData.Instance.LoadIcon("icon_plus.png", 16, 16).GrayToColor(theme.TextColor),
				addString + "...",
				"new",
				pointSize: theme.DefaultFontSize);

			var selectedIndex = dropDownList.SelectedIndex;

			dropDownList.SelectionChanged += (s3, e3) =>
			{
				if (dropDownList.SelectedIndex < dropDownList.MenuItems.Count - 1)
				{
					selectedIndex = dropDownList.SelectedIndex;
				}
			};

			addNewPreset.Selected += (s, e) =>
			{
				if (layerType == NamedSettingsLayers.Material)
				{
					var window = DialogWindow.Show(new AddMaterialDialog((newMaterial) =>
                    {
						printer.Settings.MaterialLayers.Add(newMaterial);
						printer.Settings.ActiveMaterialKey = newMaterial.LayerID;

						RebuildDropDownList(true);
					},
					() =>
					{
						var newMaterial = new PrinterSettingsLayer();
						newMaterial.Name = "Material" + printer.Settings.MaterialLayers.Count;
						printer.Settings.MaterialLayers.Add(newMaterial);
						printer.Settings.ActiveMaterialKey = newMaterial.LayerID;

						RebuildDropDownList(true);

						editButton.InvokeClick();
					}));

					window.Closed += (s2, e2) =>
					{
						if (dropDownList.SelectedIndex >= dropDownList.MenuItems.Count - 1)
                        {
							dropDownList.SelectedIndex = selectedIndex < dropDownList.MenuItems.Count - 1 ? selectedIndex : 0;
                        }
					};
				}
				else
				{
					var newQuality = new PrinterSettingsLayer();
					newQuality.Name = "Quality" + printer.Settings.QualityLayers.Count;
					printer.Settings.QualityLayers.Add(newQuality);
					printer.Settings.ActiveQualityKey = newQuality.LayerID;

					RebuildDropDownList(true);

					editButton.InvokeClick();
				}
			};

			try
			{
				string settingsKey;

				if (layerType == NamedSettingsLayers.Material)
				{
					if (extruderIndex == 0)
					{
						settingsKey = printer.Settings.ActiveMaterialKey;
					}
					else // try to find the right material based on the extruders temperature
					{
						settingsKey = null;
						var extruderTemp = printer.Settings.GetValue<double>(SettingsKey.temperature1).ToString();

						// first try to find the temp in the temperature1 settings
						bool foundTemp = false;
						foreach (var materialLayer in printer.Settings.MaterialLayers)
						{
							if (materialLayer.TryGetValue(SettingsKey.temperature1, out string temp))
							{
								if (temp == extruderTemp)
								{
									settingsKey = materialLayer.LayerID;
									foundTemp = true;
								}
							}
						}

						if (!foundTemp)
						{
							// search for the temp in T0 temperature settings
							foreach (var materialLayer in printer.Settings.MaterialLayers)
							{
								if (materialLayer.TryGetValue(SettingsKey.temperature, out string temp))
								{
									if (temp == extruderTemp)
									{
										settingsKey = materialLayer.LayerID;
									}
								}
							}
						}
					}
				}
				else
				{
					settingsKey = printer.Settings.ActiveQualityKey;
				}

				if (!string.IsNullOrEmpty(settingsKey))
				{
					dropDownList.SelectedValue = settingsKey;
				}
			}
			catch (Exception ex)
			{
				GuiWidget.BreakInDebugger(ex.Message);
			}

			return dropDownList;
		}

        private void MenuItem_Selected(object sender, EventArgs e)
		{
			// When a preset is selected store the current values of all known settings to compare against after applying the preset
			Dictionary<string, string> settingBeforeChange = new Dictionary<string, string>();
			foreach (var keyName in PrinterSettings.KnownSettings)
			{
				settingBeforeChange.Add(keyName, printer.Settings.GetValue(keyName));
			}

			var activeSettings = printer.Settings;
			MenuItem item = (MenuItem)sender;

			PrinterSettingsLayer prevSelection = null;
			PrinterSettingsLayer nextSelection = null;

			if (layerType == NamedSettingsLayers.Material)
			{
				if (extruderIndex == 0)
				{
					if (activeSettings.ActiveMaterialKey != item.Value)
					{
						// Restore deactivated user overrides by iterating the Material preset we're coming off of
						activeSettings.RestoreConflictingUserOverrides(activeSettings.MaterialLayer);

						activeSettings.ActiveMaterialKey = item.Value;
					}
				}
				else // set the temperature for the given extruder
				{
					var selectedMaterial = activeSettings.MaterialLayers.Where(l => l.LayerID == item.Value).FirstOrDefault();
					if (selectedMaterial != null)
					{
						// first check if the material has an explicit temperature for T1
						if (selectedMaterial.TryGetValue(SettingsKey.temperature1, out string temperature1))
						{
							activeSettings.SetValue(SettingsKey.temperature1, temperature1);
						}
						else
						{
							selectedMaterial.TryGetValue(SettingsKey.temperature, out string temperature);
							activeSettings.SetValue(SettingsKey.temperature1, temperature);
						}
					}
					else
					{
						activeSettings.SetValue(SettingsKey.temperature1, printer.Settings.GetValue(SettingsKey.temperature));
					}
				}
			}
			else if (layerType == NamedSettingsLayers.Quality)
			{
				if (activeSettings.ActiveQualityKey != item.Value)
				{
					prevSelection = activeSettings.QualityLayer;
					// Restore deactivated user overrides by iterating the Quality preset we're coming off of
					activeSettings.RestoreConflictingUserOverrides(activeSettings.QualityLayer);

					activeSettings.ActiveQualityKey = item.Value;
					nextSelection = activeSettings.QualityLayer;
				}
			}

			// Ensure that activated or deactivated user overrides are always persisted to disk
			activeSettings.Save();

			UiThread.RunOnIdle(() =>
			{
				var settingsToUpdate = new HashSet<string>();
				foreach (var keyName in PrinterSettings.KnownSettings)
				{
					if (settingBeforeChange[keyName] != printer.Settings.GetValue(keyName))
					{
						settingsToUpdate.Add(keyName);
					}
				}

				if (prevSelection != null)
				{
					foreach (var kvp in prevSelection)
					{
						settingsToUpdate.Add(kvp.Key);
					}
				}

				if (nextSelection != null)
				{
					foreach (var kvp in nextSelection)
					{
						settingsToUpdate.Add(kvp.Key);
					}
				}

				// we are about to change many settings only save on the last one
				var updateList = settingsToUpdate.ToList();
				ProfileManager.SaveOnSingleSettingChange = false;
				for (int i=0; i< updateList.Count-1; i++)
                {
					printer.Settings.OnSettingChanged(updateList[i]);
				}
				ProfileManager.SaveOnSingleSettingChange = true;
				if (updateList.Count > 0)
				{
					printer.Settings.OnSettingChanged(updateList[updateList.Count - 1]);
				}

				// update the style of every setting
				var sliceSettingsWidget = this.Parents<SliceSettingsWidget>().FirstOrDefault();
				if (sliceSettingsWidget != null)
                {
					sliceSettingsWidget.UpdateAllStyles();
				}
			});

			editButton.Enabled = item.Text != defaultMenuItemText;
		}

		private void RebuildDropDownList(bool updateAllSettings)
		{
			pullDownContainer.CloseChildren();
			pullDownContainer.AddChild(this.NewPulldownContainer());

			var sliceSettingsWidget = this.Parents<SliceSettingsWidget>().FirstOrDefault();
			if (sliceSettingsWidget != null)
			{
				if (updateAllSettings)
				{
                    throw new NotImplementedException();
                }
				else
                {
					sliceSettingsWidget.UpdateAllStyles();
				}
			}
		}

		private void SettingsLayers_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
		{
			RebuildDropDownList(false);
		}
	}
}