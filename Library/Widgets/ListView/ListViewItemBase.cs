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
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.Library;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.CustomWidgets
{
	public class ListViewItemBase : GuiWidget
	{
		private static ImageBuffer defaultFolderIcon = AggContext.StaticData.LoadIcon(Path.Combine("FileDialog", "folder.png")).SetPreMultiply();
		private static ImageBuffer defaultItemIcon = AggContext.StaticData.LoadIcon(Path.Combine("FileDialog", "file.png"));
		private static ImageBuffer generatingThumbnailIcon = AggContext.StaticData.LoadIcon(Path.Combine("building_thumbnail_40x40.png"));

		protected ThemeConfig theme;
		protected ListViewItem listViewItem;
		protected View3DWidget view3DWidget;
		protected bool mouseInBounds = false;
		private bool mouseDownInBounds = false;
		private Vector2 mouseDownAt;

		public ImageWidget imageWidget;
		protected int thumbWidth;
		protected int thumbHeight;

		public ListViewItemBase(ListViewItem listViewItem, int width, int height, ThemeConfig theme)
		{
			this.theme = theme;
			this.listViewItem = listViewItem;
			this.thumbWidth = width;
			this.thumbHeight = height;
		}

		public Task LoadItemThumbnail()
		{
			return LoadItemThumbnail(
				listViewItem.Model,
				listViewItem.Container,
				this.thumbWidth,
				this.thumbHeight,
				this.SetItemThumbnail,
				() =>
				{
					bool isValid = this.ActuallyVisibleOnScreen();
					if (!isValid)
					{
						raytraceSkipped = true;
						raytracePending = false;
					};

					return isValid;
				});
		}

		private async Task LoadItemThumbnail(ILibraryItem libraryItem, ILibraryContainer libraryContainer, int thumbWidth, int thumbHeight, ThumbnailSetter thumbnailSetter, Func<bool> shouldGenerateThumbnail)
		{
			var thumbnail = ListView.LoadCachedImage(libraryItem, thumbWidth, thumbHeight);
			if (thumbnail != null)
			{
				thumbnailSetter(thumbnail, raytracedImage: false);
				return;
			}

			if (thumbnail == null)
			{
				// Ask the container - allows the container to provide its own interpretation of the item thumbnail
				thumbnail = await libraryContainer.GetThumbnail(libraryItem, thumbWidth, thumbHeight);
			}

			if (thumbnail == null && libraryItem is IThumbnail)
			{
				// If the item provides its own thumbnail, try to collect it
				thumbnail = await (libraryItem as IThumbnail).GetThumbnail(thumbWidth, thumbHeight);
			}

			if (thumbnail == null)
			{
				// Ask content provider - allows type specific thumbnail creation
				var contentProvider = ApplicationController.Instance.Library.GetContentProvider(libraryItem);
				if (contentProvider != null)
				{
					// Before we have a thumbnail set to the content specific thumbnail
					thumbnail = contentProvider.DefaultImage;

					this.useRaytracedMeshThumbnails = true;

					ApplicationController.Instance.QueueForGeneration(async () =>
					{
						// When this widget is dequeued for generation, validate before processing. Off-screen widgets should be skipped and will requeue next time they become visible
						if (shouldGenerateThumbnail?.Invoke() == true)
						{
							thumbnailSetter(generatingThumbnailIcon, raytracedImage: false);

							// Ask the provider for a content specific thumbnail
							await contentProvider.GetThumbnail(
								libraryItem,
								thumbWidth,
								thumbHeight,
								thumbnailSetter);
						}
					});
				}
			}

			if (thumbnail == null)
			{
				// Use the listview defaults
				thumbnail = ((libraryItem is ILibraryContainerLink) ? defaultFolderIcon : defaultItemIcon).AlphaToPrimaryAccent();
			}

			thumbnailSetter(thumbnail, raytracedImage: false);
		}

		internal void EnsureSelection()
		{
			if (this.IsSelectableContent)
			{
				// Existing selection only survives with ctrl->click
				if (!Keyboard.IsKeyDown(Keys.ControlKey))
				{
					listViewItem.ListView?.SelectedItems.Clear();
				}

				// Any mouse down ensures selection - mouse up will evaluate if DragDrop occurred and toggle selection if not
				if (!listViewItem.ListView?.SelectedItems.Contains(listViewItem) == true)
				{
					listViewItem.ListView?.SelectedItems.Add(listViewItem);
				}

				Invalidate();
			}
		}

		internal void OnItemSelect()
		{
			if (this.IsSelectableContent
				&& !hitDragThreshold)
			{
				if (wasSelected)
				{
					listViewItem.ListView?.SelectedItems.Remove(listViewItem);
				}

				Invalidate();
			}
		}

		private bool IsSelectableContent
		{
			get
			{
				bool isContentItem = listViewItem.Model is ILibraryObject3D;
				bool isValidStream = (listViewItem.Model is ILibraryAssetStream stream
					&& ApplicationController.Instance.Library.IsContentFileType(stream.FileName));
				bool isContainerLink = listViewItem.Model is ILibraryContainerLink;

				bool isGCode = listViewItem.Model is FileSystemFileItem item && Path.GetExtension(item.FileName.ToUpper()) == ".GCODE"
					|| listViewItem.Model is SDCardFileItem sdItem && Path.GetExtension(sdItem.Name.ToUpper()) == ".GCODE";

				return isContentItem || isValidStream || isContainerLink || isGCode;
			}
		}

		public event EventHandler ImageSet;

		protected void SetItemThumbnail(ImageBuffer thumbnail, bool raytracedImage)
		{
			if (thumbnail != null)
			{
				// Resize canvas to target as fallback
				if (thumbnail.Width < thumbWidth || thumbnail.Height < thumbHeight)
				{
					thumbnail = ListView.ResizeCanvas(thumbnail, thumbWidth, thumbHeight);
				}
				else if (thumbnail.Width > thumbWidth || thumbnail.Height > thumbHeight)
				{
					thumbnail = LibraryProviderHelpers.ResizeImage(thumbnail, thumbWidth, thumbHeight);
				}

				if (raytracedImage)
				{
					this.raytracePending = false;
				}

				if (GuiWidget.DeviceScale != 1)
				{
					thumbnail = thumbnail.CreateScaledImage(GuiWidget.DeviceScale);
				}

				// TODO: Resolve and implement
				// Allow the container to draw an overlay - use signal interface or add method to interface?
				//var iconWithOverlay = ActiveContainer.DrawOverlay()

				this.imageWidget.Image = thumbnail;

				this.ImageSet?.Invoke(this, null);

				this.Invalidate();
			}
		}

		public override Color BorderColor
		{
			get => (this.IsSelected || mouseInBounds) ? theme.Colors.PrimaryAccentColor : base.BorderColor;
			set => base.BorderColor = value;
		}

		private bool hitDragThreshold = false;

		private bool wasSelected = false;

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			mouseDownInBounds = true;
			mouseDownAt = mouseEvent.Position;
			hitDragThreshold = false;

			wasSelected = this.IsSelected;

			this.EnsureSelection();

			if (IsDoubleClick(mouseEvent))
			{
				listViewItem.OnDoubleClick();
			}

			// On mouse down update the view3DWidget reference that will be used in MouseMove and MouseUp
			view3DWidget = ApplicationController.Instance.DragDropData.View3DWidget;

			base.OnMouseDown(mouseEvent);
		}

		public async override void OnLoad(EventArgs args)
		{
			await this.LoadItemThumbnail();
			base.OnLoad(args);
		}

		public async override void OnDraw(Graphics2D graphics2D)
		{
			if (useRaytracedMeshThumbnails
				&& !raytracePending
				&& this.raytraceSkipped)
			{
				raytracePending = true;

				// Requeue thumbnail generation
				await this.LoadItemThumbnail();
			}

			base.OnDraw(graphics2D);
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			var delta = mouseDownAt - mouseEvent.Position;

			// If mouseDown on us and we've moved past are drag determination threshold, notify view3DWidget
			if (mouseDownInBounds && delta.Length > 40
				&& view3DWidget != null
				&& !(listViewItem.Model is MissingFileItem))
			{
				hitDragThreshold = true;

				// Performs move and possible Scene add in View3DWidget
				view3DWidget.ExternalDragOver(screenSpaceMousePosition: this.TransformToScreenSpace(mouseEvent.Position));
			}

			base.OnMouseMove(mouseEvent);
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			this.OnItemSelect();

			if (view3DWidget?.DragOperationActive == true)
			{
				// Mouse and widget positions
				var screenSpaceMousePosition = this.TransformToScreenSpace(mouseEvent.Position);
				var meshViewerPosition = view3DWidget.meshViewerWidget.TransformToScreenSpace(view3DWidget.meshViewerWidget.LocalBounds);

				// Notify of drag operation complete
				view3DWidget.FinishDrop(mouseUpInBounds: meshViewerPosition.Contains(screenSpaceMousePosition));
			}

			view3DWidget = null;

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

		protected virtual void UpdateHoverState()
		{
		}

		public virtual bool IsHoverItem { get; set; }
		public virtual bool EditMode { get; set; }

		private bool isSelected = false;
		private bool raytraceSkipped;
		private bool useRaytracedMeshThumbnails;
		private bool raytracePending;

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