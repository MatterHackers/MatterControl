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
using System.Collections.ObjectModel;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ConfigurationPage;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.PartPreviewWindow
{

	public interface IToggleOption
	{
		string Title { get; }
		Func<bool> IsChecked { get; }
		Action<bool> SetValue { get; }
	}

	public class BoolOption : IToggleOption
	{
		public string Title { get; }

		public Func<bool> IsChecked { get; }

		public Func<bool> IsVisible { get; }

		public Action<bool> SetValue { get; }


		public BoolOption(string title, Func<bool> isChecked, Action<bool> setValue)
			: this(title, isChecked, setValue, () => true)
		{
		}

		public BoolOption(string title, Func<bool> isChecked, Action<bool> setValue, Func<bool> isVisible)
		{
			this.Title = title;
			this.IsChecked = isChecked;
			this.SetValue = setValue;
			this.IsVisible = isVisible;
		}
	}

	public class GCodeOptionsPanel : FlowLayoutWidget
	{
		private RadioIconButton speedsButton;
		private RadioIconButton materialsButton;
		private RadioIconButton noColorButton;
		private View3DConfig gcodeOptions;
		private RadioIconButton solidButton;

		public GCodeOptionsPanel(BedConfig sceneContext, PrinterConfig printer, ThemeConfig theme)
			: base(FlowDirection.TopToBottom)
		{
			gcodeOptions = sceneContext.RendererOptions;

			var buttonPanel = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Fit,
				VAnchor = VAnchor.Fit
			};

			var buttonGroup = new ObservableCollection<GuiWidget>();

			speedsButton = new RadioIconButton(AggContext.StaticData.LoadIcon("speeds.png", IconColor.Theme), theme)
			{
				SiblingRadioButtonList = buttonGroup,
				Name = "Speeds Button",
				Checked = gcodeOptions.GCodeLineColorStyle == "Speeds",
				ToolTipText = "Show Speeds".Localize(),
				Margin = theme.ButtonSpacing
			};
			speedsButton.Click += SwitchColorModes_Click;
			buttonGroup.Add(speedsButton);

			buttonPanel.AddChild(speedsButton);

			materialsButton = new RadioIconButton(AggContext.StaticData.LoadIcon("materials.png", IconColor.Theme), theme)
			{
				SiblingRadioButtonList = buttonGroup,
				Name = "Materials Button",
				Checked = gcodeOptions.GCodeLineColorStyle == "Materials",
				ToolTipText = "Show Materials".Localize(),
				Margin = theme.ButtonSpacing
			};
			materialsButton.Click += SwitchColorModes_Click;
			buttonGroup.Add(materialsButton);

			buttonPanel.AddChild(materialsButton);

			noColorButton = new RadioIconButton(AggContext.StaticData.LoadIcon("no-color.png", IconColor.Theme), theme)
			{
				SiblingRadioButtonList = buttonGroup,
				Name = "No Color Button",
				Checked = gcodeOptions.GCodeLineColorStyle == "None",
				ToolTipText = "No Color".Localize(),
				Margin = theme.ButtonSpacing
			};
			noColorButton.Click += SwitchColorModes_Click;
			buttonGroup.Add(noColorButton);

			buttonPanel.AddChild(noColorButton);

			this.AddChild(
				new SettingsItem(
					"Color View".Localize(),
					theme.Colors.PrimaryTextColor,
					buttonPanel,
					enforceGutter: false)
				{
					Margin = new BorderDouble(bottom: 2)
				});

			buttonPanel = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Fit,
				VAnchor = VAnchor.Fit
			};

			// Reset to new button group
			buttonGroup = new ObservableCollection<GuiWidget>();

			solidButton = new RadioIconButton(AggContext.StaticData.LoadIcon("solid.png", IconColor.Theme), theme)
			{
				SiblingRadioButtonList = buttonGroup,
				Name = "Solid Button",
				Checked = gcodeOptions.GCodeModelView == "Semi-Transparent",
				ToolTipText = "Show Semi-Transparent Model".Localize(),
				Margin = theme.ButtonSpacing
			};
			solidButton.Click += SwitchModelModes_Click;
			buttonGroup.Add(solidButton);

			buttonPanel.AddChild(solidButton);

			materialsButton = new RadioIconButton(AggContext.StaticData.LoadIcon("wireframe.png", IconColor.Theme), theme)
			{
				SiblingRadioButtonList = buttonGroup,
				Name = "Wireframe Button",
				Checked = gcodeOptions.GCodeModelView == "Wireframe",
				ToolTipText = "Show Wireframe Model".Localize(),
				Margin = theme.ButtonSpacing
			};
			materialsButton.Click += SwitchModelModes_Click;
			buttonGroup.Add(materialsButton);

			buttonPanel.AddChild(materialsButton);

			noColorButton = new RadioIconButton(AggContext.StaticData.LoadIcon("no-color.png", IconColor.Theme), theme)
			{
				SiblingRadioButtonList = buttonGroup,
				Name = "No Model Button",
				Checked = gcodeOptions.GCodeModelView == "None",
				ToolTipText = "No Model".Localize(),
				Margin = theme.ButtonSpacing
			};
			noColorButton.Click += SwitchModelModes_Click;
			buttonGroup.Add(noColorButton);

			buttonPanel.AddChild(noColorButton);

			this.AddChild(
				new SettingsItem(
					"Model View".Localize(),
					theme.Colors.PrimaryTextColor,
					buttonPanel,
					enforceGutter: false));

			var viewOptions = new List<BoolOption>
			{
				new BoolOption(
					"Show Print Bed".Localize(),
					() => gcodeOptions.RenderBed,
					(value) =>
					{
						gcodeOptions.RenderBed = value;
					})
			};

			if (sceneContext.BuildHeight > 0
				&& printer?.ViewState.ViewMode != PartViewMode.Layers2D)
			{
				viewOptions.Add(
					new BoolOption(
						"Show Print Area".Localize(),
						() => gcodeOptions.RenderBuildVolume,
						(value) => gcodeOptions.RenderBuildVolume = value));
			}

			viewOptions.AddRange(new[]
			{
				new BoolOption(
					"Moves".Localize(),
					() => gcodeOptions.RenderMoves,
					(value) => gcodeOptions.RenderMoves = value),
				new BoolOption(
					"Retractions".Localize(),
					() => gcodeOptions.RenderRetractions,
					(value) => gcodeOptions.RenderRetractions = value),
				new BoolOption(
					"Extrusion".Localize(),
					() => gcodeOptions.SimulateExtrusion,
					(value) => gcodeOptions.SimulateExtrusion = value),
				new BoolOption(
					"Transparent".Localize(),
					() => gcodeOptions.TransparentExtrusion,
					(value) => gcodeOptions.TransparentExtrusion = value),
				new BoolOption(
					"Hide Offsets".Localize(),
					() => gcodeOptions.HideExtruderOffsets,
					(value) => gcodeOptions.HideExtruderOffsets = value,
					() => printer.Settings.GetValue<int>(SettingsKey.extruder_count) > 1),
				new BoolOption(
					"Sync To Print".Localize(),
					() => gcodeOptions.SyncToPrint,
					(value) =>
					{
						gcodeOptions.SyncToPrint = value;
						if (!gcodeOptions.SyncToPrint)
						{
							// If we are turning off sync to print, set the slider to full.
							//layerRenderRatioSlider.SecondValue = 1;
						}
					})
			});

			foreach(var option in viewOptions)
			{
				if (option.IsVisible())
				{
					this.AddChild(
						new SettingsItem(
							option.Title,
							theme.Colors.PrimaryTextColor,
							new SettingsItem.ToggleSwitchConfig()
							{
								Name = option.Title + " Toggle",
								Checked = option.IsChecked(),
								ToggleAction = option.SetValue
							},
							enforceGutter: false)
					);
				}
			}
		}

		private void SwitchColorModes_Click(object sender, MouseEventArgs e)
		{
			if (sender is GuiWidget widget)
			{
				if (widget.Name == "Speeds Button")
				{
					gcodeOptions.GCodeLineColorStyle = "Speeds";
				}
				else if (widget.Name == "Materials Button")
				{
					gcodeOptions.GCodeLineColorStyle = "Materials";
				}
				else
				{
					gcodeOptions.GCodeLineColorStyle = "None";
				}
			}
		}

		private void SwitchModelModes_Click(object sender, MouseEventArgs e)
		{
			if (sender is GuiWidget widget)
			{
				if (widget.Name == "Solid Button")
				{
					gcodeOptions.GCodeModelView = "Semi-Transparent";
				}
				else if (widget.Name == "Wireframe Button")
				{
					gcodeOptions.GCodeModelView = "Wireframe";
				}
				else
				{
					gcodeOptions.GCodeModelView = "None";
				}
			}
		}
	}

	public class GCodeDetailsView : FlowLayoutWidget
	{
		private TextWidget massTextWidget;
		private TextWidget costTextWidget;

		private EventHandler unregisterEvents;

		public GCodeDetailsView(GCodeDetails gcodeDetails, int dataPointSize, int headingPointSize)
			: base(FlowDirection.TopToBottom)
		{
			var margin = new BorderDouble(0, 9, 0, 3);

			TextWidget AddSetting(string title, string value, GuiWidget parentWidget)
			{
				parentWidget.AddChild(
					new TextWidget(title + ":", textColor: ActiveTheme.Instance.PrimaryTextColor, pointSize: headingPointSize)
					{
						HAnchor = HAnchor.Left
					});

				var textWidget = new TextWidget(value, textColor: ActiveTheme.Instance.PrimaryTextColor, pointSize: dataPointSize)
				{
					HAnchor = HAnchor.Left,
					Margin = margin
				};

				parentWidget.AddChild(textWidget);

				return textWidget;
			}

			// put in the print time
			AddSetting("Print Time".Localize(), gcodeDetails.EstimatedPrintTime, this);

			// show the filament used
			AddSetting("Filament Length".Localize(), gcodeDetails.FilamentUsed, this);

			AddSetting("Filament Volume".Localize(), gcodeDetails.FilamentVolume, this);

			massTextWidget = AddSetting("Estimated Mass".Localize(), gcodeDetails.EstimatedMass, this);

			// Cost info is only displayed when available - conditionalCostPanel is invisible when cost <= 0
			var conditionalCostPanel = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
				Visible = gcodeDetails.TotalCost > 0
			};
			this.AddChild(conditionalCostPanel);

			costTextWidget = AddSetting("Estimated Cost".Localize(), gcodeDetails.EstimatedCost, conditionalCostPanel);

			ActiveSliceSettings.SettingChanged.RegisterEvent((s, e) =>
			{
				if (e is StringEventArgs stringEvent)
				{
					if (stringEvent.Data == SettingsKey.filament_cost
						|| stringEvent.Data == SettingsKey.filament_diameter
						|| stringEvent.Data == SettingsKey.filament_density)
					{
						massTextWidget.Text = gcodeDetails.EstimatedMass;
						conditionalCostPanel.Visible = gcodeDetails.TotalCost > 0;

						if (gcodeDetails.TotalCost > 0)
						{
							costTextWidget.Text = gcodeDetails.EstimatedCost;
						}
					}
				}
			}, ref unregisterEvents);
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}
	}
}
