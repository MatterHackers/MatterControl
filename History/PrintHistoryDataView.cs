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
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;

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

	public class PrintHistoryDataView : ScrollableWidget
	{
		public bool ShowTimestamp;

		public event EventHandler DoneLoading;

		private void SetDisplayAttributes()
		{
			this.MinimumSize = new Vector2(0, 200);
			this.AnchorAll();
			this.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;
			this.AutoScroll = true;
			this.ScrollArea.Padding = new BorderDouble(3);
		}

		public void LoadHistoryItems(int NumItemsToLoad = 0)
		{
			if (NumItemsToLoad == 0 || NumItemsToLoad < PrintHistoryData.RecordLimit)
			{
				NumItemsToLoad = PrintHistoryData.RecordLimit;
			}

			RemoveListItems();
			IEnumerable<PrintTask> partFiles = PrintHistoryData.Instance.GetHistoryItems(NumItemsToLoad);
			if (partFiles != null)
			{
				foreach (PrintTask part in partFiles)
				{
					AddChild(new PrintHistoryListItem(part, ShowTimestamp));
				}
			}

			OnDoneLoading(null);
		}

		private void OnDoneLoading(EventArgs e)
		{
			if (DoneLoading != null)
			{
				DoneLoading(this, e);
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

		public PrintHistoryDataView()
		{
			PrintHistoryData.Instance.HistoryCleared.RegisterEvent(HistoryDeleted, ref unregisterEvents);
			ShowTimestamp = (UserSettings.Instance.get("PrintHistoryFilterShowTimestamp") == "true");

			SetDisplayAttributes();
			ScrollArea.HAnchor |= Agg.UI.HAnchor.ParentLeftRight;

			AutoScroll = true;
			topToBottomItemList = new FlowLayoutWidget(FlowDirection.TopToBottom);
			topToBottomItemList.HAnchor = Agg.UI.HAnchor.Max_FitToChildren_ParentWidth;
			base.AddChild(topToBottomItemList);
			AddHandlers();

			LoadHistoryItems();
		}

		public void HistoryDeleted(object sender, EventArgs e)
		{
			ReloadData(this, null);
		}

		private void AddHandlers()
		{
			PrinterConnectionAndCommunication.Instance.CommunicationStateChanged.RegisterEvent(ReloadData, ref unregisterEvents);
		}

		private void ReloadData(object sender, EventArgs e)
		{
			UiThread.RunOnIdle(() =>
			{
				if(!this.HasBeenClosed)
				{
					LoadHistoryItems(Count);
				}
			});
		}

		private EventHandler unregisterEvents;

		public override void OnClosed(ClosedEventArgs e)
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
			itemHolder.Name = "list item holder";
			itemHolder.Margin = new BorderDouble(0, 0, 0, 0);
			itemHolder.HAnchor = Agg.UI.HAnchor.Max_FitToChildren_ParentWidth;
			itemHolder.AddChild(child);
			itemHolder.VAnchor = VAnchor.FitToChildren;
			topToBottomItemList.AddChild(itemHolder, indexInChildrenList);
		}

		private bool settingLocalBounds = false;

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

		public void RemoveListItems()
		{
			topToBottomItemList.RemoveAllChildren();
		}
	}
}