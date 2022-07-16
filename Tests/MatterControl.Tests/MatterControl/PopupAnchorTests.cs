using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.GuiAutomation;
using MatterHackers.MatterControl;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.Tests.Automation;
using MatterHackers.VectorMath;
using NUnit.Framework;
using TestInvoker;

namespace MatterControl.Tests.MatterControl
{
	// NOTE: These tests hang on GLFW currently as the window isn't closed properly.
	[TestFixture, Category("PopupAnchorTests"), Parallelizable(ParallelScope.Children)]
	public class PopupAnchorTests
	{
		[SetUp]
		public void TestSetup()
		{
			StaticData.RootPath = MatterControlUtilities.StaticDataPath;
			MatterControlUtilities.OverrideAppDataLocation(MatterControlUtilities.RootPath);
		}

		[Test, ChildProcessTest]
		public async Task WindowTest()
		{
			var systemWindow = new PopupsTestWindow(700, 300)
			{
				Details = new Dictionary<string, string>()
				{
					["Task"] = "General Popup",
					["Expected"] = "Popup should appear on click"
				}
			};

			await systemWindow.RunTest(testRunner =>
			{
				systemWindow.Padding = systemWindow.Padding.Clone(bottom: 180);

				var button = new TextButton("Popup", systemWindow.Theme)
				{
					Name = "targetA",
					VAnchor = VAnchor.Bottom,
					HAnchor = HAnchor.Left,
				};
				systemWindow.AddChild(button);

				var color = Color.LightBlue;

				button.Click += (s, e) =>
				{
					systemWindow.ShowPopup(
                        new ThemeConfig(),
						new MatePoint()
						{
							Widget = button
						},
						new MatePoint()
						{
							Widget = new GuiWidget(180d, 100d)
							{
								BackgroundColor = color,
								Border = 2,
								BorderColor = color.Blend(Color.Black, 0.4)
							}
						});
				};

				testRunner.ClickByName("targetA");

				testRunner.Delay();

				return Task.CompletedTask;
			}, 30);
		}

		[Test, ChildProcessTest]
		public async Task TopBottomPopupTest()
		{
			var systemWindow = new PopupsTestWindow(800, 600)
			{
				Details = new Dictionary<string, string>()
				{
					["Task"] = "Top-Bottom Tests",
					["Expected"] = "Popup bottoms should align with button top"
				}
			};

			await AnchorTests(
				systemWindow,
				new MatePoint()
				{
					Mate = new MateOptions(MateEdge.Left, MateEdge.Top)
				},
				new MatePoint()
				{
					Mate = new MateOptions(MateEdge.Left, MateEdge.Bottom)
				},
				new TextButton("Popup", systemWindow.Theme)
				{
					Name = "buttonA",
					VAnchor = VAnchor.Bottom,
				},
				(buttonWidget, popupWidget) =>
				{
					double buttonPosition = buttonWidget.TransformToScreenSpace(buttonWidget.LocalBounds).Top;
					double popupPosition = popupWidget.TransformToScreenSpace(popupWidget.LocalBounds).Bottom;

					Assert.AreEqual(buttonPosition, popupPosition);
				});
		}

		[Test, ChildProcessTest]
		public async Task TopTopPopupTest()
		{
			var systemWindow = new PopupsTestWindow(800, 600)
			{
				Details = new Dictionary<string, string>()
				{
					["Task"] = "Top-Bottom Tests",
					["Expected"] = "Popup tops should align with button top"
				}
			};

			await AnchorTests(
				systemWindow,
				new MatePoint()
				{
					Mate = new MateOptions(MateEdge.Left, MateEdge.Top)
				},
				new MatePoint()
				{
					Mate = new MateOptions(MateEdge.Left, MateEdge.Top)
				},
				new TextButton("Popup", systemWindow.Theme)
				{
					Name = "buttonA",
					VAnchor = VAnchor.Bottom,
				},
				(buttonWidget, popupWidget) =>
				{
					double buttonPosition = buttonWidget.TransformToScreenSpace(buttonWidget.LocalBounds).Top;
					double popupPosition = popupWidget.TransformToScreenSpace(popupWidget.LocalBounds).Top;

					Assert.AreEqual(buttonPosition, popupPosition);
				});
		}

