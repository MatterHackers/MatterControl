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

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using System;
using System.Collections.Generic;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class SettingsControlBar : FlowLayoutWidget
	{
		public SettingsControlBar()
		{
			this.HAnchor = HAnchor.Stretch;

			int numberOfHeatedExtruders = ActiveSliceSettings.Instance.Helpers.NumberOfHotEnds();

			this.AddChild(new PresetSelectorWidget("Quality".Localize(), RGBA_Bytes.Yellow, NamedSettingsLayers.Quality, 0));
			this.AddChild(new GuiWidget(8, 0));

			if (numberOfHeatedExtruders > 1)
			{
				List<RGBA_Bytes> colorList = new List<RGBA_Bytes>() { RGBA_Bytes.Orange, RGBA_Bytes.Violet, RGBA_Bytes.YellowGreen };

				for (int i = 0; i < numberOfHeatedExtruders; i++)
				{
					if (i > 0)
					{
						this.AddChild(new GuiWidget(8, 0));
					}
					int colorIndex = i % colorList.Count;
					RGBA_Bytes color = colorList[colorIndex];
					this.AddChild(new PresetSelectorWidget(string.Format("{0} {1}", "Material".Localize(), i + 1), color, NamedSettingsLayers.Material, i));
				}
			}
			else
			{
				this.AddChild(new PresetSelectorWidget("Material".Localize(), RGBA_Bytes.Orange, NamedSettingsLayers.Material, 0));
			}

			this.Height = 60 * GuiWidget.DeviceScale;
		}
	}
}
	