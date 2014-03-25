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

namespace MatterHackers.MatterControl.PrintHistory
{
   

    public class PrintHistoryListItem : FlowLayoutWidget
    {
        public PrintItemWrapper printItem;
        public RGBA_Bytes WidgetTextColor;
        public RGBA_Bytes WidgetBackgroundColor;

        public bool isActivePrint = false;
        public bool isSelectedItem = false;
        public bool isHoverItem = false;
        TextWidget partLabel;
        Button viewLink;
        public CheckBox selectionCheckBox;
        FlowLayoutWidget buttonContainer;
        LinkButtonFactory linkButtonFactory = new LinkButtonFactory();
        bool viewWindowIsOpen = false;
        PartPreviewMainWindow viewingWindow;
        Random rand;

        public PrintHistoryListItem(PrintItemWrapper printItem)
        {
            this.printItem = printItem;
            InitRandomizer();
            SetDisplayAttributes();
            AddChildElements();
            this.AddChild(new TextWidget("foo"));
            AddHandlers();
        }

        DateTime start;
        int range;
        void InitRandomizer()
        {
            rand = new Random();
            start = new DateTime(1995, 1, 1);
            range  = (DateTime.Today - start).Days;
        }

        DateTime RandomDay()
        {   
            return start.AddDays(rand.Next(range));
        }

        void AddChildElements()
        {
            TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;
            
            {
                GuiWidget indicator = new GuiWidget();
                indicator.VAnchor = Agg.UI.VAnchor.ParentBottomTop;
                indicator.Width = 15;
                if (rand.NextDouble() > 0.5)
                {
                    indicator.BackgroundColor = new RGBA_Bytes(38, 147, 51, 200);
                }
                else
                {
                    indicator.BackgroundColor = new RGBA_Bytes(233, 53, 0, 200);
                }

                FlowLayoutWidget leftColumn = new FlowLayoutWidget(FlowDirection.TopToBottom);
                leftColumn.VAnchor |= VAnchor.ParentTop;
                leftColumn.AddChild(new TextWidget(RandomDay().ToShortDateString()));

                FlowLayoutWidget middleColumn = new FlowLayoutWidget(FlowDirection.TopToBottom);
                middleColumn.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
                middleColumn.VAnchor = Agg.UI.VAnchor.ParentBottomTop;
                middleColumn.Padding = new BorderDouble(0, 6);
                middleColumn.Margin = new BorderDouble(10, 0);
                {
                    string labelName = textInfo.ToTitleCase(printItem.Name);
                    labelName = labelName.Replace('_', ' ');
                    partLabel = new TextWidget(labelName, pointSize: 12);
                    partLabel.TextColor = WidgetTextColor;
                    partLabel.MinimumSize = new Vector2(1, 16);
                    middleColumn.AddChild(partLabel);
                }

                buttonContainer = new FlowLayoutWidget();
                buttonContainer.Margin = new BorderDouble(0, 6);
                buttonContainer.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
                {
                    viewLink = linkButtonFactory.Generate(LocalizedString.Get("Reprint"));
                    viewLink.Margin = new BorderDouble(left: 0, right: 10);
                    viewLink.VAnchor = VAnchor.ParentCenter;

                    buttonContainer.AddChild(viewLink);
                    middleColumn.AddChild(buttonContainer);
                }

                this.AddChild(indicator);
                this.AddChild(leftColumn);
                this.AddChild(middleColumn);
                //mainContainer.AddChild(rightColumn);
            }
        }

        void SetDisplayAttributes()
        {
            this.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
            this.Height = 28;
            this.BackgroundColor = this.WidgetBackgroundColor;
            this.Padding = new BorderDouble(0);
            this.Margin = new BorderDouble(6, 0, 6, 6);
        }

        event EventHandler unregisterEvents;
        void AddHandlers()
        {
            ActiveTheme.Instance.ThemeChanged.RegisterEvent(onThemeChanged, ref unregisterEvents);
        }

        private void onThemeChanged(object sender, EventArgs e)
        {
            //Set background and text color to new theme
            this.Invalidate();
        }


        public override void OnClosed(EventArgs e)
        {
            if (unregisterEvents != null)
            {
                unregisterEvents(this, null);
            }
            base.OnClosed(e);
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
                //
            }
            else
            {
                string message = String.Format("Cannot find\n'{0}'.\nWould you like to remove it from the queue?", pathAndFile);
                if (StyledMessageBox.ShowMessageBox(message, "Item not found", StyledMessageBox.MessageType.YES_NO))
                {
                    PrintHistoryListControl.Instance.RemoveChild(this);
                }
            }
        }


        public override void OnDraw(Graphics2D graphics2D)
        {

            if (this.isHoverItem)
            {
                //buttonContainer.Visible = true;
            }
            else
            {
                //buttonContainer.Visible = false;
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
            }

        }
    }
}
