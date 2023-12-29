﻿/*
Copyright (c) 2019, Lars Brubaker, John Lewin
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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.Library;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.SettingsManagement;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl
{
	public class RootSystemWindow : SystemWindow
	{
		public static bool UseGl { get; set; } = true;

		private static Vector2 minSize { get; set; } = new Vector2(600, 480);

		private Stopwatch totalDrawTime = new Stopwatch();

		private AverageMillisecondTimer millisecondTimer = new AverageMillisecondTimer();

		public static bool ShowMemoryUsed = false;

		private int drawCount = 0;

		private bool exitDialogOpen = false;

		public RootSystemWindow(double width, double height)
			: base(width, height)
		{
			this.Name = "MatterControl";
			this.Padding = new BorderDouble(0); // To be re-enabled once native borders are turned off
			this.AnchorAll();

			GuiWidget.DefaultEnforceIntegerBounds = true;

			// TODO: Needs review - doesn't seem like we want to scale on Touchscreen, rather we want device specific, configuration based scaling. Suggest remove
			if (GuiWidget.TouchScreenMode)
			{
				// TODO: This steps on user scaling
				GuiWidget.DeviceScale = 1.3;
				SystemWindow.ShareSingleOsWindow = true;
			}

			string textSizeMode = UserSettings.Instance.get(UserSettingsKey.ApplicationTextSize);
			if (!string.IsNullOrEmpty(textSizeMode))
			{
				if (double.TryParse(textSizeMode, out double textSize))
				{
					GuiWidget.DeviceScale = textSize;
				}
			}

			UseOpenGL = UseGl;

			this.SetStartupTraits();
		}

		public void SetStartupTraits()
		{
			string version = "2.0";

			this.MinimumSize = minSize;

			this.Title = $"{ApplicationController.Instance.ProductName} {version}";
			if (OemSettings.Instance.WindowTitleExtra != null && OemSettings.Instance.WindowTitleExtra.Trim().Length > 0)
			{
				this.Title += $" - {OemSettings.Instance.WindowTitleExtra}";
			}

			this.Title += string.Format(" - {0}Bit", IntPtr.Size == 4 ? 32 : 64);

			string desktopPosition = ApplicationSettings.Instance.get(ApplicationSettingsKey.DesktopPosition);
			if (!string.IsNullOrEmpty(desktopPosition))
			{
				string[] sizes = desktopPosition.Split(',');

				// If the desktop position is less than -10,-10, override
				int xpos = Math.Max(int.Parse(sizes[0]), -10);
				int ypos = Math.Max(int.Parse(sizes[1]), -10);

				this.DesktopPosition = new Point2D(xpos, ypos);
			}
			else
			{
				this.DesktopPosition = new Point2D(-1, -1);
			}

			this.Maximized = ApplicationSettings.Instance.get(ApplicationSettingsKey.MainWindowMaximized) == "true";
		}

		public static (int width, int height) GetStartupBounds(int overrideWidth = -1, int overrideHeight = -1)
		{
			int width = 0;
			int height = 0;
			if (GuiWidget.TouchScreenMode)
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
				this.Title = $"Allocated = {memory:n0} : {millisecondTimer.GetAverage()}ms, d{drawCount++} Size = {this.Width}x{this.Height}, onIdle = {UiThread.CountExpired}:{UiThread.Count}, widgetsDrawn = {GuiWidget.DrawCount}";
			}

			// msGraph.AddData("ms", totalDrawTime.ElapsedMilliseconds);
			// msGraph.Draw(MatterHackers.Agg.Transform.Affine.NewIdentity(), graphics2D);
		}

		public override void OnShouldClose(ShouldCloseEventArgs eventArgs)
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

			// If we are waiting for a response and get another request, just cancel the close until we get a response.
			if (exitDialogOpen)
			{
				eventArgs.Cancel = true;
			}

			string caption = null;
			string message = null;

			if (!ApplicationController.Instance.ApplicationExiting
				&& !exitDialogOpen)
			{
				int printingCount = 0;
				int sdPrinting = 0;
				foreach (var printer in ApplicationController.Instance.ActivePrinters)
				{
					if (printer.Connection.Printing)
					{
						if (printer.Connection.CommunicationState == CommunicationStates.PrintingFromSd)
						{
							sdPrinting++;
						}

						printingCount++;
					}
				}

				if (sdPrinting > 0)
				{
					caption = "Exit while printing".Localize();
					message = "Are you sure you want to exit while a print is running from SD Card?\n\nNote: If you exit, it is recommended you wait until the print is completed before running MatterControl again.".Localize();
				}
				else if (printingCount > 0)
				{
					caption = "Abort Print".Localize();
					message = "Are you sure you want to abort the current print(s) and close MatterControl?".Localize();
				}
			}

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

								foreach (var printer in ApplicationController.Instance.ActivePrinters)
								{
									// the will shutdown any active (and non-sd) prints that are running
									printer.Connection.Disable();
								}

								this.CloseOnIdle();
							}
						},
						message,
						caption,
						StyledMessageBox.MessageType.YES_NO_WITHOUT_HIGHLIGHT);
				});
			}
			else if (!ApplicationController.Instance.ApplicationExiting)
			{
				// Check if there are unsaved design spaces
				var unsavedWorkspaces = new List<PartWorkspace>();
				foreach (var workspace in ApplicationController.Instance.Workspaces)
				{
					if (workspace.SceneContext?.Scene?.HasUnsavedChanges == true
						&& workspace.Printer == null)
                    {
						unsavedWorkspaces.Add(workspace);
					}
				}

				void SavePrinterWorkspacesAndClose()
                {
					// cancel the close so that we can save all our active work spaces
					eventArgs.Cancel = true;

					UiThread.RunOnIdle(async () =>
					{
						var application = ApplicationController.Instance;

						ApplicationController.Instance.PersistOpenTabsLayout();
						await ApplicationController.Instance.PersistPrintTabsContent();

						application.ApplicationExiting = true;

						// Make sure we tell the Application Controller to shut down. This will release the slicing thread if running.
						application.Shutdown();

						this.CloseOnIdle();
					});
				}

				if (unsavedWorkspaces.Count > 0)
				{
					exitDialogOpen = true;

					// We need to show an interactive dialog to determine if the original Close request should be honored, thus cancel the current Close request
					eventArgs.Cancel = true;

					// Ask if the user wants to save all workspaces, close without save, or cancel.
					UiThread.RunOnIdle(() =>
					{
						StyledMessageBox.ShowYNCMessageBox(
							(response) =>
							{
								exitDialogOpen = false;

								switch (response)
								{
									case StyledMessageBox.ResponseType.YES:
										UiThread.RunOnIdle(async () =>
										{
											var hadSaveError = false;
											// Persist each design workspaces to disk (this may require getting a filename
											foreach (var workspace in ApplicationController.Instance.Workspaces.ToArray())
											{
												if (workspace.Printer == null)
												{
													var sceneContext = workspace.SceneContext;
													if (sceneContext.EditContext.ContentStore == null)
													{
														hadSaveError = true;
														// If we are about to close a tab that has never been saved it will need a name before it can actually save
														// Open up the save as dialog rather than continue with saving and closing
														DialogWindow.Show(
															new SaveAsPage(
																(container, newName) =>
																{
																	sceneContext.SaveAs(container, newName);
																}));
													}
													else
													{
														await ApplicationController.Instance.Tasks.Execute("Saving".Localize() + $" \"{workspace.Name}\" ...", workspace, workspace.SceneContext.SaveChanges);
														// check for error or abort
														hadSaveError |= workspace.SceneContext.HadSaveError;
													}
												}
											}

											if (!hadSaveError)
											{
												// make sure we also save the printer workspaces as we close
												SavePrinterWorkspacesAndClose();
											}
										});
										break;

									case StyledMessageBox.ResponseType.NO:
										UiThread.RunOnIdle(() =>
										{
											// make sure we still save the printer workspaces
											SavePrinterWorkspacesAndClose();
										});
										break;
								}
							},
							unsavedWorkspaces.Count == 1 ? "You have one unsave design ({0}). Wolud you like to save changes before closing?".Localize().FormatWith(unsavedWorkspaces[0].Name)
							: "You have {0} unsave designs. Wolud you like to save changes before closing?".Localize().FormatWith(unsavedWorkspaces.Count),
							"Save Changes?".Localize(),
							"Save Changes".Localize(),
							"Discard Changes".Localize(),
							"Cancel".Localize());
					});
				}
				else
				{
					SavePrinterWorkspacesAndClose();
				}
			}
		}

		public override void OnClosed(EventArgs e)
		{
			UserSettings.Instance.Fields.StartCountDurringExit = UserSettings.Instance.Fields.StartCount;

			foreach (var printer in ApplicationController.Instance.ActivePrinters)
			{
				printer.Connection.Disable();

				// Close connection to the local datastore
				printer.Connection.HaltConnectionThread();
			}

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
						// mouseEvent.AcceptDrop = true;
					}
				}
			}

			if (GuiWidget.DebugBoundsUnderMouse)
			{
				Invalidate();
			}
		}
	}
}
