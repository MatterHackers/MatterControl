/*
Copyright (c) 2022, Lars Brubaker, John Lewin
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
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.CustomWidgets
{
    public static class SettingsRowExtensions
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

					Vector2 xPosition = PopupMenu.GetXAnchor(anchor.Mate, popup.Mate, popup.Widget, bounds);

					Vector2 screenPosition;

					screenPosition = anchorLeft + xPosition;

					// Constrain
					if (screenPosition.X + popup.Widget.Width > systemWindow.Width
						|| screenPosition.X < 0)
					{
						var altXPosition = PopupMenu.GetXAnchor(anchor.AltMate, popup.AltMate, popup.Widget, bounds);

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

					Vector2 yPosition = PopupMenu.GetYAnchor(anchor.Mate, popup.Mate, popup.Widget, bounds);

					screenPosition = anchorLeft + yPosition;

					// Constrain
					if (anchor.AltMate != null
						&& (screenPosition.Y + popup.Widget.Height > systemWindow.Height
							|| screenPosition.Y < 0))
					{
						yPosition = PopupMenu.GetYAnchor(anchor.AltMate, popup.AltMate, popup.Widget, bounds);

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
	}
}
