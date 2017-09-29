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
using MatterHackers.Agg.ImageProcessing;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.CustomWidgets
{
	public enum DockSide { Left, Bottom, Right, Top };

	public class DockingTabControl : GuiWidget
	{
		public int MinDockingWidth = 400 * (int)GuiWidget.DeviceScale;
		protected GuiWidget widgetTodockTo;
		private Dictionary<string, GuiWidget> allTabs = new Dictionary<string, GuiWidget>();
		private List<PopupButton> settingsButtons = new List<PopupButton>();

		private PrinterConfig printer;

		private GuiWidget topToBottom;

		public DockingTabControl(GuiWidget widgetTodockTo, DockSide dockSide, PrinterConfig printer)
		{
			this.printer = printer;
			this.widgetTodockTo = widgetTodockTo;
			this.DockSide = dockSide;
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

		public void AddPage(string name, GuiWidget widget)
		{
			allTabs.Add(name, widget);
			Rebuild();
		}

		public override void Initialize()
		{
			base.Initialize();

			Width = 30;
			VAnchor = VAnchor.Stretch;
			HAnchor = HAnchor.Fit;
			topToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.Fit,
				VAnchor = VAnchor.Stretch
			};
			AddChild(topToBottom);
		}

		private GuiWidget CreatePinButton()
		{
			var icon = AggContext.StaticData.LoadIcon(this.ControlIsPinned ? "Pushpin_16x.png" : "PushpinUnpin_16x.png", 16, 16);

			var imageWidget = ApplicationController.Instance.Theme.ButtonFactory.GenerateIconButton(icon);
			imageWidget.Name = "Pin Settings Button";
			imageWidget.Margin = new BorderDouble(right: 2);
			imageWidget.Click += (s, e) =>
			{
				this.ControlIsPinned = !this.ControlIsPinned;
				UiThread.RunOnIdle(this.Rebuild);
			};

			return imageWidget;
		}

		// Clamped to MinDockingWidth or value
		double PageWidth
		{
			get => Math.Max(MinDockingWidth, printer.ViewState.SliceSettingsWidth);
			set => printer.ViewState.SliceSettingsWidth = Math.Max(MinDockingWidth, value);
		}

		private void Rebuild()
		{
			settingsButtons.Clear();
			Focus();
			foreach (var nameWidget in allTabs)
			{
				nameWidget.Value.Parent?.RemoveChild(nameWidget.Value);
				nameWidget.Value.ClearRemovedFlag();
			}

			topToBottom.RemoveAllChildren();

			var tabControl = new TabControl();

			if (this.ControlIsPinned)
			{
				var resizePage = new ResizeContainer(this)
				{
					Width = PageWidth,
					VAnchor = VAnchor.Stretch,
					SpliterBarColor = ApplicationController.Instance.Theme.SplitterBackground,
					SplitterWidth = ApplicationController.Instance.Theme.SplitterWidth,
				};

				tabControl = ApplicationController.Instance.Theme.CreateTabControl();
				resizePage.AddChild(tabControl);

				topToBottom.AddChild(resizePage);
			}

			int tabIndex = 0;
			foreach (var nameWidget in allTabs)
			{
				string tabTitle = nameWidget.Key;

				if (this.ControlIsPinned)
				{
					var content = new DockingWindowContent(this, nameWidget.Value, tabTitle);

					var tabPage = new TabPage(content, tabTitle);
					var textTab = new TextTab(
						tabPage,
						tabTitle + " Tab",
						12,
						ActiveTheme.Instance.TabLabelSelected,
						RGBA_Bytes.Transparent,
						ActiveTheme.Instance.TabLabelUnselected,
						RGBA_Bytes.Transparent,
						useUnderlineStyling: true);

					tabControl.AddTab(textTab);

					int localTabIndex = tabIndex;
					textTab.Click += (s, e) =>
					{
						printer.ViewState.SliceSettingsTabIndex = localTabIndex;
					};
				}
				else // control is floating
				{
					var printer = new TypeFacePrinter(tabTitle, 12 * GuiWidget.DeviceScale);
					var rotatedLabel = new VertexSourceApplyTransform(
						printer,
						Affine.NewRotation(MathHelper.DegreesToRadians(-90)));

					var textBounds = rotatedLabel.Bounds();
					var bounds = new RectangleDouble(printer.TypeFaceStyle.DescentInPixels, textBounds.Bottom, printer.TypeFaceStyle.AscentInPixels, textBounds.Top);
					rotatedLabel.Transform = ((Affine)rotatedLabel.Transform)
						* Affine.NewTranslation(new Vector2(-printer.TypeFaceStyle.DescentInPixels, -bounds.Bottom));

					var optionsText = new GuiWidget(bounds.Width, bounds.Height)
					{
						DoubleBuffer = true,
						Margin = new BorderDouble(3, 6, 3, 0)
					};
					optionsText.AfterDraw += (s, e) =>
					{
						e.graphics2D.Render(rotatedLabel, ActiveTheme.Instance.PrimaryTextColor);
					};

					var settingsButton = new PopupButton(optionsText)
					{
						AlignToRightEdge = true,
						Name = $"{tabTitle} Sidebar",
						MakeScrollable = false,
					};

					var spliterColor = ActiveTheme.Instance.PrimaryBackgroundColor;
					var alpha = ApplicationController.Instance.Theme.SplitterBackground.Alpha0To1;
					spliterColor.Red0To1 *= (1 - alpha);
					spliterColor.Green0To1 *= (1 - alpha);
					spliterColor.Blue0To1 *= (1 - alpha);

					var resizeContainer = new ResizeContainer(this)
					{
						Width = PageWidth,
						VAnchor = VAnchor.Stretch,
						HAnchor = HAnchor.Right,
						SpliterBarColor = spliterColor,
						SplitterWidth = ApplicationController.Instance.Theme.SplitterWidth,
					};
					resizeContainer.AddChild(new DockingWindowContent(this, nameWidget.Value, tabTitle)
					{
						BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor,
						Width = PageWidth
					});

					settingsButtons.Add(settingsButton);
					settingsButton.PopupContent = resizeContainer;

					settingsButton.Click += (s, e) =>
					{
						resizeContainer.Width = PageWidth;
					};

					settingsButton.PopupLayoutEngine = new UnpinnedLayoutEngine(settingsButton.PopupContent, widgetTodockTo, DockSide);
					topToBottom.AddChild(settingsButton);

					int localTabIndex = tabIndex;
					settingsButton.Click += (s, e) =>
					{
						this.printer.ViewState.SliceSettingsTabIndex = localTabIndex;
					};
				}

				tabIndex++;
			}

			if (this.ControlIsPinned)
			{
				tabControl.TabBar.AddChild(new HorizontalSpacer());

				var pinButton = this.CreatePinButton();
				//pinButton.Margin = new BorderDouble(right: 18, bottom: 7);
				tabControl.TabBar.AddChild(pinButton);

				if (printer.ViewState.SliceSettingsTabIndex < tabControl.TabCount)
				{
					tabControl.SelectedTabIndex = printer.ViewState.SliceSettingsTabIndex;
				}
			}
		}

		internal class ResizeContainer : FlowLayoutWidget
		{
			private DockingTabControl dockingTabControl;
			private double downWidth = 0;
			private bool mouseDownOnBar = false;
			private double mouseDownX;

			private int splitterWidth = 10;

			internal ResizeContainer(DockingTabControl dockingTabControl)
			{
				this.dockingTabControl = dockingTabControl;
				this.HAnchor = HAnchor.Absolute;
				this.Cursor = Cursors.VSplit;
			}

			public RGBA_Bytes SpliterBarColor { get; set; } = ActiveTheme.Instance.TertiaryBackgroundColor;

			public int SplitterWidth
			{
				get => splitterWidth;
				set
				{
					if (splitterWidth != value)
					{
						splitterWidth = value;
						this.Padding = new BorderDouble(splitterWidth, 0, 0, 0);
					}
				}
			}

			public override void OnDraw(Graphics2D graphics2D)
			{
				graphics2D.FillRectangle(LocalBounds.Left, LocalBounds.Bottom, LocalBounds.Left + this.SplitterWidth, LocalBounds.Top, this.SpliterBarColor);
				base.OnDraw(graphics2D);
			}

			public override void OnMouseDown(MouseEventArgs mouseEvent)
			{
				if (mouseEvent.Position.x < this.SplitterWidth)
				{
					mouseDownOnBar = true;
					mouseDownX = TransformToScreenSpace(mouseEvent.Position).x;
					downWidth = Width;
				}
				base.OnMouseDown(mouseEvent);
			}

			public override void OnMouseMove(MouseEventArgs mouseEvent)
			{
				if (mouseDownOnBar)
				{
					int currentMouseX = (int)TransformToScreenSpace(mouseEvent.Position).x;
					UiThread.RunOnIdle(() =>
					{
						dockingTabControl.PageWidth = downWidth + mouseDownX - currentMouseX;
						Width = dockingTabControl.PageWidth;
					});
				}
				base.OnMouseMove(mouseEvent);
			}

			public override void OnMouseUp(MouseEventArgs mouseEvent)
			{
				mouseDownOnBar = false;
				base.OnMouseUp(mouseEvent);
			}
		}

		private class DockingWindowContent : GuiWidget, IIgnoredPopupChild
		{
			internal DockingWindowContent(DockingTabControl dockingControl, GuiWidget child, string title)
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
					};

					titleBar.AddChild(new TextWidget(title, textColor: ActiveTheme.Instance.PrimaryTextColor)
					{
						Margin = new BorderDouble(left: 12)
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

		private void widgetRelativeTo_Closed(object sender, ClosedEventArgs e)
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
						popupWidget.LocalBounds = new RectangleDouble(bounds.Left, bounds.Bottom, bounds.Left - contentWidget.Width, bounds.Top);
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