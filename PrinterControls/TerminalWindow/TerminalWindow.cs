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
using MatterHackers.VectorMath;
using System;

namespace MatterHackers.MatterControl
{
	public class TerminalWindow : SystemWindow
	{
		private static readonly Vector2 minSize = new Vector2(400, 300);
		private static readonly string TerminalWindowLeftOpen = "TerminalWindowLeftOpen";
		private static readonly string TerminalWindowSizeKey = "TerminalWindowSize";
		private static readonly string TerminalWindowPositionKey = "TerminalWindowPosition";
		private static TerminalWindow connectionWindow = null;
		private static bool terminalWasOpenOnAppClose = false;

		public static void Show()
		{
			if (connectionWindow == null)
			{
				terminalWasOpenOnAppClose = false;
				string windowSize = UserSettings.Instance.get(TerminalWindowSizeKey);
				int width = 400;
				int height = 300;
				if (windowSize != null && windowSize != "")
				{
					string[] sizes = windowSize.Split(',');
					width = Math.Max(int.Parse(sizes[0]), (int)minSize.x);
					height = Math.Max(int.Parse(sizes[1]), (int)minSize.y);
				}

				connectionWindow = new TerminalWindow(width, height);
				connectionWindow.Closed += (parentSender, e) =>
				{
					connectionWindow = null;
				};

				// start with the assumption we are open and only change this is we see it close
				UserSettings.Instance.Fields.SetBool(TerminalWindowLeftOpen, true);
			}
			else
			{
				connectionWindow.BringToFront();
			}
		}

		public static void ShowIfLeftOpen()
		{
			if (UserSettings.Instance.Fields.GetBool(TerminalWindowLeftOpen, false))
			{
				Show();
			}
		}

		public static void CloseIfOpen()
		{
			if (connectionWindow != null)
			{
				terminalWasOpenOnAppClose = true;
				connectionWindow.Close();
			}
		}

		//private since you can't make one
		private TerminalWindow(int width, int height)
			: base(width, height)
		{
			AlwaysOnTopOfMain = true;
#if __ANDROID__
			this.AddChild(new SoftKeyboardContentOffset(new TerminalWidget(true)));
#else
			this.AddChild(new TerminalWidget(true));
#endif
			Title = LocalizedString.Get("MatterControl - Terminal");
			this.ShowAsSystemWindow();
			MinimumSize = minSize;
			this.Name = "Gcode Terminal";
			string desktopPosition = UserSettings.Instance.get(TerminalWindowPositionKey);
			if (desktopPosition != null && desktopPosition != "")
			{
				string[] sizes = desktopPosition.Split(',');

				//If the desktop position is less than -10,-10, override
				int xpos = Math.Max(int.Parse(sizes[0]), -10);
				int ypos = Math.Max(int.Parse(sizes[1]), -10);
				DesktopPosition = new Point2D(xpos, ypos);
			}
		}

		private void SaveOnClosing()
		{
			// save the last size of the window so we can restore it next time.
			UserSettings.Instance.set(TerminalWindowSizeKey, string.Format("{0},{1}", Width, Height));
			UserSettings.Instance.set(TerminalWindowPositionKey, string.Format("{0},{1}", DesktopPosition.x, DesktopPosition.y));
		}

		public override void OnClosed(EventArgs e)
		{
			SaveOnClosing();
			UserSettings.Instance.Fields.SetBool(TerminalWindowLeftOpen, terminalWasOpenOnAppClose);

			base.OnClosed(e);
		}
	}
}