using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.Localizations;


namespace MatterHackers.MatterControl
{
    public class LanguageSelector : StyledDropDownList
    {        

        public LanguageSelector(string selection)
            : base(selection)
        {            


            //string pathToModels = Path.Combine(ApplicationDataStorage.Instance.ApplicationStaticDataPath, "PrinterSettings", manufacturer);
            //if (Directory.Exists(pathToModels))
            //{
            //    foreach (string manufacturerDirectory in Directory.EnumerateDirectories(pathToModels))
            //    {
            //        string model = Path.GetFileName(manufacturerDirectory);
            //        ModelDropList.AddItem(model);
            //    }
            //}

            this.MinimumSize = new Vector2(this.LocalBounds.Width, this.LocalBounds.Height);
        }
    }
       
}
