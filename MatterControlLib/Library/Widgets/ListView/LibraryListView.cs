/*
Copyright (c) 2018, John Lewin
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
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.Library;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.CustomWidgets
{
	public class LibraryListView : ScrollableWidget
	{
		public enum DoubleClickActions { PreviewItem, AddToBed }

		public event EventHandler ContentReloaded;

		private ThemeConfig theme;
		private ILibraryContext LibraryContext;

		private int scrollAmount = -1;

		private GuiWidget stashedContentView;

		private ILibraryContainerLink loadingContainerLink;

		// Default to IconListView
		private GuiWidget contentView;
		private Color loadingBackgroundColor;
		private ImageSequenceWidget loadingIndicator;

		public List<LibraryAction> MenuActions { get; set; }

		// Default constructor uses IconListView
		public LibraryListView(ILibraryContext context, ThemeConfig theme)
			: this(context, new IconListView(theme), theme)
		{
		}

		public LibraryListView(ILibraryContext context, GuiWidget libraryView, ThemeConfig theme)
		{
			contentView = new IconListView(theme);

			libraryView.Click += ContentView_Click;

			loadingBackgroundColor = new Color(theme.PrimaryAccentColor, 10);

			this.theme = theme;
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

		private void ContentView_Click(object sender, MouseEventArgs e)
		{
			if (sender is GuiWidget guiWidget)
			{
				var screenPosition = guiWidget.TransformToScreenSpace(e.Position);
				var thisPosition = this.TransformFromScreenSpace(screenPosition);
				var thisMouseClick = new MouseEventArgs(e, thisPosition.X, thisPosition.Y);
				ShowRightClickMenu(thisMouseClick);
			}
		}

		public bool ShowItems { get; set; } = true;

		public bool AllowContextMenu { get; set; } = true;

		public Predicate<ILibraryContainerLink> ContainerFilter { get; set; } = (o) => true;

		public Predicate<ILibraryItem> ItemFilter { get; set; } = (o) => true;

		public ILibraryContainer ActiveContainer => this.LibraryContext.ActiveContainer;

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

		public enum SortKey
		{
			Name,
			CreatedDate,
			ModifiedDate
		}

		private SortKey _activeSort = SortKey.Name;
		public SortKey ActiveSort
		{
			get => _activeSort;
			set
			{
				if (_activeSort != value)
				{
					_activeSort = value;
					this.ApplySort();
				}
			}
		}

		private bool _ascending = true;
		public bool Ascending
		{
			get => _ascending;
			set
			{
				if (_ascending != value)
				{
					_ascending = value;
					this.ApplySort();
				}
			}
		}

		private void ApplySort()
		{
			this.Reload().ConfigureAwait(false);
		}

		/// <summary>
		/// Empties the list children and repopulates the list with the source container content
		/// </summary>
		/// <param name="sourceContainer">The container to load</param>
		private Task DisplayContainerContent(ILibraryContainer sourceContainer)
		{
			if (this.ActiveContainer is ILibraryWritableContainer activeWritable)
			{
				activeWritable.ItemContentChanged -= WritableContainer_ItemContentChanged;
			}

			if (sourceContainer == null)
			{
				return Task.CompletedTask;
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

			IEnumerable<ILibraryItem> containerItems = from item in sourceContainer.ChildContainers
								 where item.IsVisible && this.ContainerFilter(item)
								 select item;

			// Folder items
			foreach (var childContainer in this.SortItems(containerItems))
			{
				var listViewItem = new ListViewItem(childContainer, this.ActiveContainer, this);
				listViewItem.DoubleClick += listViewItem_DoubleClick;
				items.Add(listViewItem);

				listViewItem.ViewWidget = itemsContentView.AddItem(listViewItem);
				listViewItem.ViewWidget.HasMenu = this.AllowContextMenu;
				listViewItem.ViewWidget.Name = childContainer.Name + " Row Item Collection";
			}

			// List items
			if (this.ShowItems)
			{
				var filteredResults = from item in sourceContainer.Items
									  where item.IsVisible
											&& (item.IsContentFileType() || item is MissingFileItem)
											&& this.ItemFilter(item)
									  select item;

				foreach (var item in this.SortItems(filteredResults))
				{
					var listViewItem = new ListViewItem(item, this.ActiveContainer, this);
					listViewItem.DoubleClick += listViewItem_DoubleClick;
					items.Add(listViewItem);

					listViewItem.ViewWidget = itemsContentView.AddItem(listViewItem);
					listViewItem.ViewWidget.HasMenu = this.AllowContextMenu;
					listViewItem.ViewWidget.Name = "Row Item " + item.Name;
				}

				itemsContentView.EndReload();
			}

			if (sourceContainer is ILibraryWritableContainer writableContainer)
			{
				writableContainer.ItemContentChanged += WritableContainer_ItemContentChanged;
			}

			this.ScrollPositionFromTop = Vector2.Zero;

			this.ContentReloaded?.Invoke(this, null);

			return Task.CompletedTask;
		}

		private IEnumerable<ILibraryItem> SortItems(IEnumerable<ILibraryItem> items)
		{
			switch (ActiveSort)
			{
				case SortKey.CreatedDate when this.Ascending:
					return items.OrderBy(item => item.DateCreated);

				case SortKey.CreatedDate when !this.Ascending:
					return items.OrderByDescending(item => item.DateCreated);

				case SortKey.ModifiedDate when this.Ascending:
					return items.OrderBy(item => item.DateModified);

				case SortKey.ModifiedDate when !this.Ascending:
					return items.OrderByDescending(item => item.DateModified);

				case SortKey.Name when !this.Ascending:
					return items.OrderByDescending(item => item.Name);

				default:
					return items.OrderBy(item => item.Name);
			}
		}

		private void WritableContainer_ItemContentChanged(object sender, ItemChangedEventArgs e)
		{
			if (items.Where(i => i.Model.ID == e.LibraryItem.ID).FirstOrDefault() is ListViewItem listViewItem)
			{
				listViewItem.ViewWidget.LoadItemThumbnail().ConfigureAwait(false);
			}
		}

		public enum ViewMode
		{
			Icons,
			List
		}

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

						contentView = value;
						contentView.HAnchor = HAnchor.Stretch;
						contentView.VAnchor = VAnchor.Fit | VAnchor.Top;
						contentView.Name = "Library ListContentView";
						this.AddChild(this.contentView);

						this.ScrollArea.AddChild(
							loadingIndicator = new ImageSequenceWidget(ApplicationController.Instance.GetProcessingSequence(theme.PrimaryAccentColor))
							{
								VAnchor = VAnchor.Top,
								HAnchor = HAnchor.Center,
								Visible = false
							});
					}
				}
				else
				{
					throw new FormatException("ListContentView must be assignable from IListContentView");
				}
			}
		}

		// TODO: ResizeCanvas is also colorizing thumbnails as a proof of concept
		public static ImageBuffer ResizeCanvas(ImageBuffer originalImage, int width, int height)
		{
			var destImage = new ImageBuffer(width, height, 32, originalImage.GetRecieveBlender());

			var renderGraphics = destImage.NewGraphics2D();
			renderGraphics.Clear(Color.Transparent);

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
					// Prevent invalid assignment of container.Parent due to overlapping load attempts that
					// would otherwise result in containers with self referencing parent properties
					if (loadingContainerLink != containerLink)
					{
						loadingContainerLink = containerLink;

						try
						{
							// Container items
							var container = await containerLink.GetContainer(null);
							if (container != null)
							{
								(contentView as IListContentView)?.ClearItems();

								this.BackgroundColor = loadingBackgroundColor;
								contentView.Visible = false;
								loadingIndicator.Visible = true;

								await Task.Run(() =>
								{
									container.Load();
								});

								loadingIndicator.Visible = false;
								this.BackgroundColor = Color.Transparent;
								contentView.Visible = true;

								container.Parent = ActiveContainer;
								SetActiveContainer(container);
							}
						}
						catch { }
						finally
						{
							// Clear the loading guard and any completed load attempt
							loadingContainerLink = null;
						}
					}
				}
				else
				{
					// List items
					if (itemModel != null)
					{
						if (this.DoubleClickAction == DoubleClickActions.PreviewItem)
						{
							ApplicationController.Instance.OpenIntoNewTab(new[] { itemModel });
						}
						else
						{
							var activeContext = ApplicationController.Instance.DragDropData;
							activeContext.SceneContext?.AddToPlate(new[] { itemModel });
						}
					}
				}
			});
		}

		public DoubleClickActions DoubleClickAction { get; set; } = DoubleClickActions.AddToBed;

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
				this.Reload().ConfigureAwait(false);
			}

			base.OnLoad(args);
		}

		public bool HasMenu { get; set; } = true;

		public override void OnClick(MouseEventArgs mouseEvent)
		{
			ShowRightClickMenu(mouseEvent);

			base.OnClick(mouseEvent);
		}

		private void ShowRightClickMenu(MouseEventArgs mouseEvent)
		{
			var bounds = this.LocalBounds;
			var hitRegion = new RectangleDouble(
				new Vector2(bounds.Right - 32, bounds.Top),
				new Vector2(bounds.Right, bounds.Top - 32));

			if (this.HasMenu
				&& this.MenuActions?.Any() == true
				&& (hitRegion.Contains(mouseEvent.Position)
					|| mouseEvent.Button == MouseButtons.Right))
			{
				var menu = new PopupMenu(ApplicationController.Instance.MenuTheme);

				foreach (var menuAction in this.MenuActions.Where(m => m.Scope == ActionScope.ListView))
				{
					if (menuAction is MenuSeparator)
					{
						menu.CreateSeparator();
					}
					else
					{
						var item = menu.CreateMenuItem(menuAction.Title, menuAction.Icon);
						item.Enabled = menuAction.IsEnabled(this.SelectedItems, this);

						if (item.Enabled)
						{
							item.Click += (s, e) => UiThread.RunOnIdle(() =>
							{
								menu.Close();
								menuAction.Action.Invoke(this.SelectedItems.Select(o => o.Model), this);
							});
						}
					}
				}

				RectangleDouble popupBounds;
				if (mouseEvent.Button == MouseButtons.Right)
				{
					popupBounds = new RectangleDouble(mouseEvent.X + 1, mouseEvent.Y + 1, mouseEvent.X + 1, mouseEvent.Y + 1);
				}
				else
				{
					popupBounds = new RectangleDouble(this.Width - 32, this.Height - 32, this.Width, this.Height);
				}

				var systemWindow = this.Parents<SystemWindow>().FirstOrDefault();
				systemWindow.ShowPopup(
					new MatePoint(this)
					{
						Mate = new MateOptions(MateEdge.Left, MateEdge.Bottom),
						AltMate = new MateOptions(MateEdge.Right, MateEdge.Top)
					},
					new MatePoint(menu)
					{
						Mate = new MateOptions(MateEdge.Left, MateEdge.Top),
						AltMate = new MateOptions(MateEdge.Right, MateEdge.Bottom)
					},
					altBounds: popupBounds);
			}
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

		public override void OnClosed(EventArgs e)
		{
			if (this.LibraryContext != null)
			{
				this.LibraryContext.ContainerChanged -= this.ActiveContainer_Changed;
				this.LibraryContext.ContentChanged -= this.ActiveContainer_ContentChanged;
			}

			base.OnClosed(e);
		}
	}
}
