/*
Copyright (c) 2017, Lars Brubaker, John Lewin
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
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class SliceSettingsRow : FlowLayoutWidget
	{
		private static readonly Color materialSettingBackgroundColor = Color.Orange; // new RGBA_Bytes(255, 127, 0, 108);
		private static readonly Color qualitySettingBackgroundColor = Color.YellowGreen; // new RGBA_Bytes(255, 255, 0, 108);
		public static readonly Color userSettingBackgroundColor = new Color(68, 95, 220, 150);

		public event EventHandler StyleChanged;

		private SettingsContext settingsContext;
		private PrinterConfig printer;
		private SliceSettingData settingData;

		private GuiWidget dataArea;
		private GuiWidget unitsArea;
		private GuiWidget restoreArea;
		private Button restoreButton = null;
		private VerticalLine vline;

		private const bool debugLayout = false;

		public SliceSettingsRow(PrinterConfig printer, SettingsContext settingsContext, SliceSettingData settingData, bool fullRow = false)
		{
			this.printer = printer;
			this.settingData = settingData;
			this.settingsContext = settingsContext;

			vline = new VerticalLine()
			{
				BackgroundColor = Color.Transparent,
				Margin = new BorderDouble(right: 6),
				Width = 3,
				VAnchor = VAnchor.Stretch,
				MinimumSize = new Vector2(0, 28),
				DebugShowBounds = debugLayout
			};
			this.AddChild(vline);

			this.NameArea = new GuiWidget()
			{
				MinimumSize = new Vector2(50, 0),
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit | VAnchor.Center,
				DebugShowBounds = debugLayout
			};
			this.AddChild(this.NameArea);

			dataArea = new FlowLayoutWidget
			{
				VAnchor = VAnchor.Fit | VAnchor.Center,
				DebugShowBounds = debugLayout
			};
			this.AddChild(dataArea);

			unitsArea = new GuiWidget()
			{
				HAnchor = HAnchor.Absolute,
				VAnchor = VAnchor.Fit | VAnchor.Center,
				Width = settingData.ShowAsOverride ? 50 * GuiWidget.DeviceScale : 5,
				DebugShowBounds = debugLayout
			};
			this.AddChild(unitsArea);

			// Populate unitsArea as appropriate
			// List elements contain list values in the field which normally contains label details, skip generation of invalid labels
			if (settingData.DataEditType != SliceSettingData.DataEditTypes.LIST
				&& settingData.DataEditType != SliceSettingData.DataEditTypes.HARDWARE_PRESENT)
			{
				unitsArea.AddChild(
				new WrappedTextWidget(settingData.ExtraSettings.Localize(), pointSize: 8, textColor: ActiveTheme.Instance.PrimaryTextColor)
				{
					Margin = new BorderDouble(5, 0),
				});
			}

			restoreArea = new GuiWidget()
			{
				HAnchor = HAnchor.Absolute,
				VAnchor = VAnchor.Fit | VAnchor.Center,
				Width = settingData.ShowAsOverride ? 20 * GuiWidget.DeviceScale : 5,
				DebugShowBounds = debugLayout
			};
			this.AddChild(restoreArea);

			this.Name = settingData.SlicerConfigName + " Row";

			if (settingData.ShowAsOverride)
			{
				restoreButton = ApplicationController.Instance.Theme.CreateSmallResetButton();
				restoreButton.HAnchor = HAnchor.Right;
				restoreButton.Margin = 0;
				restoreButton.Name = "Restore " + settingData.SlicerConfigName;
				restoreButton.ToolTipText = "Restore Default".Localize();
				restoreButton.Click += (sender, e) =>
				{
						// Revert the user override 
						settingsContext.ClearValue(settingData.SlicerConfigName);
				};

				restoreArea.AddChild(restoreButton);
			}
		}

		public GuiWidget NameArea { get; }

		public Color HighlightColor
		{
			get => vline.BackgroundColor;
			set
			{
				if (vline.BackgroundColor != value)
				{
					vline.BackgroundColor = value;
					this.BackgroundColor = (value == Color.Transparent) ? Color.Transparent : ApplicationController.Instance.Theme.MinimalShade;

					this.StyleChanged?.Invoke(null, null);
				}
			}
		}

		public void UpdateStyle()
		{
			if (settingsContext.ContainsKey(settingData.SlicerConfigName))
			{
				switch (settingsContext.ViewFilter)
				{
					case NamedSettingsLayers.All:
						if (settingData.ShowAsOverride)
						{
							var defaultCascade = printer.Settings.defaultLayerCascade;
							var firstParentValue = printer.Settings.GetValueAndLayerName(settingData.SlicerConfigName, defaultCascade.Skip(1));
							var currentValueAndLayerName = printer.Settings.GetValueAndLayerName(settingData.SlicerConfigName, defaultCascade);

							var currentValue = currentValueAndLayerName.Item1;
							var layerName = currentValueAndLayerName.Item2;

							if (firstParentValue.Item1 == currentValue)
							{
								if (layerName.StartsWith("Material"))
								{
									this.HighlightColor = materialSettingBackgroundColor;
								}
								else if (layerName.StartsWith("Quality"))
								{
									this.HighlightColor = qualitySettingBackgroundColor;
								}
								else
								{
									this.HighlightColor = Color.Transparent;
								}

								if (restoreButton != null)
								{
									restoreButton.Visible = false;
								}
							}
							else
							{
								this.HighlightColor = userSettingBackgroundColor;
								if (restoreButton != null) restoreButton.Visible = true;
							}
						}
						break;
					case NamedSettingsLayers.Material:
						this.HighlightColor = materialSettingBackgroundColor;
						if (restoreButton != null) restoreButton.Visible = true;
						break;
					case NamedSettingsLayers.Quality:
						this.HighlightColor = qualitySettingBackgroundColor;
						if (restoreButton != null) restoreButton.Visible = true;
						break;
				}
			}
			else if (settingsContext.IsPrimarySettingsView)
			{
				if (printer.Settings.SettingExistsInLayer(settingData.SlicerConfigName, NamedSettingsLayers.Material))
				{
					this.HighlightColor = materialSettingBackgroundColor;
				}
				else if (printer.Settings.SettingExistsInLayer(settingData.SlicerConfigName, NamedSettingsLayers.Quality))
				{
					this.HighlightColor = qualitySettingBackgroundColor;
				}
				else
				{
					this.HighlightColor = Color.Transparent;
				}

				if (restoreButton != null) restoreButton.Visible = false;
			}
			else
			{
				if (restoreButton != null) restoreButton.Visible = false;
				this.HighlightColor = Color.Transparent;
			}

		}

		public void AddContent(GuiWidget content)
		{
			dataArea.AddChild(content);
		}
	}
}
