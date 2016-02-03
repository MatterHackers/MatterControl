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

using MatterHackers.Agg;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MatterHackers.MatterControl.HtmlParsing
{
	public class ElementState
	{
		internal AlignType alignment;

		internal List<string> classes = new List<string>();

		internal string href;
		internal string id;
		internal double pointSize = 12;
		internal Point2D sizeFixed = new Point2D();
		internal Point2D sizePercent = new Point2D();
		internal string src;
		internal string typeName;

		internal VerticalAlignType verticalAlignment;

		private const string getFirstNumber = @"[0-9]+";

		private static readonly Regex getFirstNumberRegex = new Regex(getFirstNumber, RegexOptions.Compiled);

		internal ElementState()
		{
		}

		internal ElementState(ElementState copy)
		{
			alignment = copy.alignment;
			verticalAlignment = copy.verticalAlignment;
			pointSize = copy.pointSize;
			href = copy.href;
			// not part of the ongoing state
			//heightPercent = copy.heightPercent;
		}

		public enum AlignType { none, center };

		public enum VerticalAlignType { none, top };

		public AlignType Alignment { get { return alignment; } }

		public List<string> Classes { get { return classes; } }

		public string Href { get { return href; } }
		public string Id { get { return id; } }
		public double PointSize { get { return pointSize; } }
		public Point2D SizeFixed { get { return sizeFixed; } }
		public Point2D SizePercent { get { return sizePercent; } }
		public string TypeName { get { return typeName; } }

		public VerticalAlignType VerticalAlignment { get { return verticalAlignment; } }

		public void ParseStyleContent(string styleContent)
		{
			string[] splitOnSemi = styleContent.Split(';');
			for (int i = 0; i < splitOnSemi.Length; i++)
			{
				if (splitOnSemi[i].Length > 0 && splitOnSemi[i].Contains(":"))
				{
					string[] splitOnColon = splitOnSemi[i].Split(':');
					string attribute = splitOnColon[0].Trim();
					string value = splitOnColon[1];
					switch (attribute)
					{
						case "cursor":
							break;

						case "display":
							break;

						case "float":
							Console.WriteLine("Not Implemented");
							break;

						case "font-size":
							this.pointSize = GetFirstInt(value);
							break;

						case "font-weight":
							break;

						case "height":
							if (value.Contains("%"))
							{
								this.sizePercent = new Point2D(this.SizePercent.x, GetFirstInt(value));
							}
							else
							{
								this.sizeFixed = new Point2D(this.SizeFixed.x, GetFirstInt(value));
							}
							break;

						case "margin":
							break;

						case "margin-right":
						case "margin-left":
							break;

						case "width":
							if (value.Contains("%"))
							{
								this.sizePercent = new Point2D(GetFirstInt(value), this.SizePercent.y);
							}
							else
							{
								this.sizeFixed = new Point2D(GetFirstInt(value), this.SizePercent.y);
							}
							break;

						case "text-align":
							this.alignment = (ElementState.AlignType)Enum.Parse(typeof(ElementState.AlignType), value);
							break;

						case "text-decoration":
							break;

						case "vertical-align":
							this.verticalAlignment = (ElementState.VerticalAlignType)Enum.Parse(typeof(ElementState.VerticalAlignType), value);
							break;

						case "overflow":
							break;

						case "padding":
							break;

						case "'": // the ending single quote
							break;

						case "color":
							break;

						default:
							throw new NotImplementedException();
					}
				}
			}
		}

		private int GetFirstInt(string input)
		{
			Match match = getFirstNumberRegex.Match(input);
			int start = match.Index;
			int length = match.Length;
			return int.Parse(input.Substring(start, length));
		}
	}
}