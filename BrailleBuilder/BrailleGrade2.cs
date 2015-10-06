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
#if !__ANDROID__
using NUnit.Framework;
#endif
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

// finish a, b, t

namespace MatterHackers.MatterControl.Plugins.BrailleBuilder
{
	public static class BrailleGrade2
	{
#if !__ANDROID__
		static bool ranTests = false;
		[Test]
		public static void BrailGrade2Tests()
		{
			if (!ranTests)
			{
				Assert.IsTrue(ConvertWord("taylor") == "taylor");
				//Assert.IsTrue(ConvertWord("Taylor") == ",taylor");
				//Assert.IsTrue(ConvertWord("TayLor") == ",tay,lor");
				Assert.IsTrue(ConvertWord("energy") == "5}gy");
				Assert.IsTrue(ConvertWord("men") == "m5");
				Assert.IsTrue(ConvertWord("runabout") == "runab");
				Assert.IsTrue(ConvertWord("afternoon") == "afn");
				Assert.IsTrue(ConvertWord("really") == "re,y");
				Assert.IsTrue(ConvertWord("glance") == "gl.e");
				Assert.IsTrue(ConvertWord("station") == "/,n");
				Assert.IsTrue(ConvertWord("as") == "z");
				Assert.IsTrue(ConvertWord("abby") == "a2y");
				Assert.IsTrue(ConvertWord("here it is") == "\"h x is");
				//Matt Implemented
				Assert.IsTrue(ConvertWord("commitment") == "-mit;t");
				Assert.IsTrue(ConvertWord("mother") == "\"m");
				Assert.IsTrue(ConvertWord("myself") == "myf");
				Assert.IsTrue(ConvertWord("lochness") == "lo*;s");
				Assert.IsTrue(ConvertWord("Seven o'clock") == ",sev5 o'c");

				ranTests = true;
			}
		}
#endif

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

		static List<TextMapping> exactTextMappings = new List<TextMapping>()
		{
			//new TextMapping( "as", "z" ),
			//new TextMapping( "be", "2" ),
			//new TextMapping( "but", "b" ),
			////new TextMapping( "by", "0" ), // must hava word after and then does not leave the space
			//new TextMapping( "that", "t" ),
			//new TextMapping( "this", "?" ),
			////new TextMapping( "to", "6" ), // must hava word after and then does not leave the space
			////new TextMapping( "", "" ),
			////new TextMapping( "", "" ),
			////new TextMapping( "", "" ),
		};

		static List<TextMapping> anyPositionMappings = new List<TextMapping>()
		{
			//// a's
			//new TextMapping( "about", "ab" ),
			//new TextMapping( "above", "abv" ),
			//new TextMapping( "according", "ac" ),
			//new TextMapping( "across", "acr" ),
			//new TextMapping( "after", "af" ),
			//new TextMapping( "afternoon", "afn" ),
			//new TextMapping( "afterward", "afw" ),
			//new TextMapping( "again", "ag" ),
			//new TextMapping( "against", "ag." ),
			//new TextMapping( "almost", "alm" ),
			//new TextMapping( "already", "alr" ),
			//new TextMapping( "also", "al" ),
			//new TextMapping( "although", "al?" ),
			//new TextMapping( "altogether", "alt" ),
			//new TextMapping( "always", "alw" ),
			//new TextMapping( "and", "&" ),
			//new TextMapping( "ar", ">" ),
			//// b's
			//new TextMapping( "because", "2c" ),
			//new TextMapping( "before", "2f" ),
			//new TextMapping( "behind", "2h" ),
			//new TextMapping( "below", "2l" ),
			//new TextMapping( "beneath", "2n" ),
			//new TextMapping( "beside", "2s" ),
			//new TextMapping( "between", "2t" ),
			//new TextMapping( "beyond", "2y" ),
			//new TextMapping( "blind", "bl" ),
			//new TextMapping( "braille", "brl" ),
			//// c's
			//// e's
			//new TextMapping( "en", "5" ),
			//new TextMapping( "er", "}" ),
			//new TextMapping( "every", "e" ),
			//// h's
			//new TextMapping( "here", "\"h" ),
			//// i's
			//new TextMapping( "it", "x"),
			//// o's
			//new TextMapping( "of", "("),

			//// s's
			//new TextMapping( "st", "/" ),
			//// t's
			//new TextMapping( "th", "?" ),
			//new TextMapping( "the", "!" ),
			//new TextMapping( "their", "_!" ),
			//new TextMapping( "themselves", "!mvs" ),
			//new TextMapping( "there", "\"!" ),
			//new TextMapping( "those", "~?" ),
			//new TextMapping( "through", "_?" ),
			//new TextMapping( "thyself", "?yf" ),
			//new TextMapping( "time", "\"t" ),
			//new TextMapping( "today", "td" ),
			//new TextMapping( "together", "tgr" ),
			//new TextMapping( "tomorrow", "tm" ),
			//new TextMapping( "tonight", "tn" ),
			////new TextMapping( "", "" ),
			//// y's
			//new TextMapping( "you", "y" ),
		};