		[Test, ChildProcessTest]
		public async Task BottomTopPopupTest()
		{
			var systemWindow = new PopupsTestWindow(800, 600)
			{
				Details = new Dictionary<string, string>()
				{
					["Task"] = "Top-Bottom Tests",
					["Expected"] = "Popup tops should align with button bottom"
				}
			};

			await AnchorTests(
				systemWindow,
				new MatePoint()
				{
					Mate = new MateOptions(MateEdge.Right, MateEdge.Bottom)
				},
				new MatePoint()
				{
					Mate = new MateOptions(MateEdge.Left, MateEdge.Top)
				},
				new TextButton("Popup", systemWindow.Theme)
				{
					Name = "buttonA",
					VAnchor = VAnchor.Bottom,
				},
				(buttonWidget, popupWidget) =>
				{
					double buttonPosition = buttonWidget.TransformToScreenSpace(buttonWidget.LocalBounds).Bottom;
					double popupPosition = popupWidget.TransformToScreenSpace(popupWidget.LocalBounds).Top;

					Assert.AreEqual(buttonPosition, popupPosition);
				});
		}

		[Test, ChildProcessTest]
		public async Task BottomBottomPopupTest()
		{
			var systemWindow = new PopupsTestWindow(800, 600)
			{
				Details = new Dictionary<string, string>()
				{
					["Task"] = "Bottom-Bottom Tests",
					["Expected"] = "Popup bottoms should align with button bottom"
				}
			};

			await AnchorTests(
				systemWindow,
				new MatePoint()
				{
					Mate = new MateOptions(MateEdge.Left, MateEdge.Bottom)
				},
				new MatePoint()
				{
					Mate = new MateOptions(MateEdge.Left, MateEdge.Bottom)
				},
				new TextButton("Popup", systemWindow.Theme)
				{
					Name = "buttonA",
					VAnchor = VAnchor.Bottom,
				},
				(buttonWidget, popupWidget) =>
				{
					double buttonPosition = buttonWidget.TransformToScreenSpace(buttonWidget.LocalBounds).Bottom;
					double popupPosition = popupWidget.TransformToScreenSpace(popupWidget.LocalBounds).Bottom;

					Assert.AreEqual(buttonPosition, popupPosition);
				});
		}

		// Redirect down to up
		[Test, ChildProcessTest]
		public async Task BottomTopUpRedirectTest()
		{
			var systemWindow = new PopupsTestWindow(800, 600)
			{
				Details = new Dictionary<string, string>()
				{
					["Task"] = "Top-Bottom Tests",
					["Expected"] = "Popup tops should align with button bottom"
				}
			};

			await AnchorTests(
				systemWindow,
				new MatePoint()
				{
					Mate = new MateOptions(MateEdge.Left, MateEdge.Bottom),
					AltMate = new MateOptions(MateEdge.Left, MateEdge.Top)
				},
				new MatePoint()
				{
					Mate = new MateOptions(MateEdge.Left, MateEdge.Top),
					AltMate = new MateOptions(MateEdge.Left, MateEdge.Bottom)
				},
				new TextButton("Popup", systemWindow.Theme)
				{
					Name = "buttonA",
					VAnchor = VAnchor.Bottom,
				},
				(buttonWidget, popupWidget) =>
				{
					double buttonPosition = buttonWidget.TransformToScreenSpace(buttonWidget.LocalBounds).Top;
					double popupPosition = popupWidget.TransformToScreenSpace(popupWidget.LocalBounds).Bottom;

					Assert.AreEqual(buttonPosition, popupPosition);
				},
				(row) =>
				{
					row.VAnchor = VAnchor.Bottom | VAnchor.Fit;
				});
		}

		[Test, ChildProcessTest]
		public async Task TopTopUpRedirectTest()
		{
			var systemWindow = new PopupsTestWindow(800, 600)
			{
				Details = new Dictionary<string, string>()
				{
					["Task"] = "Top-Bottom Tests",
					["Expected"] = "Popup tops should align with button top"
				}
			};

			await AnchorTests(
				systemWindow,
				new MatePoint()
				{
					Mate = new MateOptions(MateEdge.Left, MateEdge.Top),
					AltMate = new MateOptions(MateEdge.Left, MateEdge.Bottom)
				},
				new MatePoint()
				{
					Mate = new MateOptions(MateEdge.Left, MateEdge.Top),
					AltMate = new MateOptions(MateEdge.Left, MateEdge.Bottom)
				},
				new TextButton("Popup", systemWindow.Theme)
				{
					Name = "buttonA",
					VAnchor = VAnchor.Bottom,
				},
				(buttonWidget, popupWidget) =>
				{
					double buttonPosition = buttonWidget.TransformToScreenSpace(buttonWidget.LocalBounds).Bottom;
					double popupPosition = popupWidget.TransformToScreenSpace(popupWidget.LocalBounds).Bottom;

					Assert.AreEqual(buttonPosition, popupPosition);
				},
				(row) =>
				{
					row.VAnchor = VAnchor.Bottom | VAnchor.Fit;
				});
		}


