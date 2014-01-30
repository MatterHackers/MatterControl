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

using MatterHackers.PolygonMesh;

namespace MatterHackers.MatterControl.ToolsPage
{
    public class ToolsListItem : ClickWidget
    {
        public PrintItemWrapper printItem;
        public RGBA_Bytes WidgetTextColor;
        public RGBA_Bytes WidgetBackgroundColor;

        public bool isActivePrint = false;
        public bool isSelectedItem = false;
        public bool isHoverItem = false;
        TextWidget partLabel;
        Button viewLink;
        Button removeLink;
        public CheckBox selectionCheckBox;
        FlowLayoutWidget buttonContainer;
        LinkButtonFactory linkButtonFactory = new LinkButtonFactory();

        public ToolsListItem(PrintItemWrapper printItem)
        {
            this.printItem = printItem;
            linkButtonFactory.fontSize = 10;
            linkButtonFactory.textColor = RGBA_Bytes.White;

            WidgetTextColor = RGBA_Bytes.Black;
            WidgetBackgroundColor = RGBA_Bytes.White;

            TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;

            SetDisplayAttributes();

            FlowLayoutWidget mainContainer = new FlowLayoutWidget(FlowDirection.LeftToRight);
            mainContainer.HAnchor |= Agg.UI.HAnchor.ParentLeftRight;
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
                middleColumn.VAnchor |= VAnchor.ParentTop;
                middleColumn.HAnchor |= HAnchor.ParentLeftRight;
                middleColumn.Padding = new BorderDouble(6);
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
                rightColumn.VAnchor |= VAnchor.ParentCenter;

                buttonContainer = new FlowLayoutWidget();
                buttonContainer.HAnchor = Agg.UI.HAnchor.Max_FitToChildren_ParentWidth;
                buttonContainer.VAnchor = Agg.UI.VAnchor.Max_FitToChildren_ParentHeight;
                {
                    viewLink = linkButtonFactory.Generate("View");
                    viewLink.Margin = new BorderDouble(left: 10, right:10);
                    viewLink.VAnchor = VAnchor.ParentCenter;

                    removeLink = linkButtonFactory.Generate("Remove");
                    removeLink.Margin = new BorderDouble(right: 10);
                    removeLink.VAnchor = VAnchor.ParentCenter;

                    buttonContainer.AddChild(viewLink);
                    buttonContainer.AddChild(removeLink);
                }
                rightColumn.AddChild(buttonContainer);

                mainContainer.AddChild(selectionCheckBoxContainer);
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
            viewLink.Click += new ButtonBase.ButtonEventHandler(onViewLinkClick);
            removeLink.Click += new ButtonBase.ButtonEventHandler(onRemoveLinkClick);
            selectionCheckBox.CheckedStateChanged += selectionCheckBox_CheckedStateChanged;
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
                ToolsListControl.Instance.SelectedItems.Add(this);
            }
        }

        private void selectionCheckBox_CheckedStateChanged(object sender, EventArgs e)
        {            
            if (selectionCheckBox.Checked == true)
            {
                this.isSelectedItem = true;
                ToolsListControl.Instance.SelectedItems.Add(this);
            }
            else
            {
                this.isSelectedItem = false;
                ToolsListControl.Instance.SelectedItems.Remove(this);
            }
        }

        private void onAddLinkClick(object sender, MouseEventArgs e)
        {
        }

        void RemoveThisFromPrintLibrary(object state)
        {
            ToolsListControl.Instance.RemoveChild(this);
            this.printItem.Delete();
        }

        private void onRemoveLinkClick(object sender, MouseEventArgs e)
        {
            UiThread.RunOnIdle(RemoveThisFromPrintLibrary);
        }

        private void onViewLinkClick(object sender, MouseEventArgs e)
        {
            UiThread.RunOnIdle(onViewLinkClick);
        }

        private void onViewLinkClick(object state)
        {
            string pathAndFile = this.printItem.FileLocation;
            Console.WriteLine(pathAndFile);
            if (File.Exists(pathAndFile))
            {
                new PartPreviewMainWindow(this.printItem);
            }
            else
            {
                string message = String.Format("Cannot find\n'{0}'.\nWould you like to remove it from the queue?", pathAndFile);
                if (StyledMessageBox.ShowMessageBox(message, "Item not found", StyledMessageBox.MessageType.YES_NO))
                {
                    ToolsListControl.Instance.RemoveChild(this);
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
                this.BackgroundColor = RGBA_Bytes.White;
                this.partLabel.TextColor = RGBA_Bytes.Black;
                this.selectionCheckBox.TextColor = RGBA_Bytes.Black;
            }

        }
    }
}
