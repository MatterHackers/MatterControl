/*
Copyright (c) 2017, Lars Brubaker, John Lewin
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

namespace MatterHackers.Localizations
{
	public static class LocalizedString
	{
		private static TranslationMap MatterControlTranslationMap;

		private static readonly object syncRoot = new object();

		static LocalizedString()
		{
			lock(syncRoot)
			{
				if (MatterControlTranslationMap == null)
				{
#if DEBUG && !__ANDROID__
					if (!System.Environment.CurrentDirectory.ToLower().Contains(".tests"))
					{
						// In debug builds we load a translation map capable of generating/updating master.txt
						MatterControlTranslationMap = new AutoGeneratingTranslationMap("Translations", UserSettings.Instance.Language);
					}
					else
					{
						// Create a pass through TranslationMap for tests by requesting a non-existing language name
						MatterControlTranslationMap = new TranslationMap("Translations", "NA");
					}
#else
					MatterControlTranslationMap = new TranslationMap("Translations", UserSettings.Instance.Language);
#endif
				}
			}
		}

		public static void ResetTranslationMap()
		{
			MatterControlTranslationMap = new TranslationMap("Translations", UserSettings.Instance.Language);
		}

		public static string Localize(this string englishString)
		{
			return MatterControlTranslationMap.Translate(englishString);
		}
	}
}