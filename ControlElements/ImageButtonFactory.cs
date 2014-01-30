using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.OpenGlGui;
using MatterHackers.PolygonMesh;
using MatterHackers.RenderOpenGl;
using MatterHackers.VectorMath;
using MatterHackers.Agg.VertexSource;
using MatterHackers.MatterControl.DataStorage;

namespace MatterHackers.MatterControl
{
    public class ImageButtonFactory
    {
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

            ImageBMPIO.LoadImageData(this.GetImageLocation(normalImageName), normalImage);
            ImageBMPIO.LoadImageData(this.GetImageLocation(pressedImageName), pressedImage);
            ImageBMPIO.LoadImageData(this.GetImageLocation(hoverImageName), hoverImage);
            ImageBMPIO.LoadImageData(this.GetImageLocation(disabledImageName), disabledImage);

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
            return Path.Combine(ApplicationDataStorage.Instance.ApplicationStaticDataPath, imageName);
        }
    }

}
