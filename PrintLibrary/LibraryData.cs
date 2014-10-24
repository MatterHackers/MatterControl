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
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using MatterHackers.Agg;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.SettingsManagement;
using MatterHackers.PolygonMesh;
using MatterHackers.Agg.UI;
using MatterHackers.PolygonMesh.Processors;

namespace MatterHackers.MatterControl.PrintLibrary
{
    public class LibraryData
    {
        private List<PrintItemWrapper> printItems = new List<PrintItemWrapper>();
        private List<PrintItemWrapper> PrintItems
        {
            get { return printItems; }
        }

        public RootedObjectEventHandler DataReloaded = new RootedObjectEventHandler();
        public RootedObjectEventHandler ItemAdded = new RootedObjectEventHandler();
        public RootedObjectEventHandler ItemRemoved = new RootedObjectEventHandler();
        public RootedObjectEventHandler OrderChanged = new RootedObjectEventHandler();

        private DataStorage.PrintItemCollection libraryCollection;

        static LibraryData instance;
        public static LibraryData Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new LibraryData();
                    instance.LoadLibraryItems();
                }
                return instance;
            }
        }

        static public void SaveToLibrary(PrintItemWrapper printItemWrapper, List<MeshGroup> meshGroups)
        {
            if (printItemWrapper.FileLocation.Contains(ApplicationDataStorage.Instance.ApplicationLibraryDataPath))
            {
                MeshOutputSettings outputInfo = new MeshOutputSettings(MeshOutputSettings.OutputType.Binary, new string[] { "Created By", "MatterControl" });
                MeshFileIo.Save(meshGroups, printItemWrapper.FileLocation, outputInfo);
            }
            else // save a copy to the library and update this to point at it
            {
                string fileName = Path.ChangeExtension(Path.GetRandomFileName(), ".amf");
                printItemWrapper.FileLocation = Path.Combine(ApplicationDataStorage.Instance.ApplicationLibraryDataPath, fileName);

                MeshOutputSettings outputInfo = new MeshOutputSettings(MeshOutputSettings.OutputType.Binary, new string[] { "Created By", "MatterControl" });
                MeshFileIo.Save(meshGroups, printItemWrapper.FileLocation, outputInfo);

                printItemWrapper.PrintItem.Commit();
            }

            printItemWrapper.OnFileHasChanged();
        }

        string keywordFilter = "";
        public string KeywordFilter
        {
            get { return keywordFilter; }
            set
            {
                if (this.keywordFilter != value)
                {
                    this.keywordFilter = value;
                    LoadLibraryItems();
                }
            }
        }

        public void AddItem(PrintItemWrapper item, int indexToInsert = -1)
        {
            if (indexToInsert == -1)
            {
                indexToInsert = PrintItems.Count;
            }
            PrintItems.Insert(indexToInsert, item);
            OnItemAdded(new IndexArgs(indexToInsert));
        }

        public void RemoveItem(PrintItemWrapper printItemWrapper)
        {
            int index = PrintItems.IndexOf(printItemWrapper);
            if (index < 0)
            {
                // It may be possible to have the same item in the remove list twice.
                // so if it is not in the PrintItems then ignore it.
                return;
            }
            PrintItems.RemoveAt(index);
            
            // and remove it from the data base
            printItemWrapper.Delete();

            OnItemRemoved(new IndexArgs(index));
        }

        public PrintItemWrapper GetPrintItemWrapper(int index)
        {
            if(index >= 0 && index < Count)
            {
                return PrintItems[index];
            }

            return null;
        }

        public List<PrintItem> CreateReadOnlyPartList()
        {
            List<PrintItem> listToReturn = new List<PrintItem>();
            for (int i = 0; i < Count; i++)
            {
                listToReturn.Add(GetPrintItemWrapper(i).PrintItem);
            }
            return listToReturn;
        }

        public DataStorage.PrintItemCollection LibraryCollection
        {
            get 
            {
                //Retrieve a list of saved printers from the Datastore            
                if (libraryCollection == null)
                {
                    libraryCollection = DataStorage.Datastore.Instance.dbSQLite.Table<DataStorage.PrintItemCollection>().Where(v => v.Name == "_library").Take(1).FirstOrDefault();
                }


                if (libraryCollection == null)
                {
                    libraryCollection = new PrintItemCollection();
                    libraryCollection.Name = "_library";
                    libraryCollection.Commit();
                    PreloadLibrary();
                }
                return libraryCollection;
            }
        }

        void PreloadLibrary()
        {
            foreach (string partFile in OemSettings.Instance.PreloadedLibraryFiles)
            {
                string partFullPath = Path.Combine(ApplicationDataStorage.Instance.ApplicationStaticDataPath, "OEMSettings", "SampleParts", partFile);
                if (System.IO.File.Exists(partFullPath))
                {
                    PrintItem printItem = new PrintItem();
                    printItem.Name = Path.GetFileNameWithoutExtension(partFullPath);
                    printItem.FileLocation = Path.GetFullPath(partFullPath);
                    printItem.PrintItemCollectionID = LibraryCollection.Id;
                    printItem.Commit();
                }
            }
        }

        IEnumerable<DataStorage.PrintItem> GetLibraryItems(string keyphrase = null)
        {   
            if (LibraryCollection == null)
            {
                return null;
            }
            else
            {
                string query;
                if (keyphrase == null)
                {
                    query = string.Format("SELECT * FROM PrintItem WHERE PrintItemCollectionID = {0} ORDER BY Name ASC;", libraryCollection.Id);
                }
                else
                {
                    query = string.Format("SELECT * FROM PrintItem WHERE PrintItemCollectionID = {0} AND Name LIKE '%{1}%' ORDER BY Name ASC;", libraryCollection.Id, keyphrase);
                }
                IEnumerable<DataStorage.PrintItem> result = (IEnumerable<DataStorage.PrintItem>)DataStorage.Datastore.Instance.dbSQLite.Query<DataStorage.PrintItem>(query);
                return result;
            }            
        }

        public void LoadLibraryItems()
        {
            PrintItems.Clear();
            IEnumerable<DataStorage.PrintItem> partFiles = GetLibraryItems(keywordFilter);
            if (partFiles != null)
            {
                foreach (PrintItem part in partFiles)
                {
                    PrintItems.Add(new PrintItemWrapper(part));
                    
                }
            }
            OnDataReloaded(null);
        }

        public void OnDataReloaded(EventArgs e)
        {
            DataReloaded.CallEvents(this, e);
        }

        public void OnItemAdded(EventArgs e)
        {
            ItemAdded.CallEvents(this, e);
        }

        public void OnItemRemoved(EventArgs e)
        {
            ItemRemoved.CallEvents(this, e);
        }

        public void SaveLibraryItems()
        {
            //
        }

        public int Count
        {
            get
            {
                return PrintItems.Count;
            }
        }

        ReportProgress fileLoadReportProgress = null;
        public void LoadFilesIntoLibrary(string[] files, ReportProgress reportProgress = null)
        {
            this.fileLoadReportProgress = reportProgress;
            if (files != null && files.Length > 0)
            {
                BackgroundWorker mergeAndSavePartsBackgroundWorker = new BackgroundWorker();
                mergeAndSavePartsBackgroundWorker.WorkerReportsProgress = true;

                mergeAndSavePartsBackgroundWorker.DoWork += new DoWorkEventHandler(mergeAndSavePartsBackgroundWorker_DoWork);
                mergeAndSavePartsBackgroundWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(mergeAndSavePartsBackgroundWorker_RunWorkerCompleted);

                mergeAndSavePartsBackgroundWorker.RunWorkerAsync(files);
            }
        }

        void mergeAndSavePartsBackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            string[] fileList = e.Argument as string[];
            foreach (string loadedFileName in fileList)
            {
                PrintItem printItem = new PrintItem();
                printItem.Name = Path.GetFileNameWithoutExtension(loadedFileName);
                printItem.FileLocation = Path.GetFullPath(loadedFileName);
                printItem.PrintItemCollectionID = LibraryData.Instance.LibraryCollection.Id;
                printItem.Commit();

                LibraryData.Instance.AddItem(new PrintItemWrapper(printItem));
            }
