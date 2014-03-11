using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Threading;
using System.IO;

using MatterHackers.Agg;

namespace MatterHackers.Localizations
{
    public class TranslationMap
    {
        Dictionary<string, string> translationDictionary = new Dictionary<string, string>();
        Dictionary<string, string> masterDictionary = new Dictionary<string, string>();

        string pathToTranslationFile;
        string pathToMasterFile;

        string twoLetterIsoLanguageName;
        public string TwoLetterIsoLanguageName
        {
            get { return twoLetterIsoLanguageName; }
        }

        public TranslationMap(string pathToTranslationsFolder, string twoLetterIsoLanguageName = "")
        {
            if (twoLetterIsoLanguageName == "")
            {
                twoLetterIsoLanguageName = Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName;
            }

            LoadTranslation(pathToTranslationsFolder, twoLetterIsoLanguageName);
        }

        void ReadIntoDictonary(Dictionary<string, string> dictionary, string pathAndFilename)
        {
            string[] lines = File.ReadAllLines(pathAndFilename);
            for (int i = 0; i < lines.Length; i+=3)
            {
                string englishString = lines[i].Substring("english:".Length);
                string translatedString = lines[i+1].Substring("translated:".Length);
                dictionary.Add(DecodeWhileReading(englishString), DecodeWhileReading(translatedString));
            }
        }

        public void LoadTranslation(string pathToTranslationsFolder, string twoLetterIsoLanguageName)
        {
            this.pathToMasterFile = Path.Combine(pathToTranslationsFolder, "Master.txt");
            ReadIntoDictonary(masterDictionary, pathToMasterFile);

            this.pathToTranslationFile = Path.Combine(pathToTranslationsFolder, twoLetterIsoLanguageName, "Translation.txt");
            ReadIntoDictonary(translationDictionary, pathToTranslationFile);

            foreach (KeyValuePair<string, string> keyValue in translationDictionary)
            {
                if (!masterDictionary.ContainsKey(keyValue.Key))
                {
                    AddNewString(masterDictionary, pathToMasterFile, keyValue.Key);
                }
            }

            foreach (KeyValuePair<string, string> keyValue in masterDictionary)
            {
                if (!translationDictionary.ContainsKey(keyValue.Key))
                {
                    AddNewString(translationDictionary, pathToTranslationFile, keyValue.Key);
                }
            }
        }

        string EncodeForSaving(string stringToEncode)
        {
            // replace the cariage returns with '\n's
            return stringToEncode.Replace("\n", "\\n");
        }

        string DecodeWhileReading(string stringToDecode)
        {
            return stringToDecode.Replace("\\n", "\n");
        }

        void AddNewString(Dictionary<string, string> dictionary, string pathAndFilename, string englishString)
        {
            // TODO: make sure we don't throw an assertion when running from the ProgramFiles directory.
            // Don't do saving when we are.
            if (!dictionary.ContainsKey(englishString))
            {
                dictionary.Add(englishString, englishString);

                using (TimedLock.Lock(this, "TranslationMap"))
                {
                    using (StreamWriter masterFileStream = File.AppendText(pathAndFilename))
                    {
                        masterFileStream.WriteLine(string.Format("English:{0}", EncodeForSaving(englishString)));
                        masterFileStream.WriteLine(string.Format("Translated:{0}", EncodeForSaving(englishString)));
                        masterFileStream.WriteLine("");
                    }
                }
            }
        }

        public string Translate(string englishString)
        {
            string tranlatedString;
            if (!translationDictionary.TryGetValue(englishString, out tranlatedString))
            {
                AddNewString(translationDictionary, pathToTranslationFile, englishString);
                AddNewString(masterDictionary, pathToMasterFile, englishString);
                return englishString;
            }

            return tranlatedString;
        }
    }
}
