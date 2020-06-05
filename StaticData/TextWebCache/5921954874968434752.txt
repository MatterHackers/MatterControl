
# Is MatterControl compatible with my 3D printer?
- This mainly depends on the language your printer speaks. Currently, MatterControl supports two languages for communicating with the printer; G-Code and S3G/X3G. G-Code is the standard language used by RepRaps and most other printers. S3G is a condensed language used by Makerbot and Flashforge. Most consumer 3D printers speak G-Code.

- In addition, you will need appropriate slice settings for your printer. MatterControl has built in profiles for many printers. A link to the complete list can be found below. If your printer is not on the list, don't worry. You will just need to fill in the settings yourself. Obtain specifications from the manufacturer and input them into MatterControl under `Settings & Controls -> Settings -> Printer` as well as a few under `Settings & Controls -> Settings -> Filament`.

- Some newer G-Code printers are using intermediary boards to run web servers in order to control the prints. These printers, while they do speak G-Code, are not able to be controlled directly by MatterControl. G-Code generated in MatterControl can still be sent to the printer using its web-based interface.

- LINK: [Known compatible printers list.](http://www.mattercontrol.com/#jumpSupportedModels)

# Why are my objects the wrong scale?
- STL files do not store any information about what units their dimensions are in. MatterControl (and all other 3D printing software) expects the dimensions in STL files to be given in mm. Most CAD software, though, will export STL files with whatever units they were designed in (usually inches). Thus, when you bring your designs into MatterControl they will be the wrong scale.

- The best solutions is to figure out how to get your design software to export STL files in millimeters. In SolidWorks, for instance, the Save As dialog has an Options button, that allows you to set many parameters for exporting an STL.

- If you cannot get your design software to do this, though, you can still rescale the part once you have it in MatterControl. View the part in 3D View, then enter Edit mode and choose SCALE from the bar on the right. A drop down menu offers many common conversion factors, or, axis dimension specifications can be entered directly in the appropriate fields.

# How do I clear the application data?

- If you are having a problem with MatterControl that is not fixed by re-installing, you may need to delete some of the data that MatterControl saves on your computer. This data will persist, even if MatterControl is uninstalled so remove that folder if you want to completely reset MatterControl to a clean slate. You can also temporarily rename the SQLite database file (MatterControl.db) to see if your settings are the cause of a problem.

- Windows
  - MatterControl keeps the user's library and settings in C:\Users\{user}\AppData\Local\MatterControl.

- Mac/Linux
  - The user's data library and settings are in ~/.local/share/MatterControl. This is a hidden folder in your user's home folder.