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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Threading;
using System.IO;

using MatterHackers.Agg.Image;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.Localizations;

using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Processors;

namespace MatterHackers.MatterControl.PrintLibrary
{
    public class LibraryRowItem : ClickWidget
    {
        public PrintItemWrapper printItemWrapper;
        public RGBA_Bytes WidgetTextColor;
        public RGBA_Bytes WidgetBackgroundColor;

        public bool isActivePrint = false;
        public bool isSelectedItem = false;
        public bool isHoverItem = false;
        TextWidget partLabel;
        Button viewLink;
        Button removeLink;
        Button exportLink;
        Button addToQueueLink;
        public CheckBox selectionCheckBox;
        FlowLayoutWidget buttonContainer;
        LinkButtonFactory linkButtonFactory = new LinkButtonFactory();
		bool exportWindowIsOpen = false;
		bool viewWindowIsOpen = false;
		PartPreviewMainWindow viewingWindow;
		ExportPrintItemWindow exportingWindow;

		private void OpenExportWindow()
		{
			if (exportWindowIsOpen == false)
			{
				exportingWindow = new ExportPrintItemWindow(this.printItemWrapper);
				this.exportWindowIsOpen = true;
				exportingWindow.Closed += new EventHandler(ExportLibraryItemWindow_Closed);
				exportingWindow.ShowAsSystemWindow ();
			}
			else 
			{
				if (exportingWindow != null)
				{
					exportingWindow.BringToFront ();
				}
			}

		}

		void ExportLibraryItemWindow_Closed(object sender, EventArgs e)
		{
			this.exportWindowIsOpen = false;
		}

