using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
    public class ViewControls2D : FlowLayoutWidget
    {
        public RadioButton translateButton;
        public RadioButton scaleButton;

        public ViewControls2D()
        {
            TextImageButtonFactory iconTextImageButtonFactory = new TextImageButtonFactory();
            iconTextImageButtonFactory.AllowThemeToAdjustImage = false;

            BackgroundColor = new RGBA_Bytes(0, 0, 0, 120);
            iconTextImageButtonFactory.FixedHeight = 20;
            iconTextImageButtonFactory.FixedWidth = 20;

            string translateIconPath = Path.Combine("Icons", "ViewTransformControls", "translate.png");
            translateButton = iconTextImageButtonFactory.GenerateRadioButton("", translateIconPath);
            translateButton.Margin = new BorderDouble(3);
            AddChild(translateButton);

            string scaleIconPath = Path.Combine("Icons", "ViewTransformControls", "scale.png");
            scaleButton = iconTextImageButtonFactory.GenerateRadioButton("", scaleIconPath);
            scaleButton.Margin = new BorderDouble(3);
            AddChild(scaleButton);

            Margin = new BorderDouble(5);
            HAnchor |= Agg.UI.HAnchor.ParentLeft;
            VAnchor = Agg.UI.VAnchor.ParentTop;
            translateButton.Checked = true;
        }
    }
}
