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

namespace MatterHackers.MatterControl
{
	public class InputBoxPage : WizardPage
	{
		private MHTextEditWidget textEditWidget;

		public InputBoxPage(string windowTitle, string label, string initialValue, string emptyText, string actionButtonTitle, Action<string> action)
		{
			this.WindowTitle = windowTitle;
			this.HeaderText = windowTitle;
			this.WindowSize = new Vector2(500, 200);

			Button actionButton = null;

			contentRow.AddChild(new TextWidget(label, pointSize: 12)
			{
				TextColor = ActiveTheme.Instance.PrimaryTextColor,
				Margin = new BorderDouble(5),
				HAnchor = HAnchor.Left
			});

			//Adds text box and check box to the above container
			textEditWidget = new MHTextEditWidget(initialValue, pixelWidth: 300, messageWhenEmptyAndNotSelected: emptyText);
			textEditWidget.Name = "InputBoxPage TextEditWidget";
			textEditWidget.HAnchor = HAnchor.Stretch;
			textEditWidget.Margin = new BorderDouble(5);
			textEditWidget.ActualTextEditWidget.EnterPressed += (s, e) =>
			{
				actionButton.OnClick(null);
			};
			contentRow.AddChild(textEditWidget);

			actionButton = textImageButtonFactory.Generate(actionButtonTitle);
			actionButton.Name = "InputBoxPage Action Button";
			actionButton.Cursor = Cursors.Hand;
			actionButton.Click += (s, e) =>
			{
				string newName = textEditWidget.ActualTextEditWidget.Text;
				if (!string.IsNullOrEmpty(newName))
				{
					action.Invoke(newName);
					this.WizardWindow.CloseOnIdle();
				}
			};
			this.AddPageAction(actionButton);
		}

		public override void OnLoad(EventArgs args)
		{
			UiThread.RunOnIdle(() =>
			{
				textEditWidget.Focus();
				textEditWidget.ActualTextEditWidget.InternalTextEditWidget.SelectAll();
			});
			base.OnLoad(args);
		}
	}
}
