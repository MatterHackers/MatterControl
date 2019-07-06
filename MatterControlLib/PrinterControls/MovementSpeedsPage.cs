/*
Copyright (c) 2018, Kevin Pope, John Lewin
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
using System.Text;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl
{
	public class MovementSpeedsPage : DialogPage
	{
		private List<string> axisLabels = new List<string>();
		private List<GuiWidget> valueEditors = new List<GuiWidget>();

		public MovementSpeedsPage(PrinterConfig printer)
		{
			this.AlwaysOnTopOfMain = true;
			this.WindowTitle = "Movement Speeds".Localize();
			this.HeaderText = "Movement Speeds Presets".Localize();

			this.WindowSize = new Vector2(500 * GuiWidget.DeviceScale, 320 * GuiWidget.DeviceScale);

			var rightLabel = new TextWidget("mm/s".Localize(), textColor: theme.TextColor, pointSize: theme.FontSize10)
			{
				VAnchor = VAnchor.Center,
				Margin = new BorderDouble(right: 20)
			};

			var headerBar = new Toolbar(theme, rightLabel)
			{
				Height = theme.ButtonHeight,
				Padding = theme.ToolbarPadding,
				HAnchor = HAnchor.Stretch
			};

			headerBar.AddChild(new TextWidget(this.WindowTitle, textColor: theme.TextColor, pointSize: theme.FontSize10));

			contentRow.AddChild(headerBar);

			// put in the movement edit controls
			string[] settingsArray = printer.Settings.Helpers.GetMovementSpeedsString().Split(',');
			int preset_count = 1;
			int tab_index = 0;

			for (int i = 0; i < settingsArray.Count() - 1; i += 2)
			{
				var row = new FlowLayoutWidget
				{
					Padding = 3,
					HAnchor = HAnchor.Stretch
				};

				TextWidget axisLabel;

				if (settingsArray[i].StartsWith("e"))
				{
					axisLabel = new TextWidget(string.Format("{0}(s)", "Extruder".Localize()), textColor: theme.TextColor);
				}
				else
				{
					axisLabel = new TextWidget(string.Format("{0} {1}", "Axis".Localize(), settingsArray[i].ToUpper()), textColor: theme.TextColor);
				}
				axisLabel.VAnchor = VAnchor.Center;
				row.AddChild(axisLabel);

				row.AddChild(new HorizontalSpacer());

				axisLabels.Add(settingsArray[i]);

				if (double.TryParse(settingsArray[i + 1], out double movementSpeed))
				{
					movementSpeed = movementSpeed / 60.0;   // Convert from mm/min to mm/s
				}

				var valueEdit = new MHNumberEdit(movementSpeed, theme, minValue: 0, pixelWidth: 60, tabIndex: tab_index++, allowDecimals: true)
				{
					Margin = 3
				};
				row.AddChild(valueEdit);
				valueEditors.Add(valueEdit);

				contentRow.AddChild(row);
				preset_count += 1;
			}

			var savePresetsButton = theme.CreateDialogButton("Save".Localize());
			savePresetsButton.Click += (s, e) =>
			{
				bool first = true;
				var settingString = new StringBuilder();

				for (int i = 0; i < valueEditors.Count(); i++)
				{
					if (!first)
					{
						settingString.Append(",");
					}

					first = false;

					settingString.Append(axisLabels[i]);
					settingString.Append(",");

					double movementSpeed = 0;
					double.TryParse(valueEditors[i].Text, out movementSpeed);
					movementSpeed = movementSpeed * 60; // Convert to mm/min

					settingString.Append(movementSpeed.ToString());
				}

				string speedString = settingString.ToString();
				if (!string.IsNullOrEmpty(speedString))
				{
					printer.Settings.SetValue(SettingsKey.manual_movement_speeds, speedString);
					printer.Bed.GCodeRenderer?.Clear3DGCode();
				}

				this.DialogWindow.CloseOnIdle();
			};

			this.AddPageAction(savePresetsButton);
		}
	}
}
