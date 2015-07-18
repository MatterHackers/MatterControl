/*
Copyright (c) 2014, Lars Brubaker
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
using System;
using System.Globalization;

namespace MatterHackers.MatterControl
{
	public class PopOutManager
	{
		private static readonly string WindowLeftOpenSufix = "_WindowLeftOpen";
		private static readonly string WindowSizeSufix = "_WindowSize";
		private static readonly string PositionSufix = "_WindowPosition";

		private string WindowLeftOpenKey;
		private string WindowSizeKey;
		private string PositionKey;

		private SystemWindow PopedOutSystemWindow = null;
		private GuiWidget widgetWhosContentsPopOut = null;
		private string windowTitle;

		private Vector2 minSize;

		private string dataBaseKeyPrefix;

		#region Public Members

		public PopOutManager(GuiWidget widgetWhosContentsPopOut, Vector2 minSize, string windowTitle, string dataBaseKeyPrefix)
		{
			TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;
			string titeCaseTitle = textInfo.ToTitleCase(windowTitle.ToLower());

			this.windowTitle = "MatterControl - " + titeCaseTitle;
			this.minSize = minSize;
			this.dataBaseKeyPrefix = dataBaseKeyPrefix;
			this.widgetWhosContentsPopOut = widgetWhosContentsPopOut;

			UiThread.RunOnIdle(() =>
			{
				ApplicationController.Instance.MainView.DrawAfter += ShowOnFirstSystemWindowDraw;
			});

			widgetWhosContentsPopOut.Closed += (sender, e) =>
			{
				WidgetWhosContentsPopOutIsClosing();
			};

			WindowLeftOpenKey = dataBaseKeyPrefix + WindowLeftOpenSufix;
			WindowSizeKey = dataBaseKeyPrefix + WindowSizeSufix;
			PositionKey = dataBaseKeyPrefix + PositionSufix;
		}

		public static void SetPopOutState(string dataBaseKeyPrefix, bool poppedOut)
		{
			string windowLeftOpenKey = dataBaseKeyPrefix + WindowLeftOpenSufix;
			UserSettings.Instance.Fields.SetBool(windowLeftOpenKey, poppedOut);
		}

		public static void SetStates(string dataBaseKeyPrefix, bool poppedOut, double width, double height, double positionX, double positionY)
		{
			string windowLeftOpenKey = dataBaseKeyPrefix + WindowLeftOpenSufix;
			string windowSizeKey = dataBaseKeyPrefix + WindowSizeSufix;
			string positionKey = dataBaseKeyPrefix + PositionSufix;

			UserSettings.Instance.Fields.SetBool(windowLeftOpenKey, poppedOut);

			UserSettings.Instance.set(windowSizeKey, string.Format("{0},{1}", width, height));
			UserSettings.Instance.set(positionKey, string.Format("{0},{1}", positionX, positionY));
		}

		public void ShowContentInWindow()
		{
			if (PopedOutSystemWindow == null)
			{
				// So the window is open now only change this is we close it.
				UserSettings.Instance.Fields.SetBool(WindowLeftOpenKey, true);

				string windowSize = UserSettings.Instance.get(WindowSizeKey);
				int width = 600;
				int height = 400;
				if (windowSize != null && windowSize != "")
				{
					string[] sizes = windowSize.Split(',');
					width = Math.Max(int.Parse(sizes[0]), (int)minSize.x);
					height = Math.Max(int.Parse(sizes[1]), (int)minSize.y);
				}

				PopedOutSystemWindow = new SystemWindow(width, height);
				PopedOutSystemWindow.Padding = new BorderDouble(3);
				PopedOutSystemWindow.Title = windowTitle;
				PopedOutSystemWindow.AlwaysOnTopOfMain = true;
				PopedOutSystemWindow.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
				PopedOutSystemWindow.Closing += SystemWindow_Closing;
				if (widgetWhosContentsPopOut.Children.Count == 1)
				{
					GuiWidget child = widgetWhosContentsPopOut.Children[0];
					widgetWhosContentsPopOut.RemoveChild(child);
					child.ClearRemovedFlag();
					widgetWhosContentsPopOut.AddChild(CreateContentForEmptyControl());
					PopedOutSystemWindow.AddChild(child);
				}
				PopedOutSystemWindow.ShowAsSystemWindow();

				PopedOutSystemWindow.MinimumSize = minSize;
				string desktopPosition = UserSettings.Instance.get(PositionKey);
				if (desktopPosition != null && desktopPosition != "")
				{
					string[] sizes = desktopPosition.Split(',');

					//If the desktop position is less than -10,-10, override
					int xpos = Math.Max(int.Parse(sizes[0]), -10);
					int ypos = Math.Max(int.Parse(sizes[1]), -10);
					PopedOutSystemWindow.DesktopPosition = new Point2D(xpos, ypos);
				}
			}
			else
			{
				PopedOutSystemWindow.BringToFront();
			}
		}

		#endregion Public Members

		private void WidgetWhosContentsPopOutIsClosing()
		{
			if (PopedOutSystemWindow != null)
			{
				SaveSizeAndPosition();
				PopedOutSystemWindow.CloseAndRemoveAllChildren();
				PopedOutSystemWindow.Close();
			}
		}

		private void ShowOnFirstSystemWindowDraw(GuiWidget drawingWidget, DrawEventArgs e)
		{
			if (widgetWhosContentsPopOut.Children.Count > 0)
			{
				UiThread.RunOnIdle(() =>
				{
					bool wasLeftOpen = UserSettings.Instance.Fields.GetBool(WindowLeftOpenKey, false);
					if (wasLeftOpen)
					{
						ShowContentInWindow();
					}
				});
			}

			ApplicationController.Instance.MainView.DrawAfter -= ShowOnFirstSystemWindowDraw;
		}

		private GuiWidget CreateContentForEmptyControl()
		{
			GuiWidget allContent = new GuiWidget(HAnchor.ParentLeftRight, VAnchor.ParentBottomTop);
			allContent.Padding = new BorderDouble(5, 10, 5, 10);

			FlowLayoutWidget flowWidget = new FlowLayoutWidget();
			flowWidget.BackgroundColor = ActiveTheme.Instance.TransparentDarkOverlay;
			flowWidget.HAnchor = HAnchor.ParentLeftRight;
			flowWidget.VAnchor = VAnchor.ParentTop;
			flowWidget.Padding = new BorderDouble(10, 0);
			flowWidget.Height = 60;

			TextImageButtonFactory bringBackButtonFactory = new TextImageButtonFactory();
			bringBackButtonFactory.normalFillColor = RGBA_Bytes.Gray;
			bringBackButtonFactory.normalTextColor = ActiveTheme.Instance.PrimaryTextColor;

			Button bringBackToTabButton = bringBackButtonFactory.Generate(LocalizedString.Get("Restore"));
			bringBackToTabButton.VAnchor = VAnchor.ParentCenter;
			bringBackToTabButton.Cursor = Cursors.Hand;
			bringBackToTabButton.Click += (sender, e) =>
			{
				UiThread.RunOnIdle(() =>
				{
					SaveWindowShouldStartClosed();
					SystemWindow temp = PopedOutSystemWindow;
					SystemWindow_Closing(null, null);
					temp.Close();
				});
			};

			TextWidget windowedModeMessage = new TextWidget(LocalizedString.Get("WINDOWED MODE: This tab has been moved to a separate window."),
				pointSize: 10, textColor: ActiveTheme.Instance.PrimaryTextColor);
			windowedModeMessage.VAnchor = VAnchor.ParentCenter;

			flowWidget.AddChild(windowedModeMessage);
			flowWidget.AddChild(new HorizontalSpacer());
			flowWidget.AddChild(bringBackToTabButton);

			allContent.AddChild(flowWidget);

			return allContent;
		}

		private void SaveWindowShouldStartClosed()
		{
			UserSettings.Instance.Fields.SetBool(WindowLeftOpenKey, false);
		}

		private void SystemWindow_Closing(object sender, WidgetClosingEnventArgs closingEvent)
		{
			SaveSizeAndPosition();
			SaveWindowShouldStartClosed();
			if (PopedOutSystemWindow.Children.Count == 1)
			{
				GuiWidget child = PopedOutSystemWindow.Children[0];
				PopedOutSystemWindow.RemoveChild(child);
				child.ClearRemovedFlag();
				widgetWhosContentsPopOut.RemoveAllChildren();
				widgetWhosContentsPopOut.AddChild(child);
			}
			PopedOutSystemWindow = null;
		}

		private void SaveSizeAndPosition()
		{
			if (PopedOutSystemWindow != null)
			{
				UserSettings.Instance.set(WindowSizeKey, string.Format("{0},{1}", PopedOutSystemWindow.Width, PopedOutSystemWindow.Height));
				UserSettings.Instance.set(PositionKey, string.Format("{0},{1}", PopedOutSystemWindow.DesktopPosition.x, PopedOutSystemWindow.DesktopPosition.y));
			}
		}
	}
}