using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.Localizations;

namespace MatterHackers.MatterControl
{
    public class PrinterChooser : GuiWidget
    {
        public StyledDropDownList ManufacturerDropList;

        public PrinterChooser(string selectedMake = null)
        {
			ManufacturerDropList = new StyledDropDownList(new LocalizedString("- Select Make -").Translated);            
            bool addOther = false;
            string pathToWhitelist = Path.Combine(ApplicationDataStorage.Instance.ApplicationStaticDataPath, "OEMSettings", "PrinterSettingsWhitelist.txt");
            string[] folderWhitelist = File.ReadAllLines(pathToWhitelist);
            string pathToManufacturers = Path.Combine(ApplicationDataStorage.Instance.ApplicationStaticDataPath, "PrinterSettings");
            if (Directory.Exists(pathToManufacturers))
            {
                int index = 0;
                int preselectIndex = -1;
                foreach (string manufacturerDirectory in Directory.EnumerateDirectories(pathToManufacturers))
                {
                    string folderName = new System.IO.DirectoryInfo(manufacturerDirectory).Name;
                    if (folderWhitelist.Contains(folderName))
                    {
                        string manufacturer = Path.GetFileName(manufacturerDirectory);
                        if (manufacturer == "Other")
                        {
                            addOther = true;
                        }
                        else
                        {
                            ManufacturerDropList.AddItem(manufacturer);
                            if (selectedMake != null)
                            {
                                if (manufacturer == selectedMake)
                                {
                                    preselectIndex = index;
                                }
                            }
                        
                            index++;

                        }
                    }
                }
                if (addOther)
                {
                    if (selectedMake != null && preselectIndex == -1)
                    {
                        preselectIndex = index;
                    }
					ManufacturerDropList.AddItem(new LocalizedString("Other").Translated);
                }
                if (preselectIndex != -1)
                {
                    ManufacturerDropList.SelectedIndex = preselectIndex;
                }

            }

            AddChild(ManufacturerDropList);

            HAnchor = HAnchor.FitToChildren;
            VAnchor = VAnchor.FitToChildren;
        }
    }

    public class ModelChooser : GuiWidget
    {
        public StyledDropDownList ModelDropList;

        public ModelChooser(string manufacturer)
        {
			ModelDropList = new StyledDropDownList(new LocalizedString("- Select Model -").Translated);
            string pathToModels = Path.Combine(ApplicationDataStorage.Instance.ApplicationStaticDataPath, "PrinterSettings", manufacturer);
            if (Directory.Exists(pathToModels))
            {
                foreach (string manufacturerDirectory in Directory.EnumerateDirectories(pathToModels))
                {
                    string model = Path.GetFileName(manufacturerDirectory);
                    ModelDropList.AddItem(model);
                }
            }
			ModelDropList.AddItem(new LocalizedString("Other").Translated);

            AddChild(ModelDropList);

            HAnchor = HAnchor.FitToChildren;
            VAnchor = VAnchor.FitToChildren;
        }
    }
}
