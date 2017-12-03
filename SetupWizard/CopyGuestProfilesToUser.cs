/*
Copyright (c) 2016, Lars Brubaker
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

using System.Collections.Generic;
using System.IO;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl
{
	public class CopyGuestProfilesToUser : DialogPage
	{
		private string importMessage = "It's time to copy your existing printer settings to your MatterHackers account. Once copied, these printers will be available whenever you sign in to MatterControl. Printers that are not copied will only be available when not signed in.".Localize();

		private CheckBox rememberChoice;

		private List<CheckBox> checkBoxes = new List<CheckBox>();

		public CopyGuestProfilesToUser()
		: base("Close")
		{
			this.WindowTitle = "Copy Printers".Localize();
			this.HeaderText = "Copy Printers to Account".Localize();

			var scrollWindow = new ScrollableWidget()
			{
				AutoScroll = true,
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch,
			};
			scrollWindow.ScrollArea.HAnchor = HAnchor.Stretch;
			contentRow.AddChild(scrollWindow);

			var container = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Stretch,
			};
			scrollWindow.AddChild(container);

			container.AddChild(new WrappedTextWidget(importMessage, textColor: ActiveTheme.Instance.PrimaryTextColor));

			var byCheckbox = new Dictionary<CheckBox, PrinterInfo>();

			var guest = ProfileManager.Load("guest");
			if (guest?.Profiles.Count > 0)
			{
				container.AddChild(new TextWidget("Printers to Copy".Localize() + ":")
				{
					TextColor = ActiveTheme.Instance.PrimaryTextColor,
					Margin = new BorderDouble(0, 3, 0, 15),
				});

				foreach (var printerInfo in guest.Profiles)
				{
					var checkBox = new CheckBox(printerInfo.Name)
					{
						TextColor = ActiveTheme.Instance.PrimaryTextColor,
						Margin = new BorderDouble(5, 0, 0, 0),
						HAnchor = HAnchor.Left,
						Checked = true,
					};
					checkBoxes.Add(checkBox);
					container.AddChild(checkBox);

					byCheckbox[checkBox] = printerInfo;
				}
			}

			var syncButton = textImageButtonFactory.Generate("Copy".Localize());
			syncButton.Name = "CopyProfilesButton";
			syncButton.Click += (s, e) =>
			{
				// do the import
				foreach (var checkBox in checkBoxes)
				{
					if (checkBox.Checked)
					{
						// import the printer
						var printerInfo = byCheckbox[checkBox];

						string existingPath = guest.ProfilePath(printerInfo);

						// PrinterSettings files must actually be copied to the users profile directory
						if (File.Exists(existingPath))
						{
							File.Copy(existingPath, printerInfo.ProfilePath);

							// Only add if copy succeeds
							ProfileManager.Instance.Profiles.Add(printerInfo);
						}
					}
				}

				guest.Save();

				// Close the window and update the PrintersImported flag
				UiThread.RunOnIdle(() =>
				{
					WizardWindow.Close();

					ProfileManager.Instance.PrintersImported = true;
					ProfileManager.Instance.Save();
				});
			};

			rememberChoice = new CheckBox("Don't remind me again".Localize(), ActiveTheme.Instance.PrimaryTextColor);
			contentRow.AddChild(rememberChoice);

			syncButton.Visible = true;

			this.AddPageAction(syncButton);
		}

		protected override void OnCancel(out bool abortCancel)
		{
			// If "Don't remind me" checked, update the PrintersImported flag on close
			if (rememberChoice.Checked)
			{
				ProfileManager.Instance.PrintersImported = true;
				ProfileManager.Instance.Save();
			}

			abortCancel = false;
		}
	}
}