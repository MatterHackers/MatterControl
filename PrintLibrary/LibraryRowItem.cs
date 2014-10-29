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
    public class LibraryRowItem : GuiWidget
    {
        public PrintItemWrapper printItemWrapper;
        public RGBA_Bytes WidgetTextColor;
        public RGBA_Bytes WidgetBackgroundColor;

        public bool isActivePrint = false;
        public bool isSelectedItem = false;
        
        private bool isHoverItem = false;
        TextWidget partLabel;
        public CheckBox selectionCheckBox;
        LinkButtonFactory linkButtonFactory = new LinkButtonFactory();
        bool viewWindowIsOpen = false;
        PartPreviewMainWindow viewingWindow;
        LibraryDataView libraryDataView;
        SlideWidget rightButtonOverlay;
        GuiWidget selectionCheckBoxContainer;
        bool editMode = false;
        public bool EditMode
        {
            get { return editMode; }
            set
            {
                if (this.editMode != value)
                {
                    this.editMode = value;
                }
            }
        }

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

        public LibraryRowItem(PrintItemWrapper printItem, LibraryDataView libraryDataView)
        {
            this.libraryDataView = libraryDataView;
            this.printItemWrapper = printItem;
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

                PartThumbnailWidget thumbnailWidget = new PartThumbnailWidget(printItem, "part_icon_transparent_40x40.png", "building_thumbnail_40x40.png", PartThumbnailWidget.ImageSizes.Size50x50);

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
                    string labelName = textInfo.ToTitleCase(printItem.Name);
                    labelName = labelName.Replace('_', ' ');
                    partLabel = new TextWidget(labelName, pointSize: 14);
                    partLabel.TextColor = WidgetTextColor;
                    partLabel.MinimumSize = new Vector2(1, 18);
                    partLabel.VAnchor = VAnchor.ParentCenter;
                    middleColumn.AddChild(partLabel);
                }
                primaryFlow.AddChild(selectionCheckBoxContainer);
                primaryFlow.AddChild(thumbnailWidget);
                primaryFlow.AddChild(middleColumn);

                primaryContainer.AddChild(primaryFlow);

                // The ConditionalClickWidget supplies a user driven Enabled property based on a delegate of your choosing
                primaryClickContainer = new ConditionalClickWidget(() => libraryDataView.EditMode);
                primaryClickContainer.HAnchor = HAnchor.ParentLeftRight;
                primaryClickContainer.VAnchor = VAnchor.ParentBottomTop;

                primaryContainer.AddChild(primaryClickContainer);

                rightButtonOverlay = getItemActionButtons();
                rightButtonOverlay.Visible = false;

                mainContainer.AddChild(primaryContainer);
                mainContainer.AddChild(rightButtonOverlay);

            }
            this.AddChild(mainContainer);
            AddHandlers();
        }

        ConditionalClickWidget primaryClickContainer;

        SlideWidget getItemActionButtons()
        {
            SlideWidget buttonContainer = new SlideWidget();
            buttonContainer.VAnchor = VAnchor.ParentBottomTop;

            FlowLayoutWidget buttonFlowContainer = new FlowLayoutWidget(FlowDirection.LeftToRight);
            buttonFlowContainer.VAnchor = VAnchor.ParentBottomTop;
            
            ClickWidget printButton = new ClickWidget();
            printButton.VAnchor = VAnchor.ParentBottomTop;
            printButton.BackgroundColor = ActiveTheme.Instance.PrimaryAccentColor;
            printButton.Width = 100;

            TextWidget printLabel = new TextWidget("Print".Localize());
            printLabel.TextColor = RGBA_Bytes.White;
            printLabel.VAnchor = VAnchor.ParentCenter;
            printLabel.HAnchor = HAnchor.ParentCenter;

            printButton.AddChild(printLabel);
            printButton.Click += (sender, e) =>
            {
                QueueData.Instance.AddItem(this.printItemWrapper,0);
                QueueData.Instance.SelectedIndex = 0;
                this.Invalidate();

            };;

            ClickWidget editButton = new ClickWidget();
            editButton.VAnchor = VAnchor.ParentBottomTop;
            editButton.BackgroundColor = ActiveTheme.Instance.SecondaryAccentColor;
            editButton.Width = 100;

            TextWidget editLabel = new TextWidget("View".Localize());
            editLabel.TextColor = RGBA_Bytes.White;
            editLabel.VAnchor = VAnchor.ParentCenter;
            editLabel.HAnchor = HAnchor.ParentCenter;

            editButton.AddChild(editLabel);
            editButton.Click += onViewPartClick;

            buttonFlowContainer.AddChild(editButton);
            buttonFlowContainer.AddChild(printButton);

            buttonContainer.AddChild(buttonFlowContainer);
            buttonContainer.Width = 200;

            return buttonContainer;
        }

        void SetDisplayAttributes()
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
        
        event EventHandler unregisterEvents;
        void AddHandlers()
        {
            //ActiveTheme.Instance.ThemeChanged.RegisterEvent(onThemeChanged, ref unregisterEvents);
            primaryClickContainer.Click += onLibraryItemClick;
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
            //selectionCheckBox.CheckedStateChanged += selectionCheckBox_CheckedStateChanged;
        }

        public override void OnClosed(EventArgs e)
        {
            if (unregisterEvents != null)
            {
                unregisterEvents(this, null);
            }
            base.OnClosed(e);
        }

        private void onLibraryItemClick(object sender, EventArgs e)
        {
            if (this.libraryDataView.EditMode == false)
            {
                //UiThread.RunOnIdle((state) =>
                //{
                //    openPartView(state);
                //});                
            }
            else
            {
                if (this.isSelectedItem == false)
                {
                    this.isSelectedItem = true;
                    this.selectionCheckBox.Checked = true;
                    libraryDataView.SelectedItems.Add(this);
                }
                else
                {
                    this.isSelectedItem = false;
                    this.selectionCheckBox.Checked = false;
                    libraryDataView.SelectedItems.Remove(this);
                }
            }
        }

        private void selectionCheckBox_CheckedStateChanged(object sender, EventArgs e)
        {
            if (selectionCheckBox.Checked == true)
            {
                this.isSelectedItem = true;
                libraryDataView.SelectedItems.Add(this);
            }
            else
            {
                this.isSelectedItem = false;
                libraryDataView.SelectedItems.Remove(this);
            }
        }

        private void onAddLinkClick(object sender, EventArgs e)
        {
        }

        void RemoveThisFromPrintLibrary(object state)
        {
            LibraryData.Instance.RemoveItem(this.printItemWrapper);
        }

        private void onRemoveLinkClick(object sender, EventArgs e)
        {
            UiThread.RunOnIdle(RemoveThisFromPrintLibrary);
        }

        private void onOpenPartViewClick(object sender, EventArgs e)
        {
            UiThread.RunOnIdle((state) =>
            {
                openPartView(state);
            });
        }

        private void onViewPartClick(object sender, EventArgs e)
        {
            UiThread.RunOnIdle((state) =>
            {
                openPartView(state, false);
            });
        }


        public void OpenPartViewWindow(bool openInEditMode = false)
        {
            if (viewWindowIsOpen == false)
            {
                viewingWindow = new PartPreviewMainWindow(this.printItemWrapper, View3DWidget.AutoRotate.Enabled, openInEditMode);
                this.viewWindowIsOpen = true;
                viewingWindow.Closed += new EventHandler(PartPreviewMainWindow_Closed);
            }
            else
            {
                if (viewingWindow != null)
                {
                    viewingWindow.BringToFront();
                }
            }

        }

        void PartPreviewMainWindow_Closed(object sender, EventArgs e)
        {
            viewWindowIsOpen = false;
        }


        private void openPartView(object state, bool openInEditMode = false)
        {
            string pathAndFile = this.printItemWrapper.FileLocation;
            if (File.Exists(pathAndFile))
            {
                OpenPartViewWindow(openInEditMode);
            }
            else
            {
                string message = String.Format("Cannot find\n'{0}'.\nWould you like to remove it from the library?", pathAndFile);
                StyledMessageBox.ShowMessageBox(null, message, "Item not found", StyledMessageBox.MessageType.YES_NO);
            }
        }

        void onConfirmRemove(bool messageBoxResponse)
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

        public override void OnDraw(Graphics2D graphics2D)
        {

            if (this.libraryDataView.EditMode)
            {
                selectionCheckBoxContainer.Visible = true;
                rightButtonOverlay.Visible = false;
            }
            else
            {
                selectionCheckBoxContainer.Visible = false;
            }

            base.OnDraw(graphics2D);

            if (this.isSelectedItem)
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
    }
}