        public LibraryRowItem(PrintItemWrapper printItem)
        {
            this.printItemWrapper = printItem;
            linkButtonFactory.fontSize = 10;
            linkButtonFactory.textColor = RGBA_Bytes.White;

            WidgetTextColor = RGBA_Bytes.Black;
            WidgetBackgroundColor = RGBA_Bytes.White;

            TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;

            SetDisplayAttributes();

            FlowLayoutWidget mainContainer = new FlowLayoutWidget(FlowDirection.LeftToRight);
            mainContainer.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
            {
                GuiWidget selectionCheckBoxContainer = new GuiWidget();
                selectionCheckBoxContainer.VAnchor = VAnchor.Max_FitToChildren_ParentHeight;
                selectionCheckBoxContainer.HAnchor = Agg.UI.HAnchor.FitToChildren;
                selectionCheckBoxContainer.Margin = new BorderDouble(left: 6);
                selectionCheckBox = new CheckBox("");
                selectionCheckBox.VAnchor = VAnchor.ParentCenter;
                selectionCheckBox.HAnchor = HAnchor.ParentCenter;
                selectionCheckBoxContainer.AddChild(selectionCheckBox);
                
                FlowLayoutWidget leftColumn = new FlowLayoutWidget(FlowDirection.TopToBottom);
                leftColumn.VAnchor |= VAnchor.ParentTop;


                FlowLayoutWidget middleColumn = new FlowLayoutWidget(FlowDirection.TopToBottom);
                middleColumn.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
                middleColumn.VAnchor = Agg.UI.VAnchor.ParentBottomTop;
                middleColumn.Padding = new BorderDouble(0,6);
                middleColumn.Margin = new BorderDouble(10,0);
                {
                    string labelName = textInfo.ToTitleCase(printItem.Name);
                    labelName = labelName.Replace('_', ' ');
                    partLabel = new TextWidget(labelName, pointSize: 12);
                    partLabel.TextColor = WidgetTextColor;
                    partLabel.MinimumSize = new Vector2(1, 16);
                    middleColumn.AddChild(partLabel);
                }

                FlowLayoutWidget rightColumn = new FlowLayoutWidget(FlowDirection.TopToBottom);
                rightColumn.VAnchor = Agg.UI.VAnchor.ParentBottomTop;                

                buttonContainer = new FlowLayoutWidget();
                buttonContainer.Margin = new BorderDouble(0,6);
                buttonContainer.HAnchor = Agg.UI.HAnchor.ParentLeftRight; 
                {
					addToQueueLink = linkButtonFactory.Generate(LocalizedString.Get("Add to Queue"));
                    addToQueueLink.Margin = new BorderDouble(left: 0, right: 10);
                    addToQueueLink.VAnchor = VAnchor.ParentCenter;

                    addToQueueLink.Click += (sender, e) =>
                    {
                        QueueData.Instance.AddItem(new PrintItemWrapper(new PrintItem(this.printItemWrapper.Name, this.printItemWrapper.FileLocation)));
                    };

					viewLink = linkButtonFactory.Generate(LocalizedString.Get("View"));
                    viewLink.Margin = new BorderDouble(left: 0, right:10);
                    viewLink.VAnchor = VAnchor.ParentCenter;                    

					exportLink = linkButtonFactory.Generate(LocalizedString.Get("Export"));
                    exportLink.Margin = new BorderDouble(left: 0, right: 10);
                    exportLink.VAnchor = VAnchor.ParentCenter;

                    exportLink.Click += (sender, e) =>
                    {
						OpenExportWindow();
                    };

					removeLink = linkButtonFactory.Generate(LocalizedString.Get("Remove"));
                    removeLink.Margin = new BorderDouble(left: 10, right: 10);
                    removeLink.VAnchor = VAnchor.ParentCenter;

                    buttonContainer.AddChild(addToQueueLink);
                    buttonContainer.AddChild(viewLink);
                    buttonContainer.AddChild(exportLink);
                    buttonContainer.AddChild(removeLink);
                }
                middleColumn.AddChild(buttonContainer);
                //rightColumn.AddChild(buttonContainer);

                mainContainer.AddChild(selectionCheckBoxContainer);
                {
                    PartThumbnailWidget thumbnailWidget = new PartThumbnailWidget(printItem, "part_icon_transparent_40x40.png", "building_thumbnail_40x40.png", new Vector2(50, 50));                    
                    mainContainer.AddChild(thumbnailWidget);
                }
                mainContainer.AddChild(leftColumn);
                mainContainer.AddChild(middleColumn);                
                mainContainer.AddChild(rightColumn);
            }
            this.AddChild(mainContainer);
            AddHandlers();
        }

        void SetDisplayAttributes()
        {
            this.VAnchor = Agg.UI.VAnchor.Max_FitToChildren_ParentHeight;
            this.HAnchor = Agg.UI.HAnchor.Max_FitToChildren_ParentWidth;
            this.Height = 28;
			this.BackgroundColor = this.WidgetBackgroundColor;
            this.Padding = new BorderDouble(0);
            this.Margin = new BorderDouble(6,0,6,6);
        }

        event EventHandler unregisterEvents;
        void AddHandlers()
        {
            ActiveTheme.Instance.ThemeChanged.RegisterEvent(onThemeChanged, ref unregisterEvents);
            //this.Click += new ButtonEventHandler(PrintLibraryListItem_Click);
            viewLink.Click += new ButtonBase.ButtonEventHandler(onViewLinkClick);
            removeLink.Click += new ButtonBase.ButtonEventHandler(onRemoveLinkClick);
            selectionCheckBox.CheckedStateChanged += selectionCheckBox_CheckedStateChanged;
        }

        void PrintLibraryListItem_Click(object sender, EventArgs e)
        {
            selectionCheckBox.Checked = !selectionCheckBox.Checked;
        }

        public override void OnClosed(EventArgs e)
        {
            if (unregisterEvents != null)
            {
                unregisterEvents(this, null);
            }
            base.OnClosed(e);
        }

        private void onClick(object sender, MouseEventArgs e)
        {
            if (this.isSelectedItem == false)
            {
                this.isSelectedItem = true;
                PrintLibraryListControl.Instance.SelectedItems.Add(this);
            }
        }

