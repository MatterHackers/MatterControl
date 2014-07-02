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

            string translateIconPath = Path.Combine("ViewTransformControls", "translate.png");
            translateButton = iconTextImageButtonFactory.GenerateRadioButton("", translateIconPath);
            translateButton.Margin = new BorderDouble(3);
            AddChild(translateButton);

            string scaleIconPath = Path.Combine("ViewTransformControls", "scale.png");
            scaleButton = iconTextImageButtonFactory.GenerateRadioButton("", scaleIconPath);
            scaleButton.Margin = new BorderDouble(3);
            AddChild(scaleButton);

            Margin = new BorderDouble(5);
            HAnchor |= Agg.UI.HAnchor.ParentLeft;
            VAnchor = Agg.UI.VAnchor.ParentTop;
            translateButton.Checked = true;
        }
    }

	public class ViewControlsToggle : FlowLayoutWidget
	{
		public RadioButton twoDimensionButton;
		public RadioButton threeDimensionButton;

		public ViewControlsToggle()
		{
			TextImageButtonFactory iconTextImageButtonFactory = new TextImageButtonFactory();
			iconTextImageButtonFactory.AllowThemeToAdjustImage = false;

			BackgroundColor = new RGBA_Bytes(0, 0, 0, 120);
			iconTextImageButtonFactory.FixedHeight = 20;
			iconTextImageButtonFactory.FixedWidth = 20;

			string translateIconPath = Path.Combine("ViewTransformControls", "2d.png");
			twoDimensionButton = iconTextImageButtonFactory.GenerateRadioButton("", translateIconPath);
			twoDimensionButton.Margin = new BorderDouble(3);
			AddChild(twoDimensionButton);

			string scaleIconPath = Path.Combine("ViewTransformControls", "3d.png");
			threeDimensionButton = iconTextImageButtonFactory.GenerateRadioButton("", scaleIconPath);
			threeDimensionButton.Margin = new BorderDouble(3);
			AddChild(threeDimensionButton);

			Margin = new BorderDouble(5,5,195,5);
			HAnchor |= Agg.UI.HAnchor.ParentRight;
			VAnchor = Agg.UI.VAnchor.ParentTop;
			string defaultView = UserSettings.Instance.get ("LayerViewDefault");

			if (defaultView == null) 
			{
				UserSettings.Instance.set ("LayerViewDefault", "2D Layer");
			}

			if (defaultView == "2D Layer") {
				twoDimensionButton.Checked = true;
			} else if (defaultView == "3D Layer") {
				threeDimensionButton.Checked = true;
			} else {
				twoDimensionButton.Checked = true;
			}

		}
	}
}
