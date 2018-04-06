/*
Copyright (c) 2018, John Lewin
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
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ConfigurationPage;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MeshVisualizer;
using MatterHackers.RenderOpenGl;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class ModelOptionsPanel : FlowLayoutWidget
	{
		private RadioIconButton shadedViewButton;
		private RadioIconButton outlinesViewButton;
		private RadioIconButton polygonsViewButton;
		private RadioIconButton materialsViewButton;
		private RadioIconButton overhangViewButton;

		public ModelOptionsPanel(BedConfig sceneContext, MeshViewerWidget meshViewerWidget, ThemeConfig theme)
			: base(FlowDirection.TopToBottom)
		{
			void switchToRenderType(RenderTypes renderType)
			{
				meshViewerWidget.RenderType = renderType;
				UserSettings.Instance.set(UserSettingsKey.defaultRenderSetting, renderType.ToString());
			}

			var buttonPanel = new FlowLayoutWidget()
			{
				HAnchor = HAnchor.Fit,
				VAnchor = VAnchor.Fit
			};

			var buttonGroup = new ObservableCollection<GuiWidget>();

			shadedViewButton = new RadioIconButton(AggContext.StaticData.LoadIcon("view_shaded.png", IconColor.Theme), theme)
			{
				SiblingRadioButtonList = buttonGroup,
				Name = "Shaded Button",
				Checked = meshViewerWidget.RenderType == RenderTypes.Shaded,
				ToolTipText = "Shaded".Localize(),
				Margin = theme.ButtonSpacing
			};
			shadedViewButton.Click += (s, e) => switchToRenderType(RenderTypes.Shaded);
			buttonGroup.Add(shadedViewButton);

			buttonPanel.AddChild(shadedViewButton);

			outlinesViewButton = new RadioIconButton(AggContext.StaticData.LoadIcon("view_outlines.png", IconColor.Theme), theme)
			{
				SiblingRadioButtonList = buttonGroup,
				Name = "Outlines Button",
				Checked = meshViewerWidget.RenderType == RenderTypes.Outlines,
				ToolTipText = "Outlines".Localize(),
				Margin = theme.ButtonSpacing
			};
			outlinesViewButton.Click += (s, e) => switchToRenderType(RenderTypes.Outlines);
			buttonGroup.Add(outlinesViewButton);

			buttonPanel.AddChild(outlinesViewButton);

			polygonsViewButton = new RadioIconButton(AggContext.StaticData.LoadIcon("view_polygons.png", IconColor.Theme), theme)
			{
				SiblingRadioButtonList = buttonGroup,
				Name = "Polygons Button",
				Checked = meshViewerWidget.RenderType == RenderTypes.Polygons,
				ToolTipText = "Polygons".Localize(),
				Margin = theme.ButtonSpacing
			};
			polygonsViewButton.Click += (s, e) => switchToRenderType(RenderTypes.Polygons);
			buttonGroup.Add(polygonsViewButton);

			buttonPanel.AddChild(polygonsViewButton);

			materialsViewButton = new RadioIconButton(AggContext.StaticData.LoadIcon("view_materials.png", IconColor.Raw), theme)
			{
				SiblingRadioButtonList = buttonGroup,
				Name = "Materials Button",
				Checked = meshViewerWidget.RenderType == RenderTypes.Materials,
				ToolTipText = "Materials".Localize(),
				Margin = theme.ButtonSpacing
			};
			materialsViewButton.Click += (s, e) => switchToRenderType(RenderTypes.Materials);
			buttonGroup.Add(materialsViewButton);

			buttonPanel.AddChild(materialsViewButton);

			overhangViewButton = new RadioIconButton(AggContext.StaticData.LoadIcon("view_overhang.png", IconColor.Raw), theme)
			{
				SiblingRadioButtonList = buttonGroup,
				Name = "Overhang Button",
				Checked = meshViewerWidget.RenderType == RenderTypes.Overhang,
				ToolTipText = "Overhang".Localize(),
				Margin = theme.ButtonSpacing
			};
			overhangViewButton.Click += (s, e) => switchToRenderType(RenderTypes.Overhang);
			buttonGroup.Add(overhangViewButton);

			buttonPanel.AddChild(overhangViewButton);

			this.AddChild(
				new SettingsItem(
					"View Style".Localize(),
					theme.Colors.PrimaryTextColor,
					buttonPanel,
					enforceGutter: false)
				{
					Margin = new BorderDouble(bottom: 2)
				});

			foreach (var option in sceneContext.GetBaseViewOptions())
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
	}
}
