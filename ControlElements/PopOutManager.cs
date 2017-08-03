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
		private static readonly string PositionSufix = "_WindowPosition";
		private static readonly string WindowLeftOpenSufix = "_WindowLeftOpen";
		private static readonly string WindowSizeSufix = "_WindowSize";
		private string dataBaseKeyPrefix;
		private Vector2 minSize;
		private SystemWindow systemWindowWithPopContent = null;
		private string PositionKey;
		private GuiWidget widgetWithPopContent = null;
		private string WindowLeftOpenKey;
		private string WindowSizeKey;
		private string windowTitle;

		public PopOutManager(GuiWidget widgetWithPopContent, Vector2 minSize, string windowTitle, string dataBaseKeyPrefix)
		{
			TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;
			string titeCaseTitle = textInfo.ToTitleCase(windowTitle.ToLower());

			this.windowTitle = "MatterControl - " + titeCaseTitle;
			this.minSize = minSize;
			this.dataBaseKeyPrefix = dataBaseKeyPrefix;
			this.widgetWithPopContent = widgetWithPopContent;

			UiThread.RunOnIdle(() =>
			{
				ApplicationController.Instance.MainView.AfterDraw += ShowOnNextMatterControlDraw;
			});

			widgetWithPopContent.Closed += (sender, e) =>
			{
				WidgetWithPopContentIsClosing();
			};

			WindowLeftOpenKey = dataBaseKeyPrefix + WindowLeftOpenSufix;
			WindowSizeKey = dataBaseKeyPrefix + WindowSizeSufix;
			PositionKey = dataBaseKeyPrefix + PositionSufix;
		}

		public static bool SaveIfClosed { get; set; }

		public void ShowContentInWindow()
		{
			if(widgetWithPopContent.HasBeenClosed)
			{
				if(systemWindowWithPopContent != null)
				{
					systemWindowWithPopContent.Close();
				}

				return;
			}

			if (systemWindowWithPopContent == null)
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

				systemWindowWithPopContent = new SystemWindow(width, height);
				systemWindowWithPopContent.Padding = new BorderDouble(3);
				systemWindowWithPopContent.Title = windowTitle;
				systemWindowWithPopContent.AlwaysOnTopOfMain = true;
				systemWindowWithPopContent.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
				systemWindowWithPopContent.Closing += SystemWindow_Closing;
				if (widgetWithPopContent.Children.Count == 1)
				{
					GuiWidget child = widgetWithPopContent.Children[0];
					widgetWithPopContent.RemoveChild(child);
					child.ClearRemovedFlag();
					widgetWithPopContent.AddChild(CreateContentForEmptyControl());
					systemWindowWithPopContent.AddChild(child);
				}
				systemWindowWithPopContent.ShowAsSystemWindow();

				systemWindowWithPopContent.MinimumSize = minSize;
				string desktopPosition = UserSettings.Instance.get(PositionKey);
				if (desktopPosition != null && desktopPosition != "")
				{
					string[] sizes = desktopPosition.Split(',');

					//If the desktop position is less than -10,-10, override
					int xpos = Math.Max(int.Parse(sizes[0]), -10);
					int ypos = Math.Max(int.Parse(sizes[1]), -10);
					systemWindowWithPopContent.DesktopPosition = new Point2D(xpos, ypos);
				}
			}
			else
			{
				systemWindowWithPopContent.BringToFront();
			}
		}

		private static void SetPopOutState(string dataBaseKeyPrefix, bool poppedOut)
		{
			string windowLeftOpenKey = dataBaseKeyPrefix + WindowLeftOpenSufix;
			UserSettings.Instance.Fields.SetBool(windowLeftOpenKey, poppedOut);
		}

		private static void SetStates(string dataBaseKeyPrefix, bool poppedOut, double width, double height, double positionX, double positionY)
		{
			string windowLeftOpenKey = dataBaseKeyPrefix + WindowLeftOpenSufix;
			string windowSizeKey = dataBaseKeyPrefix + WindowSizeSufix;
			string positionKey = dataBaseKeyPrefix + PositionSufix;

			UserSettings.Instance.Fields.SetBool(windowLeftOpenKey, poppedOut);

			UserSettings.Instance.set(windowSizeKey, string.Format("{0},{1}", width, height));
			UserSettings.Instance.set(positionKey, string.Format("{0},{1}", positionX, positionY));
		}

		private GuiWidget CreateContentForEmptyControl()
		{
			GuiWidget allContent = new GuiWidget()
			{
				HAnchor = HAnchor.ParentLeftRight,
				VAnchor = VAnchor.ParentBottomTop,
			};
			allContent.Padding = new BorderDouble(5, 10, 5, 10);

			FlowLayoutWidget flowWidget = new FlowLayoutWidget();
			flowWidget.BackgroundColor = ActiveTheme.Instance.TransparentDarkOverlay;
			flowWidget.HAnchor = HAnchor.ParentLeftRight;
			flowWidget.VAnchor = VAnchor.ParentTop;
			flowWidget.Padding = new BorderDouble(10, 0);
			flowWidget.Height = 60;

			TextImageButtonFactory bringBackButtonFactory = new TextImageButtonFactory();
			bringBackButtonFactory.Options.Normal.FillColor = RGBA_Bytes.Gray;
			bringBackButtonFactory.Options.Normal.TextColor = ActiveTheme.Instance.PrimaryTextColor;

			Button bringBackToTabButton = bringBackButtonFactory.Generate("Restore".Localize());
			bringBackToTabButton.ToolTipText = "Bring the Window back into this Tab".Localize();
			bringBackToTabButton.VAnchor = VAnchor.ParentCenter;
			bringBackToTabButton.Cursor = Cursors.Hand;
			bringBackToTabButton.Click += (sender, e) =>
			{
				UiThread.RunOnIdle(() =>
				{
					SaveWindowShouldStartClosed();
					SystemWindow temp = systemWindowWithPopContent;
					SystemWindow_Closing(null, null);
					temp.Close();
				});
			};

			TextWidget windowedModeMessage = new TextWidget("WINDOWED MODE: This tab has been moved to a separate window.".Localize(),
				pointSize: 10, textColor: ActiveTheme.Instance.PrimaryTextColor);
			windowedModeMessage.VAnchor = VAnchor.ParentCenter;

			flowWidget.AddChild(windowedModeMessage);
			flowWidget.AddChild(new HorizontalSpacer());
			flowWidget.AddChild(bringBackToTabButton);

			allContent.AddChild(flowWidget);

			return allContent;
		}

		private void SaveSizeAndPosition()
		{
			if (systemWindowWithPopContent != null)
			{
				UserSettings.Instance.set(WindowSizeKey, string.Format("{0},{1}", systemWindowWithPopContent.Width, systemWindowWithPopContent.Height));
				UserSettings.Instance.set(PositionKey, string.Format("{0},{1}", systemWindowWithPopContent.DesktopPosition.x, systemWindowWithPopContent.DesktopPosition.y));
			}
		}

		private void SaveWindowShouldStartClosed()
		{
			if (!MatterControlApplication.Instance.HasBeenClosed
				&& PopOutManager.SaveIfClosed)
			{
				UserSettings.Instance.Fields.SetBool(WindowLeftOpenKey, false);
			}
		}

		private void ShowOnNextMatterControlDraw(Object drawingWidget, DrawEventArgs e)
		{
			if (widgetWithPopContent.Children.Count > 0)
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

			ApplicationController.Instance.MainView.AfterDraw -= ShowOnNextMatterControlDraw;
		}

		private void SystemWindow_Closing(object sender, ClosingEventArgs closingEvent)
		{
			if (systemWindowWithPopContent != null)
			{
				SaveSizeAndPosition();
				SaveWindowShouldStartClosed();
				if (systemWindowWithPopContent.Children.Count == 1)
				{
					GuiWidget child = systemWindowWithPopContent.Children[0];
					systemWindowWithPopContent.RemoveChild(child);
					child.ClearRemovedFlag();
					widgetWithPopContent.RemoveAllChildren();
					widgetWithPopContent.AddChild(child);
				}
				systemWindowWithPopContent = null;
			}
		}

		private void WidgetWithPopContentIsClosing()
		{
			if (systemWindowWithPopContent != null)
			{
				SaveSizeAndPosition();
				systemWindowWithPopContent.CloseAllChildren();
				systemWindowWithPopContent.Close();
			}
		}
	}
}