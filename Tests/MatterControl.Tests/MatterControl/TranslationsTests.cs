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

using MatterHackers.MatterControl;
using NUnit.Framework;
using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Linq;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.Tests.Automation;

namespace MatterControl.Tests.MatterControl
{
	[TestFixture]
	public class TranslationsTests
	{
		[Test, Category("Translations")]
		public void RelativeFriendlyDatesTest()
		{
			{
				// May 28, 2016 at 3:13 pm
				DateTime nowTime = new DateTime(2016, 05, 28, 15, 13, 37);

				DateTime testTime = nowTime.AddMinutes(-63);
				Assert.IsTrue(RelativeTime.GetTimeBlock(nowTime, testTime) == TimeBlock.Today);
				Assert.IsTrue(RelativeTime.GetDetail(nowTime, testTime) == "2:10 PM");

				testTime = nowTime.AddHours(-25);
				Assert.IsTrue(RelativeTime.GetTimeBlock(nowTime, testTime) == TimeBlock.Yesterday);
				Assert.IsTrue(RelativeTime.GetDetail(nowTime, testTime) == "2:13 PM");

				testTime = nowTime.AddDays(-4);
				Assert.IsTrue(RelativeTime.GetTimeBlock(nowTime, testTime) == TimeBlock.SameWeek);
				Assert.IsTrue(RelativeTime.GetDetail(nowTime, testTime) == "Tuesday 3:13 PM");

				testTime = nowTime.AddDays(-6);
				Assert.IsTrue(RelativeTime.GetTimeBlock(nowTime, testTime) == TimeBlock.SameWeek);
				Assert.IsTrue(RelativeTime.GetDetail(nowTime, testTime) == "Sunday 3:13 PM");

				testTime = nowTime.AddDays(-7);
				Assert.IsTrue(RelativeTime.GetTimeBlock(nowTime, testTime) == TimeBlock.SameMonth);
				Assert.IsTrue(RelativeTime.GetDetail(nowTime, testTime) == "May 21, 3:13 PM");

				testTime = nowTime.AddDays(-37);
				Assert.IsTrue(RelativeTime.GetTimeBlock(nowTime, testTime) == TimeBlock.SameYear);
				Assert.IsTrue(RelativeTime.GetDetail(nowTime, testTime) == "April 21, 3:13 PM");

				testTime = nowTime.AddDays(-364);
				Assert.IsTrue(RelativeTime.GetTimeBlock(nowTime, testTime) == TimeBlock.PastYear);
				Assert.IsTrue(RelativeTime.GetDetail(nowTime, testTime) == "2015 May 30, 3:13 PM");
			}

			// make a grouped list
			{
				// May 28, 2016 at 3:13 pm
				DateTime nowTime = new DateTime(2016, 05, 28, 15, 13, 37);
				List<DateTime> allTimes = new List<DateTime>()
				{
					nowTime.AddMinutes(-63),
					nowTime.AddMinutes(-82),
					nowTime.AddHours(-25),
					nowTime.AddHours(-31),
					nowTime.AddDays(-4),
					nowTime.AddDays(-6),
					nowTime.AddDays(-7),
					nowTime.AddDays(-37),
					nowTime.AddDays(-364),
				};

				var orderedForDisplay = RelativeTime.GroupTimes(nowTime, allTimes);
				Assert.IsTrue(orderedForDisplay.Count == 6);
				Assert.IsTrue(orderedForDisplay[TimeBlock.Today].Count == 2);
				Assert.IsTrue(orderedForDisplay[TimeBlock.Yesterday].Count == 2);
				Assert.IsTrue(orderedForDisplay[TimeBlock.SameWeek].Count == 2);
				Assert.IsTrue(orderedForDisplay[TimeBlock.SameMonth].Count == 1);
				Assert.IsTrue(orderedForDisplay[TimeBlock.SameYear].Count == 1);
				Assert.IsTrue(orderedForDisplay[TimeBlock.PastYear].Count == 1);
			}
		}
	

		[Test, Category("Translations")]
        public void EnglishLinesOnlyContainEnglishCharachters()
        {
            string fullPath = TestContext.CurrentContext.ResolveProjectPath(4, "StaticData", "Translations");

            foreach (string directory in Directory.GetDirectories(fullPath))
            {
                string fullPathToEachTranslation = Path.Combine(directory, "Translation.txt");
                Console.Write(fullPathToEachTranslation);
                readTranslationFile(fullPathToEachTranslation);
            }
            
            
            /*File.ReadAllLines(fullPath).Where(s => s.StartsWith("English:")).Select(s =>
            {
                return s.Replace("English:", "").Trim();
            })
            .Where(s => 
                {
                    var items = s.ToCharArray().Select(c => (int)c);
                    var result1 = items.Where(i => i > 127);
                    var result2 = result1.Any();

                    return result2;
                }).ToArray();//);*/

            //checkForNonEnglishCharacters(fullPath);

        }


        public void readTranslationFile(string pathToTranslations)
        {
            bool hasInvalid;

            foreach (string s in File.ReadAllLines(pathToTranslations))
            {

                var k = s;
                if (k.StartsWith("English:"))
                {

                    k = k.Replace("English:", "").Trim();
                    var chars = k.ToCharArray();
                    var ints = chars.Select(c => (int)c).ToArray();

                    hasInvalid = checkForInvalidCharacters(ints);
                    if (hasInvalid)
                    {

                        string result = hasInvalid.ToString();
                        string fullResult = String.Format("{0}:  {1}", k, result);
                        Console.WriteLine(fullResult);

                    }

                }
            }   
        }

        public bool checkForInvalidCharacters(int[] bytesInCharacter)
        {

                bool hasInvalidCharacter = false;

                foreach (int i in bytesInCharacter)
                {

                    if (i > 127)
                    {

                        hasInvalidCharacter = true;
                        return hasInvalidCharacter;
                        
                    }
                }

            return hasInvalidCharacter;

        }
    }
}
