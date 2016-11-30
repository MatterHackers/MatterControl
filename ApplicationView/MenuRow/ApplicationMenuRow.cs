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
using MatterHackers.MatterControl.AboutPage;
using System;
using MatterHackers.Localizations;

namespace MatterHackers.MatterControl
{
	public class ApplicationMenuRow : FlowLayoutWidget
	{
		public delegate void AddRightElementDelegate(FlowLayoutWidget iconContainer);

		public static event AddRightElementDelegate AddRightElement;

		public static bool AlwaysShowUpdateStatus { get; set; }

		private FlowLayoutWidget rightElement;

		LinkButtonFactory linkButtonFactory = new LinkButtonFactory();

		private event EventHandler unregisterEvents;

		GuiWidget popUpAboutPage;

		public ApplicationMenuRow()
			: base(FlowDirection.LeftToRight)
		{
			linkButtonFactory.textColor = ActiveTheme.Instance.PrimaryTextColor;
			linkButtonFactory.fontSize = 8;

			this.HAnchor = HAnchor.ParentLeftRight;
			this.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

			// put in the file menu
			this.AddChild(new MenuOptionFile());

			this.AddChild(new MenuOptionSettings());

			// put in the help menu
			this.AddChild(new MenuOptionMacros());

			// put in the help menu
			this.AddChild(new MenuOptionHelp());

			//linkButtonFactory.textColor = ActiveTheme.Instance.SecondaryAccentColor;
			linkButtonFactory.fontSize = 10;

			Button updateStatusMessage = linkButtonFactory.Generate("Update Available");
			UpdateControlData.Instance.UpdateStatusChanged.RegisterEvent(SetUpdateNotification, ref unregisterEvents);
			popUpAboutPage = new FlowLayoutWidget();
			popUpAboutPage.Margin = new BorderDouble(30, 0, 0, 0);
			popUpAboutPage.HAnchor = HAnchor.FitToChildren;
			popUpAboutPage.VAnchor = VAnchor.FitToChildren | VAnchor.ParentCenter;
			popUpAboutPage.AddChild(updateStatusMessage);
			updateStatusMessage.Click += (sender, e) =>
			{
				UiThread.RunOnIdle(CheckForUpdateWindow.Show);
			};
			this.AddChild(popUpAboutPage);
			SetUpdateNotification(this, null);

			// put in a spacer
			this.AddChild(new HorizontalSpacer());

			// make an object that can hold custom content on the right (like the sign in)
			rightElement = new FlowLayoutWidget(FlowDirection.LeftToRight);
			rightElement.VAnchor = VAnchor.FitToChildren;
			this.AddChild(rightElement);

			this.Padding = new BorderDouble(0);

			AddRightElement?.Invoke(rightElement);

			// When the application is first started, plugins are loaded after the MainView control has been initialized,
			// and as such they not around when this constructor executes. In that case, we run the AddRightElement 
			// delegate after the plugins have been initialized via the PluginsLoaded event
			ApplicationController.Instance.PluginsLoaded.RegisterEvent((s, e) =>
			{
				AddRightElement?.Invoke(rightElement);
			}, ref unregisterEvents);
		}

		public override void OnClosed(EventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
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
							UiThread.RunOnIdle(CheckForUpdateWindow.Show);
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
							UiThread.RunOnIdle(CheckForUpdateWindow.Show);
						};
						var updateMark = new UpdateNotificationMark();
						updateMark.Margin = new BorderDouble(0, 0, 3, 2);
						updateMark.VAnchor = VAnchor.ParentTop;
						popUpAboutPage.AddChild(updateMark);
						popUpAboutPage.AddChild(updateStatusMessage);
						popUpAboutPage.Visible = true;
					}
					break;

				case UpdateControlData.UpdateStatusStates.UpToDate:
					if (AlwaysShowUpdateStatus)
					{
						popUpAboutPage.RemoveAllChildren();
						TextWidget updateStatusMessage = new TextWidget("Up to Date".Localize(), textColor: linkButtonFactory.textColor, pointSize: linkButtonFactory.fontSize);
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
						TextWidget updateStatusMessage = new TextWidget("Checking For Update...".Localize(), textColor: linkButtonFactory.textColor, pointSize: linkButtonFactory.fontSize);
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