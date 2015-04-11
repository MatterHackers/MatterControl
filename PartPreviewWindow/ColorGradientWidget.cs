using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.MeshVisualizer;
using MatterHackers.VectorMath;
using System.IO;
using MatterHackers.Agg.PlatformAbstract;

namespace MatterHackers.MatterControl.PartPreviewWindow
{

    public class ColorGradientWidget : FlowLayoutWidget 
    {
        
        List<ColorToSpeedWidget> allColorToSpeedWidgets;
        
        public ColorGradientWidget()
            : base(FlowDirection.TopToBottom)
        {

            VAnchor = VAnchor.FitToChildren;

            BackgroundColor = new RGBA_Bytes(0, 0, 0, 120);

            ColorToSpeedWidget blue = new ColorToSpeedWidget("blue.png", "BLUE");
            ColorToSpeedWidget red = new ColorToSpeedWidget("red.png", "RED");
            ColorToSpeedWidget teal = new ColorToSpeedWidget("teal.png", "TEAL");
            ColorToSpeedWidget yellow = new ColorToSpeedWidget("yellow.png", "YELLOW");
            ColorToSpeedWidget green = new ColorToSpeedWidget("green.png", "GREEN");
            ColorToSpeedWidget orange = new ColorToSpeedWidget("orange.png", "ORANGE");
            ColorToSpeedWidget lightBlue = new ColorToSpeedWidget("lightblue.png", "LIGHT-BLUE");
            
            this.AddChild(blue);
            this.AddChild(red);
            this.AddChild(teal);
            this.AddChild(yellow);
            this.AddChild(green);
            this.AddChild(orange);
            this.AddChild(lightBlue);

            Margin = new BorderDouble(top:75, left: 5, right: 530);

        }

    }
    
    public class ColorToSpeedWidget : FlowLayoutWidget
    {
        
        public ColorToSpeedWidget(String imageFileName, String layerSpeed)
            : base(FlowDirection.LeftToRight)
        {
            Margin = new BorderDouble(2);
           
            Agg.Image.ImageBuffer color = StaticData.Instance.LoadIcon(Path.Combine("ColorGradient", imageFileName));
            
            ImageWidget colorWidget = new ImageWidget(color);
            colorWidget.Margin = new BorderDouble(left:2);

            TextWidget speedTextBox = new TextWidget(layerSpeed, pointSize: 12);
            speedTextBox.TextColor = RGBA_Bytes.White;
            speedTextBox.VAnchor = VAnchor.ParentCenter;
            speedTextBox.Margin = new BorderDouble(left: 2);
            
            this.AddChild(colorWidget);
            this.AddChild(speedTextBox);

        }

    }
        
}