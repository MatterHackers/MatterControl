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

using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.CreatorPlugins;
using MatterHackers.MatterControl.PluginSystem;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text;

namespace MatterHackers.MatterControl.Plugins.BrailleBuilder
{
	public static class BrailleGrade2
	{
		static bool ranTests = false;
		[Test]
		public static void BrailGrade2Tests()
		{
			if (!ranTests)
			{
				Assert.IsTrue(ConvertWord("taylor") == "taylor");
				Assert.IsTrue(ConvertWord("Taylor") == "taylor");
				Assert.IsTrue(ConvertWord("TayLor") == "taylor");
				Assert.IsTrue(ConvertWord("energy") == "5}gy");
				Assert.IsTrue(ConvertWord("men") == "m5");
				Assert.IsTrue(ConvertWord("runabout") == "runab");
				Assert.IsTrue(ConvertWord("afternoon") == "afn");
				Assert.IsTrue(ConvertWord("really") == "re,y");
				

				ranTests = true;
			}
		}

		internal class TextMapping
		{
			internal string Key;
			internal string Value;

			internal TextMapping(string key, string value)
			{
				this.Key = key;
				this.Value = value;
			}
		}

		static List<TextMapping> anyPositionMappings = new List<TextMapping>()
		{
			new TextMapping( "about", "ab" ),
			new TextMapping( "above", "abv" ),
			new TextMapping( "according", "ac" ),
			new TextMapping( "across", "acr" ),
			new TextMapping( "after", "af" ),
			new TextMapping( "afternoon", "afn" ),
			new TextMapping( "afterward", "afw" ),
			new TextMapping( "again", "ag" ),
			new TextMapping( "against", "ag." ),
			//new TextMapping( "", "" ),
			//new TextMapping( "", "" ),
			//new TextMapping( "", "" ),
			//new TextMapping( "", "" ),
			//new TextMapping( "", "" ),
			//new TextMapping( "", "" ),
			new TextMapping( "every", "e" ),
			new TextMapping( "you", "y" ),
			new TextMapping( "en", "5" ),
			new TextMapping( "er", "}" ),
		};

		static List<TextMapping> afterTextMappings = new List<TextMapping>()
		{
			new TextMapping( "ally", ",y" ),
			//new TextMapping( "", "" ),
			//new TextMapping( "", "" ),
			//new TextMapping( "", "" ),
			//new TextMapping( "", "" ),
		};

		static List<TextMapping> beforeTextMappings = new List<TextMapping>()
		{
			//new TextMapping( "", "" ),
			//new TextMapping( "", "" ),
			//new TextMapping( "", "" ),
			//new TextMapping( "", "" ),
		};

		static List<TextMapping> betweenTextMappings = new List<TextMapping>()
		{
			//new TextMapping( "", "" ),
			//new TextMapping( "", "" ),
			//new TextMapping( "", "" ),
			//new TextMapping( "", "" ),
		};

		public static string ConvertWord(string text)
		{
			EnsureInitialized();

			string converted = text.ToLower();

			foreach (TextMapping keyValue in anyPositionMappings)
			{
				if (converted.Contains(keyValue.Key))
				{
					converted = converted.Replace(keyValue.Key, keyValue.Value);
				}
			}

			return converted;
		}

		private static bool isInitialized = false;
		private static void EnsureInitialized()
		{
			if (!isInitialized)
			{
				anyPositionMappings.Sort(SortOnKeySize);
				isInitialized = true;
			}
		}

		private static int SortOnKeySize(TextMapping x, TextMapping y)
		{
			return y.Key.Length.CompareTo(x.Key.Length);
		}

		public static string ConvertString(string text)
		{
			string[] words = text.Split(' ');

			StringBuilder finalString = new StringBuilder();
			bool first = true;

			foreach (string word in words)
			{
				if (word != null && word.Length > 0)
				{
					if (!first)
					{
						finalString.Append(" ");
					}
					finalString.Append(ConvertWord(word));

					first = false;
				}
			}

			return finalString.ToString();
		}
	}
}