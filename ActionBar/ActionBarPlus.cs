using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

using MatterHackers.Agg.Image;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;
using System.Globalization;

using MatterHackers.MatterControl;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.ActionBar;
using MatterHackers.MatterControl.PrinterControls.PrinterConnections;

namespace MatterHackers.MatterControl
{
    public class ActionBarPlus : FlowLayoutWidget
    {
        QueueDataView queueDataView;

        public ActionBarPlus(QueueDataView queueDataView)
            : base(FlowDirection.TopToBottom)
        {
            this.queueDataView = queueDataView;
            this.Create();
        }

        event EventHandler unregisterEvents;
        public void Create()
        {
            // Set Display Attributes
            this.HAnchor = HAnchor.ParentLeftRight;
            this.BackgroundColor = ActiveTheme.Instance.PrimaryAccentColor;

            // Add Child Elements
            this.AddChild(new ActionBar.PrinterActionRow());
            this.AddChild(new PrintStatusRow(queueDataView));
            this.Padding = new BorderDouble(bottom: 6);

            // Add Handlers
            ActiveTheme.Instance.ThemeChanged.RegisterEvent(ThemeChanged, ref unregisterEvents);
        }

        public void ThemeChanged(object sender, EventArgs e)
        {
            this.BackgroundColor = ActiveTheme.Instance.PrimaryAccentColor;
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
    }

    class MessageActionRow : ActionRowBase
    {
        protected override void AddChildElements()
        {
            if (HelpTextWidget.Instance.Parent != null)
            {
                HelpTextWidget.Instance.Parent.RemoveChild(HelpTextWidget.Instance);
            }

            this.AddChild(HelpTextWidget.Instance);
        }

        protected override void Initialize()
        {
            this.Margin = new BorderDouble(0,3,0,0);
        }
    }
}