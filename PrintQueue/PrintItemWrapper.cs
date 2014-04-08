using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Runtime.InteropServices;
using System.Diagnostics;

using MatterHackers.Agg.UI;
using MatterHackers.Agg;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.PrintQueue
{
    public class PrintItemWrapper
    {
        public event EventHandler SlicingOutputMessage;
        public event EventHandler Done;

        public event EventHandler FileHasChanged;

        public PrintItem PrintItem { get; set; }
        //public event EventHandler PreparationStatusChanged;

        //public enum GcodeStatuses { Prepared, Slicing, NotPrepared, SliceFailed };
        //GcodeStatuses gcodeStatus = GcodeStatuses.NotPrepared;
        //String gcodeProcessingMessage;
        String fileType;

        //String gcodePathAndFileName;
        int stlFileHashCode;
        long writeTime = 0;

        public bool CurrentlySlicing { get; set; }

         public PrintItemWrapper(DataStorage.PrintItem printItem)
        {
            this.PrintItem = printItem;
            this.fileType = System.IO.Path.GetExtension(printItem.FileLocation).ToUpper();
            //if (this.fileType == ".GCODE")
            //{
                //gcodeStatus = GcodeStatuses.Prepared;
            //}
        }

        public PrintItemWrapper(int printItemId)
        {
            this.PrintItem = DataStorage.Datastore.Instance.dbSQLite.Table<DataStorage.PrintItem>().Where(v => v.Id == printItemId).Take(1).FirstOrDefault();
            this.fileType = System.IO.Path.GetExtension(this.PrintItem.FileLocation).ToUpper();
        }

        bool doneSlicing;
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
                        string message = "Slicing Error";
                        if (File.Exists(FileLocation))
                        {
                            string gcodePathAndFileName = GetGCodePathAndFileName();
                            if (gcodePathAndFileName != "" && File.Exists(gcodePathAndFileName))
                            {
                                FileInfo info = new FileInfo(gcodePathAndFileName);
                                // This is really just to make sure it is bigger than nothing.
                                if (info.Length > 10)
                                {
                                    message = "Ready to Print";
                                }
                            }
                        }
                        else
                        {
                            message = string.Format("File Not Found\n'{0}'", FileLocation);
                        }

                        OnSlicingOutputMessage(new StringEventArgs(message));

                        if (Done != null)
                        {
                            Done(this, null);
                        }
                    }
                }
            }
        }

        public void OnSlicingOutputMessage(EventArgs e)
        {
            StringEventArgs message = e as StringEventArgs;
            if (SlicingOutputMessage != null)
            {
                SlicingOutputMessage(this, message);
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

        int StlFileHashCode
        {
            get
            {
                long currentWriteTime = File.GetLastWriteTime(this.FileLocation).ToBinary();
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

                return this.stlFileHashCode;
            }
        }

        public string GCodePathAndFileName { get { return GetGCodePathAndFileName(); } }
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
                FileHasChanged(this, null);
            }
        }
    }
}
