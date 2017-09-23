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
using MatterHackers.Agg.UI;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class MultilineStringField : UIField
	{
		private readonly int multiLineEditHeight = (int)(120 * GuiWidget.DeviceScale + .5);

		private MHTextEditWidget editWidget;

		public override void Initialize(int tabIndex)
		{
			editWidget = new MHTextEditWidget("", pixelWidth: 320, multiLine: true, tabIndex: tabIndex, typeFace: ApplicationController.MonoSpacedTypeFace)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Fit,
				Name = this.Name
			};
			editWidget.DrawFromHintedCache();
			editWidget.ActualTextEditWidget.EditComplete += (sender, e) =>
			{
				if (sender is TextEditWidget textEditWidget)
				{
					this.SetValue(
						textEditWidget.Text.Replace("\n", "\\n"),
						userInitiated: true);
				}
			};

			this.Content = editWidget;
		}

		protected override void OnValueChanged(FieldChangedEventArgs fieldChangedEventArgs)
		{
			editWidget.Text = this.Value.Replace("\\n", "\n");
			editWidget.ActualTextEditWidget.Height = Math.Min(editWidget.ActualTextEditWidget.Printer.LocalBounds.Height, 500);

			base.OnValueChanged(fieldChangedEventArgs);
		}
	}
}
