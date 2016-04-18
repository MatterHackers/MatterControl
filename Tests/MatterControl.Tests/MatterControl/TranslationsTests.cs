using MatterHackers.MatterControl;
using NUnit.Framework;
using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Linq;

namespace MatterControl.Tests.MatterControl
{

    [TestFixture]
    public class TranslationsTests
    {
        [Test, Category("Translations")]
        public void EnglishLinesOnlyContainEnglishCharachters()
        {

            var currentDirectory = new DirectoryInfo(Directory.GetCurrentDirectory());
            string pathToMatterControlFolder = currentDirectory.Parent.Parent.Parent.Parent.FullName;
            string translationsPath = @"StaticData\Translations";
            string fullPath = Path.Combine(pathToMatterControlFolder,translationsPath);

            string[] translationFiles = Directory.GetDirectories(fullPath);
            string translationsText = @"Translation.txt";

            foreach (string file in translationFiles)
            {
                string fullPathToEachTranslation = Path.Combine(file, translationsText);
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
