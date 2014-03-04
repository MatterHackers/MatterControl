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

namespace MatterHackers.MatterControl.ToolsPage
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
    
    public class ToolsListControl : ScrollableWidget
    {
        static ToolsListControl instance;
        string keywordFilter;
        private DataStorage.PrintItemCollection libraryCollection;

        public static ToolsListControl Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new ToolsListControl();
                    instance.LoadLibraryItems();
                }
                return instance;
            }
        }

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

		private void SetDisplayAttributes()
		{
			this.MinimumSize = new Vector2(0, 200);
			this.AnchorAll();
            this.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;
			this.AutoScroll = true;
			this.ScrollArea.Padding = new BorderDouble(3);
		}

        public void RemoveSelectedIndex()
        {
            if (SelectedIndex >= 0 && SelectedIndex < Count)
            {
                RemoveChild(SelectedIndex);
            }
        }


        public PrintItem SelectedPart
        {
            get
            {
                if (SelectedIndex >= 0)
                {
                    return GetSTLToPrint(SelectedIndex);
                }
                else
                {
                    return null;
                }
            }
        }

        public PrintItem GetSTLToPrint(int index)
        {
            if(index >= 0 && index < Count)
            {
                GuiWidget itemHolder = topToBottomItemList.Children[index];
                ToolsListItem child = (ToolsListItem)itemHolder.Children[0];
                return new PrintItem(child.printItem.Name, child.printItem.FileLocation);
            }

            return null;
        }

        public List<PrintItem> CreateReadOnlyPartList()
        {
            List<PrintItem> listToReturn = new List<PrintItem>();
            for (int i = 0; i < Count; i++)
            {
                listToReturn.Add(GetSTLToPrint(i));
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

        void PreloadLibrary()
        {
            List<string> calibrationPrints = GetLibraryParts();
            foreach (string partFile in calibrationPrints)
            {
                string partFullPath = Path.Combine(ApplicationDataStorage.Instance.ApplicationStaticDataPath, "OEMSettings", "SampleParts", partFile);
                if (System.IO.File.Exists(partFullPath))
                {
                    PrintItem printItem = new PrintItem();
                    printItem.Name = System.IO.Path.GetFileNameWithoutExtension(partFullPath);
                    printItem.FileLocation = System.IO.Path.GetFullPath(partFullPath);
                    printItem.PrintItemCollectionID = ToolsListControl.Instance.LibraryCollection.Id;
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

        public void ClearSelectedItems()
        {
            List<ToolsListItem> itemsToClear = new List<ToolsListItem>();
            
            foreach(ToolsListItem item in instance.SelectedItems)
            {
                itemsToClear.Add(item);                
            }            
            foreach (ToolsListItem item in itemsToClear)
            {
                item.isSelectedItem = false;
                item.selectionCheckBox.Checked = false;
            }
        }

        public void LoadLibraryItems()
        {
            RemoveAllChildren();
            IEnumerable<DataStorage.PrintItem> partFiles = GetLibraryItems(instance.KeywordFilter);
            if (partFiles != null)
            {
                foreach (PrintItem part in partFiles)
                {
                    ToolsListControl.Instance.AddChild(new ToolsListItem(new PrintItemWrapper(part)));
                }
            }
        }



        public void SaveLibraryItems()
        {
            //
        }

        public delegate void SelectedValueChangedEventHandler(object sender, EventArgs e);
        public event SelectedValueChangedEventHandler SelectedValueChanged;
        public delegate void HoverValueChangedEventHandler(object sender, EventArgs e);
        public event HoverValueChangedEventHandler HoverValueChanged;

        protected FlowLayoutWidget topToBottomItemList;

        RGBA_Bytes hoverColor = new RGBA_Bytes(204, 204, 204, 255);
        //RGBA_Bytes hoverColor = new RGBA_Bytes(0, 140, 158, 255);
        RGBA_Bytes selectedColor = new RGBA_Bytes(180, 180, 180, 255);
        //RGBA_Bytes selectedColor = new RGBA_Bytes(0, 95, 107, 255);
        RGBA_Bytes baseColor = new RGBA_Bytes(255, 255, 255);

        public SelectedPrintItems<ToolsListItem> SelectedItems = new SelectedPrintItems<ToolsListItem>();
        int selectedIndex = -1;
        int hoverIndex = -1;
        int dragIndex = -1;

        public int Count
        {
            get
            {
                return topToBottomItemList.Children.Count;
            }
        }

        public int SelectedIndex
        {
            get
            {
                return selectedIndex;
            }
            set
            {
                if (value < -1 || value >= topToBottomItemList.Children.Count)
                {
                    throw new ArgumentOutOfRangeException();
                }                
                selectedIndex = value;                
                OnSelectedIndexChanged();                
            }
        }

        public int DragIndex
        {
            get
            {
                return dragIndex;
            }
            set
            {
                if (value < -1 || value >= topToBottomItemList.Children.Count)
                {
                    throw new ArgumentOutOfRangeException();
                }

                if (value != dragIndex)
                {
                    dragIndex = value;
                }
            }
        }

        public int HoverIndex
        {
            get
            {
                return hoverIndex;
            }
            set
            {
                if (value < -1 || value >= topToBottomItemList.Children.Count)
                {
                    throw new ArgumentOutOfRangeException();
                }

                if (value != hoverIndex)
                {
                    hoverIndex = value;
                    OnHoverIndexChanged();
                    
                    for (int index = 0; index < topToBottomItemList.Children.Count; index++)
                    {                        
                        GuiWidget child = topToBottomItemList.Children[index];
                        if (index == HoverIndex)
                        {
                            ((ToolsListItem)child.Children[0]).isHoverItem = true;
                        }
                        else if (((ToolsListItem)child.Children[0]).isHoverItem == true)
                        {
                            ((ToolsListItem)child.Children[0]).isHoverItem = false;
                        }
                        child.Invalidate();
                    }

                    Invalidate();
                }
            }
        }

        public ToolsListControl()
        {
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
            this.MouseLeaveBounds += new EventHandler(control_MouseLeaveBounds);            
        }

        public override void AddChild(GuiWidget child, int indexInChildrenList = -1)
        {
            FlowLayoutWidget itemHolder = new FlowLayoutWidget();
            itemHolder.Name = "LB item holder";
            itemHolder.Margin = new BorderDouble(3, 0, 0, 0);
            itemHolder.HAnchor = Agg.UI.HAnchor.Max_FitToChildren_ParentWidth;
            itemHolder.AddChild(child);
            itemHolder.VAnchor = VAnchor.FitToChildren;
            topToBottomItemList.AddChild(itemHolder, indexInChildrenList);

            itemHolder.MouseEnterBounds += new EventHandler(itemToAdd_MouseEnterBounds);
            itemHolder.MouseLeaveBounds += new EventHandler(itemToAdd_MouseLeaveBounds);
            itemHolder.MouseDownInBounds += new MouseEventHandler(itemHolder_MouseDownInBounds);
            itemHolder.ParentChanged += new EventHandler(itemHolder_ParentChanged);
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

        public void RemoveSelectedItems()
        {
            foreach (ToolsListItem item in instance.SelectedItems)
            {
                RemoveChild(item);
                item.printItem.Delete();
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

        void itemHolder_ParentChanged(object sender, EventArgs e)
        {
            FlowLayoutWidget itemHolder = (FlowLayoutWidget)sender;
            itemHolder.MouseEnterBounds -= new EventHandler(itemToAdd_MouseEnterBounds);
            itemHolder.MouseLeaveBounds -= new EventHandler(itemToAdd_MouseLeaveBounds);
            itemHolder.MouseDownInBounds -= new MouseEventHandler(itemHolder_MouseDownInBounds);
            itemHolder.ParentChanged -= new EventHandler(itemHolder_ParentChanged);
        }

        void itemHolder_MouseDownInBounds(object sender, MouseEventArgs mouseEvent)
        {

        }

        void control_MouseLeaveBounds(object sender, EventArgs e)
        {
            HoverIndex = -1;
        }

        void itemToAdd_MouseLeaveBounds(object sender, EventArgs e)
        {
            GuiWidget widgetLeft = ((GuiWidget)sender);
            
            if (SelectedIndex >= 0)
            {
                if (widgetLeft != topToBottomItemList.Children[SelectedIndex])
                {
                    widgetLeft.BackgroundColor = new RGBA_Bytes();
                    widgetLeft.Invalidate();                    
                    Invalidate();
                }
            }
        }

        void itemToAdd_MouseEnterBounds(object sender, EventArgs e)
        {
            GuiWidget widgetEntered = ((GuiWidget)sender);
            for (int index = 0; index < topToBottomItemList.Children.Count; index++)
            {
                GuiWidget child = topToBottomItemList.Children[index];
                if (child == widgetEntered)
                {
                    HoverIndex = index;
                }
            }
        }

        public void OnSelectedIndexChanged()
        {
            Invalidate();
            if (SelectedValueChanged != null)
            {
                SelectedValueChanged(this, null);
            }
        }

        public void OnHoverIndexChanged()
        {
            Invalidate();
            if (HoverValueChanged != null)
            {
                HoverValueChanged(this, null);
            }
        }

        public void ClearSelected()
        {
            if (selectedIndex != -1)
            {
                selectedIndex = -1;
                OnSelectedIndexChanged();
            }
        }        

        public GuiWidget SelectedItem
        {
            get
            {
                if (SelectedIndex != -1)
                {
                    return Children[SelectedIndex];
                }

                return null;
            }

            set
            {
                for (int i = 0; i < Children.Count; i++)
                {
                    if (Children[SelectedIndex] == value)
                    {
                        SelectedIndex = i;
                    }
                }
            }
        }
    }
}