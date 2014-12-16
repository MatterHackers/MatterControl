﻿/*
Copyright (c) 2014, Lars Brubaker
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
using MatterHackers.Agg.Font;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl
{
    public class TextScrollWidget : GuiWidget
    {
        string[] StartLineStringFilters = null;
        event EventHandler unregisterEvents;
        List<string> allSourceLines;
        List<string> visibleLines;

        TypeFacePrinter printer = new TypeFacePrinter();
        public RGBA_Bytes TextColor = new RGBA_Bytes(102, 102, 102);
        int forceStartLine = -1;
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
                    return ((visibleLines.Count - (double)forceStartLine) / visibleLines.Count);
                }
            }

            set
            {
                forceStartLine = (int)(visibleLines.Count * (1 - value)) - 1;
                forceStartLine = Math.Max(0, forceStartLine);
                forceStartLine = Math.Min(visibleLines.Count - 1, forceStartLine);

                // If the start would be less than one screen worth of content, allow
                // the whole screen to have content and scroll with new material.
                if (forceStartLine > visibleLines.Count - NumVisibleLines)
                {
                    forceStartLine = -1;
                }
                Invalidate();
            }
        }

        public int NumVisibleLines
        {
            get { return (int)Math.Ceiling(Height / printer.TypeFaceStyle.EmSizeInPixels); }
        }

        public TextScrollWidget(List<string> sourceLines)
        {
            printer.DrawFromHintedCache = true;
            this.allSourceLines = sourceLines;
            this.visibleLines = sourceLines;
            PrinterOutputCache.Instance.HasChanged.RegisterEvent(RecievedNewLine, ref unregisterEvents);
        }

        void ConditionalyAddToVisible(string line)
        {
            if (StartLineStringFilters != null
                && StartLineStringFilters.Length > 0)
            {
                bool lineIsVisible = true;
                foreach (string startFilter in StartLineStringFilters)
                {
                    if (line == null
                        || line.Length < 3
                        || line.StartsWith(startFilter))
                    {
                        lineIsVisible = false;
                        break;
                    }
                }

                if (lineIsVisible)
                {
                    visibleLines.Add(line);
                }
            }
        }

        void RecievedNewLine(object sender, EventArgs e)
        {
            StringEventArgs stringEvent = e as StringEventArgs;
            if (stringEvent != null)
            {
                ConditionalyAddToVisible(stringEvent.Data);
                //allSourceLines.Add(stringEvent.Data);
            }
            else // the list changed in some big way (probably cleared)
            {
                if (StartLineStringFilters != null
                    && StartLineStringFilters.Length > 0)
                {
                    CreateFilteredList();
                }
            }

            Invalidate();
        }

        void CreateFilteredList()
        {
            visibleLines = new List<string>();
            foreach (string line in allSourceLines)
            {
                ConditionalyAddToVisible(line);
            }
        }

        public void SetLineStartFilter(string[] startLineStringsToFilter)
        {
            if (startLineStringsToFilter != null
                && startLineStringsToFilter.Length > 0)
            {
                StartLineStringFilters = startLineStringsToFilter;
                CreateFilteredList();
            }
            else
            {
                visibleLines = allSourceLines;
            }
        }

        public void WriteToFile(string filePath)
        {
            System.IO.File.WriteAllLines(@filePath, allSourceLines);
        }

        public override void OnClosed(EventArgs e)
        {
            if (unregisterEvents != null)
            {
                unregisterEvents(this, null);
            }
            base.OnClosed(e);
        }

        public override void OnDraw(Graphics2D graphics2D)
        {
            RectangleDouble Bounds = LocalBounds;

            int numLinesToDraw = NumVisibleLines;

            double y = LocalBounds.Bottom + printer.TypeFaceStyle.EmSizeInPixels * numLinesToDraw;
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
                        printer.Text = visibleLines[lineIndex];
                        printer.Origin = new Vector2(Bounds.Left + 2, y);
                        printer.Render(graphics2D, TextColor);
                    }
                }
                y -= printer.TypeFaceStyle.EmSizeInPixels;
                if (y < -printer.TypeFaceStyle.EmSizeInPixels)
                {
                    break;
                }
            }

            base.OnDraw(graphics2D);
        }
    }
}
