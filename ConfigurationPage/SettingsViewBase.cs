using System;
using System.Collections.Generic;
using System.IO;
using MatterHackers.Agg;
using MatterHackers.Agg.ImageProcessing;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ConfigurationPage.PrintLeveling;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.EeProm;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.ConfigurationPage
{
    public class SettingsViewBase : AltGroupBox
    {
		protected readonly int TallButtonHeight = (int)(25 * TextWidget.GlobalPointSizeScaleRatio + .5);
        protected TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();
        protected LinkButtonFactory linkButtonFactory = new LinkButtonFactory();
        protected RGBA_Bytes separatorLineColor;
        protected FlowLayoutWidget mainContainer;

        public SettingsViewBase(string title)
            : base(new TextWidget(title, pointSize: 18, textColor: ActiveTheme.Instance.SecondaryAccentColor))
        {
            SetDisplayAttributes();
            mainContainer = new FlowLayoutWidget(Agg.UI.FlowDirection.TopToBottom);
            mainContainer.HAnchor = HAnchor.ParentLeftRight;
            mainContainer.Margin = new BorderDouble(left: 6);
        }

        private void SetDisplayAttributes()
        {
            //this.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
            this.separatorLineColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 100);
            this.Margin = new BorderDouble(2, 4, 2, 0);
            this.textImageButtonFactory.normalFillColor = RGBA_Bytes.Transparent;
            this.textImageButtonFactory.disabledFillColor = RGBA_Bytes.White;

            this.textImageButtonFactory.FixedHeight = TallButtonHeight;
            this.textImageButtonFactory.fontSize = 11;
            this.textImageButtonFactory.borderWidth = 1;
            this.textImageButtonFactory.normalBorderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 200);
            this.textImageButtonFactory.hoverBorderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 200);

            this.textImageButtonFactory.disabledTextColor = RGBA_Bytes.DarkGray;
            this.textImageButtonFactory.hoverTextColor = ActiveTheme.Instance.PrimaryTextColor;
            this.textImageButtonFactory.normalTextColor = ActiveTheme.Instance.SecondaryTextColor;
            this.textImageButtonFactory.pressedTextColor = ActiveTheme.Instance.PrimaryTextColor;

            this.linkButtonFactory.fontSize = 11;
        }       
    }
}