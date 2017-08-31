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
using System.Text.RegularExpressions;

namespace MatterHackers.MatterControl.HtmlParsing
{
	public class HtmlParser
	{
		private const string typeNameEnd = @"[ >]";
		private static readonly Regex typeNameEndRegex = new Regex(typeNameEnd, RegexOptions.Compiled);
		private static List<string> voidElements = new List<string>() { "area", "base", "br", "col", "command", "embed", "hr", "img", "input", "keygen", "link", "meta", "param", "source", "track", "wbr" };
		private Stack<ElementState> elementQueue = new Stack<ElementState>();

		public ElementState CurrentElementState { get { return elementQueue.Peek(); } }

		public static string UrlDecode(string htmlContent)
		{
			string decoded = htmlContent.Replace("&trade;", "™");
			decoded = decoded.Replace("&nbsp;", " ");
			decoded = decoded.Replace("&copy;", "©");

			return decoded;
		}
		public void ParseHtml(string htmlContent, Action<HtmlParser, string> addContentFunction, Action<HtmlParser, string> closeContentFunction)
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
					ParseTypeContent(openPosition, closePosition, htmlContent);

					int endOfName = typeNameEndRegex.Match(htmlContent, openPosition + 1).Index;
					elementQueue.Peek().typeName = htmlContent.Substring(openPosition + 1, endOfName - (openPosition + 1));

					int nextOpenPosition = htmlContent.IndexOf('<', closePosition);
					if (closePosition + 1 < htmlContent.Length
						&& nextOpenPosition != -1)
					{
						string content = htmlContent.Substring(closePosition + 1, nextOpenPosition - closePosition - 1);
						if (!string.IsNullOrWhiteSpace(content))
						{
							addContentFunction(this, content);
						}
					}

