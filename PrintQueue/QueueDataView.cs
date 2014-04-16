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
    public class QueueDataView : ScrollableWidget
    {
        event EventHandler unregisterEvents;

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
                ImageIO.LoadImageData(imagePathAndFile, wattermarkImage);
                //if (!ActiveTheme.Instance.IsDarkTheme)
                //{
                //    InvertLightness.DoInvertLightness(wattermarkImage);
                //}

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

                // force a refresh of the ui in the case where we are still on the same index but have changed items.
                SelectedIndex = SelectedIndex;
            }
            else
            {
                SelectedIndex = -1;
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
                PrintItem replacementItem = new PrintItem(SelectedPart.Name, SelectedPart.FileLocation);
                QueueData.Instance.RemoveAt(SelectedIndex);
                this.SelectedIndex = currentIndex;
            }
        }

        public PrintItemWrapper SelectedPart
        {
            get
            {
                if (SelectedIndex >= 0)
                {
                    return QueueData.Instance.GetPrintItem(SelectedIndex);
                }
                else
                {
                    return null;
                }
            }
        }

        public RowItem GetPrintQueueItem(int index)
        {
            if (index >= 0 && index < topToBottomItemList.Children.Count)
            {
                GuiWidget itemHolder = topToBottomItemList.Children[index];
                RowItem child = (RowItem)itemHolder.Children[0];

                return child;
            }

            return null;
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
                        ((RowItem)child.Children[0]).isSelectedItem = true;
                        if (!PrinterCommunication.Instance.PrinterIsPrinting && !PrinterCommunication.Instance.PrinterIsPaused)
                        {
                            
                            ((RowItem)child.Children[0]).isActivePrint = true;
                            PrinterCommunication.Instance.ActivePrintItem = ((RowItem)child.Children[0]).PrintItemWrapper;
                        }
                    }
                    else
                    {
                        if (((RowItem)child.Children[0]).isSelectedItem)
                        {
                            ((RowItem)child.Children[0]).isSelectedItem = false;
                        }
                        if (!PrinterCommunication.Instance.PrinterIsPrinting && !PrinterCommunication.Instance.PrinterIsPaused)
                        {
                            if (((RowItem)child.Children[0]).isActivePrint)
                            {
                                ((RowItem)child.Children[0]).isActivePrint = false;
                            }
                        }
                    }
                    child.Invalidate();

                    Invalidate();
                }

                if (QueueData.Instance.Count == 0)
                {
                    PrinterCommunication.Instance.ActivePrintItem = null;
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
                            ((RowItem)child.Children[0]).isHoverItem = true;
                        }
                        else if (((RowItem)child.Children[0]).isHoverItem == true)
                        {
                            ((RowItem)child.Children[0]).isHoverItem = false;
                        }
                        child.Invalidate();
                    }

                    Invalidate();
                }
            }
        }

        public QueueDataView()
        {
            Name = "PrintQueueControl";

            SetDisplayAttributes();
			AddWatermark();
            ScrollArea.HAnchor |= Agg.UI.HAnchor.ParentLeftRight;

            AutoScroll = true;
            topToBottomItemList = new FlowLayoutWidget(FlowDirection.TopToBottom);
            topToBottomItemList.Name = "PrintQueueControl TopToBottom";
            topToBottomItemList.HAnchor = Agg.UI.HAnchor.Max_FitToChildren_ParentWidth;
            base.AddChild(topToBottomItemList);

            for (int i = 0; i < QueueData.Instance.Count; i++)
            {
                PrintItemWrapper item = QueueData.Instance.GetPrintItem(i);
                RowItem queueItem = new RowItem(item, this);
                AddChild(queueItem);
            }

            QueueData.Instance.ItemAdded.RegisterEvent(ItemAddedToQueue, ref unregisterEvents);
            QueueData.Instance.ItemRemoved.RegisterEvent(ItemRemovedFromToQueue, ref unregisterEvents);
            QueueData.Instance.OrderChanged.RegisterEvent(QueueOrderChanged, ref unregisterEvents);
        }

        void ItemAddedToQueue(object sender, EventArgs e)
        {
            QueueData.IndexArgs addedIndexArgs = e as QueueData.IndexArgs;
            PrintItemWrapper item = QueueData.Instance.GetPrintItem(addedIndexArgs.Index);
            RowItem queueItem = new RowItem(item, this);
            AddChild(queueItem, addedIndexArgs.Index);

            EnsureSelection();
        }

        void ItemRemovedFromToQueue(object sender, EventArgs e)
        {
            QueueData.IndexArgs removeIndexArgs = e as QueueData.IndexArgs;
            topToBottomItemList.RemoveChild(removeIndexArgs.Index);
            EnsureSelection();
            if (QueueData.Instance.Count > 0)
            {
                SelectedIndex = Math.Max(SelectedIndex - 1, 0);
            }
        }

        void QueueOrderChanged(object sender, EventArgs e)
        {
        }

        bool firstDraw = true;
        public override void OnDraw(Graphics2D graphics2D)
        {
            if (firstDraw)
            {
                firstDraw = false;
                EnsureSelection();
            }
            base.OnDraw(graphics2D);
        }

        public override void OnClosed(EventArgs e)
        {
            if (unregisterEvents != null)
            {
                unregisterEvents(this, null);
            }
            base.OnClosed(e);
        }

        public override void AddChild(GuiWidget childToAdd, int indexInChildrenList = -1)
        {
            FlowLayoutWidget itemHolder = new FlowLayoutWidget();
            itemHolder.Name = "PrintQueueControl itemHolder";
            itemHolder.Margin = new BorderDouble(0, 0, 0, 0);
            itemHolder.HAnchor = Agg.UI.HAnchor.Max_FitToChildren_ParentWidth;
            itemHolder.AddChild(childToAdd);
            itemHolder.VAnchor = VAnchor.FitToChildren;
            topToBottomItemList.AddChild(itemHolder, indexInChildrenList);

            AddItemHandlers(itemHolder);
        }

        private void AddItemHandlers(GuiWidget itemHolder)
        {
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

        public RowItem SelectedPrintQueueItem()
        {
            return GetPrintQueueItem(SelectedIndex);
        }
    }
}