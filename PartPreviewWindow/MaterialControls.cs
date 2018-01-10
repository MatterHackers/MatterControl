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
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MeshVisualizer;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public partial class SelectedObjectPanel
	{
		public class MaterialControls : FlowLayoutWidget, IIgnoredPopupChild
		{
			private ObservableCollection<GuiWidget> materialButtons = new ObservableCollection<GuiWidget>();
			private ThemeConfig theme;
			private InteractiveScene scene;

			public MaterialControls(InteractiveScene scene, ThemeConfig theme)
				: base (FlowDirection.TopToBottom)
			{
				this.theme = theme;
				this.scene = scene;
				this.HAnchor = HAnchor.Stretch;
				this.VAnchor = VAnchor.Fit;

				materialButtons.Clear();
				int extruderCount = 4;
				for (int extruderIndex = 0; extruderIndex < extruderCount; extruderIndex++)
				{
					var row = new FlowLayoutWidget()
					{
						HAnchor = HAnchor.Stretch,
						VAnchor = VAnchor.Fit
					};
					this.AddChild(row);

					var radioButton = new RadioButton(string.Format("{0} {1}", "Material".Localize(), extruderIndex + 1), textColor: theme.Colors.PrimaryTextColor);
					materialButtons.Add(radioButton);
					radioButton.SiblingRadioButtonList = materialButtons;
					row.AddChild(radioButton);

					int extruderIndexCanPassToClick = extruderIndex;
					radioButton.Click += (sender, e) =>
					{
						if (scene.HasSelection)
						{
							scene.SelectedItem.MaterialIndex = extruderIndexCanPassToClick;
							scene.Invalidate();
						}
					};

					row.AddChild(new GuiWidget(16, 16)
					{
						BackgroundColor = MaterialRendering.Color(extruderIndex),
						Margin = new BorderDouble(5, 0, 0, 0)
					});
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
						if (selectedItem.MaterialIndex == i)
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

			public override void OnClosed(ClosedEventArgs e)
			{
				scene.SelectionChanged -= Scene_SelectionChanged;
				base.OnClosed(e);
			}
		}
	}
}