					if (voidElements.Contains(elementQueue.Peek().typeName))
					{
						closeContentFunction(this, null);
						elementQueue.Pop();

						// get any content that is after this close but before the next open or close
						int nextOpenPosition2 = htmlContent.IndexOf('<', closePosition);
						if (nextOpenPosition2 > closePosition + 1)
						{
							string contentBetweenInsideAndEnd = htmlContent.Substring(closePosition + 1, nextOpenPosition2 - (closePosition + 1));
							addContentFunction(this, contentBetweenInsideAndEnd);
						}
					}
				}
				currentPosition = closePosition + 1;
			}
		}

		private static int SetEndAtCharacter(string content, int currentEndIndex, char characterToSkipTo)
		{
			// look for the character to skip to
			int characterToSkipToIndex = content.IndexOf(characterToSkipTo, currentEndIndex);
			if (characterToSkipToIndex == -1)
			{
				// there is no character to skip to
				currentEndIndex = content.Length - 1;
			}
			else
			{
				// move the end past the character to skip to
				currentEndIndex = Math.Min(characterToSkipToIndex + 1, content.Length - 1);
			}
			return currentEndIndex;
		}

		private static string[] SplitOnSpacesNotInQuotes(string content)
		{
			List<string> output = new List<string>();

			int currentStartIndex = 0;
			int currentEndIndex = 0;

			int nextSpaceIndex = content.IndexOf(' ', currentEndIndex);
			bool hasMoreSpaces = nextSpaceIndex != -1;

			while (hasMoreSpaces)
			{
				int nextSingleQuoteIndex = content.IndexOf('\'', currentEndIndex);
				int nextDoubleQuoteIndex = content.IndexOf('"', currentEndIndex);
				if ((nextSingleQuoteIndex != -1 && nextSingleQuoteIndex < nextSpaceIndex)
					|| (nextDoubleQuoteIndex != -1 && nextDoubleQuoteIndex < nextSpaceIndex))
				{
					// There is a quote that we need to skip before looking for spaces.
					// Skip the quote content that happens first
					if (nextDoubleQuoteIndex == -1 || (nextSingleQuoteIndex != -1 && nextSingleQuoteIndex < nextDoubleQuoteIndex))
					{
						// single quote came first
						currentEndIndex = SetEndAtCharacter(content, nextSingleQuoteIndex + 1, '\'');
					}
					else
					{
						// double quote came first
						currentEndIndex = SetEndAtCharacter(content, nextDoubleQuoteIndex + 1, '"');
					}
				}
				else
				{
					output.Add(content.Substring(currentStartIndex, nextSpaceIndex - currentStartIndex));
					currentStartIndex = nextSpaceIndex + 1;
					currentEndIndex = currentStartIndex;
				}

				// check if we are done processing the string
				nextSpaceIndex = content.IndexOf(' ', currentEndIndex);
				hasMoreSpaces = nextSpaceIndex != -1;
			}

			// put on the rest of the stirng
			if (currentStartIndex < content.Length)
			{
				output.Add(content.Substring(currentStartIndex, content.Length - currentStartIndex));
			}

			return output.ToArray();
		}

		private void ParseTypeContent(int openPosition, int closePosition, string htmlContent)
		{
			string text = htmlContent.Substring(openPosition, closePosition - openPosition);
			ElementState currentElementState = new ElementState(elementQueue.Peek());
			int afterTypeName = typeNameEndRegex.Match(htmlContent, openPosition).Index;
			if (afterTypeName < closePosition)
			{
				string content = htmlContent.Substring(afterTypeName, closePosition - afterTypeName).Trim();
				string[] splitOnSpace = SplitOnSpacesNotInQuotes(content);
				for (int i = 0; i < splitOnSpace.Length; i++)
				{
					string[] splitOnEquals = new Regex("=").Split(splitOnSpace[i]);
					string elementString = splitOnEquals[0];
					string elementValue = "";
					if (splitOnEquals.Length > 1)
					{
						elementValue = RemoveOuterQuotes(splitOnEquals[1]);
					}

					switch (elementString)
					{
						case "title":
						case "alt":
						case "html":
							break;

						case "style":
							currentElementState.ParseStyleContent(elementValue);
							break;

						case "align":
							break;

						case "class":
							{
								string[] classes = elementValue.Split(' ');
								foreach (string className in classes)
								{
									currentElementState.classes.Add(className);
								}
							}
							break;

						case "href":
							currentElementState.href = elementValue;
							break;

						case "src":
							currentElementState.src = elementValue;
							break;

						case "id":
							currentElementState.id = elementValue;
							break;

						default:
							break;
							//throw new NotImplementedException();
					}
				}
			}

			elementQueue.Push(currentElementState);
		}

		private string RemoveOuterQuotes(string inputString)
		{
			int singleQuoteIndex = inputString.IndexOf('\'');
			int doubleQuoteIndex = inputString.IndexOf('"');

			if (doubleQuoteIndex == -1 || (singleQuoteIndex != -1 && singleQuoteIndex < doubleQuoteIndex))
			{
				// single quote index is the least and exists
				int lastSingleQuoteIndex = inputString.LastIndexOf('\'');
				if (lastSingleQuoteIndex != -1 && lastSingleQuoteIndex != singleQuoteIndex)
				{
					return inputString.Substring(singleQuoteIndex+1, lastSingleQuoteIndex - singleQuoteIndex - 1);
				}
				return inputString.Substring(singleQuoteIndex+1);
			}
			else if (doubleQuoteIndex != -1)
			{
				int lastDoubleQuoteIndex = inputString.LastIndexOf('"');
				if (lastDoubleQuoteIndex != -1 && lastDoubleQuoteIndex != doubleQuoteIndex)
				{
					return inputString.Substring(doubleQuoteIndex+1, lastDoubleQuoteIndex - doubleQuoteIndex-1);
				}
				return inputString.Substring(doubleQuoteIndex+1);
			}
			else // there are no quotes return the input string
			{
				return inputString;
			}
		}
	}
}