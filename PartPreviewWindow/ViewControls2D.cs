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
    public class ViewControlsBase : FlowLayoutWidget
    {
        protected int buttonHeight;
        public ViewControlsBase()
        {
            if (ActiveTheme.Instance.DisplayMode == ActiveTheme.ApplicationDisplayType.Touchscreen)
            {
                buttonHeight = 40;
            }
            else
            {
                buttonHeight = 20;
            }
        }
    }

    public class ViewControls2D : ViewControlsBase
    {
        public RadioButton translateButton;
        public RadioButton scaleButton;
        
        public ViewControls2D()
        {
            if (ActiveTheme.Instance.DisplayMode == ActiveTheme.ApplicationDisplayType.Touchscreen)
            {
                buttonHeight = 40;
            }
            else
            {
                buttonHeight = 20;
            }
            
            TextImageButtonFactory iconTextImageButtonFactory = new TextImageButtonFactory();
            iconTextImageButtonFactory.AllowThemeToAdjustImage = false;
            iconTextImageButtonFactory.checkedBorderColor = RGBA_Bytes.White;

            BackgroundColor = new RGBA_Bytes(0, 0, 0, 120);
            iconTextImageButtonFactory.FixedHeight = buttonHeight;
            iconTextImageButtonFactory.FixedWidth = buttonHeight;

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

    public class ViewControlsToggle : ViewControlsBase
	{
		public RadioButton twoDimensionButton;
		public RadioButton threeDimensionButton;

		public ViewControlsToggle()
		{
			TextImageButtonFactory iconTextImageButtonFactory = new TextImageButtonFactory();
			iconTextImageButtonFactory.AllowThemeToAdjustImage = false;
            iconTextImageButtonFactory.checkedBorderColor = RGBA_Bytes.White;

			BackgroundColor = new RGBA_Bytes(0, 0, 0, 120);

            iconTextImageButtonFactory.FixedHeight = buttonHeight;
            iconTextImageButtonFactory.FixedWidth = buttonHeight;

			string translateIconPath = Path.Combine("ViewTransformControls", "2d.png");
			twoDimensionButton = iconTextImageButtonFactory.GenerateRadioButton("", translateIconPath);
			twoDimensionButton.Margin = new BorderDouble(3);
			AddChild(twoDimensionButton);

			string scaleIconPath = Path.Combine("ViewTransformControls", "3d.png");
			threeDimensionButton = iconTextImageButtonFactory.GenerateRadioButton("", scaleIconPath);
			threeDimensionButton.Margin = new BorderDouble(3);

			if (ActiveTheme.Instance.DisplayMode != ActiveTheme.ApplicationDisplayType.Touchscreen)
			{

				AddChild(threeDimensionButton);

				if (UserSettings.Instance.get("LayerViewDefault") == "3D Layer"
                    && UserSettings.Instance.Fields.AppExitedNormaly == true)
				{
					threeDimensionButton.Checked = true;
				}
				else
				{
					twoDimensionButton.Checked = true;
				}
			}
			else
			{
				twoDimensionButton.Checked = true;
			}
			Margin = new BorderDouble(5,5,200,5);
			HAnchor |= Agg.UI.HAnchor.ParentRight;
			VAnchor = Agg.UI.VAnchor.ParentTop;
		}
	}
}