        private void selectionCheckBox_CheckedStateChanged(object sender, EventArgs e)
        {            
            if (selectionCheckBox.Checked == true)
            {
                this.isSelectedItem = true;
                PrintLibraryListControl.Instance.SelectedItems.Add(this);
            }
            else
            {
                this.isSelectedItem = false;
                PrintLibraryListControl.Instance.SelectedItems.Remove(this);
            }
        }

        private void onAddLinkClick(object sender, MouseEventArgs e)
        {
        }

        void RemoveThisFromPrintLibrary(object state)
        {
            PrintLibraryListControl.Instance.RemoveChild(this);
            this.printItemWrapper.Delete();
        }

        private void onRemoveLinkClick(object sender, MouseEventArgs e)
        {
            UiThread.RunOnIdle(RemoveThisFromPrintLibrary);
        }

        private void onViewLinkClick(object sender, MouseEventArgs e)
        {
            UiThread.RunOnIdle(onViewLinkClick);
        }


		private void OpenPartViewWindow()
		{
			if (viewWindowIsOpen == false)
			{
				viewingWindow =  new PartPreviewMainWindow(this.printItemWrapper);
				this.viewWindowIsOpen = true;
				viewingWindow.Closed += new EventHandler(PartPreviewMainWindow_Closed); 
			}
			else
			{
				if(viewingWindow != null)
				{
					viewingWindow.BringToFront();
				}
			}

		}

		void PartPreviewMainWindow_Closed(object sender, EventArgs e)
		{
			viewWindowIsOpen = false;
		}


        private void onViewLinkClick(object state)
        {
            string pathAndFile = this.printItemWrapper.FileLocation;
            Console.WriteLine(pathAndFile);
            if (File.Exists(pathAndFile))
            {
				OpenPartViewWindow ();
            }
            else
            {
                string message = String.Format("Cannot find\n'{0}'.\nWould you like to remove it from the queue?", pathAndFile);
                if (StyledMessageBox.ShowMessageBox(message, "Item not found", StyledMessageBox.MessageType.YES_NO))
                {
                    PrintLibraryListControl.Instance.RemoveChild(this);
                }
            }
        }

        private void onThemeChanged(object sender, EventArgs e)
        {
            //Set background and text color to new theme
            this.Invalidate();
        }

        public override void OnDraw(Graphics2D graphics2D)
        {

            if (this.isHoverItem)
            {
                buttonContainer.Visible = true;
            }
            else
            {
                buttonContainer.Visible = false;
            }
            
            base.OnDraw(graphics2D);

            if (this.isSelectedItem)
            {
                this.BackgroundColor = ActiveTheme.Instance.PrimaryAccentColor;
                this.partLabel.TextColor = RGBA_Bytes.White;
                this.selectionCheckBox.TextColor = RGBA_Bytes.White;

                //RectangleDouble Bounds = LocalBounds;
                //RoundedRect rectBorder = new RoundedRect(Bounds, 0);
                //graphics2D.Render(new Stroke(rectBorder, 3), RGBA_Bytes.White);
            }

            else if (this.isHoverItem)
            {
                RectangleDouble Bounds = LocalBounds;
                RoundedRect rectBorder = new RoundedRect(Bounds, 0);

                this.BackgroundColor = ActiveTheme.Instance.SecondaryAccentColor;
                this.partLabel.TextColor = RGBA_Bytes.White;
                this.selectionCheckBox.TextColor = RGBA_Bytes.White;

                graphics2D.Render(new Stroke(rectBorder, 3), ActiveTheme.Instance.PrimaryAccentColor);
            }
            else
            {
                this.BackgroundColor = new RGBA_Bytes(255, 255, 255, 255);
                this.partLabel.TextColor = RGBA_Bytes.Black;
                this.selectionCheckBox.TextColor = RGBA_Bytes.Black;
            }

        }
    }
}
