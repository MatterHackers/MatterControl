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
        public ActionBarPlus()
            : base(FlowDirection.TopToBottom)
        {
            this.Create();
        }

        event EventHandler unregisterEvents;
        public void Create()
        {
            // These are used as descriptions to tell you what the code is doin,g but there is not code to see what is happening.
            // If you want to know what happens, you have to go read the functions.
            //SetDisplayAttributes();
            //AddChildElements();
            //AddHandlers();
            // I think it is a better design to just write the comments and have the code here.
            // This way we have the six lines of functional code right here.  We can read it and see what this constructor actually does.

            // Set Display Attributes
            this.HAnchor = HAnchor.ParentLeftRight;
            this.BackgroundColor = ActiveTheme.Instance.PrimaryAccentColor;

            // Add Child Elements
            this.AddChild(new ActionBar.PrinterActionRow());
            this.AddChild(new PrintStatusRow());
            this.AddChild(new MessageActionRow());

            // Add Handlers
            ActiveTheme.Instance.ThemeChanged.RegisterEvent(onThemeChanged, ref unregisterEvents);
        }

        public override void OnClosed(EventArgs e)
        {
            if (unregisterEvents != null)
            {
                unregisterEvents(this, null);
            }
            base.OnClosed(e);
        }

        private void onThemeChanged(object sender, EventArgs e)
        {
            this.BackgroundColor = ActiveTheme.Instance.PrimaryAccentColor;
            this.Invalidate();
        }
    }

    class MessageActionRow : ActionRowBase
    {
        protected override void AddChildElements()
        {
            this.AddChild(HelpTextWidget.Instance);
        }

        protected override void Initialize()
        {
            this.Margin = new BorderDouble(6, 6);
        }
    }
}