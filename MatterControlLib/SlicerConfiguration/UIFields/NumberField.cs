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

using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MeshVisualizer;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public abstract class NumberField : UIField
	{
		protected ThemedNumberEdit numberEdit;
		private readonly ThemeConfig theme;

		protected bool AllowNegatives { get; set; } = true;

		protected bool AllowDecimals { get; set; } = true;

		public NumberField(ThemeConfig theme)
		{
			this.theme = theme;
		}

        public static void SetupUpAndDownArrows(GuiWidget guiWidget)
        {
            guiWidget.KeyDown += InternalTextEditWidget_KeyDown;

            guiWidget.MouseEnterBounds += (s, e) =>
            {
                var textEditWidget = s as TextEditWidget;
                var internalTextEditWidget = textEditWidget.InternalTextEditWidget;

                if (internalTextEditWidget != null
                    && double.TryParse(internalTextEditWidget.Text, out double value))
                {
                    ApplicationController.Instance.UiHint = "Up Arrow = +1, Down Arrow = -1, (Shift * 10, Control / 10)".Localize();
                }
            };
            guiWidget.MouseLeaveBounds += (s, e) =>
            {
                ApplicationController.Instance.UiHint = "";
            };
        }

        private static void InternalTextEditWidget_KeyDown(object sender, KeyEventArgs e)
        {
            var textEditWidget = sender as TextEditWidget;
            var internalTextEditWidget = textEditWidget.InternalTextEditWidget;

            if (internalTextEditWidget != null)
            {
                var amount = 1.0;
                if (e.Shift)
                {
                    amount = 10;
                }
                else if (e.Control)
                {
                    amount = .1;
                }

                // if the up arrow is pressed and the text is a number
                if (e.KeyCode == Keys.Up)
                {
                    if (double.TryParse(internalTextEditWidget.Text, out double value))
                    {
                        value += amount;
                        internalTextEditWidget.Text = value.ToString();
                        e.Handled = true;
                        internalTextEditWidget.OnEditComplete(e);
                    }
                }
                else if (e.KeyCode == Keys.Down)
                {
                    if (double.TryParse(internalTextEditWidget.Text, out double value))
                    {
                        value -= amount;
                        internalTextEditWidget.Text = value.ToString();
                        e.Handled = true;
                        internalTextEditWidget.OnEditComplete(e);
                    }
                }
            }
        }
        
        public override void Initialize(int tabIndex)
		{
			numberEdit = new ThemedNumberEdit(0, theme, pixelWidth: ControlWidth, allowDecimals: this.AllowDecimals, allowNegatives: this.AllowNegatives, tabIndex: tabIndex)
			{
				ToolTipText = this.HelpText,
				SelectAllOnFocus = true,
				Name = this.Name,
			};
			numberEdit.ActuallNumberEdit.EditComplete += (s, e) =>
			{
				if (this.Value != numberEdit.Value.ToString())
				{
					this.SetValue(
						numberEdit.Value.ToString(),
						userInitiated: true);
				}
			};
            SetupUpAndDownArrows(numberEdit.ActuallNumberEdit);

            this.Content = numberEdit;
		}
	}
}
