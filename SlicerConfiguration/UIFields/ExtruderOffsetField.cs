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

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class ExtruderOffsetField : UIField
	{
		private SettingsContext settingsContext;
		private string slicerConfigName;

		public ExtruderOffsetField(SettingsContext settingsContext, string slicerConfigName)
		{
			this.slicerConfigName = slicerConfigName;
			this.settingsContext = settingsContext;

			//SaveCommaSeparatedIndexSetting(extruderOffset.ExtruderIndex, settingsContext, slicerConfigName, extruderOffset.Value.Replace(",", "x"));
		}

		public override void Initialize(int tabIndex)
		{
			var column = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				Margin = new BorderDouble(20, 0, 0, 0),
				HAnchor = HAnchor.Fit,
				VAnchor = VAnchor.Fit,
				BackgroundColor = RGBA_Bytes.Pink
			};
			column.AnchorAll();

			this.Content = column;

			int.TryParse(settingsContext.GetValue(SettingsKey.extruder_count), out int extruderCount);

			for (int i = 0; i < extruderCount; i++)
			{
				var row = new FlowLayoutWidget()
				{
					HAnchor = HAnchor.Fit,
					VAnchor = VAnchor.Fit,
					MinimumSize = new VectorMath.Vector2(100, 20),
					DebugShowBounds = true,
					BackgroundColor = RGBA_Bytes.Blue
				};
				column.AddChild(row);

				row.AddChild(new TextWidget($"Extruder {i + 1}"));
				row.AddChild(new MHTextEditWidget("something"));
				row.AddChild(new GuiWidget(50, 50) { BackgroundColor = RGBA_Bytes.Red });
			}


			base.Initialize(tabIndex);
		}

		private void SaveCommaSeparatedIndexSetting(int extruderIndexLocal, string slicerConfigName, string newSingleValue)
		{
			string[] settings = settingsContext.GetValue(slicerConfigName).Split(',');
			if (settings.Length > extruderIndexLocal)
			{
				settings[extruderIndexLocal] = newSingleValue;
			}
			else
			{
				string[] newSettings = new string[extruderIndexLocal + 1];
				for (int i = 0; i < extruderIndexLocal + 1; i++)
				{
					newSettings[i] = "";
					if (i < settings.Length)
					{
						newSettings[i] = settings[i];
					}
					else if (i == extruderIndexLocal)
					{
						newSettings[i] = newSingleValue;
					}
				}

				settings = newSettings;
			}

			string newValue = string.Join(",", settings);
			settingsContext.SetValue(slicerConfigName, newValue);
		}
	}
}
