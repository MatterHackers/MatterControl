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

            Agg.Image.ImageBuffer normalImage = new Agg.Image.ImageBuffer();
            Agg.Image.ImageBuffer pressedImage = new Agg.Image.ImageBuffer();
            Agg.Image.ImageBuffer hoverImage = new Agg.Image.ImageBuffer();
            Agg.Image.ImageBuffer disabledImage = new Agg.Image.ImageBuffer();

            ImageIO.LoadImageData(this.GetImageLocation(normalImageName), normalImage);
            ImageIO.LoadImageData(this.GetImageLocation(pressedImageName), pressedImage);
            ImageIO.LoadImageData(this.GetImageLocation(hoverImageName), hoverImage);
            ImageIO.LoadImageData(this.GetImageLocation(disabledImageName), disabledImage);

            if (!ActiveTheme.Instance.IsDarkTheme && invertImageColor)
            {
                InvertLightness.DoInvertLightness(normalImage);
                InvertLightness.DoInvertLightness(pressedImage);
                InvertLightness.DoInvertLightness(hoverImage);
                InvertLightness.DoInvertLightness(disabledImage);
            }

            //normalImage.NewGraphics2D().Line(0, 0, normalImage.Width, normalImage.Height, RGBA_Bytes.Violet);
            //pressedImage.NewGraphics2D().Line(0, 0, normalImage.Width, normalImage.Height, RGBA_Bytes.Violet);

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

        private string GetImageLocation(string imageName)
        {
            return Path.Combine(ApplicationDataStorage.Instance.ApplicationStaticDataPath, "Icons", imageName);
        }
    }

}
