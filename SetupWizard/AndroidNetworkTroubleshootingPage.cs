/*
Copyright (c) 2017, Kevin Pope, John Lewin
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

namespace MatterHackers.MatterControl
{
	public class NetworkTroubleshooting : DialogPage
	{
		public NetworkTroubleshooting()
		{
			contentRow.AddChild(
				new TextWidget(
					"MatterControl was unable to connect to the Internet. Please check your Wifi connection and try again".Localize() + "...",
					textColor: ActiveTheme.Instance.PrimaryTextColor));

			Button configureButton = whiteImageButtonFactory.Generate("Configure Wifi".Localize());
			configureButton.Margin = new BorderDouble(0, 0, 10, 0);
			configureButton.Click += (s, e) =>
			{
				MatterControlApplication.Instance.ConfigureWifi();
				UiThread.RunOnIdle(WizardWindow.Close, 1);

				// We could clear the failure count allowing the user to toggle wifi, then retry sign-in
				//ApplicationController.WebRequestSucceeded();
			};

			//Add buttons to buttonContainer
			AddPageAction(configureButton);
		}
	}
}
