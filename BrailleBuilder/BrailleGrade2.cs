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
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using MatterHackers.Agg;

// finish a, b, t

namespace MatterHackers.MatterControl.Plugins.BrailleBuilder
{
	public static class BrailleGrade2
	{
		internal class TextMapping
		{
			internal string Key;
			internal string Value;

			internal TextMapping(string key, string value)
			{
				this.Key = key;
				this.Value = value;
			}

			public override string ToString()
			{
				return "{0} -> {1}".FormatWith(Key, Value);
			}
		}

		static List<TextMapping> exactTextMappings = new List<TextMapping>();
		static List<TextMapping> anyPositionMappings = new List<TextMapping>();
		static List<TextMapping> afterTextMappings = new List<TextMapping>();
		static List<TextMapping> beforeTextMappings = new List<TextMapping>();
		static List<TextMapping> betweenTextMappings = new List<TextMapping>();
		static List<TextMapping> beforeWordsMappings = new List<TextMapping>();
		static List<TextMapping> numberMappngs = new List<TextMapping>();
		static List<TextMapping> postWordPunctuationMappings = new List<TextMapping>();

		public static string ConvertWord(string text, bool isLastWord = false)
		{
			EnsureInitialized();
			string converted = text;

			//Convert numbers and punctuation, skip words startign with # in the contractions
			if (IsNumeric(converted))
			{
				string numConversion = ("#" + converted);

				foreach(TextMapping keyValue in numberMappngs)
				{
					numConversion = numConversion.Replace(keyValue.Key, keyValue.Value);
				}

				converted = numConversion;
			}

			foreach (TextMapping keyValue in postWordPunctuationMappings)
			{
				if (converted[converted.Length - 1] == keyValue.Key[0])
				{
					converted = converted.Substring(0, converted.Length - 1) + keyValue.Value;
				}
			}

			if(converted[0] != '#')
			{ 
				

				// put in commas before capitals
				converted = Regex.Replace(converted, "([A-Z])", ",$1");
				converted = converted.ToLower();

				// do the replacements that must be the complete word by itself
				foreach (TextMapping keyValue in exactTextMappings)
				{
					if (compareStringIgnoringPunctuation(converted, keyValue.Key))
					{
						converted = converted.Replace(keyValue.Key, keyValue.Value);
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
							converted = converted.Substring(0, 1) + tempMiddleCharacters.Replace(keyValue.Key, keyValue.Value) + converted.Substring(converted.Length-1, 1);						
						}
					}
				}

				if (!isLastWord)
				{
					foreach (TextMapping keyValue in beforeWordsMappings)
					{
						if (converted == keyValue.Key)
						{
							converted = keyValue.Value;
							return converted;
						}
					}
				}
			}
			return converted;
		}

		private static bool compareStringIgnoringPunctuation(string possiblyPunctuatedString, string targetExactMatch)
		{			
			string punctuationStrippedString = possiblyPunctuatedString;

			if (Char.IsPunctuation(punctuationStrippedString[0]))
			{
				punctuationStrippedString = punctuationStrippedString.TrimStart(punctuationStrippedString[0]);
			}

			if (Char.IsPunctuation(punctuationStrippedString[punctuationStrippedString.Length - 1]))
			{
				punctuationStrippedString = punctuationStrippedString.TrimEnd(punctuationStrippedString[punctuationStrippedString.Length - 1]);
			}


			return punctuationStrippedString == targetExactMatch;
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
			bool skipSpace = false;
			int numWords = words.Length;
			int wordIndex = 0;

			foreach (string word in words)
			{
				wordIndex++;
				if (word != null && word.Length > 0)
				{
					if (!first && !skipSpace)
					{
						finalString.Append(" ");
						skipSpace = false;
					}
					skipSpace = checkSkipSpace(word, wordIndex != numWords?words[wordIndex]: null);
					finalString.Append(ConvertWord(word, wordIndex == numWords));

					first = false;
				}
			}

			string output = finalString.ToString();
			return output;
		}

		private static bool checkSkipSpace(string word, string nextWord)
		{
			bool skipSpace = false;
			if(nextWord != null)
			{
				foreach (TextMapping keyValue in beforeWordsMappings)
				{
					if (word == keyValue.Key)
					{
						skipSpace = true;
					}
				}

				if(skipSpace)//Special Case: The word 'for' only skips if followed by the word 'the'
				{					
					skipSpace = word != "for"|| nextWord == "the";					
				}
			}			

			return skipSpace;
		}

		private static void ConvertMappingStringToList()
		{
			string[] conversions = BrailleGrade2Mapping.mappingTable.Split('\n'); 

			foreach(string inLine in conversions)
			{
				string line = inLine.Replace("\r", "").Trim();
				if(line != null && line.Length>0)
				{
					string[] keyConversionPair = line.Split(' ');
					if(keyConversionPair.Length == 2 && keyConversionPair[0] != null && keyConversionPair[0].Length > 0 && keyConversionPair[1] != null && keyConversionPair[1].Length >0)
					{
						if(keyConversionPair[0] != "//")
						{
							TextMapping mapping = new TextMapping(keyConversionPair[0],keyConversionPair[1]);

							if(IsNumeric(mapping.Key))
							{
								numberMappngs.Add(mapping);
							}							
							else if(mapping.Key == mapping.Key.ToUpper())//if in all caps it is an exact match
							{
								mapping.Key = mapping.Key.ToLower();
								if (mapping.Key.Contains("*"))
								{
									if (mapping.Key[0] == '*')
									{
										mapping.Key = mapping.Key.Trim('*');
										postWordPunctuationMappings.Add(mapping);
									}
									else
									{
										mapping.Key = mapping.Key.Trim('*');
										beforeWordsMappings.Add(mapping);
									}
								}
								else
								{
									exactTextMappings.Add(mapping);
								}
								
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
								if(mapping.Key[0] == '*')
								{
									mapping.Key = mapping.Key.Trim('*');
									postWordPunctuationMappings.Add(mapping);
								}
								else
								{
									mapping.Key = mapping.Key.Trim('*');
									beforeWordsMappings.Add(mapping);
								}
								
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

		private static bool IsNumeric(string p)
		{			
			bool isNumeric = true;

			for(int i = 0; i < p.Length && isNumeric; i++)
			{
				char current = p[i];
				isNumeric = (Char.IsNumber(current) || char.IsPunctuation(current)) && current != '*';
			}
			
			return isNumeric;
		}

	}
}