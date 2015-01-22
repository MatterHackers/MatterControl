/*
Copyright (c) 2014, Kevin Pope
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

using System;
using System.Threading;
using System.Collections.Generic;
using System.IO;

using MatterHackers.PolygonMesh.Processors;
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
            this.allFilesToExport = list;
            this.exportPath = exportPath;

            itemCountBeingWorkedOn = 0;
        }

        EventHandler unregisterEvents;
        public void Start()
        {
			if (allFilesToExport.Count > 0)
            {
                if (StartingNextPart != null)
                {
                    StartingNextPart(this, new StringEventArgs(ItemNameBeingWorkedOn));
                }

                savedGCodeFileNames = new List<string>();
                foreach (PrintItem part in allFilesToExport)
                {
                    PrintItemWrapper printItemWrapper = new PrintItemWrapper(part);
                    string extension = Path.GetExtension(part.FileLocation).ToUpper();
                    if (MeshFileIo.ValidFileExtensions().Contains(extension))
                    {
                        SlicingQueue.Instance.QueuePartForSlicing(printItemWrapper);
                        printItemWrapper.SlicingDone.RegisterEvent(sliceItem_Done, ref unregisterEvents);
                        printItemWrapper.SlicingOutputMessage.RegisterEvent(printItemWrapper_SlicingOutputMessage, ref unregisterEvents);
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

            sliceItem.SlicingDone.UnregisterEvent(sliceItem_Done, ref unregisterEvents);
            sliceItem.SlicingOutputMessage.UnregisterEvent(printItemWrapper_SlicingOutputMessage, ref unregisterEvents);
            if (File.Exists(sliceItem.FileLocation))
            {
                savedGCodeFileNames.Add(sliceItem.GetGCodePathAndFileName());
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
				if (UpdatePartStatus != null)
				{
					UpdatePartStatus(this, new StringEventArgs("Calculating Total cm3..."));
				}

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
							GCodeFileLoaded unleveledGCode = new GCodeFileLoaded(savedGcodeFileName);
                            PrintLevelingPlane.Instance.ApplyLeveling(unleveledGCode);
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