		// Redirect up to down
		[Test, ChildProcessTest]
		public async Task BottomTopDownRedirectTest()
		{
			var systemWindow = new PopupsTestWindow(800, 600)
			{
				Details = new Dictionary<string, string>()
				{
					["Task"] = "Top-Bottom Tests",
					["Expected"] = "Popup bottoms should align with button top"
				}
			};

			await AnchorTests(
				systemWindow,
				new MatePoint()
				{
					Mate = new MateOptions(MateEdge.Left, MateEdge.Top),
					AltMate = new MateOptions(MateEdge.Left, MateEdge.Bottom),
				},
				new MatePoint()
				{
					Mate = new MateOptions(MateEdge.Left, MateEdge.Bottom),
					AltMate = new MateOptions(MateEdge.Left, MateEdge.Top),
				},
				new TextButton("Popup", systemWindow.Theme)
				{
					Name = "buttonA",
					VAnchor = VAnchor.Bottom,
				},
				(buttonWidget, popupWidget) =>
				{
					double buttonPosition = buttonWidget.TransformToScreenSpace(buttonWidget.LocalBounds).Bottom;
					double popupPosition = popupWidget.TransformToScreenSpace(popupWidget.LocalBounds).Top;

					Assert.AreEqual(buttonPosition, popupPosition);
				},
				(row) =>
				{
					row.VAnchor = VAnchor.Top | VAnchor.Fit;
				});
		}

		[Test, ChildProcessTest]
		public async Task TopTopDownRedirectTest()
		{
			var systemWindow = new PopupsTestWindow(800, 600)
			{
				Details = new Dictionary<string, string>()
				{
					["Task"] = "Bottom-Bottom Tests",
					["Expected"] = "Popup bottoms should align with button bottom"
				}
			};

			await AnchorTests(
				systemWindow,
				new MatePoint()
				{
					Mate = new MateOptions(MateEdge.Left, MateEdge.Bottom),
					AltMate = new MateOptions(MateEdge.Right, MateEdge.Top),
				},
				new MatePoint()
				{
					Mate = new MateOptions(MateEdge.Left, MateEdge.Bottom),
					AltMate = new MateOptions(MateEdge.Left, MateEdge.Top),
				},
				new TextButton("Popup", systemWindow.Theme)
				{
					Name = "buttonA",
					VAnchor = VAnchor.Bottom,
				},
				(buttonWidget, popupWidget) =>
				{
					double buttonPosition = buttonWidget.TransformToScreenSpace(buttonWidget.LocalBounds).Top;
					double popupPosition = popupWidget.TransformToScreenSpace(popupWidget.LocalBounds).Top;

					Assert.AreEqual(buttonPosition, popupPosition);
				},
				(row) =>
				{
					row.VAnchor = VAnchor.Top | VAnchor.Fit;
				});
		}

		// Redirect left to right
		[Test, ChildProcessTest]
		public async Task LeftRightRedirectTest()
		{
			var systemWindow = new PopupsTestWindow(800, 600)
			{
				Details = new Dictionary<string, string>()
				{
					["Task"] = "Top-Bottom Tests",
					["Expected"] = "Popup tops should align with button bottom"
				}
			};

			systemWindow.Padding = systemWindow.Padding.Clone(left: 0);

			int i = 0;

			await AnchorTests(
				systemWindow,
				new MatePoint()
				{
					Mate = new MateOptions(MateEdge.Left, MateEdge.Bottom),
					AltMate = new MateOptions(MateEdge.Left, MateEdge.Top)
				},
				new MatePoint()
				{
					Mate = new MateOptions(MateEdge.Left, MateEdge.Top),
					AltMate = new MateOptions(MateEdge.Left, MateEdge.Bottom)
				},
				new TextButton("Popup", systemWindow.Theme)
				{
					Name = "buttonA",
					VAnchor = VAnchor.Bottom,
				},
				(buttonWidget, popupWidget) =>
				{
					double buttonPosition = buttonWidget.TransformToScreenSpace(buttonWidget.LocalBounds).Left;
					double popupPosition = popupWidget.TransformToScreenSpace(popupWidget.LocalBounds).Left;

					if (i++ > 2)
					{
						// Switch to anchor right aligned for the last case
						buttonPosition = buttonWidget.TransformToScreenSpace(buttonWidget.LocalBounds).Right;
					}

					Assert.AreEqual(buttonPosition, popupPosition);
				},
				(row) =>
				{
					// Clear left margin so menus clip
					row.Margin = 2;
				});
		}

