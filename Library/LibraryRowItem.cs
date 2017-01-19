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
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;
using System;
using System.Globalization;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.PrintLibrary
{
	public abstract class LibraryRowItem : GuiWidget
	{
		public static readonly string LoadingPlaceholderToken = "!Placeholder_ItemToken!";
		public static readonly string LoadFailedPlaceholderToken = "!Placeholder_LoadFailedToken!";
		public static readonly string SearchResultsNotAvailableToken = "!Placeholder_SearchResultsNotAvailable!";

		public bool IsSelectedItem
		{
			get
			{
				return libraryDataView.SelectedItems.Contains(this);
			}
		}
		public CheckBox selectionCheckBox;
		public RGBA_Bytes WidgetBackgroundColor;
		public RGBA_Bytes WidgetTextColor;
		protected LibraryDataView libraryDataView;
		protected TextWidget partLabel;
		protected SlideWidget rightButtonOverlay;
		protected GuiWidget selectionCheckBoxContainer;
		private bool isHoverItem = false;
		private LinkButtonFactory linkButtonFactory = new LinkButtonFactory();
		private GuiWidget thumbnailWidget;
        protected GuiWidget middleColumn;

		private EventHandler unregisterEvents;

		/// <summary>
		/// Indicates that this item is a logical element meant to support the view or if it's a standard provider item
		/// </summary>
		public bool IsViewHelperItem { get; set; }

		/// <summary>
		/// Indicates that this item should support the slide in rightButtonOverlay actions
		/// </summary>
		public bool EnableSlideInActions { get; set; }

		public LibraryRowItem(LibraryDataView libraryDataView, GuiWidget thumbnailWidget)
		{
			this.thumbnailWidget = thumbnailWidget;
			this.libraryDataView = libraryDataView;
			this.IsViewHelperItem = false;
			this.EnableSlideInActions = true;

			MouseEnterBounds += (s, e) => UpdateHoverState();
			MouseLeaveBounds += (s, e) => UpdateHoverState();
		}

		public string ItemName { get; protected set; }

		public bool IsHoverItem
		{
			get { return isHoverItem; }
			set
			{
				if (this.isHoverItem != value && this.EnableSlideInActions)
				{
					this.isHoverItem = value;
					if (value == true && !this.libraryDataView.EditMode)
					{
						this.rightButtonOverlay.SlideIn();
					}
					else
					{
						this.rightButtonOverlay.SlideOut();
					}
				}
			}
		}

		public abstract bool Protected { get; }

		public override void OnClosed(EventArgs e)
		{
			if (unregisterEvents != null)
			{
				unregisterEvents(this, null);
			}
			base.OnClosed(e);
		}

		void selectionCheckBox_Click(object sender, EventArgs e)
		{
			if(this.selectionCheckBox.Checked)
			{
				if(!libraryDataView.SelectedItems.Contains(this))
				{
					libraryDataView.SelectedItems.Add(this);
				}
			}
			else
			{
				if(libraryDataView.SelectedItems.Contains(this))
				{
					libraryDataView.SelectedItems.Remove(this);
				}
			}
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			if (this.libraryDataView.EditMode && !this.IsViewHelperItem)
			{
				this.selectionCheckBox.Checked = this.IsSelectedItem;
				selectionCheckBoxContainer.Visible = true;
				rightButtonOverlay.Visible = false;
			}
			else
			{
				selectionCheckBoxContainer.Visible = false;
			}

			base.OnDraw(graphics2D);

			if (this.IsSelectedItem && !this.IsViewHelperItem)
			{
				this.BackgroundColor = ActiveTheme.Instance.PrimaryAccentColor;
				this.partLabel.TextColor = RGBA_Bytes.White;
				this.selectionCheckBox.TextColor = RGBA_Bytes.White;
			}
			else if (this.IsHoverItem)
			{
				RectangleDouble Bounds = LocalBounds;
				RoundedRect rectBorder = new RoundedRect(Bounds, 0);

				this.BackgroundColor = RGBA_Bytes.White;
				this.partLabel.TextColor = RGBA_Bytes.Black;
				this.selectionCheckBox.TextColor = RGBA_Bytes.Black;

				graphics2D.Render(new Stroke(rectBorder, 3), ActiveTheme.Instance.SecondaryAccentColor);
			}
			else
			{
				this.BackgroundColor = new RGBA_Bytes(255, 255, 255, 255);
				this.partLabel.TextColor = RGBA_Bytes.Black;
				this.selectionCheckBox.TextColor = RGBA_Bytes.Black;
			}
		}

		protected void CreateGuiElements()
		{
			this.Cursor = Cursors.Hand;

			linkButtonFactory.fontSize = 10;
			linkButtonFactory.textColor = RGBA_Bytes.White;

			WidgetTextColor = RGBA_Bytes.Black;
			WidgetBackgroundColor = RGBA_Bytes.White;

			SetDisplayAttributes();

			FlowLayoutWidget mainContainer = new FlowLayoutWidget(FlowDirection.LeftToRight);
			mainContainer.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
			mainContainer.VAnchor = VAnchor.ParentBottomTop;
			{
				partLabel = new TextWidget(this.ItemName.Replace('_', ' '), pointSize: 14);

				GuiWidget primaryContainer = new GuiWidget();
				primaryContainer.HAnchor = HAnchor.ParentLeftRight;
				primaryContainer.VAnchor = VAnchor.ParentBottomTop;
				primaryContainer.Name = "Row Item " + partLabel.Text;

				FlowLayoutWidget primaryFlow = new FlowLayoutWidget(FlowDirection.LeftToRight);
				primaryFlow.HAnchor = HAnchor.ParentLeftRight;
				primaryFlow.VAnchor = VAnchor.ParentBottomTop;

				selectionCheckBoxContainer = new GuiWidget();
				selectionCheckBoxContainer.VAnchor = VAnchor.ParentBottomTop;
				selectionCheckBoxContainer.Width = 40;
				selectionCheckBoxContainer.Visible = false;
				selectionCheckBoxContainer.Margin = new BorderDouble(left: 6);
				selectionCheckBox = new CheckBox("");
				selectionCheckBox.Click += selectionCheckBox_Click;
				selectionCheckBox.Name = "Row Item Select Checkbox";
				selectionCheckBox.VAnchor = VAnchor.ParentCenter;
				selectionCheckBox.HAnchor = HAnchor.ParentCenter;
				selectionCheckBoxContainer.AddChild(selectionCheckBox);

				middleColumn = new GuiWidget(0.0, 0.0);
				middleColumn.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
				middleColumn.VAnchor = Agg.UI.VAnchor.ParentBottomTop;
				middleColumn.Margin = new BorderDouble(10, 3);
				{
					partLabel.TextColor = WidgetTextColor;
					partLabel.MinimumSize = new Vector2(1, 18);
					partLabel.VAnchor = VAnchor.ParentCenter;
					middleColumn.AddChild(partLabel);

					bool mouseDownOnMiddle = false;
					middleColumn.MouseDown += (sender, e) =>
					{
						// Abort normal processing for view helpers
						if (this.IsViewHelperItem)
						{
							return;
						}
						mouseDownOnMiddle = true;
					};

					middleColumn.MouseUp += (sender, e) =>
					{
						if (mouseDownOnMiddle &
						middleColumn.LocalBounds.Contains(e.Position))
						{
							if (this.libraryDataView.EditMode)
							{
								if (this.IsSelectedItem)
								{
									libraryDataView.SelectedItems.Remove(this);
								}
								else
								{
									libraryDataView.SelectedItems.Add(this);
								}
								Invalidate();
							}
							else
							{
								// we only have single selection
								if (this.IsSelectedItem)
								{
									// It is already selected, do nothing.
								}
								else
								{
									libraryDataView.ClearSelectedItems();
									libraryDataView.SelectedItems.Add(this);
									Invalidate();
								}
							}
						}

						mouseDownOnMiddle = false;
					};
				}
				primaryFlow.AddChild(selectionCheckBoxContainer);

				primaryFlow.AddChild(thumbnailWidget);
				primaryFlow.AddChild(middleColumn);

				primaryContainer.AddChild(primaryFlow);

				rightButtonOverlay = GetItemActionButtons();
				rightButtonOverlay.Visible = false;

				mainContainer.AddChild(primaryContainer);
				mainContainer.AddChild(rightButtonOverlay);
			}
			this.AddChild(mainContainer);

			AddHandlers();
		}

		#region Abstract Functions

		public abstract void AddToQueue();

		public abstract void Edit();

		public abstract void Export();
		
		public abstract void RemoveFromCollection();

		protected abstract SlideWidget GetItemActionButtons();

		protected abstract void RemoveThisFromPrintLibrary();

		#endregion Abstract Functions

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			UpdateHoverState();
			base.OnMouseMove(mouseEvent);
		}

		void UpdateHoverState()
		{
			UiThread.RunOnIdle(() =>
			{
				switch (UnderMouseState)
				{
					case UnderMouseState.NotUnderMouse:
						IsHoverItem = false;
						break;

					case UnderMouseState.FirstUnderMouse:
						IsHoverItem = true;
						break;

					case UnderMouseState.UnderMouseNotFirst:
						if (ContainsFirstUnderMouseRecursive())
						{
							IsHoverItem = true;
						}
						else
						{
							IsHoverItem = false;
						}
						break;
				}
			});
		}

		private void AddHandlers()
		{
			//ActiveTheme.ThemeChanged.RegisterEvent(onThemeChanged, ref unregisterEvents);
			GestureFling += (object sender, FlingEventArgs eventArgs) =>
			{
				if (!this.libraryDataView.EditMode)
				{
					if (eventArgs.Direction == FlingDirection.Left)
					{
						this.rightButtonOverlay.SlideIn();
					}
					else if (eventArgs.Direction == FlingDirection.Right)
					{
						this.rightButtonOverlay.SlideOut();
					}
				}
				this.Invalidate();
			};
		}

		private void onAddLinkClick(object sender, EventArgs e)
		{
		}

		private void onConfirmRemove(bool messageBoxResponse)
		{
			if (messageBoxResponse)
			{
				libraryDataView.RemoveChild(this);
			}
		}

		private void onThemeChanged(object sender, EventArgs e)
		{
			//Set background and text color to new theme
			this.Invalidate();
		}

		private void SetDisplayAttributes()
		{
			//this.VAnchor = Agg.UI.VAnchor.FitToChildren;
			this.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
			this.Height = 50 * GuiWidget.DeviceScale;

			this.Padding = new BorderDouble(0);
			this.Margin = new BorderDouble(6, 0, 6, 6);
		}
	}
}