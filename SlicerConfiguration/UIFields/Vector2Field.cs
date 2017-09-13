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
	public class Vector2Field : BasicField
	{
		public static readonly int VectorXYEditWidth = (int)(60 * GuiWidget.DeviceScale + .5);

		private MHNumberEdit yEditWidget;

		private MHNumberEdit xEditWidget;

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

			xEditWidget = new MHNumberEdit(currentXValue, allowDecimals: true, pixelWidth: VectorXYEditWidth, tabIndex: tabIndex)
			{
				ToolTipText = this.HelpText,
				TabIndex = tabIndex,
				SelectAllOnFocus = true
			};
			xEditWidget.ActuallNumberEdit.EditComplete += (sender, e) =>
			{
				this.SetValue(
					string.Format("{0},{1}", xEditWidget.ActuallNumberEdit.Value.ToString(), yEditWidget.ActuallNumberEdit.Value.ToString()),
					userInitiated: true);
			};

			container.AddChild(new TextWidget("X:", pointSize: 10, textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				VAnchor = VAnchor.Center,
				Margin = new BorderDouble(5, 0),
			});
			container.AddChild(xEditWidget);

			double.TryParse(xyValueStrings[1], out double currentYValue);

			yEditWidget = new MHNumberEdit(currentYValue, allowDecimals: true, pixelWidth: VectorXYEditWidth, tabIndex: tabIndex)
			{
				ToolTipText = this.HelpText,
				TabIndex = tabIndex + 1,
				SelectAllOnFocus = true,
			};
			yEditWidget.ActuallNumberEdit.EditComplete += (sender, e) =>
			{
				this.SetValue(
					string.Format("{0},{1}", xEditWidget.ActuallNumberEdit.Value.ToString(), yEditWidget.ActuallNumberEdit.Value.ToString()),
					userInitiated: true);
			};

			container.AddChild(new TextWidget("Y:", pointSize: 10, textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				VAnchor = VAnchor.Center,
				Margin = new BorderDouble(15, 0, 5, 0),
			});
			container.AddChild(yEditWidget);

			this.Content = container;
		}

		protected override void OnValueChanged(FieldChangedEventArgs fieldChangedEventArgs)
		{
			string[] xyValueStrings2 = this.Value.Split(',');
			if (xyValueStrings2.Length != 2)
			{
				xyValueStrings2 = new string[] { "0", "0" };
			}

			double.TryParse(xyValueStrings2[0], out double currentValue);
			xEditWidget.ActuallNumberEdit.Value = currentValue;

			double.TryParse(xyValueStrings2[1], out currentValue);
			yEditWidget.ActuallNumberEdit.Value = currentValue;

			base.OnValueChanged(fieldChangedEventArgs);
		}
	}
}
