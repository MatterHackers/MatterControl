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

using System;
using System.Collections.ObjectModel;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MeshVisualizer;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class MaterialControls : FlowLayoutWidget, IIgnoredPopupChild
	{
		private ObservableCollection<GuiWidget> materialButtons = new ObservableCollection<GuiWidget>();
		private ThemeConfig theme;
		public event EventHandler<int> IndexChanged;

		public MaterialControls(ThemeConfig theme, int initialMaterialIndex)
			: base(FlowDirection.TopToBottom)
		{
			this.theme = theme;
			this.HAnchor = HAnchor.Fit;
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

				buttonView.AddChild(new ColorButton(MaterialRendering.Color(extruderIndex, theme.BorderColor))
				{
					Width = scaledButtonSize,
					Height = scaledButtonSize,
					VAnchor = VAnchor.Center,
					Margin = new BorderDouble(3, 0, 5, 0),
					DrawGrid = true,
				});

				buttonView.AddChild(new TextWidget(name, pointSize: theme.DefaultFontSize, textColor: theme.TextColor)
				{
					VAnchor = VAnchor.Center
				});

				var radioButtonView = new RadioButtonView(buttonView)
				{
					TextColor = theme.TextColor
				};
				radioButtonView.RadioCircle.Margin = radioButtonView.RadioCircle.Margin.Clone(right: 5);

				var radioButton = new RadioButton(radioButtonView)
				{
					HAnchor = HAnchor.Fit,
					VAnchor = VAnchor.Fit,
					TextColor = theme.TextColor,
					Checked = extruderIndex == initialMaterialIndex
				};
				materialButtons.Add(radioButton);
				this.AddChild(radioButton);

				int localExtruderIndex = extruderIndex;
				radioButton.Click += (sender, e) =>
				{
					IndexChanged?.Invoke(this, localExtruderIndex);
				};
			}
		}

		public override void OnClosed(EventArgs e)
		{
			base.OnClosed(e);
		}

		public bool KeepMenuOpen => false;
	}
}