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
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.ImageProcessing;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class SliceSelectorWidget : FlowLayoutWidget
	{
		private Button editButton;
		private ImageButtonFactory imageButtonFactory = new ImageButtonFactory();

		private string filterTag;
		private string filterLabel;
		public AnchoredDropDownList DropDownList;
		private TupleList<string, Func<bool>> DropDownMenuItems = new TupleList<string, Func<bool>>();
		private int presetIndex; //For multiple materials

		public SliceSelectorWidget(string label, RGBA_Bytes accentColor, string tag = null, int presetIndex = 1)
			: base(FlowDirection.TopToBottom)
		{
			this.presetIndex = presetIndex;
			this.filterLabel = label;
			if (tag == null)
			{
				this.filterTag = label.ToLower();
			}
			else
			{
				this.filterTag = tag;
			}

			this.HAnchor = HAnchor.ParentLeftRight;
			this.VAnchor = Agg.UI.VAnchor.Max_FitToChildren_ParentHeight;
			this.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

			GuiWidget accentBar = new GuiWidget(7, 5);
			accentBar.BackgroundColor = accentColor;
			accentBar.HAnchor = HAnchor.ParentLeftRight;


			TextWidget labelText = new TextWidget(LocalizedString.Get(label).ToUpper());
			labelText.TextColor = ActiveTheme.Instance.PrimaryTextColor;
			labelText.HAnchor = Agg.UI.HAnchor.ParentCenter;
			labelText.Margin = new BorderDouble(0, 3, 0, 6);

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
                // If there is a setting selected then reload the silce setting widget with the presetIndex to edit.
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

		private IEnumerable<DataStorage.SliceSettingsCollection> GetCollections()
		{
			IEnumerable<DataStorage.SliceSettingsCollection> results = Enumerable.Empty<DataStorage.SliceSettingsCollection>();

			//Retrieve a list of collections matching from the Datastore
			if (ActivePrinterProfile.Instance.ActivePrinter != null)
			{
				string query = string.Format("SELECT * FROM SliceSettingsCollection WHERE Tag = '{0}' AND PrinterId = {1} ORDER BY Name;", filterTag, ActivePrinterProfile.Instance.ActivePrinter.Id);
				results = (IEnumerable<DataStorage.SliceSettingsCollection>)DataStorage.Datastore.Instance.dbSQLite.Query<DataStorage.SliceSettingsCollection>(query);
			}
			return results;
		}

		private void onItemSelect(object sender, EventArgs e)
		{
			MenuItem item = (MenuItem)sender;
			if (filterTag == "material")
			{
				if (ActivePrinterProfile.Instance.GetMaterialSetting(presetIndex) != Int32.Parse(item.Value))
				{
					ActivePrinterProfile.Instance.SetMaterialSetting(presetIndex, Int32.Parse(item.Value));
				}
			}
			else if (filterTag == "quality")
			{
				if (ActivePrinterProfile.Instance.ActiveQualitySettingsID != Int32.Parse(item.Value))
				{
					ActivePrinterProfile.Instance.ActiveQualitySettingsID = Int32.Parse(item.Value);
				}
			}
			UiThread.RunOnIdle(() =>
			{
				ActiveSliceSettings.Instance.LoadAllSettings();
				ApplicationController.Instance.ReloadAdvancedControlsPanel();
			});
		}

		private void onNewItemSelect(object sender, EventArgs e)
		{
#if DO_IN_PLACE_EDIT
            // pop up a dialog to request a new setting name
            // after getting the new name select it and relead the slice setting widget editing the new setting
            throw new NotImplementedException();
#else
			UiThread.RunOnIdle(() =>
			{
				ActiveSliceSettings.Instance.LoadAllSettings();
				ApplicationController.Instance.ReloadAdvancedControlsPanel();
				if (filterTag == "material")
				{
					if (ApplicationController.Instance.EditMaterialPresetsWindow == null)
					{
						ApplicationController.Instance.EditMaterialPresetsWindow = new SlicePresetsWindow(ReloadOptions, filterLabel, filterTag, false, 0);
						ApplicationController.Instance.EditMaterialPresetsWindow.Closed += (popupWindowSender, popupWindowSenderE) => { ApplicationController.Instance.EditMaterialPresetsWindow = null; };
					}
					else
					{
						ApplicationController.Instance.EditMaterialPresetsWindow.ChangeToSlicePresetFromID(0);
						ApplicationController.Instance.EditMaterialPresetsWindow.BringToFront();
					}
				}
				if (filterTag == "quality")
				{
					if (ApplicationController.Instance.EditQualityPresetsWindow == null)
					{
						ApplicationController.Instance.EditQualityPresetsWindow = new SlicePresetsWindow(ReloadOptions, filterLabel, filterTag, false, 0);
						ApplicationController.Instance.EditQualityPresetsWindow.Closed += (popupWindowSender, popupWindowSenderE) => { ApplicationController.Instance.EditQualityPresetsWindow = null; };
					}
					else
					{
						ApplicationController.Instance.EditQualityPresetsWindow.ChangeToSlicePresetFromID(0);
						ApplicationController.Instance.EditQualityPresetsWindow.BringToFront();
					}
				}
			});
#endif
		}

		private AnchoredDropDownList CreateDropdown()
		{
			AnchoredDropDownList dropDownList = new AnchoredDropDownList("- default -", maxHeight: 300);
			dropDownList.Margin = new BorderDouble(0, 3);
			dropDownList.MinimumSize = new Vector2(dropDownList.LocalBounds.Width, dropDownList.LocalBounds.Height);
			MenuItem defaultMenuItem = dropDownList.AddItem("- default -", "0");
			defaultMenuItem.Selected += new EventHandler(onItemSelect);

			IEnumerable<DataStorage.SliceSettingsCollection> collections = GetCollections();
			foreach (DataStorage.SliceSettingsCollection collection in collections)
			{
				MenuItem menuItem = dropDownList.AddItem(collection.Name, collection.Id.ToString());
				menuItem.Selected += new EventHandler(onItemSelect);
			}

			MenuItem addNewPreset = dropDownList.AddItem("<< Add >>", "new");
			addNewPreset.Selected += new EventHandler(onNewItemSelect);

			if (filterTag == "material")
			{
				try
				{
					dropDownList.SelectedValue = ActivePrinterProfile.Instance.GetMaterialSetting(presetIndex).ToString();
				}
				catch (Exception e)
				{
					Debug.Print(e.Message);
					GuiWidget.BreakInDebugger();
					//Unable to set selected value
				}
			}
			else if (filterTag == "quality")
			{
				try
				{
					dropDownList.SelectedValue = ActivePrinterProfile.Instance.ActiveQualitySettingsID.ToString();
				}
				catch (Exception e)
				{
					Debug.Print(e.Message);
					GuiWidget.BreakInDebugger();
					//Unable to set selected value
				}
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
				if (ActiveSliceSettings.Instance.ExtruderCount > 1 && engineMenuItem.Name != "MatterSlice")
				{
					engineAllowed = false;
				}

				if (engineAllowed)
				{
					MenuItem item = AddItem(engineMenuItem.Name);
					ActivePrinterProfile.SlicingEngineTypes itemEngineType = engineMenuItem.GetSliceEngineType();
					item.Selected += (sender, e) =>
					{
						if (ActivePrinterProfile.Instance.ActiveSliceEngineType != itemEngineType)
						{
							ActivePrinterProfile.Instance.ActiveSliceEngineType = itemEngineType;
							ApplicationController.Instance.ReloadAdvancedControlsPanel();
						}
					};

					//Set item as selected if it matches the active slice engine
					if (engineMenuItem.GetSliceEngineType() == ActivePrinterProfile.Instance.ActiveSliceEngineType)
					{
						SelectedLabel = engineMenuItem.Name;
					}
				}
			}

			//If nothing is selected (ie selected engine is not available) set to
			if (SelectedLabel == "")
			{
				try
				{
					SelectedLabel = MatterSliceInfo.DisplayName;
				}
				catch (Exception e)
				{
					Debug.Print(e.Message);
					GuiWidget.BreakInDebugger();
					throw new Exception("MatterSlice is not available, for some strange reason");
				}
			}

			MinimumSize = new Vector2(LocalBounds.Width, LocalBounds.Height);
		}
	}
}