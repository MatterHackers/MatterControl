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

using System.Collections.Generic;
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class ExtruderOffsetField : UIField
	{
		private SettingsContext settingsContext;
		private string slicerConfigName;

		private List<Vector2Field> childFields;

		public ExtruderOffsetField(SettingsContext settingsContext, string slicerConfigName)
		{
			this.slicerConfigName = slicerConfigName;
			this.settingsContext = settingsContext;

			//SaveCommaSeparatedIndexSetting(extruderOffset.ExtruderIndex, settingsContext, slicerConfigName, extruderOffset.Value.Replace(",", "x"));
		}

		public override void Initialize(int tabIndex)
		{
			childFields = new List<Vector2Field>();

			var column = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				Margin = new BorderDouble(20, 0, 0, 0),
				HAnchor = HAnchor.Fit,
				VAnchor = VAnchor.Fit,
			};

			this.Content = column;

			int.TryParse(settingsContext.GetValue(SettingsKey.extruder_count), out int extruderCount);

			// If extruders_share_temperature is enabled, override the extruder count
			if (settingsContext.GetValue(SettingsKey.extruders_share_temperature) == "1")
			{
				extruderCount = 1;
			}

			for (int i = 0; i < extruderCount; i++)
			{
				var row = new FlowLayoutWidget()
				{
					HAnchor = HAnchor.Fit,
					VAnchor = VAnchor.Fit,
					MinimumSize = new Vector2(0, 28)
				};
				column.AddChild(row);

				var labelWidget = SliceSettingsWidget.CreateSettingsLabel($"Nozzle {i + 1}", "");
				labelWidget.Name = $"Nozzle {i}";
				labelWidget.Margin = new BorderDouble(right: 60, left: 20);
				row.AddChild(labelWidget);

				var field = new Vector2Field();
				field.Initialize(tabIndex++);
				field.Content.VAnchor = VAnchor.Center;
				field.ValueChanged += (s, e) =>
				{
					if (e.UserInitiated)
					{
						// Stuff multiple CSV values into single text field
						this.SetValue(
							string.Join(",", this.childFields.Select(f => f.Value.Replace(",", "x")).ToArray()), 
							true);
					}
				};
				row.AddChild(field.Content);

				childFields.Add(field);
			}

			base.Initialize(tabIndex);
		}

		protected override void OnValueChanged(FieldChangedEventArgs fieldChangedEventArgs)
		{
			var segments = this.Value.Split(',');

			for (int i = 0; i < childFields.Count; i++)
			{
				string fieldValue = (i < segments.Length) ? segments[i]?.Replace('x', ',') : null;
				childFields[i].SetValue(fieldValue ?? "0,0", userInitiated: false);
			}

			base.OnValueChanged(fieldChangedEventArgs);
		}
	}
}
