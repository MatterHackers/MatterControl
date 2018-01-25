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
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.Library;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.CustomWidgets
{
	public class RowListView : FlowLayoutWidget, IListContentView
	{
		public int ThumbWidth { get; } = 50;
		public int ThumbHeight { get; } = 50;

		public RowListView()
			: base(FlowDirection.TopToBottom)
		{
		}

		public ListViewItemBase AddItem(ListViewItem item)
		{
			var detailsView = new RowViewItem(item, this.ThumbWidth, this.ThumbHeight);
			this.AddChild(detailsView);

			return detailsView;
		}

		public void ClearItems()
		{
		}

		public void BeginReload()
		{
		}

		public void EndReload()
		{
		}
	}

	public class RowViewItem : ListViewItemBase
	{
		private CheckBox selectionCheckBox;

		private SlideWidget actionButtonContainer;

		private ConditionalClickWidget conditionalClickContainer;

		private TextWidget partLabel;

		private GuiWidget middleColumn;

		//private TextWidget partStatus;

		private GuiWidget selectionCheckBoxContainer;

		private FatFlatClickWidget viewButton;

		private TextWidget viewButtonLabel;

		private event EventHandler unregisterEvents;

		public RowViewItem(ListViewItem listViewItem, int thumbWidth, int thumbHeight)
			: base(listViewItem, thumbWidth, thumbHeight)
		{
			// Set Display Attributes
			this.VAnchor = VAnchor.Fit;
			this.HAnchor = HAnchor.Stretch | HAnchor.Fit;
			this.Height = 50;
			this.BackgroundColor = Color.White;
			this.Padding = new BorderDouble(0);
			this.Margin = new BorderDouble(6, 0, 6, 6);

			var topToBottomLayout = new FlowLayoutWidget(FlowDirection.TopToBottom) { HAnchor = HAnchor.Stretch };

			var topContentsFlowLayout = new FlowLayoutWidget(FlowDirection.LeftToRight) { HAnchor = HAnchor.Stretch };
			{
				selectionCheckBoxContainer = new GuiWidget()
				{
					VAnchor = VAnchor.Stretch,
					Width = 40,
					Visible = false,
					Margin = new BorderDouble(left: 6)
				};

				selectionCheckBox = new CheckBox("")
				{
					Name = "List Item Checkbox",
					VAnchor = VAnchor.Center,
					HAnchor = HAnchor.Center
				};
				selectionCheckBoxContainer.AddChild(selectionCheckBox);

				var leftColumn = new FlowLayoutWidget(FlowDirection.LeftToRight)
				{
					VAnchor = VAnchor.Top | VAnchor.Fit
				};
				topContentsFlowLayout.AddChild(leftColumn);

				// TODO: add in default thumbnail handling from parent or IListItem
				imageWidget = new ImageWidget(thumbWidth, thumbHeight)
				{
					Name = "List Item Thumbnail",
					BackgroundColor = ActiveTheme.Instance.PrimaryAccentColor
				};
				leftColumn.AddChild(imageWidget);

				partLabel = new TextWidget(listViewItem.Model.Name, pointSize: 14)
				{
					TextColor = Color.Black,
					MinimumSize = new Vector2(1, 18),
					VAnchor = VAnchor.Center
				};

				middleColumn = new GuiWidget(0.0, 0.0)
				{
					VAnchor = VAnchor.Stretch,
					HAnchor = HAnchor.Stretch,
					Padding = 0,
					Margin = new BorderDouble(10, 3)
				};

				bool mouseDownOnMiddle = false;
				middleColumn.MouseDown += (sender, e) =>
				{
					mouseDownOnMiddle = true;
				};

				middleColumn.MouseUp += (sender, e) =>
				{
					if (mouseDownOnMiddle
						&& middleColumn.LocalBounds.Contains(e.Position))
					{
						this.OnItemSelect();
					}

					mouseDownOnMiddle = false;
				};

				middleColumn.AddChild(partLabel);

				topContentsFlowLayout.AddChild(middleColumn);
			}

			// The ConditionalClickWidget supplies a user driven Enabled property based on a delegate of your choosing
			conditionalClickContainer = new ConditionalClickWidget(() => this.EditMode)
			{
				HAnchor = HAnchor.Stretch,
				VAnchor = VAnchor.Stretch
			};
			conditionalClickContainer.Click += onQueueItemClick;

			topToBottomLayout.AddChild(topContentsFlowLayout);
			this.AddChild(topToBottomLayout);

			actionButtonContainer = getItemActionButtons();
			actionButtonContainer.Visible = false;
			this.AddChild(conditionalClickContainer);

			this.AddChild(actionButtonContainer);
		}

		public override async void OnLoad(EventArgs args)
		{
			base.OnLoad(args);
			await this.LoadItemThumbnail();
		}

		private bool isHoverItem = false;
		public override bool IsHoverItem
		{
			get { return isHoverItem; }
			set
			{
				if (this.isHoverItem != value)
				{
					this.isHoverItem = value;
					if (value && !this.EditMode)
					{
						this.actionButtonContainer.SlideIn();
					}
					else
					{
						this.actionButtonContainer.SlideOut();
					}

					UpdateColors();
				}
			}
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}

		protected override void UpdateColors()
		{
			base.UpdateColors();

			if (this.IsActivePrint && !this.EditMode)
			{
				this.BackgroundColor = ActiveTheme.Instance.PrimaryAccentColor;
				this.partLabel.TextColor = Color.White;
				//this.partStatus.TextColor = Color.White;
				this.viewButton.BackgroundColor = Color.White;
				this.viewButtonLabel.TextColor = ActiveTheme.Instance.PrimaryAccentColor;
			}
			else if (this.IsSelected)
			{
				this.BackgroundColor = ActiveTheme.Instance.PrimaryAccentColor;
				this.partLabel.TextColor = Color.White;
				//this.partStatus.TextColor = Color.White;
				this.selectionCheckBox.TextColor = Color.White;
				this.viewButton.BackgroundColor = Color.White;
				this.viewButtonLabel.TextColor = ActiveTheme.Instance.PrimaryAccentColor;
			}
			else if (this.IsHoverItem)
			{
				this.BackgroundColor = Color.White;
				this.partLabel.TextColor = Color.Black;
				this.selectionCheckBox.TextColor = Color.Black;
				//this.partStatus.TextColor = Color.Black;
				this.viewButton.BackgroundColor = ActiveTheme.Instance.PrimaryAccentColor;
				this.viewButtonLabel.TextColor = Color.White;
			}
			else
			{
				this.BackgroundColor = new Color(255, 255, 255, 255);
				this.partLabel.TextColor = Color.Black;
				this.selectionCheckBox.TextColor = Color.Black;
				//this.partStatus.TextColor = Color.Black;
				this.viewButton.BackgroundColor = ActiveTheme.Instance.PrimaryAccentColor;
				this.viewButtonLabel.TextColor = Color.White;
			}
		}

		private SlideWidget getItemActionButtons()
		{
			var removeLabel = new TextWidget("Remove".Localize())
			{
				Name = "Queue Item " + listViewItem.Model.Name + " Remove",
				TextColor = Color.White,
				VAnchor = VAnchor.Center,
				HAnchor = HAnchor.Center
			};

			var removeButton = new FatFlatClickWidget(removeLabel)
			{
				VAnchor = VAnchor.Stretch,
				BackgroundColor = ActiveTheme.Instance.PrimaryAccentColor,
				Width = 100
			};
			removeButton.Click += onRemovePartClick;

			viewButtonLabel = new TextWidget("View".Localize())
			{
				Name = "Queue Item " + listViewItem.Model.Name + " View",
				TextColor = Color.White,
				VAnchor = VAnchor.Center,
				HAnchor = HAnchor.Center,
			};

			viewButton = new FatFlatClickWidget(viewButtonLabel)
			{
				VAnchor = VAnchor.Stretch,
				BackgroundColor = ActiveTheme.Instance.PrimaryAccentColor,
				Width = 100,
			};
			viewButton.Click += onViewPartClick;

			var buttonFlowContainer = new FlowLayoutWidget(FlowDirection.LeftToRight)
			{
				VAnchor = VAnchor.Stretch
			};
			buttonFlowContainer.AddChild(viewButton);
			buttonFlowContainer.AddChild(removeButton);

			var buttonContainer = new SlideWidget()
			{
				VAnchor = VAnchor.Stretch,
				HAnchor = HAnchor.Right
			};
			buttonContainer.AddChild(buttonFlowContainer);
			buttonContainer.Width = 200;

			return buttonContainer;
		}

		protected override async void UpdateHoverState()
		{
			if (!mouseInBounds)
			{
				IsHoverItem = false;
				return;
			}

			// Hover only occurs after mouse is in bounds for a given period of time
			await Task.Delay(500);

			if (!mouseInBounds)
			{
				IsHoverItem = false;
				return;
			}

			switch (UnderMouseState)
			{
				case UnderMouseState.NotUnderMouse:
					IsHoverItem = false;
					break;

				case UnderMouseState.FirstUnderMouse:
					IsHoverItem = true;
					break;

				case UnderMouseState.UnderMouseNotFirst:
					IsHoverItem = ContainsFirstUnderMouseRecursive();
					break;
			}
		}

		private void onQueueItemClick(object sender, EventArgs e)
		{
			if (this.IsSelected)
			{
				this.IsSelected = false;
				this.selectionCheckBox.Checked = false;
			}
			else
			{
				this.IsSelected = true;
				this.selectionCheckBox.Checked = true;
			}
		}

		private void onRemovePartClick(object sender, EventArgs e)
		{
			this.actionButtonContainer.SlideOut();
			//UiThread.RunOnIdle(DeletePartFromQueue);
		}

		private void onViewPartClick(object sender, EventArgs e)
		{
			this.actionButtonContainer.SlideOut();
			//UiThread.RunOnIdle(() =>
			//{
			//	OpenPartViewWindow(View3DWidget.OpenMode.Viewing);
			//});
		}

	}
}