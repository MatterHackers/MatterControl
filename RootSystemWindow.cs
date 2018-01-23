/*
Copyright (c) 2016, Lars Brubaker, Kevin Pope
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
using System.Diagnostics;
using System.IO;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.SettingsManagement;
using MatterHackers.RenderOpenGl.OpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl
{
	public class RootSystemWindow : SystemWindow
	{
		private static Vector2 minSize { get; set; } = new Vector2(600, 600);

		private Stopwatch totalDrawTime = new Stopwatch();

		private bool dropWasOnChild = true;

		private AverageMillisecondTimer millisecondTimer = new AverageMillisecondTimer();

		internal static bool ShowMemoryUsed = false;

		private int drawCount = 0;

		private bool exitDialogOpen = false;

		public RootSystemWindow(double width, double height)
			: base(width, height)
		{
			this.Name = "MatterControl";
			this.Padding = new BorderDouble(0); //To be re-enabled once native borders are turned off
			this.AnchorAll();

			GuiWidget.DefaultEnforceIntegerBounds = true;

			// TODO: Needs review - doesn't seem like we want to scale on Touchscreen, rather we want device specific, configuration based scaling. Suggest remove
			if (UserSettings.Instance.IsTouchScreen)
			{
				GuiWidget.DeviceScale = 1.3;
				SystemWindow.ShareSingleOsWindow = true;
			}

			string textSizeMode = UserSettings.Instance.get(UserSettingsKey.ApplicationTextSize);
			if (!string.IsNullOrEmpty(textSizeMode))
			{
				double textSize = 1.0;
				if (double.TryParse(textSizeMode, out textSize))
				{
					GuiWidget.DeviceScale = textSize;
				}
			}

			if (GL.HardwareAvailable)
			{
				UseOpenGL = true;
			}

			this.SetStartupTraits();
		}

		public void SetStartupTraits()
		{
			string version = "2.0";

			this.MinimumSize = minSize;

			this.Title = "MatterHackers: MatterControl {0}".FormatWith(version);
			if (OemSettings.Instance.WindowTitleExtra != null && OemSettings.Instance.WindowTitleExtra.Trim().Length > 0)
			{
				this.Title = this.Title + " - {1}".FormatWith(version, OemSettings.Instance.WindowTitleExtra);
			}

			this.Title = IntPtr.Size == 4 ? this.Title + " - 32Bit" : this.Title = this.Title + " - 64Bit";

			string desktopPosition = ApplicationSettings.Instance.get(ApplicationSettingsKey.DesktopPosition);
			if (!string.IsNullOrEmpty(desktopPosition))
			{
				string[] sizes = desktopPosition.Split(',');

				//If the desktop position is less than -10,-10, override
				int xpos = Math.Max(int.Parse(sizes[0]), -10);
				int ypos = Math.Max(int.Parse(sizes[1]), -10);

				this.DesktopPosition = new Point2D(xpos, ypos);
			}
			else
			{
				this.DesktopPosition = new Point2D(-1, -1);
			}

			this.Maximized = ApplicationSettings.Instance.get(ApplicationSettingsKey.MainWindowMaximized) == "true";

#if IS_WINDOWS_FORMS
			if (!Clipboard.IsInitialized)
			{
				Clipboard.SetSystemClipboard(new WindowsFormsClipboard());
			}
#endif

#if DEBUG && !__ANDROID__
			WinformsSystemWindow.InspectorCreator = (inspectingWindow) =>
			{
				if (inspectingWindow == this)
				{
					// If this is MatterControlApplication, include Scene
					var partContext = ApplicationController.Instance.DragDropData;
					return new InspectForm(inspectingWindow, partContext.SceneContext.Scene, partContext.View3DWidget);
				}
				else
				{
					// Otherwise, exclude Scene
					return new InspectForm(inspectingWindow);
				}
			};
#endif
		}

		public static (int width, int height) GetStartupBounds(int overrideWidth = -1, int overrideHeight = -1)
		{
			int width = 0;
			int height = 0;
			if (UserSettings.Instance.IsTouchScreen)
			{
				minSize = new Vector2(800, 480);
			}

			// check if the app has a size already set
			string windowSize = ApplicationSettings.Instance.get(ApplicationSettingsKey.WindowSize);
			if (windowSize != null && windowSize != "")
			{
				// try and open our window matching the last size that we had for it.
				string[] sizes = windowSize.Split(',');

				width = Math.Max(int.Parse(sizes[0]), (int)minSize.X + 1);
				height = Math.Max(int.Parse(sizes[1]), (int)minSize.Y + 1);
			}
			else // try to set it to a big size or the min size
			{
				Point2D desktopSize = AggContext.DesktopSize;

				if (overrideWidth != -1)
				{
					width = overrideWidth;
				}
				else // try to set it to a good size
				{
					if (width < desktopSize.x)
					{
						width = 1280;
					}
				}

				if (overrideHeight != -1)
				{
					// Height should be constrained to actual
					height = Math.Min(overrideHeight, desktopSize.y);
				}
				else
				{
					if (height < desktopSize.y)
					{
						height = 720;
					}
				}
			}

			return (width, height);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			totalDrawTime.Restart();
			GuiWidget.DrawCount = 0;
			using (new PerformanceTimer("Draw Timer", "MC Draw"))
			{
				base.OnDraw(graphics2D);
			}
			totalDrawTime.Stop();

			millisecondTimer.Update((int)totalDrawTime.ElapsedMilliseconds);

			if (ShowMemoryUsed)
			{
				long memory = GC.GetTotalMemory(false);
				this.Title = "Allocated = {0:n0} : {1:000}ms, d{2} Size = {3}x{4}, onIdle = {5:00}:{6:00}, widgetsDrawn = {7}".FormatWith(memory, millisecondTimer.GetAverage(), drawCount++, this.Width, this.Height, UiThread.CountExpired, UiThread.Count, GuiWidget.DrawCount);
			}

			//msGraph.AddData("ms", totalDrawTime.ElapsedMilliseconds);
			//msGraph.Draw(MatterHackers.Agg.Transform.Affine.NewIdentity(), graphics2D);
		}

		public override void OnClosing(ClosingEventArgs eventArgs)
		{
			if (this.HasBeenClosed)
			{
				return;
			}

			// save the last size of the window so we can restore it next time.
			ApplicationSettings.Instance.set(ApplicationSettingsKey.MainWindowMaximized, this.Maximized.ToString().ToLower());

			if (!this.Maximized)
			{
				ApplicationSettings.Instance.set(ApplicationSettingsKey.WindowSize, string.Format("{0},{1}", Width, Height));
				ApplicationSettings.Instance.set(ApplicationSettingsKey.DesktopPosition, string.Format("{0},{1}", DesktopPosition.x, DesktopPosition.y));
			}

			//Save a snapshot of the prints in queue
			QueueData.Instance.SaveDefaultQueue();

			// If we are waiting for a response and get another request, just cancel the close until we get a response.
			if (exitDialogOpen)
			{
				eventArgs.Cancel = true;
			}

			string caption = null;
			string message = null;

			if (!ApplicationController.Instance.ApplicationExiting
				&& !exitDialogOpen
				&& ApplicationController.Instance.ActivePrinter.Connection.PrinterIsPrinting)
			{
				if (ApplicationController.Instance.ActivePrinter.Connection.CommunicationState != CommunicationStates.PrintingFromSd)
				{
					caption = "Abort Print".Localize();
					message = "Are you sure you want to abort the current print and close MatterControl?".Localize();
				}
				else
				{
					caption = "Exit while printing".Localize();
					message = "Are you sure you want exit while a print is running from SD Card?\n\nNote: If you exit, it is recommended you wait until the print is completed before running MatterControl again.".Localize();
				}
			}
#if !__ANDROID__
			else if (PartsSheet.IsSaving())
			{
				caption = "Confirm Exit".Localize();
				message = "You are currently saving a parts sheet, are you sure you want to exit?".Localize();
			}
#endif
			if (caption != null)
			{
				// Record that we are waiting for a response to the request to close
				exitDialogOpen = true;

				// We need to show an interactive dialog to determine if the original Close request should be honored, thus cancel the current Close request
				eventArgs.Cancel = true;

				UiThread.RunOnIdle(() =>
				{
					StyledMessageBox.ShowMessageBox(
						(exitConfirmed) =>
						{
							// Record that the exitDialog has closed
							exitDialogOpen = false;

							// Continue with the original shutdown request if exit confirmed by user
							if (exitConfirmed)
							{
								ApplicationController.Instance.ApplicationExiting = true;

								ApplicationController.Instance.Shutdown();

								// Always call PrinterConnection.Disable on exit unless PrintingFromSd
								PrinterConnection printerConnection = ApplicationController.Instance.ActivePrinter.Connection;
								switch (printerConnection.CommunicationState)
								{
									case CommunicationStates.PrintingFromSd:
									case CommunicationStates.Paused when printerConnection.PrePauseCommunicationState == CommunicationStates.PrintingFromSd:
										break;

									default:
										printerConnection.Disable();
										break;
								}

								this.CloseOnIdle();
							}
						},
						message,
						caption,
						StyledMessageBox.MessageType.YES_NO);
				});
			}
			else
			{
				ApplicationController.Instance.ApplicationExiting = true;
			}
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			UserSettings.Instance.Fields.StartCountDurringExit = UserSettings.Instance.Fields.StartCount;

			if (ApplicationController.Instance.ActivePrinter.Connection.CommunicationState != CommunicationStates.PrintingFromSd)
			{
				ApplicationController.Instance.ActivePrinter.Connection.Disable();
			}
			//Close connection to the local datastore
			ApplicationController.Instance.ActivePrinter.Connection.HaltConnectionThread();
			ApplicationController.Instance.OnApplicationClosed();

			Datastore.Instance.Exit();

			base.OnClosed(e);
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			// run this first to make sure a child has the chance to take the drag drop event
			base.OnMouseMove(mouseEvent);

			if (!mouseEvent.AcceptDrop && mouseEvent.DragFiles != null)
			{
				// no child has accepted the drop
				foreach (string file in mouseEvent.DragFiles)
				{
					string extension = Path.GetExtension(file).ToUpper();
					if ((extension != "" && ApplicationSettings.ValidFileExtensions.Contains(extension))
						|| extension == ".GCODE"
						|| extension == ".ZIP")
					{
						//mouseEvent.AcceptDrop = true;
					}
				}
				dropWasOnChild = false;
			}
			else
			{
				dropWasOnChild = true;
			}

			if (GuiWidget.DebugBoundsUnderMouse)
			{
				Invalidate();
			}
		}
	}
}
