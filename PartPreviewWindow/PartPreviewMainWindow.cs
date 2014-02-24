/*
Copyright (c) 2014, Lars Brubaker
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met: 

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer. 
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution. 

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies, 
either expressed or implied, of the FreeBSD Project.
*/

using System;
using System.Diagnostics;
using System.IO;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.Font;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;
using MatterHackers.Localizations;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
    public class PartPreviewMainWindow : SystemWindow
    {
        View3DTransformPart part3DView;
        GcodeViewBasic partGcodeView;
        //PartPreview3DGcode part3DGcodeView;

        public PartPreviewMainWindow(PrintItemWrapper printItem)
            : base(690, 340)
        {
			string partPreviewTitle = new LocalizedString ("MatterControl").Translated;
			Title = string.Format("{0}: ", partPreviewTitle) + Path.GetFileName(printItem.Name);

            BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

            TabControl tabControl = new TabControl();
            tabControl.TabBar.BorderColor = new RGBA_Bytes(0, 0, 0, 0);
            tabControl.TabBar.BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor;

            double buildHeight = ActiveSliceSettings.Instance.BuildHeight;

			string part3DViewLblBeg = ("3D");
			string part3DViewLblEnd = new LocalizedString ("View").Translated;
			string part3DViewLblFull = string.Format("{0} {1} ", part3DViewLblBeg, part3DViewLblEnd);
            part3DView = new View3DTransformPart(printItem, new Vector3(ActiveSliceSettings.Instance.BedSize, buildHeight), ActiveSliceSettings.Instance.BedShape);
			TabPage partPreview3DView = new TabPage(part3DView, part3DViewLblFull);

            partGcodeView = new GcodeViewBasic(printItem, ActiveSliceSettings.Instance.GetBedSize, ActiveSliceSettings.Instance.GetBedCenter);
			TabPage layerView = new TabPage(partGcodeView, new LocalizedString("Layer View").Translated);

            //part3DGcodeView = new PartPreview3DGcode(printItem.FileLocation, bedXSize, bedYSize);

            tabControl.AddTab(new SimpleTextTabWidget(partPreview3DView , 16,
                        ActiveTheme.Instance.TabLabelSelected, new RGBA_Bytes(), ActiveTheme.Instance.TabLabelUnselected, new RGBA_Bytes()));

            tabControl.AddTab(new SimpleTextTabWidget(layerView, 16,
                        ActiveTheme.Instance.TabLabelSelected, new RGBA_Bytes(), ActiveTheme.Instance.TabLabelUnselected, new RGBA_Bytes()));       

            this.AddChild(tabControl);
            this.AnchorAll();

            AddHandlers();

            Width = 640;
            Height = 480;

            ShowAsSystemWindow();
            MinimumSize = new Vector2(400, 300);

            // We do this after showing the system window so that when we try and take fucus the parent window (the system window)
            // exists and can give the fucus to its child the gecode window.
            if (Path.GetExtension(printItem.FileLocation).ToUpper() == ".GCODE")
            {
                tabControl.TabBar.SwitchToPage(layerView);
                partGcodeView.Focus();
            }
        }

        PerformanceFeedbackWindow timingWindow = null;
        Stopwatch totalDrawTime = new Stopwatch();
        static NamedExecutionTimer partPreviewDraw = new NamedExecutionTimer("PartPreview Draw");
        public override void OnDraw(Graphics2D graphics2D)
        {
            ExecutionTimer.Instance.Reset(); 
            
            totalDrawTime.Restart();
            partPreviewDraw.Start();
            base.OnDraw(graphics2D);
            partPreviewDraw.Stop();
            totalDrawTime.Stop();
#if true //DEBUG
#if false
            if (timingWindow == null)
            {
                string staticDataPath = ApplicationDataStorage.Instance.ApplicationStaticDataPath;
                string fontPath = Path.Combine(staticDataPath, "Fonts", "LiberationMono.svg");
                TypeFace boldTypeFace = TypeFace.LoadSVG(fontPath);
                timingWindow = new PerformanceFeedbackWindow(new StyledTypeFace(boldTypeFace, 12));
            //}
            //{
                timingWindow.ShowResults(totalDrawTime.Elapsed.TotalSeconds);
            }
#endif
#endif
        }

        event EventHandler unregisterEvents;
        private void AddHandlers()
        {
            ActiveTheme.Instance.ThemeChanged.RegisterEvent(Instance_ThemeChanged, ref unregisterEvents);
            part3DView.Closed += (sender, e) => { Close(); };
            partGcodeView.Closed += (sender, e) => { Close(); };
        }

        public override void OnClosed(EventArgs e)
        {
            if (unregisterEvents != null)
            {
                unregisterEvents(this, null);
            }
            base.OnClosed(e);
        }

        void Instance_ThemeChanged(object sender, EventArgs e)
        {
            Invalidate();
        }
    }
}
