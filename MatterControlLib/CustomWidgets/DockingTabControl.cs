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
using System.Collections.Generic;
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.CustomWidgets
{
	public enum DockSide { Left, Bottom, Right, Top };

	public interface ICloseableTab
	{
	}

	public class DockingTabControl : FlowLayoutWidget
	{
		public int MinDockingWidth { get; set; }
		protected GuiWidget widgetTodockTo;
		private List<(string key, string text, GuiWidget widget)> allTabs = new List<(string key, string text, GuiWidget widget)>();

		private PrinterConfig printer;

		private ThemeConfig theme;

		public DockingTabControl(GuiWidget widgetTodockTo, DockSide dockSide, PrinterConfig printer, ThemeConfig theme)
			: base (FlowDirection.TopToBottom)
		{
			this.theme = theme;
			this.printer = printer;
			this.widgetTodockTo = widgetTodockTo;
			this.DockSide = dockSide;
			this.BorderColor = theme.MinimalShade;
			this.Border = new BorderDouble(left: 1);

			// Add dummy widget to ensure OnLoad fires
			this.AddChild(new GuiWidget(10, 10));
		}

		public event EventHandler PinStatusChanged;

		public bool ControlIsPinned
		{
			get => printer.ViewState.SliceSettingsTabPinned;
			set
			{
				if (this.ControlIsPinned != value)
				{
					printer.ViewState.SliceSettingsTabPinned = value;
					PinStatusChanged?.Invoke(this, null);
				}
			}
		}

		public DockSide DockSide { get; set; }

		public void AddPage(string key, string name, GuiWidget widget, bool allowRebuild = true)
		{
			allTabs.Add((key, name, widget));

			if (formHasLoaded && allowRebuild)
			{
				Rebuild();
			}
		}

		public void RemovePage(string key, bool allowRebuild = true)
		{
			foreach(var tab in allTabs)
			{
				if(tab.key == key)
				{
					allTabs.Remove(tab);
					if (allowRebuild)
					{
						this.Rebuild();
					}
					return;
				}
			}
		}

		public override void OnLoad(EventArgs args)
		{
			base.OnLoad(args);
			this.Rebuild();
		}

		public override void OnClosed(EventArgs e)
		{
			// Iterate and close all held tab widgets
			foreach (var item in allTabs)
			{
				item.widget.Close();
			}

			allTabs.Clear();

			base.OnClosed(e);
		}

		public override void Initialize()
		{
			base.Initialize();

			this.VAnchor = VAnchor.Stretch;
			this.HAnchor = HAnchor.Fit;
		}

		private GuiWidget CreatePinButton()
		{
			string imageFile = this.ControlIsPinned ? "Pushpin_16x.png" : "PushpinUnpin_16x.png";

			var pinTabButton = new IconButton(AggContext.StaticData.LoadIcon(imageFile, 16, 16, theme.InvertIcons), theme)
			{
				Name = "Pin Settings Button"
			};
			pinTabButton.Click += (s, e) =>
			{
				this.ControlIsPinned = !this.ControlIsPinned;
				this.printer.ViewState.DockWindowFloating = false;
				UiThread.RunOnIdle(this.Rebuild);
			};

			return pinTabButton;
		}

		// Clamped to MinDockingWidth or value
		private double _constrainedWidth;
		private double ConstrainedWidth
		{
			get => Math.Max(MinDockingWidth, printer.ViewState.SliceSettingsWidth);
			set
			{
				if (value > MinDockingWidth
					&& _constrainedWidth != value)
				{
					_constrainedWidth = value;
					printer.ViewState.SliceSettingsWidth = value;
				}
			}
		}

		public void Rebuild()
		{
			this.Focus();

			foreach (var nameWidget in allTabs)
			{
				nameWidget.widget.Parent?.RemoveChild(nameWidget.widget);
				nameWidget.widget.ClearRemovedFlag();
			}

			this.CloseAllChildren();

			SimpleTabs tabControl = null;

			var grabBarSide = DockSide == DockSide.Left ? GrabBarSide.Right : GrabBarSide.Left;
			if (this.ControlIsPinned)
			{
				var resizePage = new VerticalResizeContainer(theme, grabBarSide)
				{
					Width = this.ConstrainedWidth,
					VAnchor = VAnchor.Stretch,
					SplitterBarColor = theme.SplitterBackground,
					SplitterWidth = theme.SplitterWidth,
					MinimumSize = new Vector2(this.MinDockingWidth, 0)
				};
				resizePage.BoundsChanged += (s, e) =>
				{
					this.ConstrainedWidth = resizePage.Width;
				};

				tabControl = new SimpleTabs(theme, this.CreatePinButton())
				{
					VAnchor = VAnchor.Stretch,
					HAnchor = HAnchor.Stretch,
				};
				tabControl.TabBar.BackgroundColor = theme.TabBarBackground;

				tabControl.ActiveTabChanged += (s, e) =>
				{
					printer.ViewState.SliceSettingsTabKey = tabControl.SelectedTabKey;
				};

				resizePage.AddChild(tabControl);

				this.AddChild(resizePage);
			}

			foreach (var item in allTabs)
			{
				if (this.ControlIsPinned)
				{
					var content = new DockingWindowContent(this, item.widget, item.text, theme);

					var tab = new ToolTab(
							item.key,
							item.text,
							tabControl,
							content,
							theme,
							hasClose: item.widget is ICloseableTab,
							pointSize: theme.DefaultFontSize)
						{
							Name = item.key + " Tab",
							InactiveTabColor = Color.Transparent,
							ActiveTabColor = theme.BackgroundColor
						};

					tab.CloseClicked += (s, e) =>
					{
						if (tab.Name == "Printer Tab")
						{
							printer.ViewState.ConfigurePrinterVisible = false;
						}
						if (tab.Name == "Controls Tab")
						{
							printer.ViewState.ControlsVisible = false;
						}
						if (tab.Name == "Terminal Tab")
						{
							printer.ViewState.TerminalVisible = false;
						}
					};

					tabControl.AddTab(tab);
				}
				else // control is floating
				{
					var resizeContainer = new VerticalResizeContainer(theme, grabBarSide)
					{
						Width = this.ConstrainedWidth,
						VAnchor = VAnchor.Stretch,
						HAnchor = HAnchor.Right,
						BackgroundColor = theme.BackgroundColor,
						SplitterBarColor = theme.SplitterBackground,
						SplitterWidth = theme.SplitterWidth,
					};
					resizeContainer.AddChild(new DockingWindowContent(this, item.widget, item.text, theme)
					{
						BackgroundColor = theme.TabBodyBackground,
						Width = this.ConstrainedWidth
					});

					string localTabKey = item.key;

					var tabBarButton = new DockingTabButton(item.text, theme)
					{
						Name = $"{item.key} Sidebar",
						PopupContent = resizeContainer,
						PopupLayoutEngine = new UnpinnedLayoutEngine(resizeContainer, widgetTodockTo, DockSide)
					};
					tabBarButton.Click += (s, e) =>
					{
						resizeContainer.Width = this.ConstrainedWidth;
						this.printer.ViewState.SliceSettingsTabKey = localTabKey;
						this.printer.ViewState.DockWindowFloating = true;
					};
					tabBarButton.PopupWindowClosed += (s, e) =>
					{
						if (!ApplicationController.Instance.IsReloading)
						{
							this.printer.ViewState.DockWindowFloating = false;
						}
					};
					this.AddChild(tabBarButton);

					if (this.printer.ViewState.DockWindowFloating
						&& localTabKey == this.printer.ViewState.SliceSettingsTabKey)
					{
						UiThread.RunOnIdle(() =>
						{
							if (!tabBarButton.HasBeenClosed && tabBarButton.Parent != null)
							{
								tabBarButton.ShowPopup();
							}
						});
					}
				}
			}

			if (this.ControlIsPinned)
			{
				tabControl.TabBar.Padding = new BorderDouble(right: theme.ToolbarPadding.Right);
				tabControl.SelectedTabKey = printer.ViewState.SliceSettingsTabKey;
			}
		}

		private class DockingTabButton : PopupButton
		{
			private Color grayBorder;
			private ThemeConfig theme;

			public DockingTabButton(string tabTitle, ThemeConfig theme)
			{
				this.grayBorder = theme.GetBorderColor(theme.IsDarkTheme ? 45 : 55);
				this.theme = theme;
				this.HAnchor = HAnchor.Fit;
				this.VAnchor = VAnchor.Fit | VAnchor.Center;
				this.AlignToRightEdge = true;
				this.MakeScrollable = false;
				this.Border = new BorderDouble(right: 6);
				this.BorderColor = grayBorder;
				this.Margin = new BorderDouble(2, 8, 0, 0);
				this.HoverColor = Color.Transparent;

				var printer = new TypeFacePrinter(tabTitle, theme.DefaultFontSize * GuiWidget.DeviceScale);
				var rotatedLabel = new VertexSourceApplyTransform(
					printer,
					Affine.NewRotation(MathHelper.DegreesToRadians(-90)));

				var textBounds = rotatedLabel.GetBounds();
				var bounds = new RectangleDouble(printer.TypeFaceStyle.DescentInPixels, textBounds.Bottom, printer.TypeFaceStyle.AscentInPixels, textBounds.Top);
				rotatedLabel.Transform = ((Affine)rotatedLabel.Transform)
					* Affine.NewTranslation(new Vector2(-printer.TypeFaceStyle.DescentInPixels, -bounds.Bottom));

				this.AddChild(buttonView = new GuiWidget(bounds.Width, bounds.Height)
				{
					DoubleBuffer = true,
					Margin = new BorderDouble(3, 1),
					Selectable = false
				});
				buttonView.AfterDraw += (s, e) =>
				{
					e.Graphics2D.Render(rotatedLabel, theme.TextColor);
				};
			}

			public override void OnMouseEnterBounds(MouseEventArgs mouseEvent)
			{
				base.OnMouseEnterBounds(mouseEvent);
				this.BorderColor = theme.PrimaryAccentColor;
			}

			public override void OnMouseLeaveBounds(MouseEventArgs mouseEvent)
			{
				base.OnMouseLeaveBounds(mouseEvent);
				this.BorderColor = grayBorder;
			}
		}

		private class DockingWindowContent : GuiWidget, IIgnoredPopupChild
		{
			internal DockingWindowContent(DockingTabControl dockingControl, GuiWidget child, string title, ThemeConfig theme)
			{
				var topToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom)
				{
					VAnchor = VAnchor.Stretch,
					HAnchor = HAnchor.Stretch
				};

				if (!dockingControl.ControlIsPinned)
				{
					var titleBar = new FlowLayoutWidget()
					{
						HAnchor = HAnchor.Stretch,
						VAnchor = VAnchor.Fit,
						BackgroundColor = theme.TabBarBackground,
					};

					titleBar.AddChild(new TextWidget(title, textColor: theme.TextColor)
					{
						Margin = new BorderDouble(left: 8),
						VAnchor = VAnchor.Center
					});

					titleBar.AddChild(new HorizontalSpacer());

					titleBar.AddChild(dockingControl.CreatePinButton());

					topToBottom.AddChild(titleBar);
				}

				topToBottom.AddChild(child);

				HAnchor = HAnchor.Stretch;
				VAnchor = VAnchor.Stretch;

				AddChild(topToBottom);
			}

			public bool KeepMenuOpen => false;
		}
	}

	public class UnpinnedLayoutEngine : IPopupLayoutEngine
	{
		protected GuiWidget widgetTodockTo;
		private GuiWidget contentWidget;
		private HashSet<GuiWidget> hookedParents = new HashSet<GuiWidget>();
		private PopupWidget popupWidget;

		public UnpinnedLayoutEngine(GuiWidget contentWidget, GuiWidget widgetTodockTo, DockSide dockSide)
		{
			this.contentWidget = contentWidget;
			this.widgetTodockTo = widgetTodockTo;
			DockSide = dockSide;
			contentWidget.BoundsChanged += widgetRelativeTo_PositionChanged;
		}

		public GuiWidget Anchor { get; }

		public DockSide DockSide { get; set; }

		public double MaxHeight { get; private set; }

		public void Closed()
		{
			// Unbind callbacks on parents for position_changed if we're closing
			foreach (GuiWidget widget in hookedParents)
			{
				widget.PositionChanged -= widgetRelativeTo_PositionChanged;
				widget.BoundsChanged -= widgetRelativeTo_PositionChanged;
			}

			hookedParents.Clear();

			// Long lived originating item must be unregistered
			widgetTodockTo.Closed -= widgetRelativeTo_Closed;

			// Restore focus to originating widget on close
			if (this.widgetTodockTo != null
				&& !widgetTodockTo.HasBeenClosed)
			{
				// On menu close, select the first scrollable parent of the widgetRelativeTo
				var scrollableParent = widgetTodockTo.Parents<ScrollableWidget>().FirstOrDefault();
				if (scrollableParent != null)
				{
					scrollableParent.Focus();
				}
			}
		}

		public void ShowPopup(PopupWidget popupWidget)
		{
			this.popupWidget = popupWidget;
			SystemWindow windowToAddTo = widgetTodockTo.Parents<SystemWindow>().FirstOrDefault();
			windowToAddTo?.AddChild(popupWidget);

			GuiWidget topParent = widgetTodockTo.Parent;
			while (topParent.Parent != null
				&& topParent as SystemWindow == null)
			{
				// Regrettably we don't know who it is that is the window that will actually think it is moving relative to its parent
				// but we need to know anytime our widgetRelativeTo has been moved by any change, so we hook them all.
				if (!hookedParents.Contains(topParent))
				{
					hookedParents.Add(topParent);
					topParent.PositionChanged += widgetRelativeTo_PositionChanged;
					topParent.BoundsChanged += widgetRelativeTo_PositionChanged;
				}

				topParent = topParent.Parent;
			}

			widgetRelativeTo_PositionChanged(widgetTodockTo, null);
			widgetTodockTo.Closed += widgetRelativeTo_Closed;
		}

		private void widgetRelativeTo_Closed(object sender, EventArgs e)
		{
			// If the owning widget closed, so should we
			popupWidget.CloseMenu();
		}

		private void widgetRelativeTo_PositionChanged(object sender, EventArgs e)
		{
			if (widgetTodockTo != null)
			{
				RectangleDouble bounds = widgetTodockTo.BoundsRelativeToParent;

				GuiWidget topParent = widgetTodockTo.Parent;
				while (topParent != null && topParent.Parent != null)
				{
					topParent.ParentToChildTransform.transform(ref bounds);
					topParent = topParent.Parent;
				}

				switch (DockSide)
				{
					case DockSide.Left:
						popupWidget.HAnchor = HAnchor.Absolute;
						popupWidget.LocalBounds = new RectangleDouble(bounds.Left, bounds.Bottom, bounds.Left + contentWidget.Width, bounds.Top);
						break;

					case DockSide.Bottom:
						throw new NotImplementedException();

					case DockSide.Right:
						popupWidget.HAnchor = HAnchor.Absolute;
						popupWidget.LocalBounds = new RectangleDouble(bounds.Right - contentWidget.Width, bounds.Bottom, bounds.Right, bounds.Top);
						break;

					case DockSide.Top:
						throw new NotImplementedException();

					default:
						throw new NotImplementedException();
				}
			}
		}
	}
}