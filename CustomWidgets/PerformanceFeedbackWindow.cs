using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using MatterHackers.Agg;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.ImageProcessing;
using MatterHackers.Agg.OpenGlGui;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.MarchingSquares;
using MatterHackers.MatterControl;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.PrintLibrary;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MeshVisualizer;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Csg;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.RayTracer;
using MatterHackers.RayTracer.Traceable;
using MatterHackers.RenderOpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
    public class PerformanceFeedbackWindow : SystemWindow
    {
        string timingString;
        StyledTypeFace boldFont;

        public PerformanceFeedbackWindow()
            : base(700, 480)
        {
            BackgroundColor = RGBA_Bytes.White;
            ShowAsSystemWindow();

            string staticDataPath = ApplicationDataStorage.Instance.ApplicationStaticDataPath;
            string fontPath = Path.Combine(staticDataPath, "Fonts", "LiberationMono.svg");
            TypeFace boldTypeFace = TypeFace.LoadSVG(fontPath);
            boldFont = new StyledTypeFace(boldTypeFace, 12);
        }

        public override void OnDraw(Graphics2D graphics2D)
        {
            TypeFacePrinter stringPrinter = new TypeFacePrinter(timingString, boldFont, new Vector2(0, Height - 16));
            stringPrinter.DrawFromHintedCache = true;

            stringPrinter.Render(graphics2D, RGBA_Bytes.Black);
            
            base.OnDraw(graphics2D);
        }

        void SetDisplay(string timingString)
        {
            this.timingString = timingString;
            Invalidate();
        }

        public void ShowResults(double totalTimeTracked)
        {
            string timingString = ExecutionTimer.Instance.GetResults(totalTimeTracked);
            SetDisplay(timingString);
        }
    }
}
