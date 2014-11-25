/*
Copyright (c) 2014, Kevin Pope
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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;

using MatterHackers.Localizations;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.DataStorage;

namespace MatterHackers.MatterControl.PrinterControls
{
    public class XYZColors
    {
        public static RGBA_Bytes xColor = new RGBA_Bytes(180, 180, 180);
        public static RGBA_Bytes yColor = new RGBA_Bytes(255, 255, 255);
        public static RGBA_Bytes zColor = new RGBA_Bytes(255, 255, 255);
        public static RGBA_Bytes eColor = new RGBA_Bytes(180, 180, 180);
        public XYZColors()
        {
        }
    }
    
    public class MovementControls : ControlWidgetBase
    {
        Button disableMotors;
        Button homeAllButton;
        Button homeXButton;
        Button homeYButton;
        Button homeZButton;

        EditManualMovementSpeedsWindow editManualMovementSettingsWindow;
        
        protected override void AddChildElements()
        {
            Button editButton;
            AltGroupBox movementControlsGroupBox = new AltGroupBox(textImageButtonFactory.GenerateGroupBoxLabelWithEdit(new TextWidget("Movement Controls".Localize(), pointSize: 18, textColor: ActiveTheme.Instance.SecondaryAccentColor), out editButton));
            editButton.Click += (sender, e) =>
            {
                if (editManualMovementSettingsWindow == null)
                {
                    editManualMovementSettingsWindow = new EditManualMovementSpeedsWindow("Movement Speeds".Localize(), GetMovementSpeedsString(), SetMovementSpeeds);
                    editManualMovementSettingsWindow.Closed += (popupWindowSender, popupWindowSenderE) => { editManualMovementSettingsWindow = null; };
                }
                else
                {
                    editManualMovementSettingsWindow.BringToFront();
                }
            };

            movementControlsGroupBox.Margin = new BorderDouble(0);
            movementControlsGroupBox.TextColor = ActiveTheme.Instance.PrimaryTextColor;
            movementControlsGroupBox.BorderColor = ActiveTheme.Instance.PrimaryTextColor;
            movementControlsGroupBox.HAnchor |= Agg.UI.HAnchor.ParentLeftRight;
            movementControlsGroupBox.VAnchor = Agg.UI.VAnchor.FitToChildren;

            {
                FlowLayoutWidget manualControlsLayout = new FlowLayoutWidget(FlowDirection.TopToBottom);
                manualControlsLayout.HAnchor = Agg.UI.HAnchor.ParentLeftRight;
                manualControlsLayout.VAnchor = Agg.UI.VAnchor.FitToChildren;
                manualControlsLayout.Padding = new BorderDouble(3, 5, 3, 0) * TextWidget.GlobalPointSizeScaleRatio;
                {
                    manualControlsLayout.AddChild(GetHomeButtonBar());
                    manualControlsLayout.AddChild(CreateSeparatorLine());
                    manualControlsLayout.AddChild(new JogControls(new XYZColors()));
                    manualControlsLayout.AddChild(CreateSeparatorLine());
                    //manualControlsLayout.AddChild(GetManualMoveBar());
                }

                movementControlsGroupBox.AddChild(manualControlsLayout);
            }            
            this.AddChild(movementControlsGroupBox);
        }

        private FlowLayoutWidget GetHomeButtonBar()
        {
            FlowLayoutWidget homeButtonBar = new FlowLayoutWidget();
            homeButtonBar.HAnchor = HAnchor.ParentLeftRight;
            homeButtonBar.Margin = new BorderDouble(3, 0, 3, 6) * TextWidget.GlobalPointSizeScaleRatio;
            homeButtonBar.Padding = new BorderDouble(0);

            textImageButtonFactory.borderWidth = 1;
            textImageButtonFactory.normalBorderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 200);
            textImageButtonFactory.hoverBorderColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryTextColor, 200);

            ImageBuffer helpIconImage = new ImageBuffer();
            StaticData.Instance.LoadImage(Path.Combine("Icons", "icon_home_white_24x24.png"), helpIconImage);
            ImageWidget homeIconImageWidget = new ImageWidget(helpIconImage);
            homeIconImageWidget.Margin = new BorderDouble(0, 0, 6, 0) * TextWidget.GlobalPointSizeScaleRatio;
            homeIconImageWidget.OriginRelativeParent += new Vector2(0, 2) * TextWidget.GlobalPointSizeScaleRatio;
            RGBA_Bytes oldColor = this.textImageButtonFactory.normalFillColor;
            textImageButtonFactory.normalFillColor = new RGBA_Bytes(180, 180, 180);
            homeAllButton = textImageButtonFactory.Generate(LocalizedString.Get("ALL"));
            this.textImageButtonFactory.normalFillColor = oldColor;
            homeAllButton.Margin = new BorderDouble(0, 0, 6, 0) * TextWidget.GlobalPointSizeScaleRatio;
            homeAllButton.Click += new EventHandler(homeAll_Click);

            textImageButtonFactory.FixedWidth = (int)homeAllButton.Width;
            homeXButton = textImageButtonFactory.Generate("X", centerText: true);
            homeXButton.Margin = new BorderDouble(0, 0, 6, 0) * TextWidget.GlobalPointSizeScaleRatio;
            homeXButton.Click += new EventHandler(homeXButton_Click);

            homeYButton = textImageButtonFactory.Generate("Y", centerText: true);
            homeYButton.Margin = new BorderDouble(0, 0, 6, 0) * TextWidget.GlobalPointSizeScaleRatio;
            homeYButton.Click += new EventHandler(homeYButton_Click);

            homeZButton = textImageButtonFactory.Generate("Z", centerText: true);
            homeZButton.Margin = new BorderDouble(0, 0, 6, 0) * TextWidget.GlobalPointSizeScaleRatio;
            homeZButton.Click += new EventHandler(homeZButton_Click);

            textImageButtonFactory.normalFillColor = RGBA_Bytes.White;
            textImageButtonFactory.FixedWidth = 0;

            GuiWidget spacer = new GuiWidget();
            spacer.HAnchor = HAnchor.ParentLeftRight;

            disableMotors = textImageButtonFactory.Generate("Release".Localize().ToUpper());
            disableMotors.Margin = new BorderDouble(0);
            disableMotors.Click += new EventHandler(disableMotors_Click);

            GuiWidget spacerReleaseShow = new GuiWidget(10 * TextWidget.GlobalPointSizeScaleRatio, 0);

            homeButtonBar.AddChild(homeIconImageWidget);
            homeButtonBar.AddChild(homeAllButton);
            homeButtonBar.AddChild(homeXButton);
            homeButtonBar.AddChild(homeYButton);
            homeButtonBar.AddChild(homeZButton);
            homeButtonBar.AddChild(spacer);
            homeButtonBar.AddChild(disableMotors);
            homeButtonBar.AddChild(spacerReleaseShow);

            return homeButtonBar;
        }

        public static double XSpeed { get { return GetMovementSpeeds()["x"]; } }
        public static double YSpeed { get { return GetMovementSpeeds()["y"]; } }
        public static double ZSpeed { get { return GetMovementSpeeds()["z"]; } }

        public static double EFeedRate(int extruderIndex)
        {
            if (GetMovementSpeeds().ContainsKey("e" + extruderIndex.ToString()))
            {
                return GetMovementSpeeds()["e" + extruderIndex.ToString()];
            }

            return GetMovementSpeeds()["e0"];
        }

        static Dictionary<string, double> GetMovementSpeeds()
        {
            Dictionary<string, double> speeds = new Dictionary<string, double>();
            string movementSpeedsString = GetMovementSpeedsString();
            string[] allSpeeds = movementSpeedsString.Split(',');
            for (int i = 0; i < allSpeeds.Length / 2; i++)
            {
                speeds.Add(allSpeeds[i * 2 + 0], double.Parse(allSpeeds[i * 2 + 1]));
            }

            return speeds;
        }

        static string GetMovementSpeedsString()
        {
            string presets = "x,3000,y,3000,z,315,e0,150"; // stored x,value,y,value,z,value,e1,value,e2,value,e3,value,...
            if (PrinterConnectionAndCommunication.Instance != null && ActivePrinterProfile.Instance.ActivePrinter != null)
            {
                string savedSettings = ActivePrinterProfile.Instance.ActivePrinter.ManualMovementSpeeds;
                if (savedSettings != null && savedSettings != "")
                {
                    presets = savedSettings;
                }
            }

            return presets;
        }

        static void SetMovementSpeeds(object seder, EventArgs e)
        {
            StringEventArgs stringEvent = e as StringEventArgs;
            if (stringEvent != null && stringEvent.Data != null)
            {
                ActivePrinterProfile.Instance.ActivePrinter.ManualMovementSpeeds = stringEvent.Data;
                ActivePrinterProfile.Instance.ActivePrinter.Commit();
                ApplicationController.Instance.ReloadAdvancedControlsPanel();
            }
        }

        void disableMotors_Click(object sender, EventArgs mouseEvent)
        {
            PrinterConnectionAndCommunication.Instance.ReleaseMotors();
        }

        void homeXButton_Click(object sender, EventArgs mouseEvent)
        {
            PrinterConnectionAndCommunication.Instance.HomeAxis(PrinterConnectionAndCommunication.Axis.X);
        }

        void homeYButton_Click(object sender, EventArgs mouseEvent)
        {
            PrinterConnectionAndCommunication.Instance.HomeAxis(PrinterConnectionAndCommunication.Axis.Y);
        }

        void homeZButton_Click(object sender, EventArgs mouseEvent)
        {
            PrinterConnectionAndCommunication.Instance.HomeAxis(PrinterConnectionAndCommunication.Axis.Z);
        }

        void homeAll_Click(object sender, EventArgs mouseEvent)
        {
            PrinterConnectionAndCommunication.Instance.HomeAxis(PrinterConnectionAndCommunication.Axis.XYZ);
        }
    }
}
