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

	public class DockingTabControl : FlowLayoutWidget
	{
		public int MinDockingWidth = 400 * (int)GuiWidget.DeviceScale;
		protected GuiWidget widgetTodockTo;
		private Dictionary<string, GuiWidget> allTabs = new Dictionary<string, GuiWidget>();
		private List<PopupButton> settingsButtons = new List<PopupButton>();

		private PrinterConfig printer;

		private ThemeConfig theme;
		public DockingTabControl(GuiWidget widgetTodockTo, DockSide dockSide, PrinterConfig printer)
			: base (FlowDirection.TopToBottom)
		{
			this.theme = ApplicationController.Instance.Theme;
			this.printer = printer;
			this.widgetTodockTo = widgetTodockTo;
			this.DockSide = dockSide;

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

		public void AddPage(string name, GuiWidget widget)
		{
			allTabs.Add(name, widget);

			if (formHasLoaded)
			{
				Rebuild();
			}
		}

		public void RemovePage(string name)
		{
			if (allTabs.ContainsKey(name))
			{
				allTabs.Remove(name);
				this.Rebuild();
			}
		}

		public override void OnLoad(EventArgs args)
		{
			base.OnLoad(args);
			this.Rebuild();
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
			var icon = AggContext.StaticData.LoadIcon(imageFile, 16, 16, IconColor.Theme);

			var imageWidget = ApplicationController.Instance.Theme.ButtonFactory.GenerateIconButton(icon);
			imageWidget.Name = "Pin Settings Button";
			imageWidget.Click += (s, e) =>
			{
				this.ControlIsPinned = !this.ControlIsPinned;
				this.printer.ViewState.DockWindowFloating = false;
				UiThread.RunOnIdle(this.Rebuild);
			};

			return imageWidget;
		}

		public override double Width
		{
			get => this.PageWidth;
			set => this.PageWidth = value;
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

			this.RemoveAllChildren();

			SimpleTabs tabControl = null;
			if (this.ControlIsPinned)
			{
				var resizePage = new ResizeContainer(this)
				{
					Width = PageWidth,
					VAnchor = VAnchor.Stretch,
					SpliterBarColor = theme.SplitterBackground,
					SplitterWidth = theme.SplitterWidth,
				};

				tabControl = new SimpleTabs(this.CreatePinButton())
				{
					VAnchor = VAnchor.Stretch,
					HAnchor = HAnchor.Stretch,
				};
				tabControl.TabBar.BackgroundColor = theme.ActiveTabBarBackground;

				tabControl.ActiveTabChanged += (s, e) =>
				{
					printer.ViewState.SliceSettingsTabIndex = tabControl.SelectedTabIndex;
				};

				resizePage.AddChild(tabControl);

				this.AddChild(resizePage);
			}

			int tabIndex = 0;
			foreach (var nameWidget in allTabs)
			{
				string tabTitle = nameWidget.Key;

				if (this.ControlIsPinned)
				{
					var content = new DockingWindowContent(this, nameWidget.Value, tabTitle);

					tabControl.AddTab(
						new ToolTab(
							tabTitle,
							tabControl,
							content,
							theme,
							hasClose: false,
							pointSize: theme.FontSize11)
						{
							Name = tabTitle + " Tab",
							InactiveTabColor = Color.Transparent,
							ActiveTabColor = theme.TabBodyBackground
						});
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

					var spliterColor = theme.SplitterBackground;

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
					this.AddChild(settingsButton);

					int localTabIndex = tabIndex;
					settingsButton.Click += (s, e) =>
					{
						this.printer.ViewState.SliceSettingsTabIndex = localTabIndex;
						this.printer.ViewState.DockWindowFloating = true;
					};

					settingsButton.PopupWindowClosed += (s, e) =>
					{
						if (!ApplicationController.Instance.IsReloading)
						{
							this.printer.ViewState.DockWindowFloating = false;
						}
					};

					if (this.printer.ViewState.DockWindowFloating
						&& localTabIndex == this.printer.ViewState.SliceSettingsTabIndex)
					{
						UiThread.RunOnIdle(() =>
						{
							if (!settingsButton.HasBeenClosed && settingsButton.Parent != null)
							{
								settingsButton.ShowPopup();
							}
						});
					}
				}

				tabIndex++;
			}

			if (this.ControlIsPinned)
			{
				tabControl.TabBar.Padding = new BorderDouble(right: theme.ToolbarPadding.Right);

				if (printer.ViewState.SliceSettingsTabIndex < tabControl.TabCount)
				{
					tabControl.SelectedTabIndex = printer.ViewState.SliceSettingsTabIndex;
				}
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