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
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Runtime.InteropServices;
using System.Diagnostics;

using MatterHackers.Localizations;
using MatterHackers.Agg.UI;
using MatterHackers.Agg;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.PrintQueue
{
    public class PrintItemWrapper
    {
        public RootedObjectEventHandler SlicingOutputMessage = new RootedObjectEventHandler();
        public RootedObjectEventHandler SlicingDone = new RootedObjectEventHandler();
        public RootedObjectEventHandler FileHasChanged = new RootedObjectEventHandler();

        public PrintItem PrintItem { get; set; }
        String fileType;

        int stlFileHashCode;
        long writeTime = 0;

        public bool CurrentlySlicing { get; set; }

        bool slicingHadError = false;
        public bool SlicingHadError { get { return slicingHadError; } }
        
        public PrintItemWrapper(DataStorage.PrintItem printItem)
        {
            this.PrintItem = printItem;
            this.fileType = Path.GetExtension(printItem.FileLocation).ToUpper();
        }

        public PrintItemWrapper(int printItemId)
        {
            this.PrintItem = DataStorage.Datastore.Instance.dbSQLite.Table<DataStorage.PrintItem>().Where(v => v.Id == printItemId).Take(1).FirstOrDefault();
            this.fileType = Path.GetExtension(this.PrintItem.FileLocation).ToUpper();
        }

        bool doneSlicing;
        static string slicingError = "Slicing Error".Localize();
        static string readyToPrint = "Ready to Print".Localize();
        static string fileNotFound = "File Not Found\n'{0}'".Localize();
        public bool DoneSlicing
        {
            get
            {
                return doneSlicing;
            }

            set
            {
                if (value != doneSlicing)
                {
                    doneSlicing = value;
                    if (doneSlicing)
                    {
                        string message = slicingError;
                        slicingHadError = true;
                        if (File.Exists(FileLocation))
                        {
                            string gcodePathAndFileName = GetGCodePathAndFileName();
                            if (gcodePathAndFileName != "" && File.Exists(gcodePathAndFileName))
                            {
                                FileInfo info = new FileInfo(gcodePathAndFileName);
                                // This is really just to make sure it is bigger than nothing.
                                if (info.Length > 10)
                                {
                                    message = readyToPrint;
                                    slicingHadError = false;
                                }
                            }
                        }
                        else
                        {
                            message = string.Format(fileNotFound, FileLocation);
                        }

                        OnSlicingOutputMessage(new StringEventArgs(message));

                        SlicingDone.CallEvents(this, null);
                    }
                }
            }
        }

        public void OnSlicingOutputMessage(EventArgs e)
        {
            StringEventArgs message = e as StringEventArgs;
            if (SlicingOutputMessage != null)
            {
                SlicingOutputMessage.CallEvents(this, message);
            }
        }

        public void Delete()
        {
            PrintItem.Delete();
        }

        public string Name
        {
            get { return this.PrintItem.Name; }
        }

        public string FileLocation
        {
            get { return this.PrintItem.FileLocation; }
        }

        public int StlFileHashCode
        {
            get
            {
                long currentWriteTime = File.GetLastWriteTime(this.FileLocation).ToBinary();
                bool fileExists = System.IO.File.Exists(this.FileLocation);
                if (fileExists)
                {
                    if (this.stlFileHashCode == 0 || writeTime != currentWriteTime)
                    {
                        writeTime = currentWriteTime;
                        using (FileStream fileStream = new FileStream(this.FileLocation, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            long sizeOfFile = fileStream.Length;
                            int sizeOfRead = 1 << 16;
                            byte[] readData = new byte[sizeOfRead * 3];

                            // get a chuck from the begining
                            fileStream.Read(readData, sizeOfRead, sizeOfRead);

                            // the middle
                            fileStream.Seek(sizeOfFile / 2, SeekOrigin.Begin);
                            fileStream.Read(readData, sizeOfRead * 1, sizeOfRead);

                            // and the end
                            fileStream.Seek(Math.Max(0, sizeOfFile - sizeOfRead), SeekOrigin.Begin);
                            fileStream.Read(readData, sizeOfRead * 2, sizeOfRead);

                            // push the file size into the first bytes
                            byte[] fileSizeAsBytes = BitConverter.GetBytes(sizeOfFile);
                            for (int i = 0; i < fileSizeAsBytes.Length; i++)
                            {
                                readData[i] = fileSizeAsBytes[i];
                            }

                            this.stlFileHashCode = agg_basics.ComputeHash(readData);
                        }
                    }
                }

                return this.stlFileHashCode;
            }
        }

        public string PartToSlicePathAndFileName { get { return PrintItem.FileLocation; } }

        public bool IsGCodeFileComplete(string gcodePathAndFileName)
        {
            if (Path.GetExtension(FileLocation).ToUpper() == ".GCODE")
            {
                return true;
            }

            bool gCodeFileIsComplete = false;
            if (File.Exists(gcodePathAndFileName))
            {
                string gcodeFileContents = "";
                using (FileStream fileStream = new FileStream(gcodePathAndFileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    using (StreamReader gcodeStreamReader = new StreamReader(fileStream))
                    {
                        gcodeFileContents = gcodeStreamReader.ReadToEnd();
                    }
                }

                // check if there is a known line at the end of the file (this will let us know if slicer finished building the file).
                switch (ActivePrinterProfile.Instance.ActiveSliceEngineType)
                {
                    case ActivePrinterProfile.SlicingEngineTypes.CuraEngine:
                    case ActivePrinterProfile.SlicingEngineTypes.MatterSlice:
                    case ActivePrinterProfile.SlicingEngineTypes.Slic3r:
                        if (gcodeFileContents.Contains("filament used ="))
                        {
                            gCodeFileIsComplete = true;
                        }
                        break;

                    default:
                        throw new NotImplementedException();
                }
            }

            return gCodeFileIsComplete;
        }

        public string GetGCodePathAndFileName()
        {
            if (FileLocation.Trim() != "")
            {

                if (Path.GetExtension(FileLocation).ToUpper() == ".GCODE")
                {
                    return FileLocation;
                }

                string engineString = ((int)ActivePrinterProfile.Instance.ActiveSliceEngineType).ToString();

                string gcodeFileName = this.StlFileHashCode.ToString() + "_" + engineString + "_" + ActiveSliceSettings.Instance.GetHashCode().ToString();
                string gcodePathAndFileName = Path.Combine(DataStorage.ApplicationDataStorage.Instance.GCodeOutputPath, gcodeFileName + ".gcode");
                return gcodePathAndFileName;
            }
            else
            {
                return null;
            }
        }


        public void OnFileHasChanged()
        {
            if (FileHasChanged != null)
            {
                FileHasChanged.CallEvents(this, null);
            }
        }
    }
}
