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
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl
{
	public class TextScrollWidget : GuiWidget
	{
		private object locker = new object();

		private TerminalLog terminalLog;
		private List<string> visibleLines;

		private TypeFacePrinter typeFacePrinter = null;
		private PrinterConfig printer = null;

        /// <summary>
        /// The first line to show from the existing visible lines. If -1 then show to the bottom of the list.
        /// </summary>
        private int forceStartLine = -1;

		// Text selection
		private bool dragActive = false;
		private int selectionStartLine;
		private int selectionEndLine = -1;
		private double selectionMouseDown;

		private Func<TerminalLine, string> _lineFilterFunction;

		public TextScrollWidget(PrinterConfig printer, TerminalLog terminalLog)
		{
			this.printer = printer;
			this.typeFacePrinter = new TypeFacePrinter("", new StyledTypeFace(ApplicationController.Instance.GetTypeFace("Liberation_Mono"), 12));
			this.typeFacePrinter.DrawFromHintedCache = true;
			this.terminalLog = terminalLog;
			this.visibleLines = terminalLog.AllLines().ToList();
		}

		private double documentHeight => visibleLines.Count * typeFacePrinter.TypeFaceStyle.EmSizeInPixels;

		public double Position0To1
		{
			get
			{
				if (forceStartLine == -1)
				{
					return 0;
				}
				else
				{
					return (visibleLines.Count - (double)forceStartLine) / visibleLines.Count;
				}
			}

			set
			{
				forceStartLine = (int)(visibleLines.Count * (1 - value)) - 1;
				forceStartLine = Math.Max(0, forceStartLine);
				forceStartLine = Math.Min(visibleLines.Count - 1, forceStartLine);

				// If the start would be less than one screen worth of content, allow
				// the whole screen to have content and scroll with new material.
				// Note: alternatively, if scrolled and sticking to bottom
				if (forceStartLine > visibleLines.Count - NumVisibleLines)
				{
					forceStartLine = -1;
				}

				Invalidate();
			}
		}

		public int NumVisibleLines => (int)Math.Ceiling(Height / typeFacePrinter.TypeFaceStyle.EmSizeInPixels);

		public Color TextColor { get; set; } = new Color(102, 102, 102);

		public Func<TerminalLine, string> LineFilterFunction
		{
			get => _lineFilterFunction;
			set
			{
				_lineFilterFunction = value;
				RebuildFilteredList();
			}
		}

		private bool ConditionalyAddToVisible(TerminalLine terminalLine)
		{
			var line = terminalLine.Line;

			if (LineFilterFunction != null)
			{
				line = LineFilterFunction(terminalLine);
			}

			if (!string.IsNullOrEmpty(line))
			{
				lock (locker)
				{
					visibleLines.Add(line);
				}
				return true;
			}

			return false;
		}

		private void TerminalLog_LineAdded(object sender, TerminalLine terminalLine)
		{
			if (this.ConditionalyAddToVisible(terminalLine))
			{
				this.Invalidate();
			}
		}

		private void TerminalLog_LogCleared(object sender, EventArgs e)
		{
			this.RebuildFilteredList();
		}

		public void RebuildFilteredList()
		{
			lock (locker)
			{
				visibleLines.Clear();
			}
			foreach (var lineData in terminalLog.AllTerminalLines())
			{
				ConditionalyAddToVisible(lineData);
			}
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			RectangleDouble bounds = LocalBounds;

			int numLinesToDraw = NumVisibleLines;

			int selectionStart = Math.Min(selectionStartLine, selectionEndLine);
			int selectionEnd = Math.Max(selectionStartLine, selectionEndLine);

			double y = LocalBounds.Bottom + typeFacePrinter.TypeFaceStyle.EmSizeInPixels * numLinesToDraw;
			lock (locker)
			{
				int startLineIndex = visibleLines.Count - numLinesToDraw;
				if (forceStartLine != -1)
				{
					y = LocalBounds.Top;

					if (forceStartLine > visibleLines.Count - numLinesToDraw)
					{
						forceStartLine = -1;
					}
					else
					{
						// make sure we show all the lines we can
						startLineIndex = Math.Min(forceStartLine, startLineIndex);
					}
				}

				int endLineIndex = visibleLines.Count;
				for (int lineIndex = startLineIndex; lineIndex < endLineIndex; lineIndex++)
				{
					if (lineIndex >= 0)
					{
						if (visibleLines[lineIndex] != null)
						{
							typeFacePrinter.Text = visibleLines[lineIndex];
							typeFacePrinter.Origin = new Vector2(bounds.Left + 2, y);

							// Account for text selection
							var textSelected = selectionEndLine != -1
								&& lineIndex >= selectionStart
								&& lineIndex <= selectionEnd;

							typeFacePrinter.Render(graphics2D, textSelected ? AppContext.Theme.PrimaryAccentColor : TextColor);
						}
					}

					y -= typeFacePrinter.TypeFaceStyle.EmSizeInPixels;
					if (y < -typeFacePrinter.TypeFaceStyle.EmSizeInPixels)
					{
						break;
					}
				}
			}

			base.OnDraw(graphics2D);
		}

		private int GetLineIndexFromMouse(Vector2 mousePosition)
		{
			var yPosFromTop = LocalBounds.Height - mousePosition.Y;
			var lineIndex = (int)Math.Ceiling(yPosFromTop / typeFacePrinter.TypeFaceStyle.EmSizeInPixels);

			// After the view fills up and/or when not pinned bottom, scroll offset is simply forceStartLine
			// Otherwise it's the offset from the bottom
			int scrollOffset = forceStartLine >= 0 ? forceStartLine : visibleLines.Count - NumVisibleLines;

			// Account for scroll
			return lineIndex + scrollOffset;
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			dragActive = mouseEvent.Button == MouseButtons.Left;
			selectionMouseDown = mouseEvent.Y;

			if (dragActive)
			{
				// Account for scroll
				selectionStartLine = GetLineIndexFromMouse(mouseEvent.Position);

				// Reset end index on start drag
				selectionEndLine = -1;

				this.Invalidate();
			}

			base.OnMouseDown(mouseEvent);
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			dragActive = false;
			base.OnMouseUp(mouseEvent);
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			if (dragActive)
			{
				// Recompute line index from mouse position into doc, rather than the more
				// typical start/current delta, to ensure range stay in sync with scrolling text
				selectionEndLine = GetLineIndexFromMouse(mouseEvent.Position);
				this.Invalidate();
			}

			base.OnMouseMove(mouseEvent);
		}

		public override void OnMouseWheel(MouseEventArgs mouseEvent)
		{
			base.OnMouseWheel(mouseEvent);
			var count = visibleLines.Count;
			double scrollDelta = mouseEvent.WheelDelta / (count * 60.0);

			if (scrollDelta < 0) // Rounding seems to favor scrolling up, compensating scroll down to feel as smooth
			{
				scrollDelta *= 2;
			}
			else if (Position0To1 == 0) // If we scroll up at the bottom get pop out from the "on screen" chunk
			{
				scrollDelta = NumVisibleLines / (double)count;
			}

			double newPos = Position0To1 + scrollDelta;

			if (newPos > 1)
			{
				newPos = 1;
			}
			else if (newPos < 0)
			{
				newPos = 0;
			}

			Position0To1 = newPos;
		}

		public override void OnKeyDown(KeyEventArgs keyEvent)
		{
			// make sure children controls get to try to handle this event first
			base.OnKeyDown(keyEvent);

			// check for arrow keys (but only if no modifiers are pressed)
			if (!keyEvent.Handled
				&& !keyEvent.Control
				&& !keyEvent.Alt
				&& !keyEvent.Shift)
			{
				double startingScrollPosition = Position0To1;
				double scrollDelta = NumVisibleLines / (double)visibleLines.Count;
				double newPos = Position0To1;

				switch (keyEvent.KeyCode)
				{
					case Keys.PageDown:
						newPos -= scrollDelta;
						break;
					case Keys.PageUp:
						newPos += scrollDelta;
						break;
					case Keys.Home:
						newPos = 1;
						break;
					case Keys.End:
						newPos = 0;
						break;
				}

				if (newPos > 1)
				{
					newPos = 1;
				}
				else if (newPos < 0)
				{
					newPos = 0;
				}

				Position0To1 = newPos;

				// we only handled the key if it resulted in the area scrolling
				if (startingScrollPosition != Position0To1)
				{
					keyEvent.Handled = true;
				}
			}
		}

		public override void OnKeyUp(KeyEventArgs keyEvent)
		{
			if (keyEvent.KeyCode == Keys.C
				&& keyEvent.Control
				&& Math.Abs(selectionStartLine - selectionEndLine) > 0)
			{
				int selectionStart = Math.Min(selectionStartLine, selectionEndLine);
				int selectionEnd = Math.Max(selectionStartLine, selectionEndLine);

				// Filter visiblelines to selection range
				var filteredLines = visibleLines.Where((l, i) => i >= selectionStart && i <= selectionEnd);

				if (UserSettings.Instance.Fields.GetBool(UserSettingsKey.TerminalShowInputOutputMarks, true))
				{
					// Drop leading communication/direction indicators
					filteredLines = filteredLines.Select(l => l.Substring(l.IndexOf(' ') + 1));
				}

				// Push selected text to clipboard
				Clipboard.Instance.SetText(string.Join("\r\n", filteredLines));
			}

			base.OnKeyUp(keyEvent);
		}
	}
}