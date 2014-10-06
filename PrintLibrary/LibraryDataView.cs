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

namespace MatterHackers.MatterControl.PrintLibrary
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

    public class LibraryDataView : ScrollableWidget
    {
        event EventHandler unregisterEvents;

        private void SetDisplayAttributes()
        {
            this.MinimumSize = new Vector2(0, 200);
            this.AnchorAll();
            this.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;
            this.AutoScroll = true;
            this.ScrollArea.Padding = new BorderDouble(3, 3, 15, 3);
        }

        bool editMode = false;
        public bool EditMode
        {
            get { return editMode; }
            set
            {
                if (this.editMode != value)
                {
                    this.editMode = value;
                    if (this.editMode == false)
                    {
                        this.ClearSelectedItems();
                    }

                }
            }
        }

        public void RemoveSelectedIndex()
        {
            if (SelectedIndex >= 0 && SelectedIndex < Count)
            {
                RemoveChild(SelectedIndex);
            }
        }

        public PrintItemWrapper SelectedPart
        {
            get
            {
                if (SelectedIndex >= 0)
                {
                    return LibraryData.Instance.GetPrintItemWrapper(SelectedIndex);
                }
                else
                {
                    return null;
                }
            }
        }

        public void ClearSelectedItems()
        {
            foreach (LibraryRowItem item in SelectedItems)
            {
                item.isSelectedItem = false;
                item.selectionCheckBox.Checked = false;
            }
            this.SelectedItems.Clear();
        }

        public delegate void SelectedValueChangedEventHandler(object sender, EventArgs e);
        public event SelectedValueChangedEventHandler SelectedValueChanged;
        public delegate void HoverValueChangedEventHandler(object sender, EventArgs e);
        public event HoverValueChangedEventHandler HoverValueChanged;

        protected FlowLayoutWidget topToBottomItemList;

        RGBA_Bytes hoverColor = new RGBA_Bytes(204, 204, 204, 255);
        RGBA_Bytes selectedColor = new RGBA_Bytes(180, 180, 180, 255);
        RGBA_Bytes baseColor = new RGBA_Bytes(255, 255, 255);

        public SelectedPrintItems<LibraryRowItem> SelectedItems = new SelectedPrintItems<LibraryRowItem>();
        int selectedIndex = -1;
        int hoverIndex = -1;
        int dragIndex = -1;

        int Count
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
                            ((LibraryRowItem)child.Children[0]).IsHoverItem = true;
                        }
                        else if (((LibraryRowItem)child.Children[0]).IsHoverItem == true)
                        {
                            ((LibraryRowItem)child.Children[0]).IsHoverItem = false;
                        }
                        child.Invalidate();
                    }

                    Invalidate();
                }
            }
        }

        public LibraryDataView()
        {
            SetDisplayAttributes();
            ScrollArea.HAnchor |= Agg.UI.HAnchor.ParentLeftRight;

            AutoScroll = true;
            topToBottomItemList = new FlowLayoutWidget(FlowDirection.TopToBottom);
            topToBottomItemList.HAnchor = Agg.UI.HAnchor.Max_FitToChildren_ParentWidth;
            base.AddChild(topToBottomItemList);

            for (int i = 0; i < LibraryData.Instance.Count; i++)
            {
                PrintItemWrapper item = LibraryData.Instance.GetPrintItemWrapper(i);
                LibraryRowItem queueItem = new LibraryRowItem(item, this);
                AddChild(queueItem);
            }

            this.MouseLeaveBounds += new EventHandler(control_MouseLeaveBounds);
            LibraryData.Instance.DataReloaded.RegisterEvent(LibraryDataReloaded, ref unregisterEvents);
            LibraryData.Instance.ItemAdded.RegisterEvent(ItemAddedToLibrary, ref unregisterEvents);
            LibraryData.Instance.ItemRemoved.RegisterEvent(ItemRemovedFromToLibrary, ref unregisterEvents);
            LibraryData.Instance.OrderChanged.RegisterEvent(LibraryOrderChanged, ref unregisterEvents);
        }

        void LibraryDataReloaded(object sender, EventArgs e)
        {
            this.RemoveListItems();
            for (int i = 0; i < LibraryData.Instance.Count; i++)
            {
                PrintItemWrapper item = LibraryData.Instance.GetPrintItemWrapper(i);
                LibraryRowItem queueItem = new LibraryRowItem(item, this);
                AddChild(queueItem);
            }
        }

        public override void OnClosed(EventArgs e)
        {
            if (unregisterEvents != null)
            {
                unregisterEvents(this, null);
            }
            base.OnClosed(e);
        }

        void ItemAddedToLibrary(object sender, EventArgs e)
        {
            IndexArgs addedIndexArgs = e as IndexArgs;
            PrintItemWrapper item = LibraryData.Instance.GetPrintItemWrapper(addedIndexArgs.Index);
            LibraryRowItem libraryItem = new LibraryRowItem(item, this);
            AddChild(libraryItem, addedIndexArgs.Index);
        }

        void ItemRemovedFromToLibrary(object sender, EventArgs e)
        {
            IndexArgs removeIndexArgs = e as IndexArgs;
            topToBottomItemList.RemoveChild(removeIndexArgs.Index);

            if (LibraryData.Instance.Count > 0)
            {
                SelectedIndex = Math.Max(SelectedIndex - 1, 0);
            }
        }

        void LibraryOrderChanged(object sender, EventArgs e)
        {
            throw new NotImplementedException();
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
            foreach (LibraryRowItem item in SelectedItems)
            {
                LibraryData.Instance.RemoveItem(item.printItemWrapper);
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

        public void RemoveListItems()
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

        public override void OnDraw(Graphics2D graphics2D)
        {
            //activeView.OnDraw(graphics2D);

            base.OnDraw(graphics2D);
        }

        public override void OnMouseDown(MouseEventArgs mouseEvent)
        {
            base.OnMouseDown(mouseEvent);
        }

        public override void OnMouseUp(MouseEventArgs mouseEvent)
        {
            base.OnMouseUp(mouseEvent);
        }

        public override void OnMouseMove(MouseEventArgs mouseEvent)
        {
            base.OnMouseMove(mouseEvent);
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