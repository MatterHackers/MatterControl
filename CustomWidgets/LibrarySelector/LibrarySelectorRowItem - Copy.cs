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

namespace MatterHackers.MatterControl.CustomWidgets.LibrarySelector
{
	public abstract class LibrarySelectorRowItem : GuiWidget
	{
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
		protected LibrarySelectorWidget libraryDataView;
		protected TextWidget partLabel;
		protected SlideWidget rightButtonOverlay;
		protected GuiWidget selectionCheckBoxContainer;
		private bool isHoverItem = false;
		private LinkButtonFactory linkButtonFactory = new LinkButtonFactory();
		private GuiWidget thumbnailWidget;

		private event EventHandler unregisterEvents;

		public LibrarySelectorRowItem(LibrarySelectorWidget libraryDataView, GuiWidget thumbnailWidget)
		{
			this.thumbnailWidget = thumbnailWidget;
			this.libraryDataView = libraryDataView;
		}

		public string ItemName { get; protected set; }

		public bool IsHoverItem
		{
			get { return isHoverItem; }
			set
			{
				if (this.isHoverItem != value)
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

		public override void OnDraw(Graphics2D graphics2D)
		{
			if (this.libraryDataView.EditMode)
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

			if (this.IsSelectedItem)
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

			TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;

			SetDisplayAttributes();

			FlowLayoutWidget mainContainer = new FlowLayoutWidget(FlowDirection.LeftToRight);
			mainContainer.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
			mainContainer.VAnchor = VAnchor.ParentBottomTop;
			{
				GuiWidget primaryContainer = new GuiWidget();
				primaryContainer.HAnchor = HAnchor.ParentLeftRight;
				primaryContainer.VAnchor = VAnchor.ParentBottomTop;

				FlowLayoutWidget primaryFlow = new FlowLayoutWidget(FlowDirection.LeftToRight);
				primaryFlow.HAnchor = HAnchor.ParentLeftRight;
				primaryFlow.VAnchor = VAnchor.ParentBottomTop;

				selectionCheckBoxContainer = new GuiWidget();
				selectionCheckBoxContainer.VAnchor = VAnchor.ParentBottomTop;
				selectionCheckBoxContainer.Width = 40;
				selectionCheckBoxContainer.Visible = false;
				selectionCheckBoxContainer.Margin = new BorderDouble(left: 6);
				selectionCheckBox = new CheckBox("");
				selectionCheckBox.VAnchor = VAnchor.ParentCenter;
				selectionCheckBox.HAnchor = HAnchor.ParentCenter;
				selectionCheckBoxContainer.AddChild(selectionCheckBox);

				GuiWidget middleColumn = new GuiWidget(0.0, 0.0);
				middleColumn.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
				middleColumn.VAnchor = Agg.UI.VAnchor.ParentBottomTop;
				middleColumn.Margin = new BorderDouble(10, 6);
				{
					partLabel = new TextWidget(this.ItemName.Replace('_', ' '), pointSize: 14);
					partLabel.TextColor = WidgetTextColor;
					partLabel.MinimumSize = new Vector2(1, 18);
					partLabel.VAnchor = VAnchor.ParentCenter;
					middleColumn.AddChild(partLabel);

					middleColumn.MouseDown += (sender, e) =>
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
						}
						else
						{
							// we only have single selection
							if (this.IsSelectedItem)
							{
								// It is aleady selected, do nothing.
							}
							else
							{
								libraryDataView.SelectedItems.Clear();
								libraryDataView.SelectedItems.Add(this);
							}
						}
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

		private void AddHandlers()
		{
			MouseEnterBounds += (sender, e) =>
			{
				IsHoverItem = true;
			};
			MouseLeaveBounds += (sender, e) =>
			{
				IsHoverItem = false;
			};

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

		private void onThemeChanged(object sender, EventArgs e)
		{
			//Set background and text color to new theme
			this.Invalidate();
		}

		private void SetDisplayAttributes()
		{
			this.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
			if (ActiveTheme.Instance.DisplayMode == ActiveTheme.ApplicationDisplayType.Touchscreen)
			{
				this.Height = 65;
			}
			else
			{
				this.Height = 50;
			}

			this.Padding = new BorderDouble(0);
			this.Margin = new BorderDouble(6, 0, 6, 6);
		}
	}
}