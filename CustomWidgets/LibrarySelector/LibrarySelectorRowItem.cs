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
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrintLibrary.Provider;
using MatterHackers.VectorMath;
using System;
using System.Globalization;
using System.Threading.Tasks;
using MatterHackers.Localizations;

namespace MatterHackers.MatterControl.CustomWidgets.LibrarySelector
{
	public class LibrarySelectorRowItem : GuiWidget
	{
		LibraryProvider parentProvider;
		PrintItemCollection printItemCollection;
		public int CollectionIndex { get; private set; }

		public RGBA_Bytes WidgetBackgroundColor;
		public RGBA_Bytes WidgetTextColor;
		protected TextWidget partLabel;
		protected SlideWidget rightButtonOverlay;
		private bool isHoverItem = false;
		private LinkButtonFactory linkButtonFactory = new LinkButtonFactory();
		private GuiWidget thumbnailWidget;

		private event EventHandler unregisterEvents;

		public LibrarySelectorWidget libraryDataView { get; private set; }

		public LibrarySelectorRowItem(PrintItemCollection collection, int collectionIndex, LibrarySelectorWidget libraryDataView, LibraryProvider parentProvider, GuiWidget thumbnailWidget, string openButtonText)
		{
			this.thumbnailWidget = thumbnailWidget;
			this.libraryDataView = libraryDataView;

			this.CollectionIndex = collectionIndex;
			this.parentProvider = parentProvider;
			this.printItemCollection = collection;
			this.ItemName = printItemCollection.Name;

			this.Name = this.ItemName + " Row Item Collection";

			CreateGuiElements(openButtonText);

			MouseEnterBounds += (s, e) => EnteredBounds();
			MouseLeaveBounds += (s, e) => EnteredBounds();
		}

		public PrintItemCollection PrintItemCollection { get { return printItemCollection; } }

		public bool Protected
		{
			get { return false; }
		}

		private void ProcessDialogResponse(bool messageBoxResponse)
		{
			if (messageBoxResponse)
			{
				libraryDataView.CurrentLibraryProvider.RemoveCollection(CollectionIndex);
			}
		}

		private void ChangeCollection()
		{
			if (parentProvider == null)
			{
				libraryDataView.CurrentLibraryProvider = libraryDataView.CurrentLibraryProvider.GetProviderForCollection(printItemCollection);
			}
			else
			{
				libraryDataView.CurrentLibraryProvider = parentProvider;
			}

			UiThread.RunOnIdle(libraryDataView.RebuildView);
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			if (IsDoubleClick(mouseEvent))
			{
				UiThread.RunOnIdle(ChangeCollection);
			}
			base.OnMouseDown(mouseEvent);
		}
		
		private void SetDisplayAttributes()
		{
			//this.VAnchor = Agg.UI.VAnchor.FitToChildren;
			this.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
			this.Height = 50 * GuiWidget.DeviceScale;

			this.Padding = new BorderDouble(0);
			this.Margin = new BorderDouble(6, 0, 6, 6);
		}

		protected SlideWidget GetItemActionButtons(string openButtonText)
		{
			SlideWidget buttonContainer = new SlideWidget();
			buttonContainer.VAnchor = VAnchor.ParentBottomTop;

			FlowLayoutWidget buttonFlowContainer = new FlowLayoutWidget(FlowDirection.LeftToRight);
			buttonFlowContainer.VAnchor = VAnchor.ParentBottomTop;

			TextWidget openLabel = new TextWidget(openButtonText);
			openLabel.TextColor = RGBA_Bytes.White;
			openLabel.VAnchor = VAnchor.ParentCenter;
			openLabel.HAnchor = HAnchor.ParentCenter;

			FatFlatClickWidget openButton = new FatFlatClickWidget(openLabel);
			openButton.Cursor = Cursors.Hand;
			openButton.Name = "Open Collection";
			openButton.VAnchor = VAnchor.ParentBottomTop;
			openButton.BackgroundColor = ActiveTheme.Instance.PrimaryAccentColor;
			openButton.Width = 100;
			openButton.Click += (sender, e) =>
			{
				ChangeCollection();
			};

			buttonFlowContainer.AddChild(openButton);

			buttonContainer.AddChild(buttonFlowContainer);
			buttonContainer.Width = 100;

			return buttonContainer;
		}

		private void onThemeChanged(object sender, EventArgs e)
		{
			//Set background and text color to new theme
			this.Invalidate();
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
					if (value == true)
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
			base.OnDraw(graphics2D);
			
			if (this.IsHoverItem)
			{
				RectangleDouble Bounds = LocalBounds;
				RoundedRect rectBorder = new RoundedRect(Bounds, 0);

				this.BackgroundColor = RGBA_Bytes.White;
				this.partLabel.TextColor = RGBA_Bytes.Black;

				graphics2D.Render(new Stroke(rectBorder, 3), ActiveTheme.Instance.SecondaryAccentColor);
			}
			else
			{
				this.BackgroundColor = new RGBA_Bytes(255, 255, 255, 255);
				this.partLabel.TextColor = RGBA_Bytes.Black;
			}
		}

		protected void CreateGuiElements(string openButtonText)
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

				GuiWidget middleColumn = new GuiWidget(0.0, 0.0);
				middleColumn.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
				middleColumn.VAnchor = Agg.UI.VAnchor.ParentBottomTop;
				middleColumn.Margin = new BorderDouble(10, 6);
				{
					partLabel = new TextWidget(this.ItemName.Replace('_', ' '), pointSize: 14);
					partLabel.Name = "Row Item " + partLabel.Text;
					partLabel.TextColor = WidgetTextColor;
					partLabel.MinimumSize = new Vector2(1, 18);
					partLabel.VAnchor = VAnchor.ParentCenter;
					middleColumn.AddChild(partLabel);
				}

				primaryFlow.AddChild(thumbnailWidget);
				primaryFlow.AddChild(middleColumn);

				primaryContainer.AddChild(primaryFlow);

				rightButtonOverlay = GetItemActionButtons(openButtonText);
				rightButtonOverlay.Visible = false;

				mainContainer.AddChild(primaryContainer);
				mainContainer.AddChild(rightButtonOverlay);
			}
			this.AddChild(mainContainer);

			AddHandlers();
		}

		void EnteredBounds()
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
		}

		private void AddHandlers()
		{
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
	}
}