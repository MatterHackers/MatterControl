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

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MeshVisualizer;
using System;
using System.Collections.ObjectModel;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class MaterialControls : FlowLayoutWidget, IIgnoredPopupChild
	{
		private ObservableCollection<GuiWidget> materialButtons = new ObservableCollection<GuiWidget>();
		private ThemeConfig theme;
		private InteractiveScene scene;

		public MaterialControls(InteractiveScene scene, ThemeConfig theme)
			: base(FlowDirection.TopToBottom)
		{
			this.theme = theme;
			this.scene = scene;
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Fit;

			materialButtons.Clear();
			int extruderCount = 4;
			for (int extruderIndex = -1; extruderIndex < extruderCount; extruderIndex++)
			{
				var name = $"{"Material".Localize()} {extruderIndex +1}";
				if(extruderIndex == -1)
				{
					name = "Default".Localize();
				}

				var buttonView = new FlowLayoutWidget()
				{
					HAnchor = HAnchor.Fit,
					VAnchor = VAnchor.Fit
				};

				var scaledButtonSize = 16 * GuiWidget.DeviceScale;

				buttonView.AddChild(new ColorButton(extruderIndex == -1 ? Color.Black : MaterialRendering.Color(extruderIndex))
				{
					Margin = new BorderDouble(right: 5),
					Width = scaledButtonSize,
					Height = scaledButtonSize,
					VAnchor = VAnchor.Center,
				});

				buttonView.AddChild(new TextWidget(name, pointSize: theme.DefaultFontSize, textColor: theme.Colors.PrimaryTextColor)
				{
					VAnchor = VAnchor.Center
				});

				var radioButtonView = new RadioButtonView(buttonView)
				{
					TextColor = theme.Colors.PrimaryTextColor
				};
				radioButtonView.RadioCircle.Margin = radioButtonView.RadioCircle.Margin.Clone(right: 5);

				var radioButton = new RadioButton(radioButtonView)
				{
					HAnchor = HAnchor.Stretch,
					VAnchor = VAnchor.Fit,
					TextColor = theme.Colors.PrimaryTextColor
				};
				materialButtons.Add(radioButton);
				this.AddChild(radioButton);

				int extruderIndexCanPassToClick = extruderIndex;
				radioButton.Click += (sender, e) =>
				{
					var selectedItem = scene.SelectedItem;
					if (selectedItem != null)
					{
						selectedItem.MaterialIndex = extruderIndexCanPassToClick;
						scene.Invalidate(new InvalidateArgs(null, InvalidateType.Material));
					}
				};
			}

			scene.SelectionChanged += Scene_SelectionChanged;
		}

		private void Scene_SelectionChanged(object sender, EventArgs e)
		{
			var selectedItem = scene.SelectedItem;

			if (selectedItem != null
				&& materialButtons?.Count > 0)
			{
				bool setSelection = false;
				// Set the material selector to have the correct material button selected
				for (int i = 0; i < materialButtons.Count; i++)
				{
					// the first button is 'Default' so we are looking for the button that is i - 1 (0 = material 1 = button 1)
					if (selectedItem.MaterialIndex == i - 1)
					{
						((RadioButton)materialButtons[i]).Checked = true;
						setSelection = true;
					}
				}

				if (!setSelection)
				{
					((RadioButton)materialButtons[0]).Checked = true;
				}
			}
		}

		public override void OnClosed(EventArgs e)
		{
			scene.SelectionChanged -= Scene_SelectionChanged;
			base.OnClosed(e);
		}
	}
}