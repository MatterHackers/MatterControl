/*
Copyright (c) 2014, Kevin Pope
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

using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.ImageProcessing;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.ImageProcessing;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class PresetSelectorWidget : FlowLayoutWidget
	{
		private Button editButton;
		private NamedSettingsLayers layerType;
		private StyledDropDownList dropDownList;

		private int extruderIndex; //For multiple materials

		public PresetSelectorWidget(string label, RGBA_Bytes accentColor, NamedSettingsLayers layerType, int extruderIndex)
			: base(FlowDirection.TopToBottom)
		{
			this.extruderIndex = extruderIndex;
			this.layerType = layerType;
			
			this.HAnchor = HAnchor.ParentLeftRight;
			this.VAnchor = Agg.UI.VAnchor.Max_FitToChildren_ParentHeight;
			this.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

			GuiWidget accentBar = new GuiWidget(7, 5)
			{
				BackgroundColor = accentColor,
				HAnchor = HAnchor.ParentLeftRight
			};

			TextWidget labelText = new TextWidget(label.Localize().ToUpper())
			{
				TextColor = ActiveTheme.Instance.PrimaryTextColor,
				HAnchor = Agg.UI.HAnchor.ParentCenter,
				Margin = new BorderDouble(0, 3, 0, 6)
			};

			this.AddChild(labelText);
			this.AddChild(GetPulldownContainer());
			this.AddChild(new VerticalSpacer());
			this.AddChild(accentBar);
		}

		public virtual FlowLayoutWidget GetPulldownContainer()
		{
			dropDownList = CreateDropdown();

			FlowLayoutWidget container = new FlowLayoutWidget();
			container.HAnchor = HAnchor.ParentLeftRight;
			container.Padding = new BorderDouble(6, 0);

			editButton = TextImageButtonFactory.GetThemedEditButton();

			editButton.VAnchor = VAnchor.ParentCenter;
			editButton.Margin = new BorderDouble(right: 6);
			editButton.Click += (sender, e) =>
			{
				if (layerType == NamedSettingsLayers.Material)
                {
					if (ApplicationController.Instance.EditMaterialPresetsWindow == null)
					{
						string presetsKey = ActiveSliceSettings.Instance.MaterialPresetKey(extruderIndex);
						ApplicationController.Instance.EditMaterialPresetsWindow = new SlicePresetsWindow(ActiveSliceSettings.Instance.MaterialLayer(presetsKey), NamedSettingsLayers.Material, presetsKey);
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
						string presetsKey = ActiveSliceSettings.Instance.ActiveQualityKey;
						ApplicationController.Instance.EditQualityPresetsWindow = new SlicePresetsWindow(ActiveSliceSettings.Instance.QualityLayer(presetsKey), NamedSettingsLayers.Quality, presetsKey);
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

			container.AddChild(editButton);
			container.AddChild(dropDownList);

			return container;
		}

		private void MenuItem_Selected(object sender, EventArgs e)
		{
			var activeSettings = ActiveSliceSettings.Instance;
			MenuItem item = (MenuItem)sender;
			if (layerType == NamedSettingsLayers.Material)
			{
				if (activeSettings.MaterialPresetKey(extruderIndex) != item.Value)
				{
					activeSettings.SetMaterialPreset(extruderIndex, item.Value);
				}
			}
			else if (layerType == NamedSettingsLayers.Quality)
			{
				if (activeSettings.ActiveQualityKey != item.Value)
				{
					activeSettings.ActiveQualityKey = item.Value;
				}
			}

			UiThread.RunOnIdle(() =>
			{
				ApplicationController.Instance.ReloadAdvancedControlsPanel();
			});
		}

		private StyledDropDownList CreateDropdown()
		{
			var dropDownList = new StyledDropDownList("- default -", maxHeight: 300, useLeftIcons: true)
			{
				HAnchor = HAnchor.ParentLeftRight,
				MenuItemsPadding = new BorderDouble(10, 4, 10, 6),
			};

			dropDownList.Margin = new BorderDouble(0, 3);
			dropDownList.MinimumSize = new Vector2(dropDownList.LocalBounds.Width, dropDownList.LocalBounds.Height);

			MenuItem defaultMenuItem = dropDownList.AddItem("- default -", "");
			defaultMenuItem.Selected += MenuItem_Selected;

			var listSource = (layerType == NamedSettingsLayers.Material) ? ActiveSliceSettings.Instance.AllMaterialKeys() : ActiveSliceSettings.Instance.AllQualityKeys();
			foreach (var presetName in listSource)
			{
				MenuItem menuItem = dropDownList.AddItem(presetName, presetName);
				menuItem.Selected += MenuItem_Selected;
			}

			MenuItem addNewPreset = dropDownList.AddItem(StaticData.Instance.LoadIcon("icon_plus.png", 32, 32), "Add New Setting...", "new");
			addNewPreset.Selected += (s, e) =>
			{
				var newLayer = ActiveSliceSettings.Instance.CreatePresetsLayer(layerType);
				if (layerType == NamedSettingsLayers.Quality)
				{
					ActiveSliceSettings.Instance.ActiveQualityKey = newLayer.Name;
				}
				else if (layerType == NamedSettingsLayers.Material)
				{
					ActiveSliceSettings.Instance.SetMaterialPreset(this.extruderIndex, newLayer.Name);
				}

				// TODO: Consider adding a .Replace(existingWidget, newWidget) to GuiWidget 
				// Replace existing list with updated list
				var parent = this.dropDownList.Parent;
				parent.RemoveChild(this.dropDownList);
				this.dropDownList.Close();

				this.dropDownList = CreateDropdown();
				parent.AddChild(this.dropDownList);

				editButton.ClickButton(new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0));
			};

			try
			{
				string settingsKey;

				if (layerType == NamedSettingsLayers.Material)
				{
					settingsKey = ActiveSliceSettings.Instance.MaterialPresetKey(extruderIndex);
				}
				else
				{
					settingsKey = ActiveSliceSettings.Instance.ActiveQualityKey;
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
	}

	public class SliceEngineSelector : StyledDropDownList
	{
		public SliceEngineSelector(string label)
			: base(label)
		{
			HAnchor = HAnchor.ParentLeftRight;

			//Add Each SliceEngineInfo Objects to DropMenu
			foreach (SliceEngineInfo engineMenuItem in SlicingQueue.AvailableSliceEngines)
			{
				bool engineAllowed = true;
				if (ActiveSliceSettings.Instance.ExtruderCount() > 1 && engineMenuItem.Name != "MatterSlice")
				{
					engineAllowed = false;
				}

				if (engineAllowed)
				{
					MenuItem item = AddItem(engineMenuItem.Name);
					SlicingEngineTypes itemEngineType = engineMenuItem.GetSliceEngineType();
					item.Selected += (sender, e) =>
					{
						if (ActiveSliceSettings.Instance.ActiveSliceEngineType() != itemEngineType)
						{
							ActiveSliceSettings.Instance.ActiveSliceEngineType(itemEngineType);
							ApplicationController.Instance.ReloadAdvancedControlsPanel();
						}
					};

					//Set item as selected if it matches the active slice engine
					if (engineMenuItem.GetSliceEngineType() == ActiveSliceSettings.Instance.ActiveSliceEngineType())
					{
						SelectedLabel = engineMenuItem.Name;
					}
				}
			}

			//If nothing is selected (i.e. selected engine is not available) set to
			if (SelectedLabel == "")
			{
				try
				{
					SelectedLabel = MatterSliceInfo.DisplayName;
				}
				catch (Exception ex)
				{
					GuiWidget.BreakInDebugger(ex.Message);
					throw new Exception("Unable to find MatterSlice executable");
				}
			}

			MinimumSize = new Vector2(LocalBounds.Width, LocalBounds.Height);
		}
	}
}