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
using MatterHackers.MatterControl.PrintLibrary.Provider;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.VectorMath;
using MatterHackers.Localizations;
using System;
using System.IO;

namespace MatterHackers.MatterControl.CustomWidgets.LibrarySelector
{
	public class LibrarySelectorWidget : ScrollableWidget
	{
		public SelectedListItems<LibrarySelectorRowItem> SelectedItems = new SelectedListItems<LibrarySelectorRowItem>();

		protected FlowLayoutWidget topToBottomItemList;

		private LibraryProvider currentLibraryProvider;

		private RGBA_Bytes baseColor = new RGBA_Bytes(255, 255, 255);

		private bool editMode = false;

		private RGBA_Bytes hoverColor = new RGBA_Bytes(204, 204, 204, 255);

		private RGBA_Bytes selectedColor = new RGBA_Bytes(180, 180, 180, 255);

		private int selectedIndex = -1;

		private bool settingLocalBounds = false;

		public event Action<LibraryProvider, LibraryProvider> ChangedCurrentLibraryProvider;

		public void SetCurrentLibraryProvider(LibraryProvider libraryProvider)
		{
			this.CurrentLibraryProvider = libraryProvider;
			UiThread.RunOnIdle(RebuildView);
		}

		public LibrarySelectorWidget(bool includeQueueLibraryProvider)
		{
			currentLibraryProvider = new LibraryProviderSelector(SetCurrentLibraryProvider, includeQueueLibraryProvider);
			currentLibraryProvider.DataReloaded += LibraryDataReloaded;

			// set the display attributes
			{
				this.AnchorAll();
				this.BackgroundColor = ActiveTheme.Instance.SecondaryBackgroundColor;
				this.ScrollArea.Padding = new BorderDouble(3);
			}

			ScrollArea.HAnchor = HAnchor.ParentLeftRight;

			AutoScroll = true;
			topToBottomItemList = new FlowLayoutWidget(FlowDirection.TopToBottom);
			topToBottomItemList.HAnchor = HAnchor.ParentLeftRight;
			AddChild(topToBottomItemList);

			AddAllItems();
		}

		public delegate void HoverValueChangedEventHandler(object sender, EventArgs e);

		public event HoverValueChangedEventHandler HoverValueChanged;

		public event Action<object, EventArgs> SelectedIndexChanged;

		private EventHandler unregisterEvents;

