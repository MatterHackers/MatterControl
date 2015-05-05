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

using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Reflection;

namespace MatterHackers.MatterControl.HtmlParsing
{
	[TestFixture, Category("MatterControl.HtmlParsing")]
	public class HtmlParserTests
	{

		private static string[] HtmlParserSplitOnSpacesNotInQuotes(string methodParameter)
		{
			MethodInfo method = typeof(HtmlParser).GetMethod("SplitOnSpacesNotInQuotes", BindingFlags.Static | BindingFlags.NonPublic);
			return method.Invoke(null, parameters: new object[] { methodParameter }) as string[];
		}

		[Test]
		public static void TestSplitOnSpacesNotInQuotes()
		{
			{
				string test1 = "one two three";
				string[] results = HtmlParserSplitOnSpacesNotInQuotes(test1);
				Assert.IsTrue(results.Length == 3);
				Assert.IsTrue(results[0] == "one");
				Assert.IsTrue(results[1] == "two");
				Assert.IsTrue(results[2] == "three");
			}

			{
				string test1 = "one 'two three' four";
				string[] results = HtmlParserSplitOnSpacesNotInQuotes(test1);
				Assert.IsTrue(results.Length == 3);
				Assert.IsTrue(results[0] == "one");
				Assert.IsTrue(results[1] == "'two three'");
				Assert.IsTrue(results[2] == "four");
			}

			{
				string test1 = "one 'two three''four' five";
				string[] results = HtmlParserSplitOnSpacesNotInQuotes(test1);
				Assert.IsTrue(results.Length == 3);
				Assert.IsTrue(results[0] == "one");
				Assert.IsTrue(results[1] == "'two three''four'");
				Assert.IsTrue(results[2] == "five");
			}

			{
				string test1 = "one \"two three\" four";
				string[] results = HtmlParserSplitOnSpacesNotInQuotes(test1);
				Assert.IsTrue(results.Length == 3);
				Assert.IsTrue(results[0] == "one");
				Assert.IsTrue(results[1] == "\"two three\"");
				Assert.IsTrue(results[2] == "four");
			}

			{
				string test1 = "one \"'two' three\" four";
				string[] results = HtmlParserSplitOnSpacesNotInQuotes(test1);
				Assert.IsTrue(results.Length == 3);
				Assert.IsTrue(results[0] == "one");
				Assert.IsTrue(results[1] == "\"'two' three\"");
				Assert.IsTrue(results[2] == "four");
			}

			{
				string test1 = "one '\"two\" three' four";
				string[] results = HtmlParserSplitOnSpacesNotInQuotes(test1);
				Assert.IsTrue(results.Length == 3);
				Assert.IsTrue(results[0] == "one");
				Assert.IsTrue(results[1] == "'\"two\" three'");
				Assert.IsTrue(results[2] == "four");
			}

			{
				string test1 = "<img src=\"https://lh6.ggpht.com/FMF8JYN2rGgceXpkG1GTUlmS4Z7qfron0Fm9NDi1Oqxg_TmDLMIThQuvnBXHhJD38_GK3RSnxFCX28Cp5ekxRhzx6g=s243\" alt=\"White PLA Filament - 1.75mm\" title=\"White PLA Filament - 1.75mm\" style=\"width:243px;height:183px;\">";
				string[] results = HtmlParserSplitOnSpacesNotInQuotes(test1);
				Assert.IsTrue(results.Length == 5);
				Assert.IsTrue(results[0] == "<img");
				Assert.IsTrue(results[1] == "src=\"https://lh6.ggpht.com/FMF8JYN2rGgceXpkG1GTUlmS4Z7qfron0Fm9NDi1Oqxg_TmDLMIThQuvnBXHhJD38_GK3RSnxFCX28Cp5ekxRhzx6g=s243\"");
				Assert.IsTrue(results[2] == "alt=\"White PLA Filament - 1.75mm\"");
				Assert.IsTrue(results[3] == "title=\"White PLA Filament - 1.75mm\"");
				Assert.IsTrue(results[4] == "style=\"width:243px;height:183px;\">");
			}
		}
	}
}