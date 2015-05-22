/*
Copyright (c) 2015, Kevin Pope
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
using MatterHackers.MatterControl.CustomWidgets;
using System;
using MatterHackers.Localizations;

namespace MatterHackers.MatterControl
{
	public class ApplicationMenuRow : FlowLayoutWidget
	{
		private static FlowLayoutWidget rightElement;
		LinkButtonFactory linkButtonFactory = new LinkButtonFactory();

		public static bool AlwaysShowUpdateStatus { get; set; }

		GuiWidget popUpAboutPage;

		public ApplicationMenuRow()
			: base(FlowDirection.LeftToRight)
		{
			linkButtonFactory.textColor = ActiveTheme.Instance.PrimaryTextColor;
			linkButtonFactory.fontSize = 8;

			Button signInLink = linkButtonFactory.Generate("(Sign Out)");
			signInLink.VAnchor = Agg.UI.VAnchor.ParentCenter;
			signInLink.Margin = new BorderDouble(top: 0);

			this.HAnchor = HAnchor.ParentLeftRight;
			this.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

			// put in the file menu
			MenuOptionFile menuOptionFile = new MenuOptionFile();
			this.AddChild(menuOptionFile);

			MenuOptionSettings menuOptionSettings = new MenuOptionSettings();
			this.AddChild(menuOptionSettings);

			// put in the help menu
			MenuOptionHelp menuOptionHelp = new MenuOptionHelp();
			this.AddChild(menuOptionHelp);

			linkButtonFactory.textColor = RGBA_Bytes.Red;
			linkButtonFactory.fontSize = 10;

			Button updateStatusMessage = linkButtonFactory.Generate("Update Available");
			UpdateControlData.Instance.UpdateStatusChanged.RegisterEvent(SetUpdateNotification, ref unregisterEvents);
			popUpAboutPage = new GuiWidget();
			popUpAboutPage.Margin = new BorderDouble(30, 0, 0, 0);
			popUpAboutPage.HAnchor = HAnchor.FitToChildren;
			popUpAboutPage.VAnchor = VAnchor.FitToChildren | VAnchor.ParentCenter;
			popUpAboutPage.AddChild(updateStatusMessage);
			updateStatusMessage.Click += (sender, e) =>
			{
				UiThread.RunOnIdle((state) =>
				{
					AboutWindow.Show();
				});
			};
			this.AddChild(popUpAboutPage);
			SetUpdateNotification(this, null);

			// put in a spacer
			this.AddChild(new HorizontalSpacer());

			// make an object that can hold custom content on the right (like the sign in)
			rightElement = new FlowLayoutWidget(FlowDirection.LeftToRight);
			rightElement.Height = 24;
			rightElement.Margin = new BorderDouble(bottom: 4);
			this.AddChild(rightElement);

			this.Padding = new BorderDouble(0, 0, 6, 0);

			if (privateAddRightElement != null)
			{
				privateAddRightElement(rightElement);
			}
		}

		public delegate void AddRightElementDelegate(GuiWidget iconContainer);

		public static event AddRightElementDelegate AddRightElement
		{
			add
			{
				privateAddRightElement += value;
				// and call it right away
				value(rightElement);
			}

			remove
			{
				privateAddRightElement -= value;
			}
		}

		private static event AddRightElementDelegate privateAddRightElement;

		private event EventHandler unregisterEvents;
		public override void OnClosed(EventArgs e)
		{
			if (unregisterEvents != null)
			{
				unregisterEvents(this, null);
			}

			base.OnClosed(e);
		}

		public void SetUpdateNotification(object sender, EventArgs widgetEvent)
		{
			switch (UpdateControlData.Instance.UpdateStatus)
			{
				case UpdateControlData.UpdateStatusStates.MayBeAvailable:
					{
						popUpAboutPage.RemoveAllChildren();
						Button updateStatusMessage = linkButtonFactory.Generate("Check For Update".Localize());
						updateStatusMessage.Click += (sender2, e) =>
						{
							UiThread.RunOnIdle((state) =>
							{
								AboutWindow.Show();
							});
						};
						popUpAboutPage.AddChild(updateStatusMessage);
						popUpAboutPage.Visible = true;
					}
					break;

				case UpdateControlData.UpdateStatusStates.ReadyToInstall:
				case UpdateControlData.UpdateStatusStates.UpdateAvailable:
				case UpdateControlData.UpdateStatusStates.UpdateDownloading:
					{
						popUpAboutPage.RemoveAllChildren();
						Button updateStatusMessage = linkButtonFactory.Generate("Update Available".Localize());
						updateStatusMessage.Click += (sender2, e) =>
						{
							UiThread.RunOnIdle((state) =>
							{
								AboutWindow.Show();
							});
						};
						popUpAboutPage.AddChild(updateStatusMessage);
						popUpAboutPage.Visible = true;
					}
					break;

				case UpdateControlData.UpdateStatusStates.UpToDate:
					if (AlwaysShowUpdateStatus)
					{
						popUpAboutPage.RemoveAllChildren();
						TextWidget updateStatusMessage = new TextWidget("Up to Date".Localize(), textColor: ActiveTheme.Instance.PrimaryAccentColor, pointSize: linkButtonFactory.fontSize);
						updateStatusMessage.VAnchor = VAnchor.ParentCenter;
						popUpAboutPage.AddChild(updateStatusMessage);
						popUpAboutPage.Visible = true;

						UiThread.RunOnIdle((state) => popUpAboutPage.Visible = false, 3);
						AlwaysShowUpdateStatus = false;
					}
					else
					{
						popUpAboutPage.Visible = false;
					}
					break;

				case UpdateControlData.UpdateStatusStates.CheckingForUpdate:
					if (AlwaysShowUpdateStatus)
					{
						popUpAboutPage.RemoveAllChildren();
						TextWidget updateStatusMessage = new TextWidget("Checking For Update".Localize(), textColor: ActiveTheme.Instance.PrimaryAccentColor, pointSize: linkButtonFactory.fontSize);
						updateStatusMessage.VAnchor = VAnchor.ParentCenter;
						popUpAboutPage.AddChild(updateStatusMessage);
						popUpAboutPage.Visible = true;
					}
					else
					{
						popUpAboutPage.Visible = false;
					}
					break;

				default:
					throw new NotImplementedException();
			}
		}
	}
}