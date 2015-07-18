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
		PrintItemCollection printItemCollection;

		public LibraryRowItemCollection(PrintItemCollection collection, LibraryDataView libraryDataView, LibraryProvider parentProvider, GuiWidget thumbnailWidget)
			: base(libraryDataView, thumbnailWidget)
		{
			this.parentProvider = parentProvider;
			this.printItemCollection = collection;
			this.ItemName = printItemCollection.Name;

			CreateGuiElements();
		}

		public PrintItemCollection PrintItemCollection { get { return printItemCollection; } }

		public override bool Protected
		{
			get { throw new NotImplementedException(); }
		}

		public override void Export()
		{
			throw new NotImplementedException();
		}

		public override void Edit()
		{
			throw new NotImplementedException();
		}

		private static string collectionNotEmtyMessage = "The collection you are trying to delete '{0}' is not empty. Would you like to delete it anyway?".Localize();
		private static string collectionNotEmtyTitle = "Collection not Empty".Localize();
		private static string deleteNow = "Delete".Localize();
		private static string doNotDelete = "Do NOT Delete".Localize();

		public override void RemoveFromCollection()
		{
			using (LibraryProvider collectionProvider = LibraryDataView.CurrentLibraryProvider.GetProviderForItem(printItemCollection))
			{
				if (collectionProvider.ItemCount > 0 || collectionProvider.CollectionCount > 0)
				{
					collectionNotEmtyMessage = collectionNotEmtyMessage.FormatWith(printItemCollection.Name);
					UiThread.RunOnIdle(() =>
					{
						// Let the user know this collection is not empty and check if they want to delete it.
						StyledMessageBox.ShowMessageBox(ProcessDialogResponse, collectionNotEmtyMessage, collectionNotEmtyTitle, StyledMessageBox.MessageType.YES_NO, deleteNow, doNotDelete);
					});
				}
				else
				{
					LibraryDataView.CurrentLibraryProvider.RemoveCollection(printItemCollection);
				}
			}
		}

		private void ProcessDialogResponse(bool messageBoxResponse)
		{
			if (messageBoxResponse)
			{
				LibraryDataView.CurrentLibraryProvider.RemoveCollection(printItemCollection);
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

			TextWidget openLabel = new TextWidget("Open".Localize());
			openLabel.TextColor = RGBA_Bytes.White;
			openLabel.VAnchor = VAnchor.ParentCenter;
			openLabel.HAnchor = HAnchor.ParentCenter;

			FatFlatClickWidget openButton = new FatFlatClickWidget(openLabel);
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
				LibraryDataView.CurrentLibraryProvider = LibraryDataView.CurrentLibraryProvider.GetProviderForItem(printItemCollection);
			}
			else
			{
				LibraryDataView.CurrentLibraryProvider = parentProvider;
			}

			UiThread.RunOnIdle(libraryDataView.RebuildView);
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			if (mouseEvent.Clicks == 2)
			{
				UiThread.RunOnIdle(ChangeCollection);
			}
			base.OnMouseDown(mouseEvent);
		}

		private void SetDisplayAttributes()
		{
			//this.VAnchor = Agg.UI.VAnchor.FitToChildren;
			this.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
			this.Height = 50 * TextWidget.GlobalPointSizeScaleRatio;

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