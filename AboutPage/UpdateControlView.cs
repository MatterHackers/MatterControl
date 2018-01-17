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
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl
{
	public class UpdateControlView : FlowLayoutWidget
	{
		private GuiWidget downloadButton;
		private GuiWidget checkUpdateButton;
		private GuiWidget installButton;
		private TextWidget updateStatusText;

		private EventHandler unregisterEvents;

		public UpdateControlView(ThemeConfig theme)
		{
			this.HAnchor = HAnchor.Stretch;
			this.BackgroundColor = theme.MinimalShade;
			this.Padding = theme.ToolbarPadding.Clone(left: 8);

			this.AddChild(updateStatusText = new TextWidget(string.Format(""), textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				AutoExpandBoundsToText = true,
				VAnchor = VAnchor.Center
			});

			this.AddChild(new HorizontalSpacer());

			checkUpdateButton = new IconButton(AggContext.StaticData.LoadIcon("fa-refresh_14.png", IconColor.Theme), ApplicationController.Instance.Theme)
			{
				ToolTipText = "Check for Update".Localize(),
				BackgroundColor = theme.MinimalShade,
				Cursor = Cursors.Hand,
				Visible = false
			};
			checkUpdateButton.Click += (s, e) =>
			{
				UpdateControlData.Instance.CheckForUpdate();
			};
			this.AddChild(checkUpdateButton);

			this.MinimumSize = new Vector2(0, checkUpdateButton.Height);

			downloadButton = new TextButton("Download Update".Localize(), theme)
			{
				BackgroundColor = theme.MinimalShade,
				Visible = false
			};
			downloadButton.Click += (s, e) =>
			{
				downloadButton.Visible = false;
				updateStatusText.Text = "Retrieving download info...".Localize();

				UpdateControlData.Instance.InitiateUpdateDownload();
			};
			this.AddChild(downloadButton);

			installButton = new TextButton("Install Update".Localize(), theme)
			{
				BackgroundColor = theme.MinimalShade,
				Visible = false
			};
			installButton.Click += (s, e) =>
			{
				try
				{
					if (!UpdateControlData.Instance.InstallUpdate())
					{
						installButton.Visible = false;
						updateStatusText.Text = "Oops! Unable to install update.".Localize();
					}
				}
				catch
				{
					GuiWidget.BreakInDebugger();
					installButton.Visible = false;
					updateStatusText.Text = "Oops! Unable to install update.".Localize();
				}
			};
			this.AddChild(installButton);

			UpdateControlData.Instance.UpdateStatusChanged.RegisterEvent(UpdateStatusChanged, ref unregisterEvents);

			this.UpdateStatusChanged(null, null);
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}

		string recommendedUpdateAvailable = "There is a recommended update available".Localize();
		string requiredUpdateAvailable = "There is a required update available".Localize();

		private void UpdateStatusChanged(object sender, EventArgs e)
		{
			switch (UpdateControlData.Instance.UpdateStatus)
			{
				case UpdateControlData.UpdateStatusStates.MayBeAvailable:
					updateStatusText.Text = "New updates may be available".Localize();
					checkUpdateButton.Visible = true;
					break;

				case UpdateControlData.UpdateStatusStates.CheckingForUpdate:
					updateStatusText.Text = "Checking for updates...".Localize();
					//checkUpdateLink.Visible = false;
					break;

				case UpdateControlData.UpdateStatusStates.UnableToConnectToServer:
					updateStatusText.Text = "Oops! Unable to connect to server".Localize();
					downloadButton.Visible = false;
					installButton.Visible = false;
					checkUpdateButton.Visible = true;
					break;

				case UpdateControlData.UpdateStatusStates.UpdateAvailable:
					if (UpdateControlData.Instance.UpdateRequired)
					{
						updateStatusText.Text = requiredUpdateAvailable;
					}
					else
					{
						updateStatusText.Text = recommendedUpdateAvailable;
					}
					downloadButton.Visible = true;
					installButton.Visible = false;
					checkUpdateButton.Visible = false;
					break;

				case UpdateControlData.UpdateStatusStates.UpdateDownloading:
					updateStatusText.Text = string.Format(
						"{0} {1}%",
						"Downloading updates...".Localize(),
						UpdateControlData.Instance.DownloadPercent);
					break;

				case UpdateControlData.UpdateStatusStates.ReadyToInstall:
					updateStatusText.Text = "New updates are ready to install".Localize();
					downloadButton.Visible = false;
					installButton.Visible = true;
					checkUpdateButton.Visible = false;
					break;

				case UpdateControlData.UpdateStatusStates.UpToDate:
					updateStatusText.Text = "Your application is up-to-date".Localize();
					downloadButton.Visible = false;
					installButton.Visible = false;
					checkUpdateButton.Visible = true;
					break;

				default:
					throw new NotImplementedException();
			}
		}
	}
}