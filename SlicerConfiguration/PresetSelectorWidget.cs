/*
Copyright (c) 2016, Kevin Pope, John Lewin
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
//#define DO_IN_PLACE_EDIT

using System;
using System.Collections.Generic;
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class PresetSelectorWidget : FlowLayoutWidget
	{
		public DropDownList DropDownList;
		private string defaultMenuItemText = "- none -".Localize();
		private Button editButton;
		private int extruderIndex;
		private NamedSettingsLayers layerType;
		private PrinterConfig printer;
		private GuiWidget pullDownContainer;
		private EventHandler unregisterEvents;
		private bool whiteBackground;
		//For multiple materials

		public PresetSelectorWidget(PrinterConfig printer, string label, RGBA_Bytes accentColor, NamedSettingsLayers layerType, int extruderIndex, bool whiteBackground = false)
			: base(FlowDirection.TopToBottom)
		{
			this.printer = printer;
			this.whiteBackground = whiteBackground;
			Name = label;

			ActiveSliceSettings.MaterialPresetChanged += ActiveSliceSettings_MaterialPresetChanged;

			ActiveSliceSettings.SettingChanged.RegisterEvent((s, e) =>
			{
				StringEventArgs stringEvent = e as StringEventArgs;
				if (stringEvent != null
					&& stringEvent.Data == SettingsKey.default_material_presets)
				{
					RebuildDropDownList();
				}
			}, ref unregisterEvents);

			this.extruderIndex = extruderIndex;
			this.layerType = layerType;

			this.HAnchor = HAnchor.MaxFitOrStretch;
			this.VAnchor = VAnchor.MaxFitOrStretch;
			this.BackgroundColor = ActiveTheme.Instance.TertiaryBackgroundColor;

			GuiWidget accentBar = new GuiWidget(7, 3)
			{
				BackgroundColor = accentColor,
				HAnchor = HAnchor.Stretch
			};

			// Section Label
			this.AddChild(new TextWidget(label.Localize().ToUpper())
			{
				TextColor = whiteBackground ? RGBA_Bytes.Black : ActiveTheme.Instance.PrimaryTextColor,
				HAnchor = HAnchor.Left,
				Margin = new BorderDouble(12, 3, 0, 6)
			});

			pullDownContainer = new GuiWidget()
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit
			};
			pullDownContainer.AddChild(GetPulldownContainer());
			this.AddChild(pullDownContainer);

			this.AddChild(new VerticalSpacer());
			this.AddChild(accentBar);
		}

		public FlowLayoutWidget GetPulldownContainer()
		{
			DropDownList = CreateDropdown();

			FlowLayoutWidget container = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.MaxFitOrStretch,
				Padding = new BorderDouble(12, 0),
				Name = "Preset Pulldown Container"
			};

			editButton = ApplicationController.Instance.Theme.ButtonFactory.GenerateIconButton(AggContext.StaticData.LoadIcon("icon_edit.png", 16, 16), whiteBackground ? IconColor.Black : IconColor.Theme);
			editButton.ToolTipText = "Edit Selected Setting".Localize();
			editButton.Enabled = DropDownList.SelectedIndex != -1;
			editButton.VAnchor = VAnchor.Center;
			editButton.Margin = new BorderDouble(left: 6);
			editButton.Click += (sender, e) =>
			{
				if (layerType == NamedSettingsLayers.Material)
				{
					if (ApplicationController.Instance.EditMaterialPresetsWindow == null)
					{
						string presetsID = printer.Settings.GetMaterialPresetKey(extruderIndex);
						if (string.IsNullOrEmpty(presetsID))
						{
							return;
						}

						var layerToEdit = printer.Settings.MaterialLayers.Where(layer => layer.LayerID == presetsID).FirstOrDefault();

						var presetsContext = new PresetsContext(printer.Settings.MaterialLayers, layerToEdit)
						{
							LayerType = NamedSettingsLayers.Material,
							SetAsActive = (materialKey) =>
							{
								printer.Settings.SetMaterialPreset(this.extruderIndex, materialKey);
							},
							DeleteLayer = () =>
							{
								var materialKeys = printer.Settings.MaterialSettingsKeys;
								for (var i = 0; i < materialKeys.Count; i++)
								{
									if (materialKeys[i] == presetsID)
									{
										materialKeys[i] = "";
									}
								}

								printer.Settings.SetMaterialPreset(extruderIndex, "");
								printer.Settings.MaterialLayers.Remove(layerToEdit);
								printer.Settings.Save();

								UiThread.RunOnIdle(() => ApplicationController.Instance.ReloadAdvancedControlsPanel());
							}
						};

						ApplicationController.Instance.EditMaterialPresetsWindow = new SlicePresetsWindow(printer, presetsContext);
						ApplicationController.Instance.EditMaterialPresetsWindow.Closed += (s, e2) =>
						{
							ApplicationController.Instance.EditMaterialPresetsWindow = null;
							ApplicationController.Instance.ReloadAdvancedControlsPanel();
						};
						ApplicationController.Instance.EditMaterialPresetsWindow.ShowAsSystemWindow();
					}
					else
					{
						ApplicationController.Instance.EditMaterialPresetsWindow.BringToFront();
					}
				}

				if (layerType == NamedSettingsLayers.Quality)
				{
					if (ApplicationController.Instance.EditQualityPresetsWindow == null)
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
								printer.Settings.ActiveQualityKey = "";
								printer.Settings.QualityLayers.Remove(layerToEdit);
								printer.Settings.Save();

								UiThread.RunOnIdle(() => ApplicationController.Instance.ReloadAdvancedControlsPanel());
							}
						};

						ApplicationController.Instance.EditQualityPresetsWindow = new SlicePresetsWindow(printer, presetsContext);
						ApplicationController.Instance.EditQualityPresetsWindow.Closed += (s, e2) =>
						{
							ApplicationController.Instance.EditQualityPresetsWindow = null;
							ApplicationController.Instance.ReloadAdvancedControlsPanel();
						};
						ApplicationController.Instance.EditQualityPresetsWindow.ShowAsSystemWindow();
					}
					else
					{
						ApplicationController.Instance.EditQualityPresetsWindow.BringToFront();
					}
				}
			};

			container.AddChild(DropDownList);
			container.AddChild(editButton);

			return container;
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			ActiveSliceSettings.MaterialPresetChanged -= ActiveSliceSettings_MaterialPresetChanged;
			if (unregisterEvents != null)
			{
				unregisterEvents(this, null);
			}
			base.OnClosed(e);
		}

		private void ActiveSliceSettings_MaterialPresetChanged(object sender, EventArgs e)
		{
			RebuildDropDownList();
		}

		private DropDownList CreateDropdown()
		{
			var dropDownList = new DropDownList(defaultMenuItemText, maxHeight: 300, useLeftIcons: true)
			{
				HAnchor = HAnchor.Stretch,
				MenuItemsPadding = new BorderDouble(10, 7, 7, 7),
				TextColor = whiteBackground ? RGBA_Bytes.Black : ActiveTheme.Instance.PrimaryTextColor,
			};

			dropDownList.Name = layerType.ToString() + " DropDown List";
			dropDownList.Margin = new BorderDouble(0, 3);
			dropDownList.MinimumSize = new Vector2(dropDownList.LocalBounds.Width, dropDownList.LocalBounds.Height);

			MenuItem defaultMenuItem = dropDownList.AddItem(defaultMenuItemText, "");
			defaultMenuItem.Selected += MenuItem_Selected;

			var listSource = (layerType == NamedSettingsLayers.Material) ? printer.Settings.MaterialLayers : printer.Settings.QualityLayers;
			foreach (var layer in listSource)
			{
				MenuItem menuItem = dropDownList.AddItem(layer.Name, layer.LayerID);
				menuItem.Name = layer.Name + " Menu";
				menuItem.Selected += MenuItem_Selected;
			}

			MenuItem addNewPreset = dropDownList.AddItem(AggContext.StaticData.LoadIcon("icon_plus.png", 32, 32), "Add New Setting".Localize() + "...", "new");
			addNewPreset.Selected += (s, e) =>
			{
				var newLayer = new PrinterSettingsLayer();
				if (layerType == NamedSettingsLayers.Quality)
				{
					newLayer.Name = "Quality" + printer.Settings.QualityLayers.Count;
					printer.Settings.QualityLayers.Add(newLayer);
					printer.Settings.ActiveQualityKey = newLayer.LayerID;
				}
				else
				{
					newLayer.Name = "Material" + printer.Settings.MaterialLayers.Count;
					printer.Settings.MaterialLayers.Add(newLayer);
					printer.Settings.SetMaterialPreset(this.extruderIndex, newLayer.LayerID);
				}

				RebuildDropDownList();

				editButton.OnClick(new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0));
			};

			try
			{
				string settingsKey;

				if (layerType == NamedSettingsLayers.Material)
				{
					settingsKey = printer.Settings.GetMaterialPresetKey(extruderIndex);

					printer.Settings.MaterialLayers.CollectionChanged += SettingsLayers_CollectionChanged;
					dropDownList.Closed += (s1, e1) =>
					{
						printer.Settings.MaterialLayers.CollectionChanged -= SettingsLayers_CollectionChanged;
					};
				}
				else
				{
					settingsKey = printer.Settings.ActiveQualityKey;

					printer.Settings.QualityLayers.CollectionChanged += SettingsLayers_CollectionChanged;
					dropDownList.Closed += (s1, e1) =>
					{
						printer.Settings.QualityLayers.CollectionChanged -= SettingsLayers_CollectionChanged;
					};
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

			if (layerType == NamedSettingsLayers.Material)
			{
				if (activeSettings.GetMaterialPresetKey(extruderIndex) != item.Value)
				{
					// Restore deactivated user overrides by iterating the Material preset we're coming off of
					activeSettings.RestoreConflictingUserOverrides(activeSettings.MaterialLayer);

					activeSettings.SetMaterialPreset(extruderIndex, item.Value);

					// Deactivate conflicting user overrides by iterating the Material preset we've just switched to
					activeSettings.DeactivateConflictingUserOverrides(activeSettings.MaterialLayer);
				}
			}
			else if (layerType == NamedSettingsLayers.Quality)
			{
				if (activeSettings.ActiveQualityKey != item.Value)
				{
					// Restore deactivated user overrides by iterating the Quality preset we're coming off of
					activeSettings.RestoreConflictingUserOverrides(activeSettings.QualityLayer);

					activeSettings.ActiveQualityKey = item.Value;

					// Deactivate conflicting user overrides by iterating the Quality preset we've just switched to
					activeSettings.DeactivateConflictingUserOverrides(activeSettings.QualityLayer);
				}
			}

			// Ensure that activated or deactivated user overrides are always persisted to disk
			activeSettings.Save();

			UiThread.RunOnIdle(() =>
			{
				ApplicationController.Instance.ReloadAdvancedControlsPanel();
				foreach (var keyName in PrinterSettings.KnownSettings)
				{
					if (settingBeforeChange[keyName] != printer.Settings.GetValue(keyName))
					{
						ActiveSliceSettings.OnSettingChanged(keyName);
					}
				}
			});

			editButton.Enabled = item.Text != defaultMenuItemText;
		}

		private void RebuildDropDownList()
		{
			pullDownContainer.CloseAllChildren();
			pullDownContainer.AddChild(GetPulldownContainer());
		}

		private void SettingsLayers_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
		{
			RebuildDropDownList();
		}
	}
}