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
using System.Text.RegularExpressions;

using MatterHackers.Agg;
using MatterHackers.Agg.UI;

namespace MatterHackers.MatterControl.HtmlParsing
{
    public class ElementState
    {
        public enum AlignType { none, center };
        public enum VerticalAlignType { none, top };

        internal List<string> classes = new List<string>();
        public List<string> Classes { get { return classes; } }
        internal string typeName;
        public string TypeName { get { return typeName; } }

        internal string href;
        public string Href { get { return href; } }

        internal string id;
        public string Id { get { return id; } }

        internal AlignType alignment;
        public AlignType Alignment { get { return alignment; } }
        internal VerticalAlignType verticalAlignment;
        public VerticalAlignType VerticalAlignment { get { return verticalAlignment; } }

        internal double pointSize = 12;
        public double PointSize { get { return pointSize; } }
        internal int heightPercent = 0;
        public int HeightPercent { get { return heightPercent; } }

        internal ElementState()
        {
        }

        internal ElementState(ElementState copy)
        {
            alignment = copy.alignment;
            verticalAlignment = copy.verticalAlignment;
            pointSize = copy.pointSize;
            // not part of the ongoing state
            //heightPercent = copy.heightPercent;
        }
    }

    public class HtmlParser
    {
        Stack<ElementState> elementQueue = new Stack<ElementState>();
        public ElementState CurrentElementState { get { return elementQueue.Peek(); } }

        public delegate void ProcessContent(HtmlParser htmlParser, string content);

        const string typeNameEnd = @"[ >]";
        private static readonly Regex typeNameEndRegex = new Regex(typeNameEnd, RegexOptions.Compiled);
        public void ParseHtml(string htmlContent, ProcessContent addContentFunction, ProcessContent closeContentFunction)
        {
            elementQueue.Push(new ElementState());

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
                    closeContentFunction(this, null);
                    elementQueue.Pop();

                    // get any content that is after this close but before the next open or close
                    int nextOpenPosition = htmlContent.IndexOf('<', closePosition);
                    if (nextOpenPosition > closePosition + 1)
                    {
                        string contentBetweenInsideAndEnd = htmlContent.Substring(closePosition + 1, nextOpenPosition - (closePosition + 1));
                        addContentFunction(this, contentBetweenInsideAndEnd);
                    }
                }
                else
                {
                    ParesTypeContent(openPosition, closePosition, htmlContent);

                    int endOfName = typeNameEndRegex.Match(htmlContent, openPosition + 1).Index;
                    elementQueue.Peek().typeName = htmlContent.Substring(openPosition + 1, endOfName - (openPosition + 1));

                    int nextOpenPosition = htmlContent.IndexOf('<', closePosition);
                    string content = htmlContent.Substring(closePosition + 1, nextOpenPosition - closePosition - 1);
                    addContentFunction(this, content);
                }
                currentPosition = closePosition + 1;
            }
        }

        private void ParesTypeContent(int openPosition, int closePosition, string htmlContent)
        {
            string text = htmlContent.Substring(openPosition, closePosition - openPosition);
            ElementState style = new ElementState(elementQueue.Peek());
            int afterTypeName = typeNameEndRegex.Match(htmlContent, openPosition).Index;
            if (afterTypeName < closePosition)
            {
                string content = htmlContent.Substring(afterTypeName, closePosition - afterTypeName).Trim();
                string[] splitOnSpace = new Regex("' ").Split(content);
                for (int i = 0; i < splitOnSpace.Length; i++)
                {
                    string[] splitOnEquals = new Regex("='").Split(splitOnSpace[i]);
                    switch (splitOnEquals[0])
                    {
                        case "style":
                            ParseStyleContent(splitOnEquals[1].Substring(0, splitOnEquals[1].Length - 1), style);
                            break;

                        case "align":
                            break;

                        case "class":
                            {
                                string[] classes = splitOnEquals[1].Split(' ');
                                foreach (string className in classes)
                                {
                                    style.classes.Add(className);
                                }
                            }
                            break;

                        case "href":
                            style.href = splitOnEquals[1].Substring(0, splitOnEquals[1].Length - 1);
                            break;

                        case "id":
                            style.id = splitOnEquals[1].Substring(0, splitOnEquals[1].Length - 1);
                            break;

                        default:
                            throw new NotImplementedException();
                    }
                }
            }

            elementQueue.Push(style);
        }

        private void ParseStyleContent(string styleContent, ElementState style)
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
                            style.alignment = (ElementState.AlignType)Enum.Parse(typeof(ElementState.AlignType), splitOnColon[1]);
                            break;

                        case "font-size":
                            style.pointSize = int.Parse(splitOnColon[1].Substring(0, splitOnColon[1].Length - 2));
                            break;

                        case "vertical-align":
                            style.verticalAlignment = (ElementState.VerticalAlignType)Enum.Parse(typeof(ElementState.VerticalAlignType), splitOnColon[1]);
                            break;

                        default:
                            throw new NotImplementedException();
                    }
                }
            }
        }
    }
}
