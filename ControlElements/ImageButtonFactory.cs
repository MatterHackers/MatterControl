using System.IO;
using MatterHackers.Agg;
using MatterHackers.Agg.ImageProcessing;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.DataStorage;

namespace MatterHackers.MatterControl
{
    public class ImageButtonFactory
    {
        public bool invertImageColor = true;
        
        public Button Generate(string normalImageName, string hoverImageName, string pressedImageName = null, string disabledImageName = null)
        {

            if (pressedImageName == null)
            {
                pressedImageName = hoverImageName;
            }

            if (disabledImageName == null)
            {
                disabledImageName = normalImageName;
            }

            Agg.Image.ImageBuffer normalImage = StaticData.Instance.LoadIcon(normalImageName);
            Agg.Image.ImageBuffer pressedImage = StaticData.Instance.LoadIconpressedImageName);
            Agg.Image.ImageBuffer hoverImage = StaticData.Instance.LoadIcon(hoverImageName);
            Agg.Image.ImageBuffer disabledImage = StaticData.Instance.LoadIcon(disabledImageName);

            if (!ActiveTheme.Instance.IsDarkTheme && invertImageColor)
            {
                InvertLightness.DoInvertLightness(normalImage);
                InvertLightness.DoInvertLightness(pressedImage);
                InvertLightness.DoInvertLightness(hoverImage);
                InvertLightness.DoInvertLightness(disabledImage);
            }

            ButtonViewStates buttonViewWidget = new ButtonViewStates(
                new ImageWidget(normalImage),
                new ImageWidget(hoverImage),
                new ImageWidget(pressedImage),
                new ImageWidget(disabledImage)
            );

            //Create button based on view container widget
            Button imageButton = new Button(0, 0, buttonViewWidget);
            imageButton.Margin = new BorderDouble(0);
            imageButton.Padding = new BorderDouble(0);
            return imageButton;
        }
    }

}
