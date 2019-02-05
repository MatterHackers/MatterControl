/*
Copyright (c) 2018, Lars Brubaker, John Lewin
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

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ConfigurationPage;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class GCodeOptionsPanel : FlowLayoutWidget
	{
		private RadioIconButton speedsButton;
		private RadioIconButton materialsButton;
		private RadioIconButton noColorButton;
		private View3DConfig gcodeOptions;

		public GCodeOptionsPanel(ISceneContext sceneContext, PrinterConfig printer, ThemeConfig theme)
			: base(FlowDirection.TopToBottom)
		{
			gcodeOptions = sceneContext.RendererOptions;

			var buttonPanel = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Fit,
				VAnchor = VAnchor.Fit
			};

			var buttonGroup = new ObservableCollection<GuiWidget>();

			speedsButton = new RadioIconButton(AggContext.StaticData.LoadIcon("speeds.png", theme.InvertIcons), theme)
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

			materialsButton = new RadioIconButton(AggContext.StaticData.LoadIcon("materials.png", theme.InvertIcons), theme)
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

			noColorButton = new RadioIconButton(AggContext.StaticData.LoadIcon("no-color.png", theme.InvertIcons), theme)
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
					theme,
					optionalControls: buttonPanel,
					enforceGutter: false));

			gcodeOptions = sceneContext.RendererOptions;

			var viewOptions = sceneContext.GetBaseViewOptions();

			viewOptions.AddRange(new[]
			{
				new BoolOption(
					"Model".Localize(),
					() => gcodeOptions.GCodeModelView,
					(value) => gcodeOptions.GCodeModelView = value),
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

			var optionsContainer = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit
			};
			this.AddChild(optionsContainer);

			void BuildMenu()
			{
				foreach (var option in viewOptions.Where(option => option.IsVisible()))
				{
					var settingsItem = new SettingsItem(
						option.Title,
						theme,
						new SettingsItem.ToggleSwitchConfig()
						{
							Name = option.Title + " Toggle",
							Checked = option.IsChecked(),
							ToggleAction = option.SetValue
						},
						enforceGutter: false);

					settingsItem.Padding = settingsItem.Padding.Clone(right: 8);

					optionsContainer.AddChild(settingsItem);
				}
			}

			BuildMenu();

			PropertyChangedEventHandler syncProperties = (s, e) =>
			{
				if (e.PropertyName == nameof(gcodeOptions.RenderBed)
					|| e.PropertyName == nameof(gcodeOptions.RenderBuildVolume))
				{
					optionsContainer.CloseAllChildren();
					BuildMenu();
				}
			};

			gcodeOptions.PropertyChanged += syncProperties;

			optionsContainer.Closed += (s, e) =>
			{
				gcodeOptions.PropertyChanged -= syncProperties;
			};
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
	}
}
