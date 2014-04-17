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
using System.Linq;
using System.Text;
using System.IO;

using MatterHackers.Agg.Image;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;
using MatterHackers.MatterControl;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.Agg.ImageProcessing;

namespace MatterHackers.MatterControl.PrintQueue
{
    public class QueueData
    {
        private List<PrintItemWrapper> printItems = new List<PrintItemWrapper>();
        private List<PrintItemWrapper> PrintItems
        {
            get { return printItems; }
        }
        
        public RootedObjectEventHandler ItemAdded = new RootedObjectEventHandler();
        public RootedObjectEventHandler ItemRemoved = new RootedObjectEventHandler();
        public RootedObjectEventHandler OrderChanged = new RootedObjectEventHandler();

        static QueueData instance;
        public static QueueData Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new QueueData();
                    instance.LoadDefaultQueue();
                }
                return instance;
            }
        }

        public void SwapItemsOnIdle(int indexA, int indexB)
        {
            UiThread.RunOnIdle(SwapItems, new SwapIndexArgs(indexA, indexB));
        }

        void SwapItems(object state)
        {
            int indexA = ((SwapIndexArgs)state).indexA;
            int indexB = ((SwapIndexArgs)state).indexB;

            if (indexA >= 0 && indexA < Count
                && indexB >= 0 && indexB < Count
                && indexA != indexB)
            {
                PrintItemWrapper hold = PrintItems[indexA];
                PrintItems[indexA] = PrintItems[indexB];
                PrintItems[indexB] = hold;

                OnOrderChanged(null);

                SaveDefaultQueue();
            }
        }

        public void OnOrderChanged(EventArgs e)
        {
            OrderChanged.CallEvents(this, e);
        }

        public class IndexArgs : EventArgs
        {
            internal int index;

            public int Index { get { return index; } }
            internal IndexArgs(int index)
            {
                this.index = index;
            }
        }

        public class SwapIndexArgs : EventArgs
        {
            internal int indexA;
            internal int indexB;
            internal SwapIndexArgs(int indexA, int indexB)
            {
                this.indexA = indexA;
                this.indexB = indexB;
            }
        }

        public void RemoveIndexOnIdle(int index)
        {
            UiThread.RunOnIdle(RemoveIndex, new IndexArgs(index));
        }

        void RemoveIndex(object state)
        {
            IndexArgs removeArgs = state as IndexArgs;
            if (removeArgs != null)
            {
                RemoveAt(removeArgs.index);
            }
        }

        public void RemoveAt(int index)
        {
            if (index >= 0 && index < Count)
            {
                PrintItems.RemoveAt(index);

                OnItemRemoved(new IndexArgs(index));

                SaveDefaultQueue();
            }
        }

        public void OnItemRemoved(EventArgs e)
        {
            ItemRemoved.CallEvents(this, e);
        }

        public PrintItemWrapper GetPrintItem(int index)
        {
            if (index >= 0 && index < PrintItems.Count)
            {
                return PrintItems[index];
            }

            return null;
        }

        public int GetIndex(PrintItemWrapper printItem)
        {
            return PrintItems.IndexOf(printItem);
        }

        public string[] GetItemNames()
        {
            List<string> itemNames = new List<string>(); ;
            for (int i = 0; i < PrintItems.Count; i++)
            {
                itemNames.Add(PrintItems[i].Name);
            }

            return itemNames.ToArray();
        }

        public List<PrintItem> CreateReadOnlyPartList()
        {
            List<PrintItem> listToReturn = new List<PrintItem>();
            for (int i = 0; i < Count; i++)
            {
                listToReturn.Add(GetPrintItem(i).PrintItem);
            }
            return listToReturn;
        }

        public void AddItem(PrintItemWrapper item, int indexToInsert = -1)
        {
            if (indexToInsert == -1)
            {
                indexToInsert = PrintItems.Count;
            }
            PrintItems.Insert(indexToInsert, item);
            OnItemAdded(new IndexArgs(indexToInsert));
            SaveDefaultQueue();
        }

        public void LoadDefaultQueue()
        {
            RemoveAll();
            ManifestFileHandler manifest = new ManifestFileHandler(null);
            List<PrintItem> partFiles = manifest.ImportFromJson();
            if (partFiles != null)
            {
                foreach (PrintItem item in partFiles)
                {
                    AddItem(new PrintItemWrapper(item));
                }
            }
        }

        public void OnItemAdded(EventArgs e)
        {
            ItemAdded.CallEvents(this, e);
        }

        public void SaveDefaultQueue()
        {
            List<PrintItem> partList = CreateReadOnlyPartList();
            ManifestFileHandler manifest = new ManifestFileHandler(partList);
            manifest.ExportToJson();
        }

        public int Count
        {
            get
            {
                return PrintItems.Count;
            }
        }

        public void RemoveAll()
        {
            for (int i = PrintItems.Count-1; i >= 0; i--)
            {
                RemoveAt(i);
            }
        }
    }
}