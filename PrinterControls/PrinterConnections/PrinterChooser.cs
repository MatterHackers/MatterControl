/*
Copyright (c) 2015, Kevin Pope
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

using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using System.Collections.Generic;
using MatterHackers.MatterControl.SettingsManagement;
using System.IO;
using System.Linq;

namespace MatterHackers.MatterControl
{
	public class PrinterChooser : GuiWidget
	{
		public StyledDropDownList ManufacturerDropList;
		private int countOfMakes = 0;
		private string manufacturer;

		public int CountOfMakes { get { return countOfMakes; } }

		public PrinterChooser(string selectedMake = null)
		{
			string defaultManufacturerLabel = LocalizedString.Get("Select Make");
			string defaultManufacturerLabelFull = string.Format("- {0} -", defaultManufacturerLabel);
			ManufacturerDropList = new StyledDropDownList(defaultManufacturerLabelFull, maxHeight: 200);
            ManufacturerDropList.Name = "Select Make";
            bool addOther = false;
			string[] printerWhiteListStrings = OemSettings.Instance.PrinterWhiteList.ToArray();
			List<ManufacturerNameMapping> manufacturerNameMappings = OemSettings.Instance.ManufacturerNameMappings;
			string pathToManufacturers = "PrinterSettings";
			if (StaticData.Instance.DirectoryExists(pathToManufacturers))
			{
				int index = 0;
				int preselectIndex = -1;
				foreach (string manufacturerDirectory in StaticData.Instance.GetDirectories(pathToManufacturers))
				{
					string folderName = Path.GetFileName(manufacturerDirectory.TrimEnd(new[] { '/', '\\' }));

					// Apply whitelist
					if (!printerWhiteListStrings.Contains(folderName))
					{
						continue;
					}

					// Set manufacturer name to the directory name
					this.manufacturer = Path.GetFileName(manufacturerDirectory);

					// Override the manufacturer name if a manufacturerNameMappings exists
					foreach (ManufacturerNameMapping nameMapping in manufacturerNameMappings)
					{
						if(nameMapping.NameOnDisk == folderName)
						{
							this.manufacturer = nameMapping.NameToDisplay;
							break;
						}
					}

					if (this.manufacturer == "Other")
					{
						addOther = true;
					}
					else
					{
						ManufacturerDropList.AddItem(manufacturer);
						if (selectedMake != null)
						{
							if (this.manufacturer == selectedMake)
							{
								preselectIndex = index;
							}
						}
						index++;
					}
					countOfMakes += 1;
				}

				if (addOther)
				{
					if (selectedMake != null && preselectIndex == -1)
					{
						preselectIndex = index;
					}
					ManufacturerDropList.AddItem(LocalizedString.Get("Other"));
				}
				if (preselectIndex != -1)
				{
					ManufacturerDropList.SelectedIndex = preselectIndex;
				}
			}

			if (ManufacturerDropList.MenuItems.Count == 1)
			{
				ManufacturerDropList.SelectedIndex = 0;
			}

			AddChild(ManufacturerDropList);

			HAnchor = HAnchor.FitToChildren;
			VAnchor = VAnchor.FitToChildren;
		}
	}

	public class ModelChooser : GuiWidget
	{
		public StyledDropDownList ModelDropList;
		private int countOfModels = 0;

		public int CountOfModels { get { return countOfModels; } }

		public ModelChooser(string manufacturer)
		{
			string defaultModelDropDownLabel = LocalizedString.Get("Select Model");
			string defaultModelDropDownLabelFull = string.Format("- {0} -", defaultModelDropDownLabel);
			ModelDropList = new StyledDropDownList(defaultModelDropDownLabelFull, maxHeight: 200);
            ModelDropList.Name = defaultModelDropDownLabel;

            string pathToModels = Path.Combine("PrinterSettings", manufacturer);
			if (StaticData.Instance.DirectoryExists((pathToModels)))
			{
				foreach (string manufacturerDirectory in StaticData.Instance.GetDirectories(pathToModels))
				{
					string model = Path.GetFileName(manufacturerDirectory);
					ModelDropList.AddItem(model);
					countOfModels += 1;
				}
			}

			ModelDropList.AddItem(LocalizedString.Get("Other"));
			AddChild(ModelDropList);

			HAnchor = HAnchor.FitToChildren;
			VAnchor = VAnchor.FitToChildren;
		}

		public void SelectIfOnlyOneModel()
		{
			if (ModelDropList.MenuItems.Count == 2)
			{
				ModelDropList.SelectedIndex = 0;
			}
		}
	}
}