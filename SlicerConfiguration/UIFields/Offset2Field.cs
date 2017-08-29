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
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class Offset2Field : ISettingsField
	{
		private MHNumberEdit xEditWidget;
		private MHNumberEdit yEditWidget;

		public Action UpdateStyle { get; set; }

		public string Value { get; set; }

		public int ExtruderIndex { get; set; } = 0;

		public GuiWidget Create(SettingsContext settingsContext, SliceSettingData settingData, int tabIndex)
		{
			var container = new FlowLayoutWidget();

			Vector2 offset = ActiveSliceSettings.Instance.Helpers.ExtruderOffset(0);

			xEditWidget = new MHNumberEdit(offset.x, allowDecimals: true, allowNegatives: true, pixelWidth: Vector2Field.VectorXYEditWidth, tabIndex: tabIndex)
			{
				ToolTipText = settingData.HelpText,
				SelectAllOnFocus = true,
			};
			xEditWidget.ActuallNumberEdit.EditComplete += (sender, e) =>
			{
				SliceSettingsWidget.SaveCommaSeparatedIndexSetting(this.ExtruderIndex, settingsContext, settingData.SlicerConfigName, xEditWidget.ActuallNumberEdit.Value.ToString() + "x" + yEditWidget.ActuallNumberEdit.Value.ToString());
				this.UpdateStyle();
			};

			container.AddChild(new TextWidget("X:", pointSize: 10, textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				VAnchor = VAnchor.Center,
				Margin = new BorderDouble(5, 0),
			});
			container.AddChild(xEditWidget);

			yEditWidget = new MHNumberEdit(offset.y, allowDecimals: true, allowNegatives: true, pixelWidth: Vector2Field.VectorXYEditWidth, tabIndex: tabIndex)
			{
				ToolTipText = settingData.HelpText,
				SelectAllOnFocus = true
			};
			yEditWidget.ActuallNumberEdit.EditComplete += (sender, e) =>
			{
				SliceSettingsWidget.SaveCommaSeparatedIndexSetting(this.ExtruderIndex, settingsContext, settingData.SlicerConfigName, xEditWidget.ActuallNumberEdit.Value.ToString() + "x" + yEditWidget.ActuallNumberEdit.Value.ToString());
				this.UpdateStyle();
			};

			container.AddChild(new TextWidget("Y:", pointSize: 10, textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				VAnchor = VAnchor.Center,
				Margin = new BorderDouble(15, 0, 5, 0),
			});
			container.AddChild(yEditWidget);

			return container;
		}

		public void OnValueChanged(string text)
		{
			Vector2 offset2 = ActiveSliceSettings.Instance.Helpers.ExtruderOffset(this.ExtruderIndex);
			xEditWidget.ActuallNumberEdit.Value = offset2.x;
			yEditWidget.ActuallNumberEdit.Value = offset2.y;
		}
	}
}
