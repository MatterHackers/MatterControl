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

namespace MatterHackers.MatterControl.PrintQueue
{
    public class PrintQueueControl : ScrollableWidget
    {
        public RootedObjectEventHandler ItemAdded = new RootedObjectEventHandler();
        public RootedObjectEventHandler ItemRemoved = new RootedObjectEventHandler();

        static PrintQueueControl instance;
        public static PrintQueueControl Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new PrintQueueControl();
                    instance.LoadDefaultQueue();
                    instance.EnsureSelection();
                }
                return instance;
            }
        }

        // make this private so it can only be built from the Instance
		private void SetDisplayAttributes()
		{
			this.MinimumSize = new Vector2(0, 200);
			this.AnchorAll();
            this.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;
			this.AutoScroll = true;
			this.ScrollArea.Padding = new BorderDouble(3, 3, 15, 3);
		}

		private void AddWatermark()
		{
			string imagePathAndFile = Path.Combine(ApplicationDataStorage.Instance.ApplicationStaticDataPath, "OEMSettings", "watermark.png");
            if (File.Exists(imagePathAndFile))
            {
                ImageBuffer wattermarkImage = new ImageBuffer();
                ImageBMPIO.LoadImageData(imagePathAndFile, wattermarkImage);
                GuiWidget watermarkWidget = new ImageWidget(wattermarkImage);
                watermarkWidget.VAnchor = Agg.UI.VAnchor.ParentCenter;
                watermarkWidget.HAnchor = Agg.UI.HAnchor.ParentCenter;
                this.AddChildToBackground(watermarkWidget);
            }
		}

        public void EnsureSelection()
        {
            if (Count > 0)
            {
                if (SelectedIndex < 0)
                {
                    SelectedIndex = 0;
                }
                else if (SelectedIndex > Count - 1)
                {
                    SelectedIndex = Count - 1;
                }
            }
        }

        public void SwapItemsDurringUiAction(int indexA, int indexB)
        {
            UiThread.RunOnIdle(SwapItems, new SwapIndexArgs(indexA, indexB));
        }

        void SwapItems(object state)
        {
            int indexA = ((SwapIndexArgs)state).indexA;
            int indexB = ((SwapIndexArgs)state).indexB;
            int selectedAtEnd = indexB;
            // make sure indexA is the smaller index
            if (indexA > indexB)
            {
                int temp = indexA;
                indexA = indexB;
                indexB = temp;
            }

            if (indexA >= 0 && indexA < Count
                && indexB >= 0 && indexB < Count
                && indexA != indexB)
            {
                GuiWidget itemA = topToBottomItemList.Children[indexA];
                GuiWidget itemB = topToBottomItemList.Children[indexB];
                topToBottomItemList.RemoveChild(indexB);
                topToBottomItemList.RemoveChild(indexA);
                topToBottomItemList.AddChild(itemB, indexA);
                topToBottomItemList.AddChild(itemA, indexB);

                AddItemHandlers(itemA);
                AddItemHandlers(itemB);

                this.SelectedIndex = selectedAtEnd;
            }
        }

        public void MoveToNext()
        {
            if (SelectedIndex >= 0 && SelectedIndex < Count)
            {
                if (this.SelectedIndex == Count - 1)
                {
                    this.SelectedIndex = 0;
                }
                else
                {
                    this.SelectedIndex++;
                }
                
            }
        }

        public void MoveSelectedToBottom()
        {
            if (SelectedIndex >= 0 && SelectedIndex < Count)
            {
                int currentIndex = SelectedIndex;
                PrintQueueItem replacementItem = new PrintQueueItem(PrintQueueControl.Instance.SelectedPart.Name, PrintQueueControl.Instance.SelectedPart.FileLocation);
                this.RemoveChild(SelectedIndex);
                this.AddChild(replacementItem);
                this.SelectedIndex = currentIndex;
            }
        }

        class RemoveIndexArgs
        {
            internal int index;
            internal RemoveIndexArgs(int index)
            {
                this.index = index;
            }
        }

        class SwapIndexArgs
        {
            internal int indexA;
            internal int indexB;
            internal SwapIndexArgs(int indexA, int indexB)
            {
                this.indexA = indexA;
                this.indexB = indexB;
            }
        }

        public void RemoveIndex(int index)
        {
            UiThread.RunOnIdle(RemoveIndexAfterEvent, new RemoveIndexArgs(index));
        }

        void RemoveIndexAfterEvent(object state)
        {
            RemoveIndexArgs removeArgs = state as RemoveIndexArgs;
            if (removeArgs != null & removeArgs.index >= 0 && removeArgs.index < Count)
            {
                int currentIndex = removeArgs.index;

                //If the item to be removed is the active print item, set the active print item to null.
                GuiWidget itemHolder = topToBottomItemList.Children[currentIndex];
                PrintQueueItem child = (PrintQueueItem)itemHolder.Children[0];
                if (child.isActivePrint)
                {
                    if (PrinterCommunication.Instance.PrinterIsPrinting)
                    {
                        return;
                    }
                    PrinterCommunication.Instance.ActivePrintItem = null;
                }
                RemoveChild(currentIndex);
                SelectedIndex = System.Math.Min(SelectedIndex, Count - 1);

                SaveDefaultQueue();
            }
        }

        public PrintItemWrapper SelectedPart
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

        public PrintQueueItem GetPrintQueueItem(int index)
        {
            if (index >= 0 && index < topToBottomItemList.Children.Count)
            {
                GuiWidget itemHolder = topToBottomItemList.Children[index];
                PrintQueueItem child = (PrintQueueItem)itemHolder.Children[0];

                return child;
            }

            return null;
        }

        public int GetIndex(PrintItemWrapper printItem)
        {
            for (int i = 0; i < topToBottomItemList.Children.Count; i++)
            {
                PrintQueueItem queueItem = GetPrintQueueItem(i);
                if (queueItem != null && queueItem.PrintItemWrapper == printItem)
                {
                    return i;
                }
            }

            return -1;
        }

        public string[] GetItemNames()
        {
            List<string> itemNames = new List<string>(); ;
            for (int i = 0; i < topToBottomItemList.Children.Count; i++)
            {
                PrintQueueItem queueItem = GetPrintQueueItem(i);
                if (queueItem != null)
                {
                    itemNames.Add(queueItem.PrintItemWrapper.Name);
                }
            }

            return itemNames.ToArray();
        }

        public PrintItemWrapper GetSTLToPrint(int index)
        {
            if(index >= 0 && index < Count)
            {
                return GetPrintQueueItem(index).PrintItemWrapper;
            }

            return null;
        }

        public List<PrintItem> CreateReadOnlyPartList()
        {
            List<PrintItem> listToReturn = new List<PrintItem>();
            for (int i = 0; i < Count; i++)
            {
                listToReturn.Add(GetSTLToPrint(i).PrintItem);
            }
            return listToReturn;
        }

        public void LoadDefaultQueue()
        {
            RemoveAllChildren();
            ManifestFileHandler manifest = new ManifestFileHandler(null);
            List<PrintItem> partFiles = manifest.ImportFromJson();
            if (partFiles != null)
            {
                foreach (PrintItem part in partFiles)
                {
                    PrintQueueControl.Instance.AddChild(new PrintQueueItem(part.Name, part.FileLocation));
                }
            }
        }

        public void SaveDefaultQueue()
        {
            List<PrintItem> partList = PrintQueueControl.Instance.CreateReadOnlyPartList();
            ManifestFileHandler manifest = new ManifestFileHandler(partList);
            manifest.ExportToJson();
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

                for (int index = 0; index < topToBottomItemList.Children.Count; index++)
                {
                    GuiWidget child = topToBottomItemList.Children[index];
                    if (index == selectedIndex)
                    {
                        ((PrintQueueItem)child.Children[0]).isSelectedItem = true;
                        if (!PrinterCommunication.Instance.PrinterIsPrinting && !PrinterCommunication.Instance.PrinterIsPaused)
                        {
                            
                            ((PrintQueueItem)child.Children[0]).isActivePrint = true;
                            PrinterCommunication.Instance.ActivePrintItem = ((PrintQueueItem)child.Children[0]).PrintItemWrapper;
                        }
                    }
                    else
                    {
                        if (((PrintQueueItem)child.Children[0]).isSelectedItem)
                        {
                            ((PrintQueueItem)child.Children[0]).isSelectedItem = false;
                        }
                        if (!PrinterCommunication.Instance.PrinterIsPrinting && !PrinterCommunication.Instance.PrinterIsPaused)
                        {
                            if (((PrintQueueItem)child.Children[0]).isActivePrint)
                            {
                                ((PrintQueueItem)child.Children[0]).isActivePrint = false;
                            }
                        }
                    }
                    child.Invalidate();

                    Invalidate();
                }
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
                            ((PrintQueueItem)child.Children[0]).isHoverItem = true;
                        }
                        else if (((PrintQueueItem)child.Children[0]).isHoverItem == true)
                        {
                            ((PrintQueueItem)child.Children[0]).isHoverItem = false;
                        }
                        child.Invalidate();
                    }

                    Invalidate();
                }
            }
        }

        public PrintQueueControl()
        {
            SetDisplayAttributes();
			AddWatermark();
            ScrollArea.HAnchor |= Agg.UI.HAnchor.ParentLeftRight;

            AutoScroll = true;
            topToBottomItemList = new FlowLayoutWidget(FlowDirection.TopToBottom);
            topToBottomItemList.HAnchor = Agg.UI.HAnchor.Max_FitToChildren_ParentWidth;
            base.AddChild(topToBottomItemList);
        }

        public override void AddChild(GuiWidget childToAdd, int indexInChildrenList = -1)
        {
            FlowLayoutWidget itemHolder = new FlowLayoutWidget();
            itemHolder.Name = "LB item holder";
            itemHolder.Margin = new BorderDouble(0, 0, 0, 0);
            itemHolder.HAnchor = Agg.UI.HAnchor.Max_FitToChildren_ParentWidth;
            itemHolder.AddChild(childToAdd);
            itemHolder.VAnchor = VAnchor.FitToChildren;
            topToBottomItemList.AddChild(itemHolder, indexInChildrenList);

            AddItemHandlers(itemHolder);

            ItemAdded.CallEvents(this, new GuiWidgetEventArgs(childToAdd));
        }

        private void AddItemHandlers(GuiWidget itemHolder)
        {
            itemHolder.MouseEnterBounds += new EventHandler(itemToAdd_MouseEnterBounds);
            itemHolder.MouseLeaveBounds += new EventHandler(itemToAdd_MouseLeaveBounds);
            itemHolder.MouseDownInBounds += new MouseEventHandler(itemHolder_MouseDownInBounds);
            itemHolder.ParentChanged += new EventHandler(itemHolder_ParentChanged);
        }

        public override void RemoveAllChildren()
        {
            for (int i = topToBottomItemList.Children.Count-1; i >= 0; i--)
            {
                RemoveIndex(i);
            }
        }

        public override void RemoveChild(int index)
        {
            GuiWidget childToRemove = topToBottomItemList.Children[index];
            RemoveChild(childToRemove);
        }

        public override void RemoveChild(GuiWidget childToRemove)
        {
            for (int i = topToBottomItemList.Children.Count - 1; i >= 0; i--)
            {
                GuiWidget itemHolder = topToBottomItemList.Children[i];
                if (itemHolder == childToRemove || itemHolder.Children[0] == childToRemove)
                {
                    topToBottomItemList.RemoveChild(itemHolder);
                    OnItemRemoved(new GuiWidgetEventArgs(childToRemove));
                }
            }
        }

        private void OnItemRemoved(GuiWidgetEventArgs e)
        {
            ItemRemoved.CallEvents(this, e);
        }

        bool settingLocalBounds = false;
        public override RectangleDouble LocalBounds
        {
            set
            {
                if (!settingLocalBounds && value != LocalBounds)
                {
                    Vector2 currentTopLeftOffset = new Vector2();
                    if (Parent != null)
                    {
                        currentTopLeftOffset = TopLeftOffset;
                    }
                    settingLocalBounds = true;

                    base.LocalBounds = value;

                    if (Parent != null)
                    {
                        TopLeftOffset = currentTopLeftOffset;
                    }
                    settingLocalBounds = false;
                }
            }

            get
            {
                return base.LocalBounds;
            }
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
            GuiWidget widgetClicked = ((GuiWidget)sender);
            for (int index = 0; index < topToBottomItemList.Children.Count; index++)
            {
                GuiWidget child = topToBottomItemList.Children[index];
                if (child == widgetClicked)
                {
                    SelectedIndex = index;
                }
            }
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

        public PrintQueueItem SelectedPrintQueueItem()
        {
            return GetPrintQueueItem(SelectedIndex);
        }
    }
}