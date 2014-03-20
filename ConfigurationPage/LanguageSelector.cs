using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.Localizations;


namespace MatterHackers.MatterControl
{
    public class LanguageSelector : GuiWidget
    {
        public StyledDropDownList LanguageDropList;

        public LanguageSelector()
        {
            string defaultModelDropDownLbl = LocalizedString.Get("Select Model");
            string defaultModelDropDownLblFull = string.Format("- {0} -", defaultModelDropDownLbl);

            List<string> languageList = new List<string>( new string[]{"English", "Spanish", "German"});
            
            LanguageDropList = new StyledDropDownList(defaultModelDropDownLblFull);
            LanguageDropList.AddItem("Default");

            foreach (string language in languageList)
            {
                LanguageDropList.AddItem(language);
            }

            //string pathToModels = Path.Combine(ApplicationDataStorage.Instance.ApplicationStaticDataPath, "PrinterSettings", manufacturer);
            //if (Directory.Exists(pathToModels))
            //{
            //    foreach (string manufacturerDirectory in Directory.EnumerateDirectories(pathToModels))
            //    {
            //        string model = Path.GetFileName(manufacturerDirectory);
            //        ModelDropList.AddItem(model);
            //    }
            //}
            

            AddChild(LanguageDropList);

            HAnchor = HAnchor.FitToChildren;
            VAnchor = VAnchor.FitToChildren;
        }
    }
       
}
