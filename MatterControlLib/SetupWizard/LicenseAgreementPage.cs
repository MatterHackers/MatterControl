﻿/*
Copyright (c) 2017, John Lewin
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
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl;
using MatterHackers.MatterControl.CustomWidgets;
using System;

public class LicenseAgreementPage : DialogPage
{
	public LicenseAgreementPage(Action acceptAction)
	{
			this.WindowTitle = "Software License Agreement".Localize();
		this.HeaderText = "Software License Agreement".Localize();

		string eulaText = StaticData.Instance.ReadAllText("Matter CAD EULA.txt").Replace("\r\n", "\n");

		var scrollable = new ScrollableWidget(true);
		scrollable.AnchorAll();
		scrollable.ScrollArea.HAnchor = HAnchor.Stretch;
		contentRow.AddChild(scrollable);

		scrollable.ScrollArea.Margin = new BorderDouble(0, 0, 15, 0);
		scrollable.AddChild(new WrappedTextWidget(eulaText, textColor: theme.TextColor, doubleBufferText: false)
		{
			DrawFromHintedCache = true,
			Name = "LicenseAgreementPage",
		});

		var acceptButton = theme.CreateDialogButton("Accept".Localize());
		acceptButton.Click += (s, e) =>
		{
			UserSettings.Instance.set(UserSettingsKey.SoftwareLicenseAccepted, "true");
			this.Close();
			acceptAction?.Invoke();
		};

		acceptButton.Visible = true;

		this.AddPageAction(acceptButton);
	}

	protected override void OnCancel(out bool abortCancel)
	{
		// Exit if EULA is not accepted
		MatterHackers.MatterControl.AppContext.RootSystemWindow.Close();

		abortCancel = false;
	}
}
