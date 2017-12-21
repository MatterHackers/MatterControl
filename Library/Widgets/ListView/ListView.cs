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
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.Library;
using MatterHackers.MatterControl.PrintLibrary;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.CustomWidgets
{
	public class ListView : ScrollableWidget
	{
		public event EventHandler ContentReloaded;

		private EventHandler unregisterEvents;

		private ILibraryContext LibraryContext;

		private int scrollAmount = -1;

		private GuiWidget stashedContentView;

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
			context.ContentChanged += ActiveContainer_ContentChanged;
		}

		public bool ShowItems { get; set; } = true;

		public Predicate<ILibraryContainerLink> ContainerFilter { get; set; } = (o) => true;

		public Predicate<ILibraryItem> ItemFilter { get; set; } = (o) => true;

		public ILibraryContainer ActiveContainer => this.LibraryContext.ActiveContainer;

		public Color ThumbnailBackground { get; } = ActiveTheme.Instance.TertiaryBackgroundColor.AdjustLightness(1.05).ToColor();
		public Color ThumbnailForeground { get; set; } = ActiveTheme.Instance.PrimaryAccentColor;

		private async void ActiveContainer_Changed(object sender, ContainerChangedEventArgs e)
		{
			var activeContainer = e.ActiveContainer;

			// Anytime the container changes, 
			Type targetType = activeContainer?.DefaultView;
			if (targetType != null
				&& targetType != this.ListContentView.GetType())
			{
				// If no original view is stored in stashedContentView then store a reference before the switch
				if (stashedContentView == null)
				{
					stashedContentView = this.ListContentView;
				}

				// If the current view doesn't match the view requested by the container, construct and switch to the requested view
				var targetView = Activator.CreateInstance(targetType) as GuiWidget;
				if (targetView != null)
				{
					this.ListContentView = targetView;
				}
			}
			else if (stashedContentView != null)
			{
				// Switch back to the original view
				this.ListContentView = stashedContentView;
				stashedContentView = null;
			}

			await DisplayContainerContent(activeContainer);
		}

		public async Task Reload()
		{
			await DisplayContainerContent(ActiveContainer);
		}

		private async void ActiveContainer_ContentChanged(object sender, EventArgs e)
		{
			await DisplayContainerContent(ActiveContainer);
		}

		private List<ListViewItem> items = new List<ListViewItem>();

		public IEnumerable<ListViewItem> Items => items;

		/// <summary>
		/// Empties the list children and repopulates the list with the source container content
		/// </summary>
		/// <param name="sourceContainer">The container to load</param>
		private async Task DisplayContainerContent(ILibraryContainer sourceContainer)
		{
			if (this.ActiveContainer is ILibraryWritableContainer activeWritable)
			{
				activeWritable.ItemContentChanged -= WritableContainer_ItemContentChanged;
			}

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

			itemsContentView.BeginReload();

			// Folder items
			foreach (var childContainer in sourceContainer.ChildContainers.Where(c => c.IsVisible && this.ContainerFilter(c)))
			{
				var listViewItem = new ListViewItem(childContainer, this);
				listViewItem.DoubleClick += listViewItem_DoubleClick;
				items.Add(listViewItem);

				listViewItem.ViewWidget = itemsContentView.AddItem(listViewItem);
				listViewItem.ViewWidget.Name = childContainer.Name + " Row Item Collection";
			}

			// List items
			if (this.ShowItems)
			{
				var filteredResults = from item in sourceContainer.Items
									  where item.IsVisible
											&& item.IsContentFileType()
											&& this.ItemFilter(item)
									  select item;

				foreach (var item in filteredResults)
				{
					var listViewItem = new ListViewItem(item, this);
					listViewItem.DoubleClick += listViewItem_DoubleClick;
					items.Add(listViewItem);

					listViewItem.ViewWidget = itemsContentView.AddItem(listViewItem);
					listViewItem.ViewWidget.Name = "Row Item " + item.Name;
				}

				itemsContentView.EndReload();

			}

			if (sourceContainer is ILibraryWritableContainer writableContainer)
			{
				writableContainer.ItemContentChanged += WritableContainer_ItemContentChanged;
			}

			this.Invalidate();

			this.ContentReloaded?.Invoke(this, null);
		}

		private void WritableContainer_ItemContentChanged(object sender, ItemChangedEventArgs e)
		{
			var firstItem = items.Where(i => i.Model.ID == e.LibraryItem.ID).FirstOrDefault();
			if (firstItem != null)
			{
				firstItem.ViewWidget.LoadItemThumbnail().ConfigureAwait(false);
			}
		}

		public enum ViewMode
		{
			Icons,
			List
		}

		// Default to IconListView
		private GuiWidget contentView = new IconListView();

		/// <summary>
		/// The GuiWidget responsible for rendering ListViewItems
		/// </summary>
		public GuiWidget ListContentView
		{
			get { return contentView; }
			set
			{
				if (value is IListContentView)
				{
					scrollAmount = -1;

					if (contentView != null
						&& contentView != value)
					{
						this.ScrollArea.CloseAllChildren();

						this.contentView = value;
						this.contentView.HAnchor = HAnchor.Stretch;
						this.contentView.Name = "Library ListView";
						this.AddChild(this.contentView);
					}
				}
				else
				{
					throw new FormatException("ListContentView must be assignable from IListContentView");
				}
			}
		}

		internal ImageBuffer LoadCachedImage(ListViewItem listViewItem, int width, int height)
		{
			ImageBuffer cachedItem = LoadImage(ApplicationController.Instance.ThumbnailCachePath(listViewItem.Model, width, height));
			if (cachedItem != null)
			{
				return cachedItem;
			}

			// Check for big render, resize, cache and return
			var bigRender = LoadImage(ApplicationController.Instance.ThumbnailCachePath(listViewItem.Model));
			if (bigRender != null)
			{
				try
				{
					var thumbnail = LibraryProviderHelpers.ResizeImage(bigRender, width, height);

					// Cache at requested size
					AggContext.ImageIO.SaveImageData(
						ApplicationController.Instance.ThumbnailCachePath(listViewItem.Model, width, height), 
						thumbnail);

					return thumbnail;
				}
				catch { } // suppress and return null on errors
			}

			return null;
		}

		private ImageBuffer LoadImage(string filePath)
		{
			ImageBuffer thumbnail = null;

			try
			{
				if (File.Exists(filePath))
				{
					var temp = new ImageBuffer();
					AggContext.ImageIO.LoadImageData(filePath, temp);
					temp.SetRecieveBlender(new BlenderPreMultBGRA());

					thumbnail = temp;
				}

				return thumbnail;
			}
			catch { } // Suppress exceptions, return null on any errors

			return thumbnail;
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

			renderGraphics.FillRectangle(center, Color.Transparent);

			return destImage;
		}

		private void listViewItem_DoubleClick(object sender, MouseEventArgs e)
		{
			UiThread.RunOnIdle(async () =>
			{
				var listViewItem = sender as ListViewItem;
				var itemModel = listViewItem.Model;

				if (itemModel is ILibraryContainerLink containerLink)
				{
					// Container items
					var container = await containerLink.GetContainer(null);

					await Task.Run(() =>
					{
						container.Load();
					});

					if (container != null)
					{
						container.Parent = ActiveContainer;
						SetActiveContainer(container);
					}
				}
				else
				{
					// List items
					if (itemModel is ILibraryContentStream contentModel)
					{
						var activeContext = ApplicationController.Instance.DragDropData;
						if (activeContext.View3DWidget != null)
						{
							var scene = activeContext.SceneContext.Scene;
							var bedCenter = activeContext.SceneContext.BedCenter;

							var sceneChildren = scene.Children.ToList();

							var injector = new InsertionGroup(new[] { itemModel }, activeContext.View3DWidget, scene, bedCenter, () => false);
							injector.ContentLoaded += (s, args) =>
							{
								// Get the bounds of the loaded InsertionGroup with all of its content
								var aabb = injector.GetAxisAlignedBoundingBox(Matrix4X4.Identity);

								// Remove position
								injector.Matrix *= Matrix4X4.CreateTranslation(new Vector3(-aabb.minXYZ.X, -aabb.minXYZ.Y, 0));

								// Recenter
								injector.Matrix *= Matrix4X4.CreateTranslation(new Vector3(bedCenter.X - aabb.XSize / 2, (double)(bedCenter.Y - aabb.YSize / 2), 0));

								// Move again after content loaded
								PlatingHelper.MoveToOpenPosition(injector, sceneChildren);
							};

							// Move to bed center - (before we know the bounds of the content to load)
							injector.Matrix *= Matrix4X4.CreateTranslation(new Vector3(bedCenter.X, (double)bedCenter.Y, 0));

							scene.Children.Add(injector);

							PlatingHelper.MoveToOpenPosition(injector, sceneChildren);
						}
					}
				}
			});
		}

		public void SetActiveContainer(ILibraryContainer container)
		{
			this.LibraryContext.ActiveContainer = container;
		}

		public ObservableCollection<ListViewItem> SelectedItems { get; } = new ObservableCollection<ListViewItem>();

		public ListViewItem DragSourceRowItem { get; set; }

		public override void OnLoad(EventArgs args)
		{
			if (this.ListContentView.Children.Count <= 0)
			{
				this.Reload();
			}

			base.OnLoad(args);
		}

		public override void OnMouseWheel(MouseEventArgs mouseEvent)
		{
			if (scrollAmount == -1)
			{
				scrollAmount = (int) (this.contentView.Children.FirstOrDefault()?.Height ?? 20);
			}

			int direction = (mouseEvent.WheelDelta > 0) ? -1 : 1;

			ScrollPosition += new Vector2(0, scrollAmount * direction);
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			if (this.LibraryContext != null)
			{
				this.LibraryContext.ContainerChanged -= this.ActiveContainer_Changed;
				this.LibraryContext.ContentChanged -= this.ActiveContainer_ContentChanged;
			}

			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}
	}
}
