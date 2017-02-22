/*
Copyright (c) 2016, Kevin Pope, John Lewin
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
using System.IO;
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PrintQueue
{
	public class QueueDataView : ScrollableWidget
	{
		private EventHandler unregisterEvents;

		private bool mouseDownWithinQueueItemContainer = false;

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
					SelectedIndexChanged();
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

		internal FlowLayoutWidget topToBottomItemList;

		public delegate void HoverValueChangedEventHandler(object sender, EventArgs e);

		public event HoverValueChangedEventHandler HoverValueChanged;

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
				topToBottomItemList.AddChild(new WrappedQueueRowItem(this, QueueData.Instance.GetPrintItemWrapper(i)));
			}

			QueueData.Instance.SelectedIndexChanged.RegisterEvent((s,e) => SelectedIndexChanged(), ref unregisterEvents);
			QueueData.Instance.ItemAdded.RegisterEvent(ItemAddedToQueue, ref unregisterEvents);
			QueueData.Instance.ItemRemoved.RegisterEvent(ItemRemovedFromQueue, ref unregisterEvents);

			PrinterConnectionAndCommunication.Instance.ActivePrintItemChanged.RegisterEvent(PrintItemChange, ref unregisterEvents);

			SelectedIndexChanged();
		}

		private void PrintItemChange(object sender, EventArgs e)
		{
			QueueData.Instance.SelectedPrintItem = PrinterConnectionAndCommunication.Instance.ActivePrintItem;
		}

		private void SelectedIndexChanged()
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
					queueRowItem.selectionCheckBox.Checked = true;
				}
				else
				{
					// Don't test for .Checked as the property already performs validation
					queueRowItem.selectionCheckBox.Checked = false;
				}
			}

			// Skip this processing while in EditMode
			if (this.editMode) return;

			PrinterConnectionAndCommunication.Instance.ActivePrintItem = QueueData.Instance.SelectedPrintItem;
		}

		private void ItemAddedToQueue(object sender, EventArgs e)
		{
			var addedIndexArgs = e as ItemChangedArgs;
			PrintItemWrapper item = QueueData.Instance.GetPrintItemWrapper(addedIndexArgs.Index);
			topToBottomItemList.AddChild(new WrappedQueueRowItem(this, item), addedIndexArgs.Index);
		}

		private void ItemRemovedFromQueue(object sender, EventArgs e)
		{
			var removeIndexArgs = e as ItemChangedArgs;
			topToBottomItemList.RemoveChild(removeIndexArgs.Index);
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			var topToBottomItemListBounds = topToBottomItemList.LocalBounds;
			mouseDownWithinQueueItemContainer = topToBottomItemList.LocalBounds.Contains(mouseEvent.Position);

			base.OnMouseDown(mouseEvent);
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			mouseDownWithinQueueItemContainer = false;
			this.SuppressScroll = false;
			base.OnMouseUp(mouseEvent);
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			this.SuppressScroll = mouseDownWithinQueueItemContainer && !PositionWithinLocalBounds(mouseEvent.X, 20);
			base.OnMouseMove(mouseEvent);
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

		public QueueRowItem DragSourceRowItem { get; internal set; }

		private void itemHolder_MouseDownInBounds(object sender, MouseEventArgs mouseEvent)
		{
			// Hard-coded processing rule to avoid changing the SelectedIndex when clicks occur
			// with the thumbnail region - aka the first 55 pixels
			if (!EditMode && mouseEvent.X < 56) return;

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

		private static bool WidgetOrChildIsFirstUnderMouse(GuiWidget startWidget)
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

			foreach (var index in QueueData.Instance.SelectedIndexes)
			{
				var queueItem = GetQueueRowItem(index);
				if (queueItem != null)
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