#if false
            SaveToLibrary(PrintItemWrapper printItemWrapper, List<MeshGroup> meshGroups);            
            
            // we sent the data to the asynch lists but we will not pull it back out (only use it as a temp holder).
            PushMeshGroupDataToAsynchLists(TraceInfoOpperation.DO_COPY);

            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            BackgroundWorker backgroundWorker = (BackgroundWorker)sender;
            try
            {
                // push all the transforms into the meshes
                for (int i = 0; i < asynchMeshGroups.Count; i++)
                {
                    asynchMeshGroups[i].Transform(asynchMeshGroupTransforms[i].TotalTransform);

                    int nextPercent = (i + 1) * 40 / asynchMeshGroups.Count;
                    backgroundWorker.ReportProgress(nextPercent);
                }

                LibraryData.SaveToLibrary(printItemWrapper, asynchMeshGroups);
            }
            catch (System.UnauthorizedAccessException)
            {
                UiThread.RunOnIdle((state) =>
                {
                    //Do something special when unauthorized?
                    StyledMessageBox.ShowMessageBox(null, "Oops! Unable to save changes.", "Unable to save");
                });
            }
            catch
            {
                UiThread.RunOnIdle((state) =>
                {
                    StyledMessageBox.ShowMessageBox(null, "Oops! Unable to save changes.", "Unable to save");
                });
            }
#endif
        }

        void mergeAndSavePartsBackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
        }
   }
}