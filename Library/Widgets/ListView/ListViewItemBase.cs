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
using System.IO;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.MatterControl.Library;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.CustomWidgets
{
	public class ListViewItemBase : GuiWidget
	{
		private static ImageBuffer defaultFolderIcon = LibraryProviderHelpers.LoadInvertIcon("FileDialog", "folder.png");
		private static ImageBuffer defaultItemIcon = LibraryProviderHelpers.LoadInvertIcon("FileDialog", "file.png");
		private static ImageBuffer generatingThumbnailIcon = LibraryProviderHelpers.LoadInvertIcon("building_thumbnail_40x40.png");

		protected ListViewItem listViewItem;
		protected View3DWidget view3DWidget;
		protected bool mouseInBounds = false;
		private bool mouseDownInBounds = false;
		private Vector2 mouseDownAt;

		protected ImageWidget imageWidget;
		protected int thumbWidth;
		protected int thumbHeight;

		public ListViewItemBase(ListViewItem listViewItem, int width, int height)
		{
			this.listViewItem = listViewItem;
			this.thumbWidth = width;
			this.thumbHeight = height;
		}

		private static bool WidgetOnScreen(GuiWidget widget, RectangleDouble bounds)
		{
			if (!widget.Visible)
			{
				return false;
			}
			else
			{
				if (widget.Parent != null)
				{
					var boundsInParentSpace = widget.TransformToParentSpace(widget.Parent, bounds);
					var intersects = boundsInParentSpace.IntersectRectangles(boundsInParentSpace, widget.Parent.LocalBounds);
					if (!intersects
						|| boundsInParentSpace.Width <= 0 
						|| boundsInParentSpace.Height <= 0
						|| !WidgetOnScreen(widget.Parent, boundsInParentSpace))
					{
						return false;
					}
				}
			}

			return true;
		}

		protected async Task LoadItemThumbnail()
		{
			var listView = listViewItem.ListView;

			var thumbnail = listView.LoadCachedImage(listViewItem);
			if (thumbnail != null)
			{
				SetItemThumbnail(thumbnail);
				return;
			}

			var itemModel = listViewItem.Model;

			if (thumbnail == null)
			{
				// Ask the container - allows the container to provide its own interpretation of the item thumbnail
				thumbnail = await listView.ActiveContainer.GetThumbnail(itemModel, thumbWidth, thumbHeight);
			}

			if (thumbnail == null && itemModel is IThumbnail)
			{
				// If the item provides its own thumbnail, try to collect it
				thumbnail = await (itemModel as IThumbnail).GetThumbnail(thumbWidth, thumbHeight);
			}

			if (thumbnail == null)
			{
				// Ask content provider - allows type specific thumbnail creation
				var contentProvider = ApplicationController.Instance.Library.GetContentProvider(itemModel);
				if (contentProvider != null 
					&& contentProvider is MeshContentProvider)
				{
					// Before we have a thumbnail set to the content specific thumbnail
					thumbnail = contentProvider.DefaultImage.AlphaToPrimaryAccent();

					ApplicationController.Instance.QueueForGeneration(async () =>
					{
						// When this widget is dequeued for generation, validate before processing. Off-screen widgets should be skipped and will requeue next time they become visible
						if (ListViewItemBase.WidgetOnScreen(this, this.LocalBounds))
						{
							SetItemThumbnail(generatingThumbnailIcon.AlphaToPrimaryAccent());

							// Then try to load a content specific thumbnail
							await contentProvider.GetThumbnail(
								itemModel,
								thumbWidth,
								thumbHeight,
								(image) =>
								{
									// Use the content providers default image if an image failed to load
									SetItemThumbnail(image ?? contentProvider.DefaultImage, true);
								});
						}
					});
				}
				else if (contentProvider != null)
				{
					// Then try to load a content specific thumbnail
					await contentProvider.GetThumbnail(
						itemModel,
						thumbWidth,
						thumbHeight,
						(image) => thumbnail = image);
				}
			}

			if (thumbnail == null)
			{
				// Use the listview defaults
				thumbnail = ((itemModel is ILibraryContainerLink) ? defaultFolderIcon : defaultItemIcon).AlphaToPrimaryAccent();
			}

			SetItemThumbnail(thumbnail);
		}

		internal void OnItemSelect()
		{
			bool isContentItem = listViewItem.Model is ILibraryContentItem;
			bool isValidStream = (listViewItem.Model is ILibraryContentStream stream
				&& ApplicationController.Instance.Library.IsContentFileType(stream.FileName));
			bool isContainerLink = listViewItem.Model is ILibraryContainerLink;

			bool isGCode = listViewItem.Model is FileSystemFileItem item && Path.GetExtension(item.FileName.ToUpper()) == ".GCODE"
				|| listViewItem.Model is SDCardFileItem sdItem && Path.GetExtension(sdItem.Name.ToUpper()) == ".GCODE";

			if (isContentItem || isValidStream || isContainerLink || isGCode)
			{
				if (this.IsSelected)
				{
					listViewItem.ListView.SelectedItems.Remove(listViewItem);
				}
				else
				{
					if (!Keyboard.IsKeyDown(Keys.ControlKey))
					{
						listViewItem.ListView.SelectedItems.Clear();
					}

					listViewItem.ListView.SelectedItems.Add(listViewItem);
				}

				Invalidate();
			}
		}

		protected void SetItemThumbnail(ImageBuffer thumbnail, bool colorize = false)
		{
			if (thumbnail != null)
			{
				// Resize canvas to target as fallback
				if (thumbnail.Width < thumbWidth || thumbnail.Height < thumbHeight)
				{
					thumbnail = listViewItem.ListView.ResizeCanvas(thumbnail, thumbWidth, thumbHeight);
				}
				else if (thumbnail.Width > thumbWidth || thumbnail.Height > thumbHeight)
				{
					thumbnail = LibraryProviderHelpers.ResizeImage(thumbnail, thumbWidth, thumbHeight);
				}

				// TODO: Resolve and implement
				// Allow the container to draw an overlay - use signal interface or add method to interface?
				//var iconWithOverlay = ActiveContainer.DrawOverlay()

				this.imageWidget.Image = thumbnail;

				this.Invalidate();
			}
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			base.OnDraw(graphics2D);

			var widgetBorder = new RoundedRect(LocalBounds, 0);

			// Draw the hover border if the mouse is in bounds or if its the ActivePrint item
			if (mouseInBounds || (this.IsActivePrint && !this.EditMode))
			{
				//Draw interior border
				graphics2D.Render(new Stroke(widgetBorder, 3), ActiveTheme.Instance.SecondaryAccentColor);
			}

			if (this.IsHoverItem)
			{
				RectangleDouble Bounds = LocalBounds;
				RoundedRect rectBorder = new RoundedRect(Bounds, 0);

				this.BackgroundColor = RGBA_Bytes.White;

				graphics2D.Render(new Stroke(rectBorder, 3), ActiveTheme.Instance.SecondaryAccentColor);
			}
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			mouseDownInBounds = true;
			mouseDownAt = mouseEvent.Position;

			if (IsDoubleClick(mouseEvent))
			{
				listViewItem.OnDoubleClick();
			}

			// On mouse down update the view3DWidget reference that will be used in MouseMove and MouseUp
			view3DWidget = ApplicationController.Instance.ActiveView3DWidget;

			base.OnMouseDown(mouseEvent);
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			var delta = mouseDownAt - mouseEvent.Position;

			bool dragActive = mouseDownInBounds && delta.Length > 40;
			// If dragging and the drag threshold has been hit, start a drag operation but loading the drag items
			if (dragActive 
				&& (listViewItem.Model is ILibraryContentStream || listViewItem.Model is ILibraryContentItem))
			{
				if (view3DWidget != null && view3DWidget.DragDropSource == null)
				{
					if (listViewItem.Model is ILibraryContentStream contentModel)
					{
						// Update the ListView pointer for the dragging item
						listViewItem.ListView.DragSourceRowItem = listViewItem;

						var progressBar = new DragDropLoadProgress(this.view3DWidget, null);

						var contentResult = contentModel.CreateContent(progressBar.ProgressReporter);

						progressBar.TrackingObject = contentResult.Object3D;

						if (contentResult != null)
						{
							// Assign a new drag source
							view3DWidget.DragDropSource = contentResult.Object3D;
						}
					}
					else if (listViewItem.Model is ILibraryContentItem)
					{
						(listViewItem.Model as ILibraryContentItem).GetContent(null).ContinueWith((task) =>
						{
							view3DWidget.DragDropSource = task.Result;
						});
					}
				} 

				// Performs move in View3DWidget and indicates if add occurred
				var screenSpaceMousePosition = this.TransformToScreenSpace(mouseEvent.Position);
				view3DWidget.AltDragOver(screenSpaceMousePosition);
			}

			base.OnMouseMove(mouseEvent);
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			if (view3DWidget?.DragDropSource != null && view3DWidget.Scene.Children.Contains(view3DWidget.DragDropSource))
			{
				// Mouse and widget positions
				var screenSpaceMousePosition = this.TransformToScreenSpace(mouseEvent.Position);
				var meshViewerPosition = this.view3DWidget.meshViewerWidget.TransformToScreenSpace(view3DWidget.meshViewerWidget.LocalBounds);

				// If the mouse is not within the meshViewer, remove the inserted drag item
				if (!meshViewerPosition.Contains(screenSpaceMousePosition))
				{
					view3DWidget.Scene.ModifyChildren(children => children.Remove(view3DWidget.DragDropSource));
					view3DWidget.Scene.ClearSelection();
				}
				else
				{
					// Create and push the undo operation
					view3DWidget.AddUndoOperation(
						new InsertCommand(view3DWidget, view3DWidget.DragDropSource));
				}

				view3DWidget.FinishDrop();
			}

			mouseDownInBounds = false;
			base.OnMouseUp(mouseEvent);
		}

		public override void OnMouseEnterBounds(MouseEventArgs mouseEvent)
		{
			base.OnMouseEnterBounds(mouseEvent);
			mouseInBounds = true;
			UpdateHoverState();
			Invalidate();
		}

		public override void OnMouseLeaveBounds(MouseEventArgs mouseEvent)
		{
			mouseInBounds = false;
			base.OnMouseLeaveBounds(mouseEvent);
			UpdateHoverState();
			Invalidate();
		}

		protected virtual void UpdateColors()
		{
		}

		protected virtual async void UpdateHoverState()
		{
		}

		public virtual bool IsHoverItem { get; set; }
		public virtual bool EditMode { get; set; }

		private bool isActivePrint = false;
		public bool IsActivePrint
		{
			get
			{
				return isActivePrint;
			}
			set
			{
				if (isActivePrint != value)
				{
					isActivePrint = value;
					UpdateColors();
				}
			}
		}

		private bool isSelected = false;

		public bool IsSelected
		{
			get
			{
				return isSelected;
			}
			set
			{
				if (isSelected != value)
				{
					//selectionCheckBox.Checked = value;

					isSelected = value;
					UpdateColors();
				}
			}
		}
	}
}