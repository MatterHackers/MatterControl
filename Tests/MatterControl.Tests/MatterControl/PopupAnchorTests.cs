using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.Tests.Automation;
using MatterHackers.VectorMath;
using NUnit.Framework;

namespace MatterControl.Tests.MatterControl
{
	[TestFixture, Category("PopupAnchorTests"), RunInApplicationDomain, Apartment(ApartmentState.STA)]
	public class PopupAnchorTests
	{
		[SetUp]
		public void TestSetup()
		{
			AggContext.StaticData = new FileSystemStaticData(TestContext.CurrentContext.ResolveProjectPath(4, "StaticData"));
			MatterControlUtilities.OverrideAppDataLocation(TestContext.CurrentContext.ResolveProjectPath(4));
		}

		[Test]
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

		[Test]
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

		[Test]
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

		[Test]
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

		[Test]
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
		[Test]
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

		[Test]
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
		[Test]
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

		[Test]
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
		[Test]
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
		[Test]
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

		private static Task AnchorTests(PopupsTestWindow systemWindow, MatePoint anchor, MatePoint popup, TextButton button, Action<GuiWidget, GuiWidget> validator, Action<GuiWidget> rowAdjuster = null)
		{
			return systemWindow.RunTest(testRunner =>
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

					systemWindow.ShowPopup(anchor, popup);
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

		[Test, Ignore("Thorough but too long")]
		public async Task WindowTest3()
		{
			string targetWidget = "targetA";

			var systemWindow = new PopupsTestWindow(800, 600)
			{
				Details = new Dictionary<string, string>()
				{
					["Task"] = "Top-Left Anchor : Bottom-Left Popup",
					["Expected"] = "Popup should appear above left of anchor click"
				}
			};

			systemWindow.Padding = systemWindow.Padding.Clone(bottom: 180);

			await systemWindow.RunTest(testRunner =>
			{
				var row = new FlowLayoutWidget()
				{
					VAnchor = VAnchor.Top | VAnchor.Fit,
					HAnchor = HAnchor.Left | HAnchor.Fit,
					Margin = new BorderDouble(left: 120)
				};
				systemWindow.AddChild(row);

				var button = new TextButton("Popup", systemWindow.Theme)
				{
					Name = "targetA",
					VAnchor = VAnchor.Bottom,
				};
				row.AddChild(button);

				var spacer = new GuiWidget()
				{
					Width = 100,
				};
				row.AddChild(spacer);

				var button2 = new GuiWidget(140, 140)
				{
					BackgroundColor = Color.Blue,
					Name = "targetB"
				};

				var hitBounds = new RectangleDouble(65, 45, 65 + 32, 45 + 32);

				button2.AfterDraw += (s, e) =>
				{
					e.Graphics2D.Rectangle(hitBounds, Color.White);
				};
				row.AddChild(button2);

				var anchor = new MatePoint()
				{
					Mate = new MateOptions()
					{
						HorizontalEdge = MateEdge.Right,
						VerticalEdge = MateEdge.Bottom,
					},
					Widget = button,
				};

				var popup = new MatePoint()
				{
					Mate = new MateOptions()
					{
						HorizontalEdge = MateEdge.Left,
						VerticalEdge = MateEdge.Top,
					}
				};

				button.Click += (s, e) =>
				{
					popup.Widget = new GuiWidget(180d, 100d)
					{
						BackgroundColor = Color.LightBlue,
						Border = 2,
						BorderColor = Color.LightBlue.Blend(Color.Black, 0.4)
					};

					systemWindow.ShowPopup(anchor, popup);
				};

				button2.Click += (s, e) =>
				{
					popup.Widget = new GuiWidget(180d, 100d)
					{
						BackgroundColor = Color.LightBlue,
						Border = 2,
						BorderColor = Color.LightBlue.Blend(Color.Black, 0.4)
					};

					systemWindow.ShowPopup(anchor, popup, hitBounds);
				};

				bool firstPass = true;

				for (var i = 0; i < 16; i++)
				{
					switch (i)
					{
						// Bottom-Top positions
						case 0:
							anchor.Mate.HorizontalEdge = MateEdge.Left;
							popup.Mate.HorizontalEdge = MateEdge.Right;

							anchor.Mate.VerticalEdge = MateEdge.Top;
							popup.Mate.VerticalEdge = MateEdge.Bottom;

							anchor.AltMate.VerticalEdge = MateEdge.Bottom;
							popup.AltMate.VerticalEdge = MateEdge.Top;

							break;
						case 1:
							anchor.Mate.HorizontalEdge = MateEdge.Left;
							popup.Mate.HorizontalEdge = MateEdge.Left;

							anchor.Mate.VerticalEdge = MateEdge.Top;
							popup.Mate.VerticalEdge = MateEdge.Bottom;

							anchor.AltMate.VerticalEdge = MateEdge.Bottom;
							popup.AltMate.VerticalEdge = MateEdge.Top;
							break;

						case 2:
							anchor.Mate.HorizontalEdge = MateEdge.Right;
							popup.Mate.HorizontalEdge = MateEdge.Right;

							anchor.Mate.VerticalEdge = MateEdge.Top;
							popup.Mate.VerticalEdge = MateEdge.Bottom;

							anchor.AltMate.VerticalEdge = MateEdge.Bottom;
							popup.AltMate.VerticalEdge = MateEdge.Top;
							break;

						case 3:
							anchor.Mate.HorizontalEdge = MateEdge.Right;
							popup.Mate.HorizontalEdge = MateEdge.Left;

							anchor.Mate.VerticalEdge = MateEdge.Top;
							popup.Mate.VerticalEdge = MateEdge.Bottom;

							anchor.AltMate.VerticalEdge = MateEdge.Bottom;
							popup.AltMate.VerticalEdge = MateEdge.Top;
							break;

						// Top-Top positions
						case 4:
							anchor.Mate.HorizontalEdge = MateEdge.Left;
							popup.Mate.HorizontalEdge = MateEdge.Right;

							anchor.Mate.VerticalEdge = MateEdge.Top;
							popup.Mate.VerticalEdge = MateEdge.Top;

							anchor.AltMate.VerticalEdge = MateEdge.Bottom;
							popup.AltMate.VerticalEdge = MateEdge.Bottom;
							break;

						case 5:
							anchor.Mate.HorizontalEdge = MateEdge.Left;
							popup.Mate.HorizontalEdge = MateEdge.Left;

							anchor.Mate.VerticalEdge = MateEdge.Top;
							popup.Mate.VerticalEdge = MateEdge.Top;

							anchor.AltMate.VerticalEdge = MateEdge.Bottom;
							popup.AltMate.VerticalEdge = MateEdge.Bottom;
							break;

						case 6:
							anchor.Mate.HorizontalEdge = MateEdge.Right;
							popup.Mate.HorizontalEdge = MateEdge.Right;

							anchor.Mate.VerticalEdge = MateEdge.Top;
							popup.Mate.VerticalEdge = MateEdge.Top;

							anchor.AltMate.VerticalEdge = MateEdge.Bottom;
							popup.AltMate.VerticalEdge = MateEdge.Bottom;
							break;

						case 7:
							anchor.Mate.HorizontalEdge = MateEdge.Right;
							popup.Mate.HorizontalEdge = MateEdge.Left;

							anchor.Mate.VerticalEdge = MateEdge.Top;
							popup.Mate.VerticalEdge = MateEdge.Top;

							anchor.AltMate.VerticalEdge = MateEdge.Bottom;
							popup.AltMate.VerticalEdge = MateEdge.Bottom;
							break;

						// Bottom-Bottom positions
						case 8:
							anchor.Mate.HorizontalEdge = MateEdge.Left;
							popup.Mate.HorizontalEdge = MateEdge.Right;

							anchor.Mate.VerticalEdge = MateEdge.Bottom;
							popup.Mate.VerticalEdge = MateEdge.Bottom;

							anchor.AltMate.VerticalEdge = MateEdge.Top;
							popup.AltMate.VerticalEdge = MateEdge.Bottom;
							break;

						case 9:
							anchor.Mate.HorizontalEdge = MateEdge.Left;
							popup.Mate.HorizontalEdge = MateEdge.Left;

							anchor.Mate.VerticalEdge = MateEdge.Bottom;
							popup.Mate.VerticalEdge = MateEdge.Bottom;

							anchor.AltMate.VerticalEdge = MateEdge.Top;
							popup.AltMate.VerticalEdge = MateEdge.Bottom;
							break;

						case 10:
							anchor.Mate.HorizontalEdge = MateEdge.Right;
							popup.Mate.HorizontalEdge = MateEdge.Right;

							anchor.Mate.VerticalEdge = MateEdge.Bottom;
							popup.Mate.VerticalEdge = MateEdge.Bottom;

							anchor.AltMate.VerticalEdge = MateEdge.Top;
							popup.AltMate.VerticalEdge = MateEdge.Bottom;
							break;

						case 11:
							anchor.Mate.HorizontalEdge = MateEdge.Right;
							popup.Mate.HorizontalEdge = MateEdge.Left;

							anchor.Mate.VerticalEdge = MateEdge.Bottom;
							popup.Mate.VerticalEdge = MateEdge.Bottom;

							anchor.AltMate.VerticalEdge = MateEdge.Top;
							popup.AltMate.VerticalEdge = MateEdge.Bottom;
							break;

						// Bottom-Top positions
						case 12:
							anchor.Mate.HorizontalEdge = MateEdge.Left;
							popup.Mate.HorizontalEdge = MateEdge.Right;

							anchor.Mate.VerticalEdge = MateEdge.Bottom;
							popup.Mate.VerticalEdge = MateEdge.Top;

							anchor.AltMate.VerticalEdge = MateEdge.Top;
							popup.AltMate.VerticalEdge = MateEdge.Bottom;
							break;

						case 13:
							anchor.Mate.HorizontalEdge = MateEdge.Left;
							popup.Mate.HorizontalEdge = MateEdge.Left;

							anchor.Mate.VerticalEdge = MateEdge.Bottom;
							popup.Mate.VerticalEdge = MateEdge.Top;

							anchor.AltMate.VerticalEdge = MateEdge.Top;
							popup.AltMate.VerticalEdge = MateEdge.Bottom;
							break;

						case 14:
							anchor.Mate.HorizontalEdge = MateEdge.Right;
							popup.Mate.HorizontalEdge = MateEdge.Right;

							anchor.Mate.VerticalEdge = MateEdge.Bottom;
							popup.Mate.VerticalEdge = MateEdge.Top;

							anchor.AltMate.VerticalEdge = MateEdge.Top;
							popup.AltMate.VerticalEdge = MateEdge.Bottom;
							break;

						case 15:
							anchor.Mate.HorizontalEdge = MateEdge.Right;
							popup.Mate.HorizontalEdge = MateEdge.Left;

							anchor.Mate.VerticalEdge = MateEdge.Bottom;
							popup.Mate.VerticalEdge = MateEdge.Top;

							anchor.AltMate.VerticalEdge = MateEdge.Top;
							popup.AltMate.VerticalEdge = MateEdge.Bottom;
							break;
					}

					testRunner.ClickByName(targetWidget);
					testRunner.Delay(1);

					if (i == 15 && firstPass)
					{
						firstPass = false;
						i = -1;
						targetWidget = "targetB";
						anchor.Widget = button2;
					}

					popup.Widget.Unfocus();
				}

				testRunner.Delay();

				return Task.CompletedTask;
			}, 95);
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
