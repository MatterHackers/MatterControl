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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using MatterHackers.Agg;
using MatterHackers.Agg.UI;

namespace MatterHackers.MatterControl
{
    public class WidgetFromHtml
    {
        internal class StyleState
        {
            internal enum AlignType { none, center };
            internal enum VerticalAlignType { none, top };

            internal AlignType alignment;
            internal VerticalAlignType vertiaclAlignment;
            
            internal double pointSize = 12;
            internal int heightPercent = 0;

            internal StyleState()
            {
            }

            internal StyleState(StyleState copy)
            {
                alignment = copy.alignment;
                vertiaclAlignment = copy.vertiaclAlignment;
                pointSize = copy.pointSize;
                heightPercent = copy.heightPercent;
            }
        }

        Stack<StyleState> styleQueue = new Stack<StyleState>();

        public delegate string ProcessContent(string content);

        class ClassToFunctionMapping
        {
            string className;
            ProcessContent function;

            public ClassToFunctionMapping(string className, ProcessContent function)
            {
                this.className = className;
                this.function = function;
            }
        }

        List<WidgetFromHtml.ClassToFunctionMapping> classFunctionMapping = new List<WidgetFromHtml.ClassToFunctionMapping>();

        public void AddMapping(string className, ProcessContent function)
        {
            classFunctionMapping.Add(new ClassToFunctionMapping(className, function));
        }

        GuiWidget widgetBeingCreated;
        public GuiWidget CreateWidget(string htmlContent)
        {
            styleQueue.Push(new StyleState());
            widgetBeingCreated = new FlowLayoutWidget(FlowDirection.TopToBottom);

            int currentPosition = 0;
            while (currentPosition < htmlContent.Length)
            {
                int openPosition = htmlContent.IndexOf('<', currentPosition);
                if (openPosition == -1)
                {
                    break;
                }
                int closePosition = htmlContent.IndexOf('>', openPosition);
                if (htmlContent[openPosition + 1] == '/')
                {
                    styleQueue.Pop();
                }
                else
                {
                    ParesTypeContent(openPosition, closePosition, htmlContent);

                    if(htmlContent.Substring(openPosition+1).StartsWith("span"))
                    {
                        int nextOpenPosition = htmlContent.IndexOf('<', closePosition);
                        AddContent(htmlContent.Substring(closePosition, nextOpenPosition - closePosition));
                    }
                }
                currentPosition = closePosition + 1;
            }

            return widgetBeingCreated;
        }

        private void AddContent(string htmlContent)
        {
            widgetBeingCreated.AddChild(new TextWidget(htmlContent));
        }

        private void ParesTypeContent(int openPosition, int closePosition, string htmlContent)
        {
            string text = htmlContent.Substring(openPosition, closePosition - openPosition);
            StyleState style = new StyleState(styleQueue.Peek());
            int afterType = htmlContent.IndexOf(' ', openPosition);
            if (afterType < closePosition)
            {
                string content = htmlContent.Substring(afterType, closePosition - afterType).Trim();
                string[] splitOnEquals = content.Split('=');
                switch (splitOnEquals[0])
                {
                    case "style":
                        ParseStyleContent(splitOnEquals[1].Substring(1, splitOnEquals[1].Length - 2), style);
                        break;
                }
            }

            styleQueue.Push(style);
        }

        private void ParseStyleContent(string styleContent, StyleState style)
        {
            string[] splitOnSemi = styleContent.Split(';');
            for (int i = 0; i < splitOnSemi.Length; i++)
            {
                if (splitOnSemi[i].Length > 0)
                {
                    string[] splitOnColon = splitOnSemi[i].Split(':');
                    switch (splitOnColon[0])
                    {
                        case "height":
                            style.heightPercent = int.Parse(splitOnColon[1].Substring(0, splitOnColon[1].Length - 1));
                            break;

                        case "text-align":
                            style.alignment = (StyleState.AlignType)Enum.Parse(typeof(StyleState.AlignType), splitOnColon[1]);
                            break;

                        case "font-size":
                            style.pointSize = int.Parse(splitOnColon[1].Substring(0, splitOnColon[1].Length - 2));
                            break;

                        case "vertical-align":
                            style.vertiaclAlignment = (StyleState.VerticalAlignType)Enum.Parse(typeof(StyleState.VerticalAlignType), splitOnColon[1]);
                            break;

                        default:
                            throw new NotImplementedException();
                    }
                }
            }
        }
    }
}
