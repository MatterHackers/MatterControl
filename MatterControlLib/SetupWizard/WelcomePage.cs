/*
Copyright (c) 2018, Lars Brubaker, John Lewin, Greg Diaz
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
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.Tour
{
	public class WelcomePage : DialogPage
	{
		public WelcomePage()
			: base("Done".Localize())
		{
			this.WindowTitle = "MatterControl".Localize();
			this.WindowSize = new Vector2(400, 250);

			this.HeaderText = "Welcome to MatterControl".Localize();

			var welcome = "Let's show you around before you get started.".Localize();

			var textWidget = new WrappedTextWidget(welcome)
			{
				Margin = new BorderDouble(left: 10, top: 10),
				TextColor = theme.TextColor,
				HAnchor = HAnchor.Stretch
			};

			contentRow.AddChild(textWidget);

			contentRow.AddChild(new VerticalSpacer());

			var showWelcomePageCheckBox = new CheckBox("Don't remind me again".Localize())
			{
				TextColor = theme.TextColor,
				Margin = new BorderDouble(top: 6, left: 6),
				HAnchor = Agg.UI.HAnchor.Left,
				Checked = ApplicationSettings.Instance.get(UserSettingsKey.ShownWelcomeMessage) == "false"
			};
			showWelcomePageCheckBox.Click += (sender, e) =>
			{
				if (showWelcomePageCheckBox.Checked)
				{
					ApplicationSettings.Instance.set(UserSettingsKey.ShownWelcomeMessage, "false");
				}
				else
				{
					ApplicationSettings.Instance.set(UserSettingsKey.ShownWelcomeMessage, "true");
				}
			};
			contentRow.AddChild(showWelcomePageCheckBox);

			var nextButton = theme.CreateDialogButton("Next".Localize());
			nextButton.Name = "Next Button";
			nextButton.Click += (s, e) =>
			{
				this.DialogWindow.CloseOnIdle();
				UiThread.RunOnIdle(ProductTour.StartTour);
			};

			this.AddPageAction(nextButton);
		}
	}
}
