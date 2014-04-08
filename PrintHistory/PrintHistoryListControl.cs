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
using MatterHackers.MatterControl.PrintQueue;

namespace MatterHackers.MatterControl.PrintHistory
{
    public class SelectedPrintItems<T> : List<T>
    {
        public event EventHandler OnAdd;
        public event EventHandler OnRemove;

        new public void Add(T item)
        {
            base.Add(item);
            if (null != OnAdd)
            {
                OnAdd(this, null);
            }
        }

        new public void Remove(T item)
        {
            base.Remove(item);
            if (null != OnRemove)
            {
                OnRemove(this, null);
            }
        }
    }

    public class PrintHistoryListControl : ScrollableWidget
    {
        static PrintHistoryListControl instance;
        public bool ShowTimestamp;

        public static PrintHistoryListControl Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new PrintHistoryListControl();
                    instance.LoadHistoryItems();
                }
                return instance;
            }
        }

        private void SetDisplayAttributes()
        {
            this.MinimumSize = new Vector2(0, 200);
            this.AnchorAll();
            this.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;
            this.AutoScroll = true;
            this.ScrollArea.Padding = new BorderDouble(3, 3, 15, 3);
        }



        private List<string> GetLibraryParts()
        {
            List<string> libraryFilesToPreload = new List<string>();
            string setupSettingsPathAndFile = Path.Combine(ApplicationDataStorage.Instance.ApplicationStaticDataPath, "OEMSettings", "PreloadedLibraryFiles.txt");
            if (System.IO.File.Exists(setupSettingsPathAndFile))
            {
                try
                {
                    string[] lines = System.IO.File.ReadAllLines(setupSettingsPathAndFile);
                    foreach (string line in lines)
                    {
                        //Ignore commented lines
                        if (!line.StartsWith("#"))
                        {
                            string settingLine = line.Trim();
                            libraryFilesToPreload.Add(settingLine);
                        }
                    }
                }
                catch
                {

                }
            }
            return libraryFilesToPreload;
        }



        int RecordLimit = 20;
        IEnumerable<DataStorage.PrintTask> GetHistoryItems(int recordCount)
        {
            string query;
            if (UserSettings.Instance.get("PrintHistoryFilterShowCompleted") == "true")
            {                
                query = string.Format("SELECT * FROM PrintTask WHERE PrintComplete = 1 ORDER BY PrintStart DESC LIMIT {0};", recordCount);
            }
            else
            {
                query = string.Format("SELECT * FROM PrintTask ORDER BY PrintStart DESC LIMIT {0};", recordCount);
            }
            IEnumerable<DataStorage.PrintTask> result = (IEnumerable<DataStorage.PrintTask>)DataStorage.Datastore.Instance.dbSQLite.Query<DataStorage.PrintTask>(query);
            return result;
        }

        public void LoadHistoryItems(int NumItemsToLoad = 0)
        {
            if (NumItemsToLoad == 0 || NumItemsToLoad < RecordLimit)
            {
                NumItemsToLoad = RecordLimit;
            }
            
            RemoveAllChildren();
            IEnumerable<DataStorage.PrintTask> partFiles = GetHistoryItems(NumItemsToLoad);
            if (partFiles != null)
            {
                foreach (PrintTask part in partFiles)
                {
                    PrintHistoryListControl.Instance.AddChild(new PrintHistoryListItem(part, ShowTimestamp));
                }
            }
        }


        protected FlowLayoutWidget topToBottomItemList;
       

        public int Count
        {
            get
            {
                return topToBottomItemList.Children.Count;
            }
        }

       

        public PrintHistoryListControl()
        {
            ShowTimestamp = (UserSettings.Instance.get("PrintHistoryFilterShowTimestamp") == "true");
            
            SetDisplayAttributes();
            ScrollArea.HAnchor |= Agg.UI.HAnchor.ParentLeftRight;

            AutoScroll = true;
            topToBottomItemList = new FlowLayoutWidget(FlowDirection.TopToBottom);
            topToBottomItemList.HAnchor = Agg.UI.HAnchor.Max_FitToChildren_ParentWidth;
            base.AddChild(topToBottomItemList);
            AddHandlers(); 
            
        }

        void AddHandlers()
        {
            PrinterCommunication.Instance.ConnectionStateChanged.RegisterEvent(ReloadData, ref unregisterEvents);
        }

        void ReloadData(object sender, EventArgs e)
        {            
            LoadHistoryItems(Count);
        }

        event EventHandler unregisterEvents;
        public override void OnClosed(EventArgs e)
        {
            if (unregisterEvents != null)
            {
                unregisterEvents(this, null);
            }
            base.OnClosed(e);
        }

        public override void AddChild(GuiWidget child, int indexInChildrenList = -1)
        {
            FlowLayoutWidget itemHolder = new FlowLayoutWidget();
            itemHolder.Name = "LB item holder";
            itemHolder.Margin = new BorderDouble(0, 0, 0, 0);
            itemHolder.HAnchor = Agg.UI.HAnchor.Max_FitToChildren_ParentWidth;
            itemHolder.AddChild(child);
            itemHolder.VAnchor = VAnchor.FitToChildren;
            topToBottomItemList.AddChild(itemHolder, indexInChildrenList);
        }

        bool settingLocalBounds = false;
        public override RectangleDouble LocalBounds
        {
            set
            {
                if (!settingLocalBounds)
                {
                    Vector2 currentTopLeftOffset = new Vector2();
                    if (Parent != null)
                    {
                        currentTopLeftOffset = TopLeftOffset;
                    }
                    settingLocalBounds = true;
                    if (topToBottomItemList != null)
                    {
                        if (VerticalScrollBar.Visible)
                        {
                            topToBottomItemList.Width = Math.Max(0, value.Width - ScrollArea.Padding.Width - topToBottomItemList.Margin.Width - VerticalScrollBar.Width);
                        }
                        else
                        {
                            topToBottomItemList.Width = Math.Max(0, value.Width - ScrollArea.Padding.Width - topToBottomItemList.Margin.Width);
                        }
                    }

                    base.LocalBounds = value;
                    if (Parent != null)
                    {
                        TopLeftOffset = currentTopLeftOffset;
                    }
                    settingLocalBounds = false;
                }
            }
        }

        public override void RemoveChild(int index)
        {
            topToBottomItemList.RemoveChild(index);
        }

        public override void RemoveChild(GuiWidget childToRemove)
        {
            for (int i = topToBottomItemList.Children.Count - 1; i >= 0; i--)
            {
                GuiWidget itemHolder = topToBottomItemList.Children[i];
                if (itemHolder == childToRemove || itemHolder.Children[0] == childToRemove)
                {
                    topToBottomItemList.RemoveChild(itemHolder);
                }
            }
        }

        public override void RemoveAllChildren()
        {
            topToBottomItemList.RemoveAllChildren();
        }


    }
}