		static List<TextMapping> afterTextMappings = new List<TextMapping>()
		{
			//new TextMapping( "ally", ",y" ),
			//new TextMapping( "ance", ".e" ),
			//new TextMapping( "ation", ",n" ),
			//new TextMapping( "ble", "#" ),
			//new TextMapping( "tion", ";n" ),
			////new TextMapping( "", "" ),
			////new TextMapping( "", "" ),
		};

		static List<TextMapping> beforeTextMappings = new List<TextMapping>()
		{
			//new TextMapping( "be", "2" ), // is aften not first in precidence (so not enabled for now)
			//new TextMapping( "", "" ),
			//new TextMapping( "", "" ),
		};

		static List<TextMapping> betweenTextMappings = new List<TextMapping>()
		{
			//new TextMapping( "bb", "2" ),
			////new TextMapping( "", "" ),
			////new TextMapping( "", "" ),
			////new TextMapping( "", "" ),
			////new TextMapping( "", "" ),
		};

		public static string ConvertWord(string text)
		{
			EnsureInitialized();

			// put in commas before capitals
			string converted = Regex.Replace(text, "([A-Z])", ",$1");
			converted = converted.ToLower();

			// do the replacements that must be the complete word by itself
			foreach (TextMapping keyValue in exactTextMappings)
			{
				if (converted == keyValue.Key)
				{
					converted = keyValue.Value;
					return converted;
				}
			}

			// do the replacements that must come after other characters
			string tempAfterFirstCharacter = converted.Substring(1);
			foreach (TextMapping keyValue in afterTextMappings)
			{
				if (tempAfterFirstCharacter.Contains(keyValue.Key))
				{
					converted = converted.Substring(0, 1) + tempAfterFirstCharacter.Replace(keyValue.Key, keyValue.Value);
					tempAfterFirstCharacter = converted.Substring(1);
				}
			}

			// do the replacements that must come after other characters
			string tempBeforeLastCharacter = converted.Substring(0,converted.Length-1);
			foreach (TextMapping keyValue in beforeTextMappings)
			{
				if (tempBeforeLastCharacter.Contains(keyValue.Key))
				{
					converted = tempBeforeLastCharacter.Replace(keyValue.Key, keyValue.Value) + converted[converted.Length-1];
					tempBeforeLastCharacter = converted.Substring(0, converted.Length - 1);
				}
			}

			// do the replacements that can go anywhere
			foreach (TextMapping keyValue in anyPositionMappings)
			{
				if (converted.Contains(keyValue.Key))
				{
					converted = converted.Replace(keyValue.Key, keyValue.Value);
				}
			}

			if (converted.Length > 2)
			{
				// do the replacements that must come after and before other characters
				string tempMiddleCharacters = converted.Substring(1, converted.Length - 2);
				foreach (TextMapping keyValue in betweenTextMappings)
				{
					if (tempMiddleCharacters.Contains(keyValue.Key))
					{
						int findPosition = tempMiddleCharacters.IndexOf(keyValue.Key);
						int afterReplacemntStart = 1 + findPosition + keyValue.Key.Length;
						int afterReplacementLength = converted.Length - afterReplacemntStart;
						converted = converted.Substring(0, 1) + tempMiddleCharacters.Replace(keyValue.Key, keyValue.Value) + converted.Substring(afterReplacemntStart, afterReplacementLength);
						tempMiddleCharacters = converted.Substring(1, converted.Length - 2);
					}
				}
			}

			return converted;
		}

		private static bool isInitialized = false;
		private static void EnsureInitialized()
		{
			if (!isInitialized)
			{
				ConvertMappingStringToList();
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

		private static void ConvertMappingStringToList()
		{
			string[] conversions = BrailleGrade2Mapping.mappingTable.Split('\n'); 

			foreach(string line in conversions)
			{
				if(line != null && line.Length>0)
				{
					string[] keyConversionPair = line.Split(' ');
					if(keyConversionPair.Length == 2 && keyConversionPair[0] != null && keyConversionPair[0].Length > 0 && keyConversionPair[1] != null && keyConversionPair[1].Length >0)
					{
						if(keyConversionPair[0] != "//")
						{
							TextMapping mapping = new TextMapping(keyConversionPair[0],keyConversionPair[1]);
							if(mapping.Key == mapping.Key.ToUpper())//if in all caps it is an exact match
							{
								exactTextMappings.Add(mapping);
							}
							else if(mapping.Key[0] == '+' && mapping.Key[mapping.Key.Length-1] == '+')//check between
							{
								mapping.Key = mapping.Key.Trim('+');
								betweenTextMappings.Add(mapping);
							}
							else if (mapping.Key[0] == '+') 
							{
								mapping.Key = mapping.Key.Trim('+');
								afterTextMappings.Add(mapping);
							}
							else if(mapping.Key[mapping.Key.Length-1] == '+')
							{
								mapping.Key = mapping.Key.Trim('+');
								beforeTextMappings.Add(mapping);
							}
							else if(mapping.Key.Contains("*"))
							{
								//TODO - implement words before/after key
							}
							else//if not a special type then it is an anyPositionMapping
							{
								anyPositionMappings.Add(mapping);
							}
						}
					}
				}
			}

		}
	}
}