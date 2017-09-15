/*
Copyright(c) 2017, John Lewin
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
DISCLAIMED.IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
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
using MatterHackers.MatterControl;
using MatterHackers.MatterControl.SlicerConfiguration;
using NUnit.Framework;

namespace MatterControl.Tests.MatterControl
{
	public class UIFieldTestWindow : SystemWindow
	{
		public MHTextEditWidget ExpectedText { get; }
		public MHTextEditWidget InputText { get; }

		private UIField field;

		public UIFieldTestWindow(int width, int height, UIField field)
			: base(width, height)
		{
			this.BackgroundColor = new RGBA_Bytes(56, 56, 56);

			GuiWidget column, row;
			double pixelWidth = 70;

			// Store
			this.field = field;

			// Initialize the field and store the generated content reference
			field.Initialize(0);

			GuiWidget widgetUnderTest = field.Content;

			row = new FlowLayoutWidget
			{
				VAnchor = VAnchor.Center | VAnchor.Fit,
				HAnchor = HAnchor.Center | HAnchor.Fit
			};
			this.AddChild(row);

			column = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				Margin = new BorderDouble(0, 10),
			};
			row.AddChild(column);

			column.AddChild(new TextWidget("Input:", textColor: RGBA_Bytes.White)
			{
				Margin = new BorderDouble(right: 10, bottom: 2),
			});

			this.InputText = new MHTextEditWidget("", pixelWidth: pixelWidth)
			{
				Margin = new BorderDouble(right: 8)
			};
			column.AddChild(InputText);

			column = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				Margin = new BorderDouble(0, 10),
			};
			row.AddChild(column);

			column.AddChild(new TextWidget("Expected:", textColor: RGBA_Bytes.White)
			{
				Margin = new BorderDouble(right: 10, bottom: 2)
			});

			this.ExpectedText = new MHTextEditWidget("", pixelWidth: pixelWidth)
			{
				Margin = new BorderDouble(right: 8)
			};
			column.AddChild(ExpectedText);

			column = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				Margin = new BorderDouble(0, 10),
			};
			row.AddChild(column);

			column.AddChild(new TextWidget("Actual:", textColor: RGBA_Bytes.White)
			{
				Margin = new BorderDouble(right: 10, bottom: 2)
			});
			column.AddChild(widgetUnderTest);
		}

		public void SetAndValidateValues(string expectedValue, string inputValue, Func<UIField, string> collectValueFromWidget, int delay = 500)
		{
			// Set expected and source
			this.ExpectedText.Text = expectedValue;
			this.InputText.Text = inputValue;

			// Update field
			field.SetValue(inputValue, false);

			// Assert expected field value
			Assert.AreEqual(expectedValue, field.Value);

			// Assert expected widget value
			Assert.AreEqual(expectedValue, collectValueFromWidget(field));

			// Sleep
			System.Threading.Thread.Sleep(delay);
		}
	}
}