		public LibraryProvider CurrentLibraryProvider
		{
			get
			{
				return currentLibraryProvider;
			}

			set
			{
				if (currentLibraryProvider != value)
				{
					// unhook the update we were getting
					currentLibraryProvider.DataReloaded -= this.LibraryDataReloaded;
					// and hook the new one
					value.DataReloaded += this.LibraryDataReloaded;

					bool isChildOfCurrent = value.ParentLibraryProvider == currentLibraryProvider;

					// Dispose of all children below this one.
					while (!isChildOfCurrent && currentLibraryProvider != value
						&& currentLibraryProvider.ParentLibraryProvider != null)
					{
						LibraryProvider parent = currentLibraryProvider.ParentLibraryProvider;
						currentLibraryProvider.Dispose();
						currentLibraryProvider = parent;
					}

					currentLibraryProvider = value;

					if (ChangedCurrentLibraryProvider != null)
					{
						ChangedCurrentLibraryProvider(null, value);
					}
				}
			}
		}

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
						while (SelectedItems.Count > 1)
						{
							SelectedItems.RemoveAt(SelectedItems.Count - 1);
						}
					}
				}
			}
		}

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

		private int Count
		{
			get
			{
				return topToBottomItemList.Children.Count;
			}
		}

		public void AddListItemToTopToBottom(GuiWidget child, int indexInChildrenList = -1)
		{
			FlowLayoutWidget itemHolder = new FlowLayoutWidget();
			itemHolder.Name = "list item holder";
			itemHolder.Margin = new BorderDouble(0, 0, 0, 0);
			itemHolder.HAnchor = Agg.UI.HAnchor.Max_FitToChildren_ParentWidth;
			itemHolder.AddChild(child);
			itemHolder.VAnchor = VAnchor.FitToChildren;
			topToBottomItemList.AddChild(itemHolder, indexInChildrenList);
		}

		public void ClearSelected()
		{
			if (selectedIndex != -1)
			{
				selectedIndex = -1;
				OnSelectedIndexChanged();
			}
		}

		public void ClearSelectedItems()
		{
			this.SelectedItems.Clear();
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			currentLibraryProvider.DataReloaded -= LibraryDataReloaded;

			if (unregisterEvents != null)
			{
				unregisterEvents(this, null);
			}

			// Dispose of all children below this one.
			// Dispose of all children below this one.
			while (currentLibraryProvider != null
				&& currentLibraryProvider.ParentLibraryProvider != null)
			{
				LibraryProvider parent = currentLibraryProvider.ParentLibraryProvider;
				currentLibraryProvider.Dispose();
				currentLibraryProvider = parent;
			}

			base.OnClosed(e);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			//activeView.OnDraw(graphics2D);

			base.OnDraw(graphics2D);
		}

		public void OnHoverIndexChanged()
		{
			Invalidate();
			if (HoverValueChanged != null)
			{
				HoverValueChanged(this, null);
			}
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			base.OnMouseDown(mouseEvent);
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			base.OnMouseMove(mouseEvent);
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			base.OnMouseUp(mouseEvent);
		}

		public void OnSelectedIndexChanged()
		{
			Invalidate();
			if (SelectedIndexChanged != null)
			{
				SelectedIndexChanged(this, null);
			}
		}

		public void RebuildView()
		{
			AddAllItems();
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

		public void RemoveSelectedItems()
		{
			foreach (LibrarySelectorRowItem item in SelectedItems)
			{
				throw new NotImplementedException();
				//item.RemoveFromParentCollection();
			}
		}

		protected GuiWidget GetThumbnailWidget(LibraryProvider parentProvider, PrintItemCollection printItemCollection, ImageBuffer imageBuffer)
		{
			Vector2 expectedSize = new Vector2((int)(50 * GuiWidget.DeviceScale), (int)(50 * GuiWidget.DeviceScale));
			if (imageBuffer.Width != expectedSize.x)
			{
				ImageBuffer scaledImageBuffer = new ImageBuffer((int)expectedSize.x, (int)expectedSize.y);
				scaledImageBuffer.NewGraphics2D().Render(imageBuffer, 0, 0, scaledImageBuffer.Width, scaledImageBuffer.Height);
				imageBuffer = scaledImageBuffer;
			}

			ImageWidget folderThumbnail = new ImageWidget(imageBuffer);
			folderThumbnail.BackgroundColor = ActiveTheme.Instance.PrimaryAccentColor;

			Button clickThumbnail = new Button(0, 0, folderThumbnail);

			PrintItemCollection localPrintItemCollection = printItemCollection;
			clickThumbnail.Click += (sender, e) =>
			{
				if (UserSettings.Instance.IsTouchScreen)
				{
					if (parentProvider == null)
					{
						this.CurrentLibraryProvider = this.CurrentLibraryProvider.GetProviderForCollection(localPrintItemCollection);
					}
					else
					{
						this.CurrentLibraryProvider = parentProvider;
					}
				}
				else
				{
					MouseEventArgs mouseEvent = e as MouseEventArgs;

					if (mouseEvent != null
						&& IsDoubleClick(mouseEvent))
					{
						if (parentProvider == null)
						{
							this.CurrentLibraryProvider = this.CurrentLibraryProvider.GetProviderForCollection(localPrintItemCollection);
						}
						else
						{
							this.CurrentLibraryProvider = parentProvider;
						}
					}
				}

				UiThread.RunOnIdle(RebuildView);
			};

			return clickThumbnail;
		}

		private void AddAllItems()
		{
			topToBottomItemList.RemoveAllChildren();

			var provider = this.CurrentLibraryProvider;

			if (provider != null)
			{
				if (provider.ProviderKey != LibraryProviderSelector.ProviderKeyName)
				{
					PrintItemCollection parent = new PrintItemCollection("..", provider.ProviderKey);
					LibrarySelectorRowItem queueItem = new LibrarySelectorRowItem(parent, -1, this, provider.ParentLibraryProvider, GetThumbnailWidget(provider.ParentLibraryProvider, parent, LibraryProvider.UpFolderImage), "Back".Localize());
					AddListItemToTopToBottom(queueItem);
				}

				for (int i = 0; i < provider.CollectionCount; i++)
				{
					PrintItemCollection item = provider.GetCollectionItem(i);
					if (item.Key != "LibraryProviderPurchasedKey" && item.Key != "LibraryProviderSharedKey")
					{
						LibrarySelectorRowItem queueItem = new LibrarySelectorRowItem(item, i, this, null, GetThumbnailWidget(null, item, provider.GetCollectionFolderImage(i)), "Open".Localize());
						AddListItemToTopToBottom(queueItem);
					}
				}
			}
		}

		private void LibraryDataReloaded(object sender, EventArgs e)
		{
			UiThread.RunOnIdle(AddAllItems);
		}
	}
}