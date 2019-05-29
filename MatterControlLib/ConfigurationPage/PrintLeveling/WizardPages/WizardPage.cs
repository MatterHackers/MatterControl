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

using System;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;

namespace MatterHackers.MatterControl
{
	public class WizardPage : DialogPage
	{
		public TextButton NextButton { get; }
		protected PrinterConfig printer;

		public Action<WizardPage> PageLoad { get; set; }

		public Action PageClose { get; set; }

		protected ISetupWizard setupWizard;

		public WizardPage(ISetupWizard setupWizard, string headerText, string instructionsText)
			: this(setupWizard)
		{
			this.HeaderText = headerText;

			if (!string.IsNullOrEmpty(instructionsText))
			{
				contentRow.AddChild(
					this.CreateTextField(instructionsText.Replace("\t", "    ")));
			}
		}

		public WizardPage(ISetupWizard setupWizard)
		{
			this.setupWizard = setupWizard;
			this.printer = setupWizard.Printer;

			var nextButton = theme.CreateDialogButton("Next".Localize());
			nextButton.Name = "Next Button";
			nextButton.Click += (s, e) =>
			{
				this.MoveToNextPage();
			};

			this.AcceptButton = nextButton;

			this.AddPageAction(nextButton);

			this.NextButton = nextButton;
		}

		protected void MoveToNextPage()
		{
			OnAdvance();
			setupWizard.MoveNext();
			if (setupWizard.Current is WizardPage wizardPage)
			{
				this.DialogWindow.ChangeToPage(wizardPage);
			}
		}

		public virtual void OnAdvance()
		{
		}

		protected GuiWidget CreateTextField(string text)
		{
			return new WrappedTextWidget(text, pointSize: theme.DefaultFontSize, textColor: theme.TextColor)
			{
				Margin = new BorderDouble(left: 10, top: 10),
				HAnchor = HAnchor.Stretch
			};
		}

		public override void OnLoad(EventArgs args)
		{
			this.PageLoad?.Invoke(this);
			base.OnLoad(args);
		}

		public override void OnClosed(EventArgs e)
		{
			this.PageClose?.Invoke();
			base.OnClosed(e);
		}

		protected void ShowWizardFinished(Action doneClicked = null)
		{
			var doneButton = new TextButton("Done".Localize(), theme)
			{
				Name = "Done Button",
				BackgroundColor = theme.MinimalShade
			};

			doneButton.Click += (s, e) =>
			{
				doneClicked?.Invoke();
				this.FinishWizard();
			};

			this.AcceptButton = doneButton;

			this.AddPageAction(doneButton);

			NextButton.Visible = false;
			this.HideCancelButton();
		}

		protected void FinishWizard()
		{
			if (this.DialogWindow is StagedSetupWindow setupWindow
				&& setupWindow.AutoAdvance)
			{
				setupWindow.NextIncompleteStage();
			}
			else
			{
				this.DialogWindow.ClosePage(allowAbort: false);
			}
		}
	}
}