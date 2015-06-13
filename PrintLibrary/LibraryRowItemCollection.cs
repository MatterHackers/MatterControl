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
		PrintItemCollection collection;

		public LibraryRowItemCollection(PrintItemCollection collection, LibraryDataView libraryDataView)
			: base(libraryDataView)
		{
			this.collection = collection;
			CreateGuiElements();
		}

		public override void Export()
		{
			throw new NotImplementedException();
		}

		public override void Edit()
		{
			throw new NotImplementedException();
		}

		public override void RemoveFromCollection()
		{
			throw new NotImplementedException();
		}

		public override void AddToQueue()
		{
			throw new NotImplementedException();
		}

		public override void RemoveFromParentCollection()
		{
			throw new NotImplementedException();
		}

		private ConditionalClickWidget primaryClickContainer;

		protected override GuiWidget GetThumbnailWidget()
		{
			PartThumbnailWidget thumbnailWidget = new PartThumbnailWidget(null, "part_icon_transparent_40x40.png", "building_thumbnail_40x40.png", PartThumbnailWidget.ImageSizes.Size50x50);
			return thumbnailWidget;
		}

		protected override string GetItemName()
		{
			return collection.Name;
		}

		protected override SlideWidget getItemActionButtons()
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
				throw new NotImplementedException();
			};

			buttonFlowContainer.AddChild(openButton);

			buttonContainer.AddChild(buttonFlowContainer);
			buttonContainer.Width = 200;

			return buttonContainer;
		}

		private void SetDisplayAttributes()
		{
			//this.VAnchor = Agg.UI.VAnchor.FitToChildren;
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