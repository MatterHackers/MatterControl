/*
Copyright (c) 2018, Lars Brubaker, John Lewin
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
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using global::MatterControl.Printing;
using Markdig.Agg;
using Markdig.Renderers.Agg;
using MatterHackers.Agg;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.DataConverters3D.UndoCommands;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.MatterControl.Extensibility;
using MatterHackers.MatterControl.Library;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.PartPreviewWindow.View3D;
using MatterHackers.MatterControl.Plugins;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrinterControls.PrinterConnections;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.SettingsManagement;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MatterControl.Tour;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.VectorMath;
using MatterHackers.VectorMath.TrackBall;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

[assembly: InternalsVisibleTo("MatterControl.Tests")]
[assembly: InternalsVisibleTo("MatterControl.AutomationTests")]
[assembly: InternalsVisibleTo("CloudServices.Tests")]

namespace MatterHackers.MatterControl
{

	public static class Application
	{
		private static ProgressBar progressBar;
		private static TextWidget statusText;
		private static FlowLayoutWidget progressPanel;
		private static string lastSection = "";
		private static Stopwatch timer;

		public static bool EnableF5Collect { get; set; }

		public static bool EnableNetworkTraffic { get; set; } = true;

		public static MiniTouchScreen MiniTouchScreen { get; set; } = new MiniTouchScreen();

		public static SystemWindow LoadRootWindow(int width, int height)
		{
			timer = Stopwatch.StartNew();

			if (false)
			{
				// set the default font
				AggContext.DefaultFont = ApplicationController.GetTypeFace(NamedTypeFace.Nunito_Regular);
				AggContext.DefaultFontBold = ApplicationController.GetTypeFace(NamedTypeFace.Nunito_Bold);
				AggContext.DefaultFontItalic = ApplicationController.GetTypeFace(NamedTypeFace.Nunito_Italic);
				AggContext.DefaultFontBoldItalic = ApplicationController.GetTypeFace(NamedTypeFace.Nunito_Bold_Italic);
			}

			var systemWindow = new RootSystemWindow(width, height);

			var overlay = new GuiWidget()
			{
				BackgroundColor = AppContext.Theme.BackgroundColor,
			};
			overlay.AnchorAll();

			systemWindow.AddChild(overlay);

			var mutedAccentColor = AppContext.Theme.SplashAccentColor;

			var spinner = new LogoSpinner(overlay, rotateX: -0.05)
			{
				MeshColor = mutedAccentColor
			};

			progressPanel = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				Position = new Vector2(0, height * .25),
				HAnchor = HAnchor.Center | HAnchor.Fit,
				VAnchor = VAnchor.Fit,
				MinimumSize = new Vector2(400, 100),
				Margin = new BorderDouble(0, 0, 0, 200)
			};
			overlay.AddChild(progressPanel);

			progressPanel.AddChild(statusText = new TextWidget("", textColor: AppContext.Theme.TextColor)
			{
				MinimumSize = new Vector2(200, 30),
				HAnchor = HAnchor.Center,
				AutoExpandBoundsToText = true
			});

			progressPanel.AddChild(progressBar = new ProgressBar()
			{
				FillColor = mutedAccentColor,
				BorderColor = Color.Gray, // theme.BorderColor75,
				Height = 11,
				Width = 230,
				HAnchor = HAnchor.Center,
				VAnchor = VAnchor.Absolute
			});

			AppContext.RootSystemWindow = systemWindow;

			// hook up a keyboard watcher to rout keys when not handled by children

			systemWindow.KeyPressed += SystemWindow_KeyPressed;

			systemWindow.KeyDown += (s, keyEvent) =>
			{
				var view3D = systemWindow.Descendants<View3DWidget>().Where((v) => v.ActuallyVisibleOnScreen()).FirstOrDefault();
				var printerTabPage = systemWindow.Descendants<PrinterTabPage>().Where((v) => v.ActuallyVisibleOnScreen()).FirstOrDefault();
				var offsetDist = 50;
				var arrowKeyOperation = keyEvent.Shift ? TrackBallTransformType.Translation : TrackBallTransformType.Rotation;

				var gcode2D = systemWindow.Descendants<GCode2DWidget>().Where((v) => v.ActuallyVisibleOnScreen()).FirstOrDefault();

				if (keyEvent.KeyCode == Keys.F1)
				{
					ApplicationController.Instance.ActivateHelpTab();
				}

				if (EnableF5Collect
					&& keyEvent.KeyCode == Keys.F5)
				{
					GC.Collect();
					systemWindow.Invalidate();
				}

				if (!keyEvent.Handled
					&& gcode2D != null)
				{
					switch (keyEvent.KeyCode)
					{
						case Keys.Oemplus:
						case Keys.Add:
							if (keyEvent.Control)
							{
								// Zoom out
								gcode2D.Zoom(1.2);
								keyEvent.Handled = true;
							}

							break;

						case Keys.OemMinus:
						case Keys.Subtract:
							if (keyEvent.Control)
							{
								// Zoom in
								gcode2D.Zoom(.8);
								keyEvent.Handled = true;
							}

							break;
					}
				}

				if (!keyEvent.Handled
					&& view3D != null)
				{
					switch (keyEvent.KeyCode)
					{
						case Keys.C:
							if (keyEvent.Control)
							{
								view3D.Scene.Copy();
								keyEvent.Handled = true;
							}

							break;

						case Keys.P:
							if (keyEvent.Control)
							{
								view3D.PushToPrinterAndPrint();
							}

							break;

						case Keys.X:
							if (keyEvent.Control)
							{
								view3D.Scene.Cut();
								keyEvent.Handled = true;
							}

							break;

						case Keys.Y:
							if (keyEvent.Control)
							{
								view3D.Scene.UndoBuffer.Redo();
								keyEvent.Handled = true;
							}

							break;

						case Keys.A:
							if (keyEvent.Control)
							{
								view3D.SelectAll();
								keyEvent.Handled = true;
							}

							break;

						case Keys.S:
							if (keyEvent.Control)
							{
								view3D.Save();
								keyEvent.Handled = true;
							}

							break;

						case Keys.V:
							if (keyEvent.Control)
							{
								view3D.sceneContext.Paste();
								keyEvent.Handled = true;
							}

							break;

						case Keys.Oemplus:
						case Keys.Add:
							if (keyEvent.Control)
							{
								// Zoom out
								Offset3DView(view3D, new Vector2(0, offsetDist), TrackBallTransformType.Scale);
								keyEvent.Handled = true;
							}

							break;

						case Keys.OemMinus:
						case Keys.Subtract:
							if (keyEvent.Control)
							{
								// Zoom in
								Offset3DView(view3D, new Vector2(0, -offsetDist), TrackBallTransformType.Scale);
								keyEvent.Handled = true;
							}

							break;

						case Keys.Z:
							if (keyEvent.Control)
							{
								if (keyEvent.Shift)
								{
									view3D.Scene.Redo();
								}
								else
								{
									// undo last operation
									view3D.Scene.Undo();
								}

								keyEvent.Handled = true;
							}

							break;

						case Keys.Insert:
							if (keyEvent.Shift)
							{
								view3D.sceneContext.Paste();
								keyEvent.Handled = true;
							}

							break;

						case Keys.Delete:
						case Keys.Back:
							view3D.Scene.DeleteSelection();
							keyEvent.Handled = true;
							keyEvent.SuppressKeyPress = true;
							break;

						case Keys.Escape:
							if (view3D.CurrentSelectInfo.DownOnPart)
							{
								view3D.CurrentSelectInfo.DownOnPart = false;

								view3D.Scene.SelectedItem.Matrix = view3D.TransformOnMouseDown;

								keyEvent.Handled = true;
								keyEvent.SuppressKeyPress = true;
							}

							foreach (var interactionVolume in view3D.InteractionLayer.InteractionVolumes)
							{
								interactionVolume.CancelOperation();
							}

							break;

						case Keys.Left:
							if (keyEvent.Control
								&& printerTabPage != null
								&& !printerTabPage.sceneContext.ViewState.ModelView)
							{
								// Decrement slider
								printerTabPage.LayerFeaturesIndex -= 1;
							}
							else
							{
								if (view3D.sceneContext.Scene.SelectedItem is IObject3D object3D)
								{
									NudgeItem(view3D, object3D, ArrowDirection.Left, keyEvent);
								}
								else
								{
									// move or rotate view left
									Offset3DView(view3D, new Vector2(-offsetDist, 0), arrowKeyOperation);
								}
							}

							keyEvent.Handled = true;
							keyEvent.SuppressKeyPress = true;
							break;

						case Keys.Right:
							if (keyEvent.Control
								&& printerTabPage != null
								&& !printerTabPage.sceneContext.ViewState.ModelView)
							{
								// Increment slider
								printerTabPage.LayerFeaturesIndex += 1;
							}
							else
							{
								if (view3D.sceneContext.Scene.SelectedItem is IObject3D object3D)
								{
									NudgeItem(view3D, object3D, ArrowDirection.Right, keyEvent);
								}
								else
								{
									// move or rotate view right
									Offset3DView(view3D, new Vector2(offsetDist, 0), arrowKeyOperation);
								}
							}

							keyEvent.Handled = true;
							keyEvent.SuppressKeyPress = true;
							break;

						case Keys.Up:
							if (view3D.Printer != null
								&& printerTabPage != null
								&& view3D.Printer.ViewState.ViewMode != PartViewMode.Model)
							{
								printerTabPage.LayerScrollbar.Value += 1;
							}
							else
							{
								if (view3D.sceneContext.Scene.SelectedItem is IObject3D object3D)
								{
									NudgeItem(view3D, object3D, ArrowDirection.Up, keyEvent);
								}
								else
								{
									Offset3DView(view3D, new Vector2(0, offsetDist), arrowKeyOperation);
								}
							}

							keyEvent.Handled = true;
							keyEvent.SuppressKeyPress = true;
							break;

						case Keys.Down:
							if (view3D.Printer != null
								&& printerTabPage != null
								&& view3D.Printer.ViewState.ViewMode != PartViewMode.Model)
							{
								printerTabPage.LayerScrollbar.Value -= 1;
							}
							else
							{
								if (view3D.sceneContext.Scene.SelectedItem is IObject3D object3D)
								{
									NudgeItem(view3D, object3D, ArrowDirection.Down, keyEvent);
								}
								else
								{
									Offset3DView(view3D, new Vector2(0, -offsetDist), arrowKeyOperation);
								}
							}

							keyEvent.Handled = true;
							keyEvent.SuppressKeyPress = true;
							break;
					}
				}
			};

			// Hook SystemWindow load and spin up MatterControl once we've hit first draw
			systemWindow.Load += (s, e) =>
			{
				// Show the End User License Agreement if it has not been shown (on windows it is shown in the installer)
				if (AggContext.OperatingSystem != OSType.Windows
					&& UserSettings.Instance.get(UserSettingsKey.SoftwareLicenseAccepted) != "true")
				{
					var eula = new LicenseAgreementPage(LoadMC)
					{
						Margin = new BorderDouble(5)
					};

					systemWindow.AddChild(eula);
				}
				else
				{
					LoadMC();
				}
			};

			void LoadMC()
			{
				ReportStartupProgress(0.02, "First draw->RunOnIdle");

				// UiThread.RunOnIdle(() =>
				Task.Run(async () =>
				{
					try
					{
						ReportStartupProgress(0.15, "MatterControlApplication.Initialize");

						ApplicationController.LoadTranslationMap();

						var mainView = await Initialize(systemWindow, (progress0To1, status) =>
						{
							ReportStartupProgress(0.2 + progress0To1 * 0.7, status);
						});

						ReportStartupProgress(0.9, "AddChild->MainView");
						systemWindow.AddChild(mainView, 0);

						ReportStartupProgress(1, "");
						systemWindow.BackgroundColor = Color.Transparent;
						overlay.Close();
					}
					catch (Exception ex)
					{
						UiThread.RunOnIdle(() =>
						{
							statusText.Visible = false;

							var errorTextColor = Color.White;

							progressPanel.Margin = 0;
							progressPanel.VAnchor = VAnchor.Center | VAnchor.Fit;
							progressPanel.BackgroundColor = Color.DarkGray;
							progressPanel.Padding = 20;
							progressPanel.Border = 1;
							progressPanel.BorderColor = Color.Red;

							var theme = new ThemeConfig();

							progressPanel.AddChild(
								new TextWidget("Startup Failure".Localize() + ":", pointSize: theme.DefaultFontSize, textColor: errorTextColor));

							progressPanel.AddChild(
								new TextWidget(ex.Message, pointSize: theme.FontSize9, textColor: errorTextColor));

							var closeButton = new TextButton("Close", theme)
							{
								BackgroundColor = theme.SlightShade,
								HAnchor = HAnchor.Right,
								VAnchor = VAnchor.Absolute
							};
							closeButton.Click += (s1, e1) =>
							{
								systemWindow.Close();
							};

							spinner.SpinLogo = false;
							progressBar.Visible = false;

							progressPanel.AddChild(closeButton);
						});
					}

					AppContext.IsLoading = false;
				});
			}

			ReportStartupProgress(0, "ShowAsSystemWindow");

			return systemWindow;
		}

		private static void SystemWindow_KeyPressed(object sender, KeyPressEventArgs keyEvent)
		{
			if (sender is SystemWindow systemWindow)
			{
				var view3D = systemWindow.Descendants<View3DWidget>().Where((v) => v.ActuallyVisibleOnScreen()).FirstOrDefault();
				var printerTabPage = systemWindow.Descendants<PrinterTabPage>().Where((v) => v.ActuallyVisibleOnScreen()).FirstOrDefault();

				if (!keyEvent.Handled
					&& view3D != null)
				{
					switch (keyEvent.KeyChar)
					{
						case 'w':
						case 'W':
							view3D.ResetView();
							keyEvent.Handled = true;
							break;

						case ' ':
							view3D.Scene.ClearSelection();
							keyEvent.Handled = true;
							break;
					}
				}
			}
		}

		private static void NudgeItem(View3DWidget view3D, IObject3D item, ArrowDirection arrowDirection, KeyEventArgs keyEvent)
		{

			var world = view3D.InteractionLayer.World;

			var vector3 = default(Vector3);

			// TODO: analyze the view and adjust movements to be relative to the current perspective
			//
			// Naive movements in Identity space
			switch (arrowDirection)
			{
				case ArrowDirection.Left:
					vector3 = new Vector3(-view3D.InteractionLayer.SnapGridDistance, 0, 0);
					break;

				case ArrowDirection.Right:
					vector3 = new Vector3(view3D.InteractionLayer.SnapGridDistance, 0, 0);
					break;

				case ArrowDirection.Up:
					if (keyEvent.Control)
					{
						vector3 = new Vector3(0, 0, view3D.InteractionLayer.SnapGridDistance);
					}
					else
					{
						vector3 = new Vector3(0, view3D.InteractionLayer.SnapGridDistance, 0);
					}

					break;

				case ArrowDirection.Down:
					if (keyEvent.Control)
					{
						vector3 = new Vector3(0, 0, -view3D.InteractionLayer.SnapGridDistance);
					}
					else
					{
						vector3 = new Vector3(0, -view3D.InteractionLayer.SnapGridDistance, 0);
					}

					break;
			}

			var matrix = world.GetXYInViewRotation(item.GetCenter());

			item.Translate(vector3.Transform(matrix));
		}

		private static void Offset3DView(View3DWidget view3D, Vector2 offset, TrackBallTransformType operation)
		{
			var center = view3D.TrackballTumbleWidget.LocalBounds.Center;

			view3D.TrackballTumbleWidget.TrackBallController.OnMouseDown(center, Matrix4X4.Identity, operation);
			view3D.TrackballTumbleWidget.TrackBallController.OnMouseMove(center + offset);
			view3D.TrackballTumbleWidget.TrackBallController.OnMouseUp();
			view3D.TrackballTumbleWidget.Invalidate();
		}

		public static async Task<GuiWidget> Initialize(SystemWindow systemWindow, Action<double, string> reporter)
		{
			var loading = "Loading...";
#if DEBUG
			loading = null;
#endif
			reporter?.Invoke(0.01, (loading != null) ? loading : "PlatformInit");
			AppContext.Platform.PlatformInit((status) =>
			{
				reporter?.Invoke(0.01, (loading != null) ? loading : status);
			});

			// TODO: Appears to be unused and should be removed
			// set this at startup so that we can tell next time if it got set to true in close
			UserSettings.Instance.Fields.StartCount = UserSettings.Instance.Fields.StartCount + 1;

			reporter?.Invoke(0.05, (loading != null) ? loading : "ApplicationController");
			var applicationController = ApplicationController.Instance;

			// Accessing any property on ProfileManager will run the static constructor and spin up the ProfileManager instance
			reporter?.Invoke(0.2, (loading != null) ? loading : "ProfileManager");
			bool na2 = ProfileManager.Instance.IsGuestProfile;

			await ProfileManager.Instance.Initialize();

			reporter?.Invoke(0.25, (loading != null) ? loading : "Initialize printer");

			reporter?.Invoke(0.3, (loading != null) ? loading : "Plugins");
			ApplicationController.Plugins.InitializePlugins(systemWindow);

			reporter?.Invoke(0.4, (loading != null) ? loading : "MainView");
			applicationController.MainView = new MainViewWidget(applicationController.Theme);

			reporter?.Invoke(0.91, (loading != null) ? loading : "OnLoadActions");
			applicationController.OnLoadActions();

			// Wired up to MainView.Load with the intent to fire startup actions and tasks in order with reporting
			async void InitialWindowLoad(object s, EventArgs e)
			{
				try
				{
					PrinterSettings.SliceEngines["MatterSlice"] = new EngineMappingsMatterSlice();

					// Initial load builds UI elements, then constructs workspace tabs as they're encountered in RestoreUserTabs()
					await applicationController.RestoreUserTabs();

					// Batch startup actions
					await applicationController.Tasks.Execute(
						"Finishing Startup".Localize(),
						null,
						(progress, cancellationToken) =>
						{
							var status = new ProgressStatus();

							int itemCount = ApplicationController.StartupActions.Count;

							double i = 1;

							foreach (var action in ApplicationController.StartupActions.OrderByDescending(t => t.Priority))
							{
								status.Status = action.Title;
								progress.Report(status);

								action.Action?.Invoke();
								status.Progress0To1 = i++ / itemCount;
								progress.Report(status);
							}

							return Task.CompletedTask;
						});

					// Batch execute startup tasks
					foreach (var task in ApplicationController.StartupTasks.OrderByDescending(t => t.Priority))
					{
						await applicationController.Tasks.Execute(task.Title, null, task.Action);
					}


					if (UserSettings.Instance.get(UserSettingsKey.ShownWelcomeMessage) != "false")
					{
						UiThread.RunOnIdle(() =>
						{
							DialogWindow.Show<WelcomePage>();
						});
					}
				}
				catch
				{
				}

				// Unhook after execution
				applicationController.MainView.Load -= InitialWindowLoad;
			}

			// Hook after first draw
			applicationController.MainView.Load += InitialWindowLoad;

			return applicationController.MainView;
		}

		private static void ReportStartupProgress(double progress0To1, string section)
		{
			UiThread.RunOnIdle(() =>
			{
				statusText.Text = section;
				progressBar.RatioComplete = progress0To1;
				progressPanel.Invalidate();

				Console.WriteLine($"Time to '{lastSection}': {timer.ElapsedMilliseconds}");
				timer.Restart();

				lastSection = section;
			});
		}
	}
}
