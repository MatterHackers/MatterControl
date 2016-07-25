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

using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.IO;

namespace MatterHackers.MatterControl.PrintQueue
{
	public class QueueDataView : ScrollableWidget
	{
		public static int selectedQueueItemIndex = -1;

		private event EventHandler unregisterEvents;

		// make this private so it can only be built from the Instance
		private void SetDisplayAttributes()
		{
			this.MinimumSize = new Vector2(0, 200);
			this.AnchorAll();
			this.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;
			this.AutoScroll = true;
			this.ScrollArea.Padding = new BorderDouble(3);
		}

		private bool editMode = false;

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
						this.EnsureSelection();
					}
					else
					{
						foreach (var item in SelectedItems)
						{
							item.isSelectedItem = true;
							item.selectionCheckBox.Checked = true;
						}
					}
				}
			}
		}

		public void ClearSelectedItems()
		{
			foreach (var item in SelectedItems)
			{
				item.isSelectedItem = false;
				item.selectionCheckBox.Checked = false;
			}
			this.SelectedItems.Clear();
		}

		private void AddWatermark()
		{
			string imagePathAndFile = Path.Combine("OEMSettings", "watermark.png");
			if (StaticData.Instance.FileExists(imagePathAndFile))
			{
				ImageBuffer wattermarkImage = new ImageBuffer();
				StaticData.Instance.LoadImage(imagePathAndFile, wattermarkImage);

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
				PrintItem replacementItem = new PrintItem(SelectedPrintItem.Name, SelectedPrintItem.FileLocation);
				QueueData.Instance.RemoveAt(SelectedIndex);
				this.SelectedIndex = currentIndex;
			}
		}

		public SelectedListItems<QueueRowItem> SelectedItems = new SelectedListItems<QueueRowItem>();

		public PrintItemWrapper SelectedPrintItem
		{
			get
			{
				if (SelectedIndex >= 0)
				{
					return QueueData.Instance.GetPrintItemWrapper(SelectedIndex);
				}
				else
				{
					return null;
				}
			}

			set
			{
				if (SelectedPrintItem != value)
				{
					for (int index = 0; index < topToBottomItemList.Children.Count; index++)
					{
						GuiWidget child = topToBottomItemList.Children[index];
						QueueRowItem rowItem = child.Children[0] as QueueRowItem;
						if (rowItem.PrintItemWrapper == value)
						{
							SelectedIndex = index;
							return;
						}
					}

					throw new Exception("Item not in queue.");
				}
			}
		}

		public QueueRowItem GetPrintQueueItem(int index)
		{
			if (index >= 0 && index < topToBottomItemList.Children.Count)
			{
				GuiWidget itemHolder = topToBottomItemList.Children[index];
				QueueRowItem child = (QueueRowItem)itemHolder.Children[0];

				return child;
			}

			return null;
		}

		public delegate void SelectedValueChangedEventHandler(object sender, EventArgs e);

		public event SelectedValueChangedEventHandler SelectedValueChanged;

		public delegate void HoverValueChangedEventHandler(object sender, EventArgs e);

		public event HoverValueChangedEventHandler HoverValueChanged;

		protected FlowLayoutWidget topToBottomItemList;

		private RGBA_Bytes hoverColor = new RGBA_Bytes(204, 204, 204, 255);

		//RGBA_Bytes hoverColor = new RGBA_Bytes(0, 140, 158, 255);
		private RGBA_Bytes selectedColor = new RGBA_Bytes(180, 180, 180, 255);

		//RGBA_Bytes selectedColor = new RGBA_Bytes(0, 95, 107, 255);
		private RGBA_Bytes baseColor = new RGBA_Bytes(255, 255, 255);

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
				return QueueData.Instance.SelectedIndex;
			}
			set
			{
				QueueData.Instance.SelectedIndex = value;
			}
		}

		public override void SendToChildren(object objectToRout)
		{
			base.SendToChildren(objectToRout);
		}

		public QueueDataView()
		{
			Name = "PrintQueueControl";

			SetDisplayAttributes();
			AddWatermark();
			ScrollArea.HAnchor = HAnchor.ParentLeftRight;

			AutoScroll = true;
			topToBottomItemList = new FlowLayoutWidget(FlowDirection.TopToBottom);
			topToBottomItemList.Name = "PrintQueueControl TopToBottom";
			topToBottomItemList.HAnchor = HAnchor.ParentLeftRight;
			base.AddChild(topToBottomItemList);

			for (int i = 0; i < QueueData.Instance.Count; i++)
			{
				PrintItemWrapper item = QueueData.Instance.GetPrintItemWrapper(i);
				QueueRowItem queueItem = new QueueRowItem(item, this);
				AddChild(queueItem);
			}

			QueueData.Instance.SelectedIndexChanged.RegisterEvent(SelectedIndexChanged, ref unregisterEvents);
			QueueData.Instance.ItemAdded.RegisterEvent(ItemAddedToQueue, ref unregisterEvents);
			QueueData.Instance.ItemRemoved.RegisterEvent(ItemRemovedFromToQueue, ref unregisterEvents);
			QueueData.Instance.OrderChanged.RegisterEvent(QueueOrderChanged, ref unregisterEvents);

			PrinterConnectionAndCommunication.Instance.ActivePrintItemChanged.RegisterEvent(PrintItemChange, ref unregisterEvents);

			WidescreenPanel.PreChangePanels.RegisterEvent(SaveCurrentlySelctedItemIndex, ref unregisterEvents);

			selectedQueueItemIndex = Math.Min(selectedQueueItemIndex, QueueData.Instance.Count - 1);
			SelectedIndex = selectedQueueItemIndex;
			EnsureSelection();
		}

		private void SaveCurrentlySelctedItemIndex(object sender, EventArgs e)
		{
			selectedQueueItemIndex = SelectedIndex;
		}

		private void PrintItemChange(object sender, EventArgs e)
		{
			SelectedPrintItem = PrinterConnectionAndCommunication.Instance.ActivePrintItem;
		}

		private void SelectedIndexChanged(object sender, EventArgs e)
		{
			// Skip this processing while in EditMode
			if (this.editMode) return;

			OnSelectedIndexChanged();
			for (int index = 0; index < topToBottomItemList.Children.Count; index++)
			{
				GuiWidget child = topToBottomItemList.Children[index];
				var queueRowItem = (QueueRowItem)child.Children[0];

				if (index == SelectedIndex)
				{
					if (!PrinterConnectionAndCommunication.Instance.PrinterIsPrinting && !PrinterConnectionAndCommunication.Instance.PrinterIsPaused)
					{
						queueRowItem.isActivePrint = true;
						PrinterConnectionAndCommunication.Instance.ActivePrintItem = queueRowItem.PrintItemWrapper;
					}
					else if (queueRowItem.PrintItemWrapper == PrinterConnectionAndCommunication.Instance.ActivePrintItem)
					{
						// the selection must be the active print item
						queueRowItem.isActivePrint = true;
					}
				}
				else
				{
					// Don't test for .Checked as the property already performs validation
					queueRowItem.selectionCheckBox.Checked = false;

					if (queueRowItem.isSelectedItem)
					{
						queueRowItem.isSelectedItem = false;
					}

					if (!PrinterConnectionAndCommunication.Instance.PrinterIsPrinting && !PrinterConnectionAndCommunication.Instance.PrinterIsPaused)
					{
						if (queueRowItem.isActivePrint)
						{
							queueRowItem.isActivePrint = false;
						}
					}
				}
				//child.Invalidate();
				//Invalidate();
			}

			if (QueueData.Instance.Count == 0)
			{
				PrinterConnectionAndCommunication.Instance.ActivePrintItem = null;
			}
		}

		private void ItemAddedToQueue(object sender, EventArgs e)
		{
			IndexArgs addedIndexArgs = e as IndexArgs;
			PrintItemWrapper item = QueueData.Instance.GetPrintItemWrapper(addedIndexArgs.Index);
			QueueRowItem queueItem = new QueueRowItem(item, this);
			AddChild(queueItem, addedIndexArgs.Index);

			EnsureSelection();
		}

		private void ItemRemovedFromToQueue(object sender, EventArgs e)
		{
			IndexArgs removeIndexArgs = e as IndexArgs;
			topToBottomItemList.RemoveChild(removeIndexArgs.Index);
			EnsureSelection();
			if (QueueData.Instance.Count > 0 && SelectedIndex > QueueData.Instance.Count - 1)
			{
				SelectedIndex = Math.Max(SelectedIndex - 1, 0);
			}
		}

		private void QueueOrderChanged(object sender, EventArgs e)
		{
			throw new NotImplementedException();
		}

		public override void OnLoad(EventArgs args)
		{
			EnsureSelection();
			base.OnLoad(args);
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
			itemHolder.HAnchor = HAnchor.ParentLeftRight;
			itemHolder.AddChild(childToAdd);
			itemHolder.VAnchor = VAnchor.FitToChildren;
			topToBottomItemList.AddChild(itemHolder, indexInChildrenList);

			AddItemHandlers(itemHolder);
		}

		private void AddItemHandlers(GuiWidget itemHolder)
		{
			itemHolder.MouseDownInBounds += itemHolder_MouseDownInBounds;
			itemHolder.ParentChanged += new EventHandler(itemHolder_ParentChanged);
		}

		private bool settingLocalBounds = false;

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

		private void itemHolder_ParentChanged(object sender, EventArgs e)
		{
			FlowLayoutWidget itemHolder = (FlowLayoutWidget)sender;
			itemHolder.MouseDownInBounds -= itemHolder_MouseDownInBounds;
			itemHolder.ParentChanged -= new EventHandler(itemHolder_ParentChanged);
		}

		private void itemHolder_MouseDownInBounds(object sender, MouseEventArgs mouseEvent)
		{
			// Hard-coded processing rule to avoid changing the SelectedIndex when clicks occur
			// with the thumbnail region - aka the first 55 pixels
			if (mouseEvent.X < 56) return;

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

		private void itemToAdd_MouseLeaveBounds(object sender, EventArgs e)
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

		static bool WidgetOrChildIsFirstUnderMouse(GuiWidget startWidget)
		{
			if (startWidget.UnderMouseState == UnderMouseState.FirstUnderMouse)
			{
				return true;
			}

			foreach (GuiWidget child in startWidget.Children)
			{
				if (child != null)
				{
					if (WidgetOrChildIsFirstUnderMouse(child))
					{
						return true;
					}
				}
			}

			return false;
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
			if (SelectedIndex != -1)
			{
				SelectedIndex = -1;
				OnSelectedIndexChanged();
			}
		}

		public GuiWidget SelectedItem
		{
			get
			{
				if (SelectedIndex != -1)
				{
					return topToBottomItemList.Children[SelectedIndex].Children[0];
				}

				return null;
			}

			set
			{
				for (int i = 0; i < Children.Count; i++)
				{
					if (topToBottomItemList.Children[SelectedIndex].Children[0] == value)
					{
						SelectedIndex = i;
					}
				}
			}
		}

		public QueueRowItem SelectedPrintQueueItem()
		{
			return GetPrintQueueItem(SelectedIndex);
		}
	}

	public class PrintItemAction
	{
		public string Title { get; set; }
		public Action<IEnumerable<QueueRowItem>, QueueDataWidget> Action { get; set; }
		public bool SingleItemOnly { get; set; } = false;
	}

	public abstract class PrintItemMenuExtension
	{
		public abstract IEnumerable<PrintItemAction> GetMenuItems();
	}
}