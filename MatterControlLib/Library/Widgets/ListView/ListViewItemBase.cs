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
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.Library;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.CustomWidgets
{
	public class ListViewItemBase : GuiWidget
	{
		protected ThemeConfig theme;
		protected ListViewItem listViewItem;
		protected View3DWidget view3DWidget;
		protected bool mouseInBounds = false;
		private bool mouseDownInBounds = false;
		private Vector2 mouseDownAt;

		private ImageBuffer overflowIcon;

		public ImageWidget imageWidget;
		protected int thumbWidth;
		protected int thumbHeight;

		public ListViewItemBase(ListViewItem listViewItem, int width, int height, ThemeConfig theme)
		{
			this.theme = theme;
			this.listViewItem = listViewItem;
			this.thumbWidth = width;
			this.thumbHeight = height;

			overflowIcon = AggContext.StaticData.LoadIcon(Path.Combine("ViewTransformControls", "overflow.png"), 32, 32, theme.InvertIcons);
		}

		public bool HasMenu { get; set; } = false;

		public async Task LoadItemThumbnail()
		{
			// On first draw, lookup and set best thumbnail
			await ApplicationController.Instance.Library.LoadItemThumbnail(
				this.SetSizedThumbnail,
				(meshContentProvider) =>
				{
					// Store meshContentProvider reference
					this.meshContentProvider = meshContentProvider;

					// Schedule work
					this.ScheduleRaytraceOperation();
				},
				listViewItem.Model,
				listViewItem.Container,
				this.thumbWidth,
				this.thumbHeight,
				theme);
		}

		private void ScheduleRaytraceOperation()
		{
			if (meshContentProvider == null)
			{
				return;
			}

			ApplicationController.Instance.Thumbnails.QueueForGeneration(async () =>
			{
				// When dequeued for generation, ensure visible before raytracing. Off-screen widgets are dequeue and will reschedule if redrawn
				if (!this.ActuallyVisibleOnScreen())
				{
					// Skip raytracing operation, requeue on next draw
					raytraceSkipped = true;
					raytracePending = false;
					requeueRaytraceOnDraw = true;
				}
				else
				{
					raytraceSkipped = false;
					requeueRaytraceOnDraw = false;

					// Show processing image
					this.SetUnsizedThumbnail(theme.GeneratingThumbnailIcon);

					// Ask the MeshContentProvider to RayTrace the image
					var thumbnail = await meshContentProvider.GetThumbnail(listViewItem.Model, thumbWidth, thumbHeight);
					if (thumbnail != null)
					{
						requeueRaytraceOnDraw = false;
						raytracePending = false;

						if (thumbnail.Width != thumbWidth
						|| thumbnail.Height != thumbHeight)
						{
							this.SetUnsizedThumbnail(thumbnail);
						}
						else
						{
							this.SetSizedThumbnail(thumbnail);

							if (listViewItem.Container is ILibraryWritableContainer writableContainer)
							{
								writableContainer.SetThumbnail(listViewItem.Model, thumbWidth, thumbHeight, thumbnail);
							}
						}
					}
				}
			});
		}

		internal void EnsureSelection()
		{
			if (this.IsSelectableContent)
			{
				// Clear existing selection when item is not selected and control key is not press
				if (!this.IsSelected
					&& !Keyboard.IsKeyDown(Keys.ControlKey))
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
				if (toggleSelection)
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

				bool isGCode = listViewItem.Model is FileSystemFileItem item && Path.GetExtension(item.Name).IndexOf(".gco", StringComparison.OrdinalIgnoreCase) == 0
					|| listViewItem.Model is SDCardFileItem sdItem && Path.GetExtension(sdItem.Name).IndexOf(".gco", StringComparison.OrdinalIgnoreCase) == 0;

				return isContentItem || isValidStream || isContainerLink || isGCode;
			}
		}

		public event EventHandler ImageSet;

		protected void SetUnsizedThumbnail(ImageBuffer thumbnail)
		{
			this.SetSizedThumbnail(
				ApplicationController.Instance.Library.EnsureCorrectThumbnailSizing(
					thumbnail,
					thumbWidth,
					thumbHeight));
		}

		private void SetSizedThumbnail(ImageBuffer thumbnail)
		{
			if (thumbnail != null
				&& this.imageWidget != null
				&& (this.imageWidget.Image == null
				|| !thumbnail.Equals(this.imageWidget.Image, 5)))
			{
				this.imageWidget.Image = thumbnail;
				this.ImageSet?.Invoke(this, null);
				this.Invalidate();
			}
		}

		public override Color BorderColor
		{
			get => (this.IsSelected || mouseInBounds) ? theme.PrimaryAccentColor : base.BorderColor;
			set => base.BorderColor = value;
		}

		private bool hitDragThreshold = false;

		private bool toggleSelection = false;

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			mouseDownInBounds = true;
			mouseDownAt = mouseEvent.Position;
			hitDragThreshold = false;

			// Used to toggle selection on selected items - revised to require control key
			toggleSelection = this.IsSelected && Keyboard.IsKeyDown(Keys.ControlKey);

			this.EnsureSelection();

			if (IsDoubleClick(mouseEvent))
			{
				listViewItem.OnDoubleClick();
			}

			// On mouse down update the view3DWidget reference that will be used in MouseMove and MouseUp
			view3DWidget = ApplicationController.Instance.DragDropData.View3DWidget;

			base.OnMouseDown(mouseEvent);
		}

		public override void OnLoad(EventArgs args)
		{
			foreach (var child in Children)
			{
				child.Selectable = false;
			}

			// On first draw, lookup and set best thumbnail
			this.LoadItemThumbnail().ConfigureAwait(false);

			base.OnLoad(args);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			if (requeueRaytraceOnDraw
				&& !raytracePending
				&& raytraceSkipped)
			{
				raytracePending = true;

				// Requeue thumbnail generation
				this.ScheduleRaytraceOperation();
			}

			if (this.mouseInBounds
				&& this.HasMenu)
			{
				var bounds = this.LocalBounds;
				graphics2D.Render(overflowIcon, new Point2D(bounds.Right - 32, bounds.Top - 32 - 3));
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
				view3DWidget.ExternalDragOver(screenSpaceMousePosition: this.TransformToScreenSpace(mouseEvent.Position), sourceWidget: this.listViewItem.ListView);
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

		public override void OnClick(MouseEventArgs mouseEvent)
		{
			var bounds = this.LocalBounds;
			var hitRegion = new RectangleDouble(
				new Vector2(bounds.Right - 32, bounds.Top),
				new Vector2(bounds.Right, bounds.Top - 32));

			if (this.HasMenu
				&& listViewItem?.ListView?.MenuActions?.Any() == true
				&& (hitRegion.Contains(mouseEvent.Position)
					|| mouseEvent.Button == MouseButtons.Right))
			{
				var menu = new PopupMenu(ApplicationController.Instance.MenuTheme);

				foreach (var menuAction in listViewItem.ListView.MenuActions.Where(m => m.Scope == ActionScope.ListItem))
				{
					if (menuAction is MenuSeparator)
					{
						menu.CreateSeparator();
					}
					else if (menuAction.IsEnabled(this.listViewItem.ListView.SelectedItems, this.listViewItem.ListView))
					{
						var item = menu.CreateMenuItem(menuAction.Title, menuAction.Icon);
						item.Click += (s, e) => UiThread.RunOnIdle(() =>
						{
							menu.Close();
							menuAction.Action.Invoke(this.listViewItem.ListView.SelectedItems.Select(o => o.Model), this.listViewItem.ListView);
						});
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

			base.OnClick(mouseEvent);
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
		private bool requeueRaytraceOnDraw;
		private bool raytracePending;
		private MeshContentProvider meshContentProvider;

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