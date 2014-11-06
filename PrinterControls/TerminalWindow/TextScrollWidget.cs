/*
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
using MatterHackers.Agg;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl
{
    public class TextScrollWidget : GuiWidget
    {
        const int TOTOL_POW2 = 64;
        int lineCount = 0;
        string[] lines = new string[TOTOL_POW2];

        public RGBA_Bytes TextColor = new RGBA_Bytes(102, 102, 102);

        public TextScrollWidget()
        {
        }

        public void WriteLine(Object sender, EventArgs e)
        {
            StringEventArgs lineString = e as StringEventArgs;
            Write(lineString.Data + "\n");
        }

        TypeFacePrinter printer = new TypeFacePrinter();
        public void Write(string lineString)
        {
            string[] splitOnNL = lineString.Split('\n');
            foreach (string line in splitOnNL)
            {
                if (line.Length > 0)
                {
                    printer.Text = line;
                    Vector2 stringSize = printer.GetSize();

                    int arrayIndex = (lineCount % TOTOL_POW2);
                    lines[arrayIndex] = line;

                    lineCount++;
                }
            }

            Invalidate();
        }

        public void WriteToFile(string filePath)
        {
            System.IO.File.WriteAllLines(@filePath, lines);
        }

        public override void OnDraw(Graphics2D graphics2D)
        {
            TypeFacePrinter printer = new TypeFacePrinter();
            printer.DrawFromHintedCache = true;

            RectangleDouble Bounds = LocalBounds;

            double y = LocalBounds.Bottom + printer.TypeFaceStyle.EmSizeInPixels * (TOTOL_POW2 - 1) + 5;
            for (int index = lineCount; index < lineCount + TOTOL_POW2; index++)
            {
                if (y > LocalBounds.Top)
                {
                    y -= printer.TypeFaceStyle.EmSizeInPixels;
                    continue;
                }
                int arrayIndex = (index % TOTOL_POW2);
                if (lines[arrayIndex] != null)
                {
                    printer.Text = lines[arrayIndex];
                    printer.Origin = new Vector2(Bounds.Left + 2, y);
                    printer.Render(graphics2D, TextColor);
                }
                y -= printer.TypeFaceStyle.EmSizeInPixels;
                if (y < -printer.TypeFaceStyle.EmSizeInPixels)
                {
                    break;
                }
            }

            base.OnDraw(graphics2D);
        }

        public void Clear()
        {
            for (int index = 0; index < TOTOL_POW2; index++)
            {
                lines[index] = "";
            }
            lineCount = 0;
        }
    }
}
