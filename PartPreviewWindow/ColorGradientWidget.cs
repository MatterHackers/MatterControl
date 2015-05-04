using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.GCodeVisualizer;
using MatterHackers.Agg.UI;
using MatterHackers.MeshVisualizer;
using MatterHackers.VectorMath;
using System.IO;
using MatterHackers.Agg.PlatformAbstract;

namespace MatterHackers.MatterControl.PartPreviewWindow
{

    public class ColorGradientWidget : FlowLayoutWidget 
    {

        public ColorGradientWidget(GCodeFile gcodeFileTest)
            : base(FlowDirection.TopToBottom)
        {

            BackgroundColor = new RGBA_Bytes(0, 0, 0, 120);

            HashSet<float> speeds = new HashSet<float>();
            PrinterMachineInstruction previousInstruction = gcodeFileTest.Instruction(0);
            for (int i = 1; i < gcodeFileTest.LineCount; i++)
            {
                PrinterMachineInstruction instruction = gcodeFileTest.Instruction(i);
                if(instruction.EPosition > previousInstruction.EPosition)
                {
                    speeds.Add((float)instruction.FeedRate);
                }
                previousInstruction = instruction;
            }

            ExtrusionColors extrusionColors = new ExtrusionColors();

            speeds.Select(speed => extrusionColors.GetColorForSpeed(speed)).ToArray();

            float[] orderedSpeeds = speeds.OrderByDescending(speed => speed).ToArray();

            float slow = orderedSpeeds.Last();
            float fast = orderedSpeeds.First();
            float range = fast - slow;
            int count = 6;
            float increment = (fast - slow) / count;
            int index = 0;

            int[] rangeValues = Enumerable.Range(0, 7).Select(x => (int)(slow + increment * index++)).ToArray();
            RGBA_Bytes[] speedColors = rangeValues.OrderBy(s => s).Select(speed => extrusionColors.GetColorForSpeed(speed)).ToArray();

            for (int i = 0; i < speedColors.Length; i++) 
            {
                var color = speedColors[i];
                var speed = rangeValues[i];

                GuiWidget colorWidget = new GuiWidget();
                colorWidget.Width = 20;
                colorWidget.Height = 20;
                colorWidget.BackgroundColor = color;
                colorWidget.Margin = new BorderDouble(2);

                ColorToSpeedWidget test = new ColorToSpeedWidget(colorWidget, speed.ToString());
                this.AddChild(test);
                
            }

            Margin = new BorderDouble(5, 5, 200, 50);
            HAnchor |= Agg.UI.HAnchor.ParentLeft;
            VAnchor = Agg.UI.VAnchor.ParentTop;

    }
    
    public class ColorToSpeedWidget : FlowLayoutWidget
    {
        public  GuiWidget speedColor;
        public string layerSpeed;

        public ColorToSpeedWidget(GuiWidget colorWidget, String speed)
            : base(FlowDirection.LeftToRight)
        {
            Margin = new BorderDouble(2);

            speedColor = colorWidget;
            layerSpeed = speed + " mm\\s";

            colorWidget.Margin = new BorderDouble(left: 2);

            TextWidget speedTextBox = new TextWidget(layerSpeed, pointSize: 12);
            speedTextBox.TextColor = RGBA_Bytes.White;
            speedTextBox.VAnchor = VAnchor.ParentCenter;
            speedTextBox.Margin = new BorderDouble(left: 2);

            this.AddChild(colorWidget);
            this.AddChild(speedTextBox);
        }
        }

    }
        
}