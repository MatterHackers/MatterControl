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
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.Library;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.CustomWidgets
{
	public class ListView : ScrollableWidget
	{
		private EventHandler unregisterEvents;

		private bool editMode = false;

		internal GuiWidget contentView;

		private ILibraryContext LibraryContext;

		public ListView(ILibraryContext context)
		{
			this.LibraryContext = context;

			// Set Display Attributes
			this.MinimumSize = new Vector2(0, 200);
			this.AnchorAll();
			this.BackgroundColor = ActiveTheme.Instance.TertiaryBackgroundColor;
			this.AutoScroll = true;
			this.ScrollArea.Padding = new BorderDouble(3);

			// AddWatermark
			string imagePath = Path.Combine("OEMSettings", "watermark.png");
			if (StaticData.Instance.FileExists(imagePath))
			{
				this.AddChildToBackground(new ImageWidget(StaticData.Instance.LoadImage(imagePath))
				{
					VAnchor = VAnchor.ParentCenter,
					HAnchor = HAnchor.ParentCenter
				});
			}

			this.ScrollArea.HAnchor = HAnchor.ParentLeftRight;

			AutoScroll = true;

			this.ListContentView = new IconListView();
			context.ContainerChanged += ActiveContainer_Changed;
			context.ContainerReloaded += ActiveContainer_Reloaded;
		}

		public ILibraryContainer ActiveContainer => this.LibraryContext.ActiveContainer;

		public RGBA_Bytes ThumbnailBackground { get; } = ActiveTheme.Instance.TertiaryBackgroundColor.AdjustLightness(1.1).GetAsRGBA_Bytes();
		public RGBA_Bytes ThumbnailForeground { get; set; } = ActiveTheme.Instance.PrimaryAccentColor;

		private GuiWidget stashedView = null;

		private void ActiveContainer_Changed(object sender, ContainerChangedEventArgs e)
		{
			var activeContainer = e.ActiveContainer;

			var containerDefaultView = activeContainer?.DefaultView;

			if (containerDefaultView != null 
				&& containerDefaultView != this.ListContentView)
			{
				stashedView = this.contentView;
				// Critical that assign to the contentView backing field and not the ListContentView property that uses it
				this.SetContentView(activeContainer.DefaultView);
			}
			else if (stashedView != null)
			{
				this.SetContentView(stashedView);
				stashedView = null;
			}

			DisplayContainerContent(activeContainer);
		}

		private void ActiveContainer_Reloaded(object sender, EventArgs e)
		{
			DisplayContainerContent(ActiveContainer);
		}

		private List<ListViewItem> items = new List<ListViewItem>();

		public IEnumerable<ListViewItem> Items => items;

		/*
* bool isTraceable = listViewItem.Model is ILibraryPrintItem;
bool hasID = !string.IsNullOrEmpty(listViewItem.Model.ID);
List<ListViewItem> acquireItems, 
if (hasID 
	&& isTraceable
	&& thumbnail == null)
{
	// Schedule for collection, display default thumb until then
	acquireItems.Add(listViewItem);
}
*/
		/// <summary>
		/// Empties the list children and repopulates the list with the source container content
		/// </summary>
		/// <param name="sourceContainer">The container to load</param>
		/// <returns>Async Task</returns>
		private async Task DisplayContainerContent(ILibraryContainer sourceContainer)
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
			foreach (var childContainer in sourceContainer.ChildContainers.Where(c => c.IsVisible))
			{
				var listViewItem = new ListViewItem(childContainer, this);
				listViewItem.DoubleClick += listViewItem_DoubleClick;
				items.Add(listViewItem);

				itemsContentView.AddItem(listViewItem);
				listViewItem.ViewWidget.Name = childContainer.Name + " Row Item Collection";
			}

			// List items
			foreach (var item in sourceContainer.Items.Where(i => i.IsVisible))
			{
				var listViewItem = new ListViewItem(item, this);
				listViewItem.DoubleClick += listViewItem_DoubleClick;
				items.Add(listViewItem);

				itemsContentView.AddItem(listViewItem);
				listViewItem.ViewWidget.Name = "Row Item " + item.Name;
			}
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
			this.contentView.HAnchor = HAnchor.ParentLeftRight;
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
				ImageIO.LoadImageData(cachePath, thumbnail);
				thumbnail.SetRecieveBlender(new BlenderPreMultBGRA());
				
				return thumbnail.MultiplyWithPrimaryAccent();
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

				if (listViewItem?.Text == "..")
				{
					// Up folder tiem
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
						if (result.Object3D != null)
						{
							var scene = MatterControlApplication.Instance.ActiveView3DWidget.Scene;

							scene.ModifyChildren(children =>
							{
								children.Add(result.Object3D);
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
 