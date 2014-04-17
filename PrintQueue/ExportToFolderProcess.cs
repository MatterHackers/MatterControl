using System;
using System.Threading;
using System.Collections.Generic;
using System.IO;

using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.Agg.UI;
using MatterHackers.GCodeVisualizer;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.PrintQueue
{
    public class ExportToFolderProcess
    {
        List<PrintItem> allFilesToExport;
        List<string> savedGCodeFileNames;
        public event EventHandler UpdatePartStatus;
        public event EventHandler StartingNextPart;
        public event EventHandler DoneSaving;

        int itemCountBeingWorkedOn;
        private string exportPath;
        public int ItemCountBeingWorkedOn
        {
            get
            {
                return itemCountBeingWorkedOn;
            }
        }

        public string ItemNameBeingWorkedOn
        {
            get
            {
                if (ItemCountBeingWorkedOn < allFilesToExport.Count)
                {
                    return allFilesToExport[ItemCountBeingWorkedOn].Name;
                }

                return "";
            }
        }

        public int CountOfParts
        {
            get
            {
                return allFilesToExport.Count;
            }
        }

        public ExportToFolderProcess(List<DataStorage.PrintItem> list, string exportPath)
        {
            // TODO: Complete member initialization
            this.allFilesToExport = list;
            this.exportPath = exportPath;

            itemCountBeingWorkedOn = 0;
        }

        public void Start()
        {
            if (QueueData.Instance.Count > 0)
            {
                if (StartingNextPart != null)
                {
                    StartingNextPart(this, new StringEventArgs(ItemNameBeingWorkedOn));
                }

                savedGCodeFileNames = new List<string>();
                allFilesToExport = QueueData.Instance.CreateReadOnlyPartList();
                foreach (PrintItem part in allFilesToExport)
                {
                    PrintItemWrapper printItemWrapper = new PrintItemWrapper(part);
                    if (Path.GetExtension(part.FileLocation).ToUpper() == ".STL")
                    {
                        SlicingQueue.Instance.QueuePartForSlicing(printItemWrapper);
                        printItemWrapper.SlicingDone += new EventHandler(sliceItem_Done);
                        printItemWrapper.SlicingOutputMessage += printItemWrapper_SlicingOutputMessage;
                    }
                    else if (Path.GetExtension(part.FileLocation).ToUpper() == ".GCODE")
                    {
                        sliceItem_Done(printItemWrapper, null);
                    }
                }
            }
        }

        void printItemWrapper_SlicingOutputMessage(object sender, EventArgs e)
        {
            StringEventArgs message = (StringEventArgs)e;
            if (UpdatePartStatus != null)
            {
                UpdatePartStatus(this, message);
            }
        }

        void sliceItem_Done(object sender, EventArgs e)
        {
            PrintItemWrapper sliceItem = (PrintItemWrapper)sender;

            sliceItem.SlicingDone -= new EventHandler(sliceItem_Done);
            sliceItem.SlicingOutputMessage -= printItemWrapper_SlicingOutputMessage;
            if (File.Exists(sliceItem.FileLocation))
            {
                savedGCodeFileNames.Add(sliceItem.GCodePathAndFileName);
            }

            itemCountBeingWorkedOn++;
            if (itemCountBeingWorkedOn < allFilesToExport.Count)
            {
                if (StartingNextPart != null)
                {
                    StartingNextPart(this, new StringEventArgs(ItemNameBeingWorkedOn));
                }
            }
            else
            {
                UpdatePartStatus(this, new StringEventArgs("Calculating Total cm3..."));

                if (savedGCodeFileNames.Count > 0)
                {
                    double total = 0;
                    foreach (string gcodeFileName in savedGCodeFileNames)
                    {
                        string[] lines = File.ReadAllLines(gcodeFileName);
                        if (lines.Length > 0)
                        {
                            string filamentAmountLine = lines[lines.Length - 1];
                            bool foundAmountInGCode = false;
                            int startPos = filamentAmountLine.IndexOf("(");
                            if (startPos > 0)
                            {
                                int endPos = filamentAmountLine.IndexOf("cm3)", startPos);
                                if (endPos > 0)
                                {
                                    string value = filamentAmountLine.Substring(startPos + 1, endPos - (startPos + 1));
                                    double amountForThisFile;
                                    if (double.TryParse(value, out amountForThisFile))
                                    {
                                        foundAmountInGCode = true;
                                        total += amountForThisFile;
                                    }
                                }
                            }
                            if (!foundAmountInGCode)
                            {
                                GCodeFile gcodeFile = new GCodeFile(gcodeFileName);
                                total += gcodeFile.GetFilamentCubicMm(ActiveSliceSettings.Instance.FilamentDiameter) / 1000;
                            }
                        }
                    }

                    // now copy all the gcode to the path given
                    for (int i = 0; i < savedGCodeFileNames.Count; i++)
                    {
                        string savedGcodeFileName = savedGCodeFileNames[i];
                        string originalFileName = Path.GetFileName(allFilesToExport[i].Name);
                        string outputFileName = Path.ChangeExtension(originalFileName, ".gcode");
                        string outputPathAndName = Path.Combine(exportPath, outputFileName);

                        if (ActivePrinterProfile.Instance.DoPrintLeveling)
                        {
                            GCodeFile unleveledGCode = new GCodeFile(savedGcodeFileName);
                            PrintLeveling.Instance.ApplyLeveling(unleveledGCode);
                            unleveledGCode.Save(outputPathAndName);
                        }
                        else
                        {
                            File.Copy(savedGcodeFileName, outputPathAndName, true);
                        }
                    }

                    if (DoneSaving != null)
                    {
                        DoneSaving(this, new StringEventArgs(string.Format("{0:0.0}", total)));
                    }
                }
            }
        }
    }
}

