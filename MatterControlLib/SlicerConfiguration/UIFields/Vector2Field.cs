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

using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class Vector2Field : UIField
	{
		public static readonly int VectorXYEditWidth = (int)(60 * GuiWidget.DeviceScale + .5);

		private MHNumberEdit yEditWidget;

		private MHNumberEdit xEditWidget;
		private ThemeConfig theme;

		public Vector2Field(ThemeConfig theme)
		{
			this.theme = theme;
		}

		public Vector2 Vector2
		{
			get => new Vector2(xEditWidget.Value, yEditWidget.Value);
			set
			{
				xEditWidget.Value = value.X;
				yEditWidget.Value = value.Y;
			}
		}

		public override void Initialize(int tabIndex)
		{
			var container = new FlowLayoutWidget();

			string[] xyValueStrings = this.Value?.Split(',');
			if (xyValueStrings == null
				|| xyValueStrings.Length != 2)
			{
				xyValueStrings = new string[] { "0", "0" };
			}

			double.TryParse(xyValueStrings[0], out double currentXValue);

			xEditWidget = new MHNumberEdit(currentXValue, theme, singleCharLabel: 'X', allowNegatives: true, allowDecimals: true, pixelWidth: VectorXYEditWidth, tabIndex: tabIndex)
			{
				ToolTipText = this.HelpText,
				TabIndex = tabIndex,
				SelectAllOnFocus = true,
				Margin = theme.ButtonSpacing
			};
			xEditWidget.ActuallNumberEdit.EditComplete += (sender, e) =>
			{
				this.SetValue(
					string.Format("{0},{1}", xEditWidget.ActuallNumberEdit.Value.ToString("0.###"), yEditWidget.ActuallNumberEdit.Value.ToString("0.###")),
					userInitiated: true);
			};

			container.AddChild(xEditWidget);

			double.TryParse(xyValueStrings[1], out double currentYValue);

			yEditWidget = new MHNumberEdit(currentYValue, theme, 'Y', allowNegatives: true, allowDecimals: true, pixelWidth: VectorXYEditWidth, tabIndex: tabIndex)
			{
				ToolTipText = this.HelpText,
				TabIndex = tabIndex + 1,
				SelectAllOnFocus = true,
			};
			yEditWidget.ActuallNumberEdit.EditComplete += (sender, e) =>
			{
				this.SetValue(
					string.Format("{0},{1}", xEditWidget.ActuallNumberEdit.Value.ToString("0.###"), yEditWidget.ActuallNumberEdit.Value.ToString("0.###")),
					userInitiated: true);
			};

			container.AddChild(yEditWidget);

			this.Content = container;
		}

		protected override string ConvertValue(string newValue)
		{
			// Ensure we have a two value CSV or force to '0,0'
			string[] xyzwStrings = newValue.Split(',');
			if (xyzwStrings.Length != 2)
			{
				xyzwStrings = new string[] { "0", "0" };
			}

			// Convert string segments to double, then back to expected CSV string
			return string.Join(
				",",
				xyzwStrings.Select(s =>
				{
					double.TryParse(s, out double doubleValue);
					return doubleValue.ToString();
				}).ToArray());
		}

		protected override void OnValueChanged(FieldChangedEventArgs fieldChangedEventArgs)
		{
			string[] xyStrings = this.Value.Split(',');
			if (xyStrings.Length != 2)
			{
				xyStrings = new string[] { "0", "0" };
			}

			xEditWidget.Text = xyStrings[0];
			yEditWidget.Text = xyStrings[1];

			base.OnValueChanged(fieldChangedEventArgs);
		}
	}
}
