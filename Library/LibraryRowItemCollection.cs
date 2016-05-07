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
using MatterHackers.Agg.Image;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.PrintLibrary.Provider;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.VectorMath;
using System;
using System.Globalization;
using System.IO;

namespace MatterHackers.MatterControl.PrintLibrary
{
	public class LibraryRowItemCollection : LibraryRowItem
	{
		LibraryProvider parentProvider;
		LibraryProvider currentProvider;
		PrintItemCollection printItemCollection;
		public int CollectionIndex { get; private set; }
		string openButtonText;

		public LibraryRowItemCollection(PrintItemCollection collection, LibraryProvider currentProvider, int collectionIndex, LibraryDataView libraryDataView, LibraryProvider parentProvider, GuiWidget thumbnailWidget, string openButtonText)
			: base(libraryDataView, thumbnailWidget)
		{
			this.openButtonText = openButtonText;
			this.currentProvider = currentProvider;
			this.CollectionIndex = collectionIndex;
			this.parentProvider = parentProvider;
			this.printItemCollection = collection;
			this.ItemName = printItemCollection.Name;

			this.Name = this.ItemName + " Row Item Collection";

			if (collection.Key == LibraryRowItem.LoadingPlaceholderToken)
			{
				this.EnableSlideInActions = false;
				this.IsViewHelperItem = true;
			}

			CreateGuiElements();
		}

		public PrintItemCollection PrintItemCollection { get { return printItemCollection; } }

		public override bool Protected
		{
			get
			{
				return currentProvider.IsProtected();
			}
		}

		public override void Export()
		{
			throw new NotImplementedException();
		}

		public override void Edit()
		{
			throw new NotImplementedException();
		}

		private static readonly string collectionNotEmtyMessage = "The folder '{0}' is not empty.\n\nWould you like to delete it anyway?".Localize();
		private static readonly string collectionNotEmtyTitle = "Delete folder?".Localize();
		private static readonly string deleteNow = "Delete".Localize();
		private static readonly string doNotDelete = "Cancel".Localize();

		public override void RemoveFromCollection()
		{
			int collectionItemCollectionCount = currentProvider.GetCollectionChildCollectionCount(CollectionIndex);
			int collectionItemItemCount = currentProvider.GetCollectionItemCount(CollectionIndex);

			if (collectionItemCollectionCount > 0 || collectionItemItemCount > 0)
			{
				string message = collectionNotEmtyMessage.FormatWith(currentProvider.GetCollectionItem(CollectionIndex).Name);
				UiThread.RunOnIdle(() =>
				{
					// Let the user know this collection is not empty and check if they want to delete it.
					StyledMessageBox.ShowMessageBox(ProcessDialogResponse, message, collectionNotEmtyTitle, StyledMessageBox.MessageType.YES_NO, deleteNow, doNotDelete);
				});
			}
			else
			{
				currentProvider.RemoveCollection(CollectionIndex);
			}
		}

		private void ProcessDialogResponse(bool messageBoxResponse)
		{
			if (messageBoxResponse)
			{
				currentProvider.RemoveCollection(CollectionIndex);
			}
		}

		public override void AddToQueue()
		{
			throw new NotImplementedException();
		}

		private ConditionalClickWidget primaryClickContainer;

		protected override SlideWidget GetItemActionButtons()
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
			openButton.Name = "Open Collection";
			openButton.Cursor = Cursors.Hand;
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

		private void ChangeCollection()
		{
			if (parentProvider == null)
			{
				libraryDataView.CurrentLibraryProvider = currentProvider.GetProviderForCollection(printItemCollection);
			}
			else
			{
				libraryDataView.CurrentLibraryProvider = parentProvider;
			}
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			if (IsDoubleClick(mouseEvent) 
				&& this.EnableSlideInActions)
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

		private void onAddLinkClick(object sender, EventArgs e)
		{
		}

		protected override void RemoveThisFromPrintLibrary()
		{
			throw new NotImplementedException();
		}

		private void onRemoveLinkClick(object sender, EventArgs e)
		{
			UiThread.RunOnIdle(RemoveThisFromPrintLibrary);
		}

		private void onThemeChanged(object sender, EventArgs e)
		{
			//Set background and text color to new theme
			this.Invalidate();
		}
	}
}