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
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;

namespace MatterHackers.MatterControl
{
	public class RenameItemPage : WizardPage
	{
		private Action<string> renameCallback;

		private MHTextEditWidget saveAsNameWidget;

		public RenameItemPage(string windowTitle, string currentItemName, Action<string> functionToCallToRenameItem)
			: base(unlocalizedTextForTitle: windowTitle)
		{
			this.renameCallback = functionToCallToRenameItem;

			this.Text = windowTitle;

			var textBoxHeader = new TextWidget("Name".Localize(), pointSize: 12)
			{
				TextColor = ActiveTheme.Instance.PrimaryTextColor,
				Margin = new BorderDouble(5),
				HAnchor = HAnchor.Left
			};
			contentRow.AddChild(textBoxHeader);

			//Adds text box and check box to the above container
			saveAsNameWidget = new MHTextEditWidget(currentItemName, pixelWidth: 300, messageWhenEmptyAndNotSelected: "Enter New Name Here".Localize());
			saveAsNameWidget.HAnchor = HAnchor.Stretch;
			saveAsNameWidget.Margin = new BorderDouble(5);
			saveAsNameWidget.ActualTextEditWidget.EnterPressed += (s, e) =>
			{
				SubmitForm();
			};
			contentRow.AddChild(saveAsNameWidget);

			var renameItemButton = textImageButtonFactory.Generate("Rename".Localize());
			renameItemButton.Name = "Rename Button";
			renameItemButton.Visible = true;
			renameItemButton.Cursor = Cursors.Hand;
			renameItemButton.Click += (s, e) =>
			{
				SubmitForm();
			};
			footerRow.AddChild(renameItemButton);

			//Adds Create and Close Button to button container
			footerRow.AddChild(new HorizontalSpacer());

			footerRow.AddChild(cancelButton);
		}

		public override void OnLoad(EventArgs args)
		{
			UiThread.RunOnIdle(() =>
			{
				saveAsNameWidget.Focus();
				saveAsNameWidget.ActualTextEditWidget.InternalTextEditWidget.SelectAll();
			});
			base.OnLoad(args);
		}

		private void SubmitForm()
		{
			string newName = saveAsNameWidget.ActualTextEditWidget.Text;
			if (newName != "")
			{
				renameCallback(newName);
				this.WizardWindow.CloseOnIdle();
			}
		}
	}
}