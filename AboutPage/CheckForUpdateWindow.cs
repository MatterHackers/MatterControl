using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.ContactForm;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.HtmlParsing;
using MatterHackers.MatterControl.PrintLibrary;
using MatterHackers.MatterControl.PrintQueue;
using System.IO;
using System.Net;


namespace MatterHackers.MatterControl.AboutPage
{

    public class CheckForUpdateWindow : SystemWindow
    {

        private static CheckForUpdateWindow checkUpdate = null;

        public CheckForUpdateWindow()
            : base (500,500)
        {

            FlowLayoutWidget topToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
			topToBottom.AnchorAll();

            FlowLayoutWidget mainLabelContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);
            //mainLabelContainer.HAnchor |= Agg.UI.HAnchor.ParentLeft;
            //mainLabelContainer.VAnchor = Agg.UI.VAnchor.ParentTop;
            //mainLabelContainer.DebugShowBounds = true;



            TextWidget checkUpdateLabel = new TextWidget("Update Status".Localize(), pointSize: 20);
            checkUpdateLabel.TextColor = RGBA_Bytes.White;
            checkUpdateLabel.Margin = new BorderDouble(5, 5, 0, 5);

            UpdateControlView test = new UpdateControlView();


            topToBottom.AddChild(mainLabelContainer);
            topToBottom.AddChild(test);
            mainLabelContainer.AddChild(checkUpdateLabel);

            this.AddChild(topToBottom);
            this.Title = "Check For Update".Localize();
            this.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;
            this.ShowAsSystemWindow();


        }

        public static void Show()
        {
            if (checkUpdate == null)
            {
                checkUpdate = new CheckForUpdateWindow();
                checkUpdate.Closed += (parentSender, e) =>
                {
                    checkUpdate = null;
                };
            }
            else
            {
                checkUpdate.BringToFront();
            }
        }

    }
}
