/*
Copyright (c) 2019, Lars Brubaker, John Lewin
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
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class Vector4Field : UIField
	{
		public static int VectorXYZWEditWidth = (int)(45 * GuiWidget.DeviceScale + .5);

		private MHNumberEdit xEditWidget;
		private MHNumberEdit yEditWidget;
		private MHNumberEdit zEditWidget;
		private MHNumberEdit wEditWidget;

		private ThemeConfig theme;

		protected char[] labels = new[] { 'X', 'Y', 'Z', 'W' };

		public Vector4Field(ThemeConfig theme)
		{
			this.theme = theme;
		}

		public Vector4 Vector4
		{
			get => new Vector4(xEditWidget.Value, yEditWidget.Value, zEditWidget.Value, wEditWidget.Value);
			set
			{
				xEditWidget.Value = value.X;
				yEditWidget.Value = value.Y;
				zEditWidget.Value = value.Z;
				wEditWidget.Value = value.W;
			}
		}

		public override void Initialize(int tabIndex)
		{
			var container = new FlowLayoutWidget();

			string[] xyzValueStrings = this.Value?.Split(',');
			if (xyzValueStrings == null
				|| xyzValueStrings.Length != 4)
			{
				xyzValueStrings = new string[] { "0", "0", "0", "0" };
			}

			double.TryParse(xyzValueStrings[0], out double currentXValue);

			xEditWidget = new MHNumberEdit(currentXValue, theme, labels[0] /* X */, allowNegatives: true, allowDecimals: true, pixelWidth: VectorXYZWEditWidth, tabIndex: tabIndex)
			{
				ToolTipText = this.HelpText,
				TabIndex = tabIndex,
				SelectAllOnFocus = true,
				Margin = theme.ButtonSpacing
			};
			xEditWidget.ActuallNumberEdit.EditComplete += (sender, e) =>
			{
				this.SetValue(
					string.Format("{0},{1},{2},{3}",
						xEditWidget.ActuallNumberEdit.Value.ToString(),
						yEditWidget.ActuallNumberEdit.Value.ToString(),
						zEditWidget.ActuallNumberEdit.Value.ToString(),
						wEditWidget.ActuallNumberEdit.Value.ToString()),
					userInitiated: true);
			};

			container.AddChild(xEditWidget);

			double.TryParse(xyzValueStrings[1], out double currentYValue);

			yEditWidget = new MHNumberEdit(currentYValue, theme, labels[1] /* Y */, allowNegatives: true, allowDecimals: true, pixelWidth: VectorXYZWEditWidth, tabIndex: tabIndex)
			{
				ToolTipText = this.HelpText,
				TabIndex = tabIndex + 1,
				SelectAllOnFocus = true,
				Margin = theme.ButtonSpacing
			};
			yEditWidget.ActuallNumberEdit.EditComplete += (sender, e) =>
			{
				this.SetValue(
					string.Format("{0},{1},{2},{3}",
						xEditWidget.ActuallNumberEdit.Value.ToString(),
						yEditWidget.ActuallNumberEdit.Value.ToString(),
						zEditWidget.ActuallNumberEdit.Value.ToString(),
						wEditWidget.ActuallNumberEdit.Value.ToString()),
					userInitiated: true);
			};

			container.AddChild(yEditWidget);

			double.TryParse(xyzValueStrings[2], out double currentZValue);

			zEditWidget = new MHNumberEdit(currentZValue, theme, labels[2] /* Z */, allowNegatives: true, allowDecimals: true, pixelWidth: VectorXYZWEditWidth, tabIndex: tabIndex)
			{
				ToolTipText = this.HelpText,
				TabIndex = tabIndex + 1,
				SelectAllOnFocus = true,
				Margin = theme.ButtonSpacing
			};
			zEditWidget.ActuallNumberEdit.EditComplete += (sender, e) =>
			{
				this.SetValue(
					string.Format("{0},{1},{2},{3}",
						xEditWidget.ActuallNumberEdit.Value.ToString(),
						yEditWidget.ActuallNumberEdit.Value.ToString(),
						zEditWidget.ActuallNumberEdit.Value.ToString(),
						wEditWidget.ActuallNumberEdit.Value.ToString()),
					userInitiated: true);
			};

			container.AddChild(zEditWidget);

			double.TryParse(xyzValueStrings[3], out double currentWValue);

			wEditWidget = new MHNumberEdit(currentZValue, theme, labels[3] /* W */, allowNegatives: true, allowDecimals: true, pixelWidth: VectorXYZWEditWidth, tabIndex: tabIndex)
			{
				ToolTipText = this.HelpText,
				TabIndex = tabIndex + 1,
				SelectAllOnFocus = true,
				Margin = theme.ButtonSpacing
			};
			wEditWidget.ActuallNumberEdit.EditComplete += (sender, e) =>
			{
				this.SetValue(
					string.Format("{0},{1},{2},{3}",
						xEditWidget.ActuallNumberEdit.Value.ToString(),
						yEditWidget.ActuallNumberEdit.Value.ToString(),
						zEditWidget.ActuallNumberEdit.Value.ToString(),
						wEditWidget.ActuallNumberEdit.Value.ToString()),
					userInitiated: true);
			};

			container.AddChild(wEditWidget);

			this.Content = container;
		}

		protected override string ConvertValue(string newValue)
		{
			// Ensure we have a four value CSV or force to '0,0,0,0'
			string[] xyzwStrings = newValue.Split(',');
			if (xyzwStrings.Length != 4)
			{
				xyzwStrings = new string[] { "0", "0", "0", "0" };
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
			string[] xyzwStrings = this.Value.Split(',');
			if (xyzwStrings.Length != 4)
			{
				xyzwStrings = new string[] { "0", "0", "0", "0" };
			}

			xEditWidget.Text = xyzwStrings[0];
			yEditWidget.Text = xyzwStrings[1];
			zEditWidget.Text = xyzwStrings[2];
			wEditWidget.Text = xyzwStrings[3];

			base.OnValueChanged(fieldChangedEventArgs);
		}
	}
}
