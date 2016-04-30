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
		private ImageButtonFactory imageButtonFactory = new ImageButtonFactory();

		private string filterTag;
		private string filterLabel;
		public StyledDropDownList DropDownList;
		private TupleList<string, Func<bool>> DropDownMenuItems = new TupleList<string, Func<bool>>();

		private int extruderIndex; //For multiple materials

		public PresetSelectorWidget(string label, RGBA_Bytes accentColor, string tag, int extruderIndex)
			: base(FlowDirection.TopToBottom)
		{
			this.extruderIndex = extruderIndex;
			this.filterLabel = label;
			this.filterTag = (tag == null) ? label.ToLower() : tag;
			
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
			DropDownList = CreateDropdown();

			FlowLayoutWidget container = new FlowLayoutWidget();
			container.HAnchor = HAnchor.ParentLeftRight;
			container.Padding = new BorderDouble(6, 0);

			ImageBuffer normalImage = StaticData.Instance.LoadIcon("icon_edit_white_32x32.png");
			int iconSize = (int)(16 * TextWidget.GlobalPointSizeScaleRatio);
			normalImage = ImageBuffer.CreateScaledImage(normalImage, iconSize, iconSize);

			editButton = imageButtonFactory.Generate(normalImage, WhiteToColor.CreateWhiteToColor(normalImage, RGBA_Bytes.Gray));

			editButton.VAnchor = VAnchor.ParentCenter;
			editButton.Margin = new BorderDouble(right: 6);
			editButton.Click += (sender, e) =>
			{
#if DO_IN_PLACE_EDIT
                if (filterTag == "quality")
                {
                    SliceSettingsWidget.SettingsIndexBeingEdited = 2;
                }
                else
                {
                    SliceSettingsWidget.SettingsIndexBeingEdited = 3;
                }
                // If there is a setting selected then reload the slice setting widget with the presetIndex to edit.
                ApplicationController.Instance.ReloadAdvancedControlsPanel();
                // If no setting selected then call onNewItemSelect(object sender, EventArgs e)
#else
				if (filterTag == "material")
				{
					if (ApplicationController.Instance.EditMaterialPresetsWindow == null)
					{
						ApplicationController.Instance.EditMaterialPresetsWindow = new SlicePresetsWindow(ReloadOptions, filterLabel, filterTag);
						ApplicationController.Instance.EditMaterialPresetsWindow.Closed += (popupWindowSender, popupWindowSenderE) => { ApplicationController.Instance.EditMaterialPresetsWindow = null; };
					}
					else
					{
						ApplicationController.Instance.EditMaterialPresetsWindow.BringToFront();
					}
				}

				if (filterTag == "quality")
				{
					if (ApplicationController.Instance.EditQualityPresetsWindow == null)
					{
						ApplicationController.Instance.EditQualityPresetsWindow = new SlicePresetsWindow(ReloadOptions, filterLabel, filterTag);
						ApplicationController.Instance.EditQualityPresetsWindow.Closed += (popupWindowSender, popupWindowSenderE) => { ApplicationController.Instance.EditQualityPresetsWindow = null; };
					}
					else
					{
						ApplicationController.Instance.EditQualityPresetsWindow.BringToFront();
					}
				}
#endif
			};

			container.AddChild(editButton);
			container.AddChild(DropDownList);

			return container;
		}

		protected void ReloadOptions(object sender, EventArgs e)
		{
			ApplicationController.Instance.ReloadAdvancedControlsPanel();
		}

		private void onItemSelect(object sender, EventArgs e)
		{
			var activeSettings = ActiveSliceSettings.Instance;
			MenuItem item = (MenuItem)sender;
			if (filterTag == "material")
			{
				if (activeSettings.MaterialPresetKey(extruderIndex) != item.Text)
				{
					activeSettings.SetMaterialPreset(extruderIndex, item.Text);
				}
			}
			else if (filterTag == "quality")
			{
				if (activeSettings.ActiveQualityKey != item.Text)
				{
					activeSettings.ActiveQualityKey = item.Text;
				}
			}

			UiThread.RunOnIdle(() =>
			{
				ApplicationController.Instance.ReloadAdvancedControlsPanel();
			});
		}

		private void onNewItemSelect(object sender, EventArgs e)
		{
#if DO_IN_PLACE_EDIT
            // pop up a dialog to request a new setting name
            // after getting the new name select it and reload the slice setting widget editing the new setting
            throw new NotImplementedException();
#else
			UiThread.RunOnIdle(() =>
			{
				ApplicationController.Instance.ReloadAdvancedControlsPanel();
				if (filterTag == "material")
				{
					if (ApplicationController.Instance.EditMaterialPresetsWindow == null)
					{
						ApplicationController.Instance.EditMaterialPresetsWindow = new SlicePresetsWindow(ReloadOptions, filterLabel, filterTag, false);
						ApplicationController.Instance.EditMaterialPresetsWindow.Closed += (popupWindowSender, popupWindowSenderE) => { ApplicationController.Instance.EditMaterialPresetsWindow = null; };
					}
					else
					{
						ApplicationController.Instance.EditMaterialPresetsWindow.ChangeToSlicePresetFromID("");
						ApplicationController.Instance.EditMaterialPresetsWindow.BringToFront();
					}
				}
				if (filterTag == "quality")
				{
					if (ApplicationController.Instance.EditQualityPresetsWindow == null)
					{
						ApplicationController.Instance.EditQualityPresetsWindow = new SlicePresetsWindow(ReloadOptions, filterLabel, filterTag, false);
						ApplicationController.Instance.EditQualityPresetsWindow.Closed += (popupWindowSender, popupWindowSenderE) => { ApplicationController.Instance.EditQualityPresetsWindow = null; };
					}
					else
					{
						ApplicationController.Instance.EditQualityPresetsWindow.ChangeToSlicePresetFromID("");
						ApplicationController.Instance.EditQualityPresetsWindow.BringToFront();
					}
				}
			});
#endif
		}

		private StyledDropDownList CreateDropdown()
		{
			var dropDownList = new StyledDropDownList("- default -", maxHeight: 300)
			{
				UseLeftIcons = true,
				HAnchor = HAnchor.ParentLeftRight,
				MenuItemsPadding = new BorderDouble(10, 4, 10, 6),
			};

			dropDownList.Margin = new BorderDouble(0, 3);
			dropDownList.MinimumSize = new Vector2(dropDownList.LocalBounds.Width, dropDownList.LocalBounds.Height);

			MenuItem defaultMenuItem = dropDownList.AddItem("- default -", "0");
			defaultMenuItem.Selected += new EventHandler(onItemSelect);

			var listSource = (filterTag == "material") ? ActiveSliceSettings.Instance.AllMaterialKeys() : ActiveSliceSettings.Instance.AllQualityKeys();
			foreach (var presetName in listSource)
			{
				MenuItem menuItem = dropDownList.AddItem(presetName, presetName);
				menuItem.Selected += onItemSelect;
			}

			MenuItem addNewPreset = dropDownList.AddItem(InvertLightness.DoInvertLightness(StaticData.Instance.LoadIcon("icon_circle_plus.png")), "Add New Setting...", "new");
			addNewPreset.Selected += onNewItemSelect;

			if (false)
			{
				FlowLayoutWidget container = new FlowLayoutWidget();
				container.HAnchor = HAnchor.ParentLeftRight;

				TextImageButtonFactory buttonFactory = new TextImageButtonFactory();
				buttonFactory.normalTextColor = ActiveTheme.Instance.PrimaryTextColor;
				buttonFactory.hoverTextColor = ActiveTheme.Instance.PrimaryTextColor;
				buttonFactory.disabledTextColor = ActiveTheme.Instance.PrimaryTextColor;
				buttonFactory.pressedTextColor = ActiveTheme.Instance.PrimaryTextColor;
				buttonFactory.borderWidth = 0;

				Button addPresetButton = buttonFactory.Generate(LocalizedString.Get("Add"), "icon_circle_plus.png");
				addPresetButton.ToolTipText = "Add a new Settings Preset".Localize();
				addPresetButton.Click += (sender, e) =>
				{
					onNewItemSelect(sender, e);
				};
				container.AddChild(addPresetButton);

				Button importPresetButton = buttonFactory.Generate(LocalizedString.Get("Import"));
				importPresetButton.ToolTipText = "Import an existing Settings Preset".Localize();
				importPresetButton.Click += (sender, e) =>
				{
				};
				container.AddChild(importPresetButton);

				dropDownList.MenuItems.Add(new MenuItem(container));
			}

			try
			{
				string settingsKey;

				if (filterTag == "material")
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
		private TupleList<string, Func<bool>> engineOptionsMenuItems = new TupleList<string, Func<bool>>();

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