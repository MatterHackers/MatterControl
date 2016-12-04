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
using System.Linq;

namespace MatterHackers.MatterControl.PrintQueue
{
	public class QueueDataView : ScrollableWidget
	{
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
						QueueData.Instance.MakeSingleSelection();
					}
				}
			}
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

		public QueueRowItem GetQueueRowItem(int index)
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

			for (int i = 0; i < QueueData.Instance.ItemCount; i++)
			{
				PrintItemWrapper item = QueueData.Instance.GetPrintItemWrapper(i);
				QueueRowItem queueItem = new QueueRowItem(item, this);
				AddChild(queueItem);
			}

			QueueData.Instance.SelectedIndexChanged.RegisterEvent(SelectedIndexChanged, ref unregisterEvents);
			QueueData.Instance.ItemAdded.RegisterEvent(ItemAddedToQueue, ref unregisterEvents);
			QueueData.Instance.ItemRemoved.RegisterEvent(ItemRemovedFromQueue, ref unregisterEvents);
			QueueData.Instance.OrderChanged.RegisterEvent(QueueOrderChanged, ref unregisterEvents);

			PrinterConnectionAndCommunication.Instance.ActivePrintItemChanged.RegisterEvent(PrintItemChange, ref unregisterEvents);

			SelectedIndexChanged(null, null);
		}

	private void PrintItemChange(object sender, EventArgs e)
		{
			QueueData.Instance.SelectedPrintItem = PrinterConnectionAndCommunication.Instance.ActivePrintItem;
		}

		private void SelectedIndexChanged(object sender, EventArgs e)
		{
			if (this.editMode == false)
			{
				QueueData.Instance.MakeSingleSelection();
			}

			for (int index = 0; index < topToBottomItemList.Children.Count; index++)
			{
				GuiWidget child = topToBottomItemList.Children[index];
				var queueRowItem = (QueueRowItem)child.Children[0];

				if (QueueData.Instance.SelectedIndexes.Contains(index))
				{
					queueRowItem.isSelectedItem = true;
					queueRowItem.selectionCheckBox.Checked = true;
				}
				else
				{
					queueRowItem.isSelectedItem = false;
					queueRowItem.selectionCheckBox.Checked = false;
				}
			}

			// Skip this processing while in EditMode
			if (this.editMode) return;

			for (int index = 0; index < topToBottomItemList.Children.Count; index++)
			{
				GuiWidget child = topToBottomItemList.Children[index];
				var queueRowItem = (QueueRowItem)child.Children[0];

				if (index == QueueData.Instance.SelectedIndex)
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
			}

			if (QueueData.Instance.ItemCount == 0)
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
		}

		private void ItemRemovedFromQueue(object sender, EventArgs e)
		{
			IndexArgs removeIndexArgs = e as IndexArgs;
			topToBottomItemList.RemoveChild(removeIndexArgs.Index);
		}

		private void QueueOrderChanged(object sender, EventArgs e)
		{
			throw new NotImplementedException();
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
					if (EditMode)
					{
						QueueData.Instance.ToggleSelect(index);
					}
					else
					{
						QueueData.Instance.SelectedIndex = index;
					}
				}
			}
		}

		private void itemToAdd_MouseLeaveBounds(object sender, EventArgs e)
		{
			GuiWidget widgetLeft = ((GuiWidget)sender);
			if (QueueData.Instance.SelectedIndex >= 0)
			{
				if (widgetLeft != topToBottomItemList.Children[QueueData.Instance.SelectedIndex])
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

		public void OnHoverIndexChanged()
		{
			Invalidate();
			if (HoverValueChanged != null)
			{
				HoverValueChanged(this, null);
			}
		}

		internal List<QueueRowItem> GetSelectedItems()
		{
			List<QueueRowItem> list = new List<QueueRowItem>();

			foreach(var index in QueueData.Instance.SelectedIndexes)
			{
				var queueItem = GetQueueRowItem(index);
				if(queueItem != null)
				{
					list.Add(queueItem);
				}
			}

			return list;
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