		// Redirect right to left
		[Test, ChildProcessTest]
		public async Task RightLeftRedirectTest()
		{
			var systemWindow = new PopupsTestWindow(800, 600)
			{
				Details = new Dictionary<string, string>()
				{
					["Task"] = "Bottom-Bottom Tests",
					["Expected"] = "Popup bottoms should align with button bottom"
				}
			};

			int i = 0;

			await AnchorTests(
				systemWindow,
				new MatePoint()
				{
					Mate = new MateOptions(MateEdge.Left, MateEdge.Bottom),
					AltMate = new MateOptions(MateEdge.Left, MateEdge.Top),
				},
				new MatePoint()
				{
					Mate = new MateOptions(MateEdge.Left, MateEdge.Bottom),
					AltMate = new MateOptions(MateEdge.Right, MateEdge.Top),
				},
				new TextButton("Popup", systemWindow.Theme)
				{
					Name = "buttonA",
					VAnchor = VAnchor.Bottom,
				},
				(buttonWidget, popupWidget) =>
				{
					double buttonPosition = buttonWidget.TransformToScreenSpace(buttonWidget.LocalBounds).Left;
					double popupPosition = popupWidget.TransformToScreenSpace(popupWidget.LocalBounds).Right;

					if (i++ == 2)
					{
						// Switch to anchor right aligned for the last case
						buttonPosition = buttonWidget.TransformToScreenSpace(buttonWidget.LocalBounds).Right;
					}

					Assert.AreEqual(buttonPosition, popupPosition);
				},
				(row) =>
				{
					row.HAnchor = HAnchor.Right | HAnchor.Fit;
					row.VAnchor = VAnchor.Center | VAnchor.Fit;
				});
		}


		private static async Task AnchorTests(PopupsTestWindow systemWindow, MatePoint anchor, MatePoint popup, TextButton button, Action<GuiWidget, GuiWidget> validator, Action<GuiWidget> rowAdjuster = null)
		{
			await systemWindow.RunTest(testRunner =>
			{
				button.BackgroundColor = Color.LightGray;
				button.HoverColor = Color.LightBlue;
				button.MouseDownColor = Color.Magenta;

				var row = new FlowLayoutWidget()
				{
					VAnchor = VAnchor.Center | VAnchor.Fit,
					HAnchor = HAnchor.Left | HAnchor.Fit,
					Margin = new BorderDouble(left: 120)
				};
				systemWindow.AddChild(row);

				row.AddChild(button);

				rowAdjuster?.Invoke(row);

				button.Click += (s, e) =>
				{
					popup.Widget = new GuiWidget(180d, 100d)
					{
						BackgroundColor = Color.LightBlue,
						Border = 2,
						BorderColor = Color.LightBlue.Blend(Color.Black, 0.4)
					};

					systemWindow.ShowPopup(new ThemeConfig(), anchor, popup);
				};

				anchor.Widget = button;

				for (var i = 0; i < 4; i++)
				{
					switch (i)
					{
						case 0:
							anchor.Mate.HorizontalEdge = MateEdge.Left;
							popup.Mate.HorizontalEdge = MateEdge.Right;
							break;

						case 1:
							anchor.Mate.HorizontalEdge = MateEdge.Left;
							popup.Mate.HorizontalEdge = MateEdge.Left;
							break;

						case 2:
							anchor.Mate.HorizontalEdge = MateEdge.Right;
							popup.Mate.HorizontalEdge = MateEdge.Right;
							break;

						case 3:
							anchor.Mate.HorizontalEdge = MateEdge.Right;
							popup.Mate.HorizontalEdge = MateEdge.Left;
							break;
					}

					testRunner.ClickByName("buttonA");
					testRunner.Delay();

					validator.Invoke(button, popup.Widget);

					popup.Widget.Unfocus();
				}

				testRunner.Delay();

				return Task.CompletedTask;
			}, 25);
		}

		public class PopupsTestWindow : SystemWindow
		{
			private FlowLayoutWidget column;

			public ThemeConfig Theme { get; }

			public PopupsTestWindow(int width, int height)
				: base(width, height)
			{
				this.BackgroundColor = new Color(56, 56, 56);

				Theme = new ThemeConfig();

				this.Padding = new BorderDouble(left: 120, bottom: 10, right: 10, top: 10);

				column = new FlowLayoutWidget(FlowDirection.TopToBottom)
				{
					HAnchor = HAnchor.Right | HAnchor.Fit,
					VAnchor = VAnchor.Stretch,
				};
				this.AddChild(column);
			}

			private Dictionary<string, string> _details;

			public Dictionary<string, string> Details
			{
				get => _details;
				set
				{
					_details = value;

					foreach (var kvp in value)
					{
						this.ShowDetails(kvp.Key, kvp.Value);
					}
				}
			}

			public void ShowDetails(string heading, string text)
			{
				// Store
				var row = new FlowLayoutWidget
				{
					VAnchor = VAnchor.Fit,
					HAnchor = HAnchor.Left | HAnchor.Fit
				};
				column.AddChild(row);

				row.AddChild(new TextWidget(heading + ":", textColor: Color.White, pointSize: 9)
				{
					MinimumSize = new Vector2(80, 0)
				});

				row.AddChild(new TextWidget(text, textColor: Color.White, pointSize: 9));
			}
		}
	}
}
