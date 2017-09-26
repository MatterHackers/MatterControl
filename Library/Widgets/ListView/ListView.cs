/*
Copyright (c) 2017, John Lewin
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
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.Library;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.CustomWidgets
{
	public class ListView : ScrollableWidget
	{
		private EventHandler unregisterEvents;

		internal GuiWidget contentView;

		private ILibraryContext LibraryContext;

		// Default constructor uses IconListView
		public ListView(ILibraryContext context)
			: this(context, new IconListView())
		{
		}

		public ListView(ILibraryContext context, GuiWidget libraryView)
		{
			this.LibraryContext = context;

			// Set Display Attributes
			this.MinimumSize = new Vector2(0, 200);
			this.AnchorAll();
			this.AutoScroll = true;
			this.ScrollArea.Padding = new BorderDouble(3);
			this.ScrollArea.HAnchor = HAnchor.Stretch;
			this.ListContentView = libraryView;

			context.ContainerChanged += ActiveContainer_Changed;
			context.ContainerReloaded += ActiveContainer_Reloaded;
		}

		public bool ShowItems { get; set; } = true;

		public Predicate<ILibraryContainerLink> ContainerFilter { get; set; } = (o) => true;

		public Predicate<ILibraryItem> ItemFilter { get; set; } = (o) => true;

		public ILibraryContainer ActiveContainer => this.LibraryContext.ActiveContainer;

		public RGBA_Bytes ThumbnailBackground { get; } = ActiveTheme.Instance.TertiaryBackgroundColor.AdjustLightness(1.05).GetAsRGBA_Bytes();
		public RGBA_Bytes ThumbnailForeground { get; set; } = ActiveTheme.Instance.PrimaryAccentColor;

		private GuiWidget stashedView = null;

		private void ActiveContainer_Changed(object sender, ContainerChangedEventArgs e)
		{
			var activeContainer = e.ActiveContainer;

			Type targetType = activeContainer?.DefaultView;
			if (targetType != null 
				&& targetType != this.ListContentView.GetType())
			{
				stashedView = this.contentView;

				// If the current view doesn't match the view requested by the container, construct and switch to the requested view
				var targetView = Activator.CreateInstance(targetType) as GuiWidget;
				if (targetView != null)
				{
					this.SetContentView(targetView);
				}
			}
			else if (stashedView != null)
			{
				// Switch back to the original view
				this.SetContentView(stashedView);
				stashedView = null;
			}

			DisplayContainerContent(activeContainer);
		}

		public void Reload()
		{
			DisplayContainerContent(ActiveContainer);
		}

		private void ActiveContainer_Reloaded(object sender, EventArgs e)
		{
			DisplayContainerContent(ActiveContainer);
		}

		private List<ListViewItem> items = new List<ListViewItem>();

		public IEnumerable<ListViewItem> Items => items;

		/// <summary>
		/// Empties the list children and repopulates the list with the source container content
		/// </summary>
		/// <param name="sourceContainer">The container to load</param>
		private void DisplayContainerContent(ILibraryContainer sourceContainer)
		{
			UiThread.RunOnIdle(() =>
			{
				if (sourceContainer == null)
				{
					return;
				}

				var itemsNeedingLoad = new List<ListViewItem>();

				this.items.Clear();

				this.SelectedItems.Clear();
				contentView.CloseAllChildren();

				var itemsContentView = contentView as IListContentView;
				itemsContentView.ClearItems();

				int width = itemsContentView.ThumbWidth;
				int height = itemsContentView.ThumbHeight;

				// Folder items
				if (UserSettings.Instance.get("ShowContainers") == "1")
				{
					foreach (var childContainer in sourceContainer.ChildContainers.Where(c => c.IsVisible && this.ContainerFilter(c)))
					{
						var listViewItem = new ListViewItem(childContainer, this);
						listViewItem.DoubleClick += listViewItem_DoubleClick;
						items.Add(listViewItem);

						itemsContentView.AddItem(listViewItem);
						listViewItem.ViewWidget.Name = childContainer.Name + " Row Item Collection";
					}
				}

				// List items
				if (this.ShowItems)
				{
					foreach (var item in sourceContainer.Items.Where(i => i.IsVisible && this.ItemFilter(i)))
					{
						var listViewItem = new ListViewItem(item, this);
						listViewItem.DoubleClick += listViewItem_DoubleClick;
						items.Add(listViewItem);

						itemsContentView.AddItem(listViewItem);
						listViewItem.ViewWidget.Name = "Row Item " + item.Name;
					}
				}

				this.Invalidate();
			});
		}

		public enum ViewMode
		{
			Icons,
			List
		}

		private void SetContentView(GuiWidget contentView)
		{
			this.ScrollArea.CloseAllChildren();

			this.contentView = contentView;
			this.contentView.HAnchor = HAnchor.Stretch;
			this.contentView.Name = "Library ListView";
			this.AddChild(this.contentView);
		}

		public GuiWidget ListContentView
		{
			get { return contentView; }
			set
			{
				if (value is IListContentView)
				{
					SetContentView(value);

					// Allow some time for layout to occur and contentView to become sized before loading content
					UiThread.RunOnIdle(() =>
					{
						DisplayContainerContent(ActiveContainer);
					});
				}
				else
				{
					throw new FormatException("ListContentView must be assignable from IListContentView");
				}
			}
		}

		internal ImageBuffer LoadCachedImage(ListViewItem listViewItem)
		{
			string cachePath = ApplicationController.Instance.CachePath(listViewItem.Model);

			bool isCached = !string.IsNullOrEmpty(cachePath) && File.Exists(cachePath);
			if (isCached)
			{
				ImageBuffer thumbnail = new ImageBuffer();
				AggContext.ImageIO.LoadImageData(cachePath, thumbnail);
				thumbnail.SetRecieveBlender(new BlenderPreMultBGRA());

				return thumbnail;
			}

			return null;
		}

		// TODO: ResizeCanvas is also colorizing thumbnails as a proof of concept
		public ImageBuffer ResizeCanvas(ImageBuffer originalImage, int width, int height)
		{
			var destImage = new ImageBuffer(width, height, 32, originalImage.GetRecieveBlender());

			var renderGraphics = destImage.NewGraphics2D();
			renderGraphics.Clear(this.ThumbnailBackground);

			var x = width / 2 - originalImage.Width / 2;
			var y = height / 2 - originalImage.Height / 2;

			var center = new RectangleInt(x, y + originalImage.Height, x + originalImage.Width, y);
			//renderGraphics.FillRectangle(center, this.ThumbnailForeground);

			renderGraphics.ImageRenderQuality = Graphics2D.TransformQuality.Best;

			//originalImage = originalImage.Multiply(this.ThumbnailBackground);

			renderGraphics.Render(originalImage, width /2 - originalImage.Width /2, height /2 - originalImage.Height /2);

			renderGraphics.FillRectangle(center, RGBA_Bytes.Transparent);

			return destImage;
		}

		private void listViewItem_DoubleClick(object sender, MouseEventArgs e)
		{
			UiThread.RunOnIdle(async () =>
			{
				var listViewItem = sender as ListViewItem;
				var itemModel = listViewItem.Model;

				// TODO: No longer applicable... ***********************************
				if (listViewItem?.Text == "..")
				{
					// Up folder item
					if (ActiveContainer?.Parent != null)
					{
						LoadContainer(ActiveContainer.Parent);
					}
				}
				else if (itemModel is ILibraryContainerLink)
				{
					// Container items
					var containerLink = itemModel as ILibraryContainerLink;
					if (containerLink != null)
					{
						var container = await containerLink.GetContainer(null);
						if (container != null)
						{
							container.Parent = ActiveContainer;
							LoadContainer(container);
						}
					}
				}
				else
				{
					// List items
					var contentModel = itemModel as ILibraryContentStream;
					if (contentModel != null)
					{
						listViewItem.StartProgress();

						var result = contentModel.CreateContent(listViewItem.ProgressReporter);
						if (result.Object3D != null && ApplicationController.Instance.DragDropData.View3DWidget != null)
						{
							var scene = ApplicationController.Instance.DragDropData.View3DWidget.InteractionLayer.Scene;
							scene.Children.Modify(list =>
							{
								list.Add(result.Object3D);
							});
						}
					}

					listViewItem.EndProgress();
				}
			});
		}

		public void LoadContainer(ILibraryContainer temp)
		{
			this.LibraryContext.ActiveContainer = temp;
		}

		public ObservableCollection<ListViewItem> SelectedItems { get; } = new ObservableCollection<ListViewItem>();

		public ListViewItem DragSourceRowItem { get; set; }

		public override void OnClosed(ClosedEventArgs e)
		{
			if (this.LibraryContext != null)
			{
				this.LibraryContext.ContainerChanged -= this.ActiveContainer_Changed;
				this.LibraryContext.ContainerReloaded -= this.ActiveContainer_Reloaded;
			}

			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}
	}
}
 