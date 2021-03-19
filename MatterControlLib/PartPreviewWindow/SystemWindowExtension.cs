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
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	[Flags]
	public enum MateEdge
	{
		Top = 1,
		Bottom = 2,
		Left = 4,
		Right = 8
	}

	public class MateOptions
	{
		public MateOptions(MateEdge horizontalEdge = MateEdge.Left, MateEdge verticalEdge = MateEdge.Bottom)
		{
			this.HorizontalEdge = horizontalEdge;
			this.VerticalEdge = verticalEdge;
		}

		public MateEdge HorizontalEdge { get; set; }

		public MateEdge VerticalEdge { get; set; }

		public bool Top => this.VerticalEdge.HasFlag(MateEdge.Top);

		public bool Bottom => this.VerticalEdge.HasFlag(MateEdge.Bottom);

		public bool Left => this.HorizontalEdge.HasFlag(MateEdge.Left);

		public bool Right => this.HorizontalEdge.HasFlag(MateEdge.Right);
	}

	public class MatePoint
	{
		public MateOptions Mate { get; set; } = new MateOptions();

		public MateOptions AltMate { get; set; } = new MateOptions();

		public GuiWidget Widget { get; set; }

		public MatePoint()
		{
		}

		public MatePoint(GuiWidget widget)
		{
			this.Widget = widget;
		}

		public RectangleDouble Offset { get; set; }
	}

	public interface IOverrideAutoClose
	{
		bool AllowAutoClose { get; }
	}

	public static class SystemWindowExtension
	{
		public static void ShowPopover(this SystemWindow systemWindow, MatePoint anchor, MatePoint popup, RectangleDouble altBounds = default(RectangleDouble), double secondsToClose = 0)
		{
			var settingsRow = anchor.Widget as SettingsRow;
			var sliceSettingsPopover = popup.Widget as ClickablePopover;

			var hookedWidgets = new HashSet<GuiWidget>();

			void Anchor_Closed(object sender, EventArgs e)
			{
				if (popup.Widget is IOverrideAutoClose overideAutoClose
					&& !overideAutoClose.AllowAutoClose)
				{
					return;
				}

				// If the owning widget closed, so should we
				popup.Widget.Close();

				foreach (var widget in hookedWidgets)
				{
					widget.Closed -= Anchor_Closed;
				}
			}

			void WidgetRelativeTo_PositionChanged(object sender, EventArgs e)
			{
				if (anchor.Widget?.Parent != null)
				{
					// Calculate left aligned screen space position (using widgetRelativeTo.parent)
					Vector2 anchorLeft = anchor.Widget.Parent.TransformToScreenSpace(anchor.Widget.Position);
					anchorLeft += new Vector2(altBounds.Left, altBounds.Bottom);

					Vector2 popupPosition = anchorLeft;

					var bounds = altBounds == default(RectangleDouble) ? anchor.Widget.LocalBounds : altBounds;

					Vector2 xPosition = GetXAnchor(anchor.Mate, popup.Mate, popup.Widget, bounds);

					Vector2 screenPosition;

					screenPosition = anchorLeft + xPosition;

					// Constrain
					if (screenPosition.X + popup.Widget.Width > systemWindow.Width
						|| screenPosition.X < 0)
					{
						var altXPosition = GetXAnchor(anchor.AltMate, popup.AltMate, popup.Widget, bounds);

						var altScreenPosition = anchorLeft + altXPosition;

						// Prefer clipping on edge revealed by resize
						if ((popup.AltMate.Right && altScreenPosition.X > -15)
							|| (popup.AltMate.Left && altScreenPosition.X + popup.Widget.Width < systemWindow.Width))
						{
							xPosition = altXPosition;

							if (settingsRow != null
								&& sliceSettingsPopover != null)
							{
								sliceSettingsPopover.ArrowDirection = settingsRow.ArrowDirection == ArrowDirection.Left ? ArrowDirection.Right : ArrowDirection.Left;
							}
						}
					}

					popupPosition += xPosition;

					Vector2 yPosition = GetYAnchor(anchor.Mate, popup.Mate, popup.Widget, bounds);

					screenPosition = anchorLeft + yPosition;

					// Constrain
					if (anchor.AltMate != null
						&& (screenPosition.Y + popup.Widget.Height > systemWindow.Height
							|| screenPosition.Y < 0))
					{
						yPosition = GetYAnchor(anchor.AltMate, popup.AltMate, popup.Widget, bounds);

						if (settingsRow != null)
						{
							settingsRow.ArrowDirection = settingsRow.ArrowDirection == ArrowDirection.Up ? ArrowDirection.Down : ArrowDirection.Up;
						}
					}

					popup.Widget.Closed += Anchor_Closed;
					anchor.Widget.Closed += Anchor_Closed;
					hookedWidgets.Add(anchor.Widget);

					foreach (var widget in anchor.Widget.Parents<GuiWidget>())
					{
						widget.Closed += Anchor_Closed;
						hookedWidgets.Add(widget);
					}

					popupPosition += yPosition;

					popup.Widget.Position = popupPosition;
				}
			}

			WidgetRelativeTo_PositionChanged(anchor.Widget, null);

			popup.Widget.BoundsChanged += (s, e) => WidgetRelativeTo_PositionChanged(anchor.Widget, null);

			// When the widgets position changes, sync the popup position
			systemWindow?.AddChild(popup.Widget);

			if (secondsToClose > 0)
			{
				UiThread.RunOnIdle(() => Anchor_Closed(null, null), secondsToClose);
			}
		}

		private static void RightHorizontalSplitPopup(SystemWindow systemWindow, MatePoint anchor, MatePoint popup, RectangleDouble altBounds)
		{
			// Calculate left for right aligned split
			Vector2 popupPosition = new Vector2(systemWindow.Width - popup.Widget.Width, 0);

			Vector2 anchorLeft = anchor.Widget.Parent.TransformToScreenSpace(anchor.Widget.Position);

			popup.Widget.Height = anchorLeft.Y;

			popup.Widget.Position = popupPosition;
		}

		public static void ShowPopup(this SystemWindow systemWindow, MatePoint anchor, MatePoint popup, RectangleDouble altBounds = default(RectangleDouble), int borderWidth = 1)
		{
			ShowPopup(systemWindow, anchor, popup, altBounds, borderWidth, BestPopupPosition);
		}

		public static void ShowRightSplitPopup(this SystemWindow systemWindow, MatePoint anchor, MatePoint popup, RectangleDouble altBounds = default(RectangleDouble), int borderWidth = 1)
		{
			ShowPopup(systemWindow, anchor, popup, altBounds, borderWidth, RightHorizontalSplitPopup);
		}

		public static void ShowPopup(this SystemWindow systemWindow, MatePoint anchor, MatePoint popup, RectangleDouble altBounds, int borderWidth, Action<SystemWindow, MatePoint, MatePoint, RectangleDouble> layoutHelper)
		{
			var hookedParents = new HashSet<GuiWidget>();

			List<IIgnoredPopupChild> ignoredWidgets = popup.Widget.Children.OfType<IIgnoredPopupChild>().ToList();

			void Widget_Draw(object sender, DrawEventArgs e)
			{
				if (borderWidth > 0)
				{
					e.Graphics2D.Render(
						new Stroke(
							new RoundedRect(popup.Widget.LocalBounds, 0),
							borderWidth * 2),
						AppContext.Theme.PopupBorderColor);
				}
			}

			void WidgetRelativeTo_PositionChanged(object sender, EventArgs e)
			{
				if (anchor.Widget?.Parent != null)
				{
					layoutHelper.Invoke(systemWindow, anchor, popup, altBounds);
				}
			}

			void CloseMenu()
			{
				popup.Widget.AfterDraw -= Widget_Draw;

				popup.Widget.Close();

				anchor.Widget.Closed -= Anchor_Closed;

				// Unbind callbacks on parents for position_changed if we're closing
				foreach (GuiWidget widget in hookedParents)
				{
					widget.PositionChanged -= WidgetRelativeTo_PositionChanged;
					widget.BoundsChanged -= WidgetRelativeTo_PositionChanged;
				}

				// Long lived originating item must be unregistered
				anchor.Widget.Closed -= Anchor_Closed;

				// Restore focus to originating widget on close
				if (anchor.Widget?.HasBeenClosed == false)
				{
					anchor.Widget.Focus();
				}
			}

			void FocusChanged(object s, EventArgs e)
			{
				UiThread.RunOnIdle(() =>
				{
					// Fired any time focus changes. Traditionally we closed the menu if we weren't focused.
					// To accommodate children (or external widgets) having focus we also query for and consider special cases
					bool specialChildHasFocus = ignoredWidgets.Any(w => w.ContainsFocus || w.Focused || w.KeepMenuOpen);
					bool descendantIsHoldingOpen = popup.Widget.Descendants<GuiWidget>().Any(w => w is IIgnoredPopupChild ignoredPopupChild
						&& ignoredPopupChild.KeepMenuOpen);

					// If the focused changed and we've lost focus and no special cases permit, close the menu
					if (!popup.Widget.ContainsFocus
						&& !specialChildHasFocus
						&& !descendantIsHoldingOpen
						&& !PopupWidget.DebugKeepOpen)
					{
						CloseMenu();
					}
				});
			}

			void Anchor_Closed(object sender, EventArgs e)
			{
				// If the owning widget closed, so should we
				CloseMenu();
			}

			foreach (var ancestor in anchor.Widget.Parents<GuiWidget>().Where(p => p != systemWindow))
			{
				if (hookedParents.Add(ancestor))
				{
					ancestor.PositionChanged += WidgetRelativeTo_PositionChanged;
					ancestor.BoundsChanged += WidgetRelativeTo_PositionChanged;
				}
			}

			popup.Widget.ContainsFocusChanged += FocusChanged;
			popup.Widget.AfterDraw += Widget_Draw;

			WidgetRelativeTo_PositionChanged(anchor.Widget, null);
			anchor.Widget.Closed += Anchor_Closed;

			// When the widgets position changes, sync the popup position
			systemWindow?.AddChild(popup.Widget);

			popup.Widget.Closed += (s, e) =>
			{
				Console.WriteLine();
			};

			popup.Widget.Focus();

			popup.Widget.Invalidate();
		}

		private static void BestPopupPosition(SystemWindow systemWindow, MatePoint anchor, MatePoint popup, RectangleDouble altBounds)
		{
			// Calculate left aligned screen space position (using widgetRelativeTo.parent)
			Vector2 anchorLeft = anchor.Widget.Parent.TransformToScreenSpace(anchor.Widget.Position);
			anchorLeft += new Vector2(altBounds.Left, altBounds.Bottom);

			Vector2 popupPosition = anchorLeft;

			var bounds = altBounds == default(RectangleDouble) ? anchor.Widget.LocalBounds : altBounds;

			Vector2 xPosition = GetXAnchor(anchor.Mate, popup.Mate, popup.Widget, bounds);

			Vector2 screenPosition;

			screenPosition = anchorLeft + xPosition;

			// Constrain
			if (screenPosition.X + popup.Widget.Width > systemWindow.Width
				|| screenPosition.X < 0)
			{
				xPosition = GetXAnchor(anchor.AltMate, popup.AltMate, popup.Widget, bounds);
			}

			popupPosition += xPosition;

			Vector2 yPosition = GetYAnchor(anchor.Mate, popup.Mate, popup.Widget, bounds);

			screenPosition = anchorLeft + yPosition;

			// Constrain
			if (anchor.AltMate != null
				&& (screenPosition.Y + popup.Widget.Height > systemWindow.Height
					|| screenPosition.Y < 0))
			{
				yPosition = GetYAnchor(anchor.AltMate, popup.AltMate, popup.Widget, bounds);
			}

			popupPosition += yPosition;

			popup.Widget.Position = popupPosition;
		}

		private static Vector2 GetYAnchor(MateOptions anchor, MateOptions popup, GuiWidget popupWidget, RectangleDouble bounds)
		{
			if (anchor.Top && popup.Bottom)
			{
				return new Vector2(0, bounds.Height);
			}
			else if (anchor.Top && popup.Top)
			{
				return new Vector2(0, popupWidget.Height - bounds.Height) * -1;
			}
			else if (anchor.Bottom && popup.Top)
			{
				return new Vector2(0, -popupWidget.Height);
			}

			return Vector2.Zero;
		}

		private static Vector2 GetXAnchor(MateOptions anchor, MateOptions popup, GuiWidget popupWidget, RectangleDouble bounds)
		{
			if (anchor.Right && popup.Left)
			{
				return new Vector2(bounds.Width, 0);
			}
			else if (anchor.Left && popup.Right)
			{
				return new Vector2(-popupWidget.Width, 0);
			}
			else if (anchor.Right && popup.Right)
			{
				return new Vector2(popupWidget.Width - bounds.Width, 0) * -1;
			}

			return Vector2.Zero;
		}
	}
}