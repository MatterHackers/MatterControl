using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl;
using MatterHackers.MeshVisualizer;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
    public class ViewControls3D : FlowLayoutWidget
    {
        GuiWidget partSelectSeparator;
        MeshViewerWidget meshViewerWidget;

        public RadioButton translateButton;
        public RadioButton rotateButton;
        public RadioButton scaleButton;
        public RadioButton partSelectButton;
        int buttonHeight;

        public bool PartSelectVisible
        {
            get { return partSelectSeparator.Visible; }
            set 
            {
                partSelectSeparator.Visible = value;
                partSelectButton.Visible = value;
            }
        }
                
        public ViewControls3D(MeshViewerWidget meshViewerWidget)
        {
            if (ActiveTheme.Instance.DisplayMode == ActiveTheme.ApplicationDisplayType.Touchscreen)
            {
                buttonHeight = 40;
            }
            else
            {
                buttonHeight = 20;
            }
            
            this.meshViewerWidget = meshViewerWidget;
            TextImageButtonFactory textImageButtonFactory = new TextImageButtonFactory();

            textImageButtonFactory.normalTextColor = ActiveTheme.Instance.PrimaryTextColor;
            textImageButtonFactory.hoverTextColor = ActiveTheme.Instance.PrimaryTextColor;
            textImageButtonFactory.disabledTextColor = ActiveTheme.Instance.PrimaryTextColor;
            textImageButtonFactory.pressedTextColor = ActiveTheme.Instance.PrimaryTextColor;

            BackgroundColor = new RGBA_Bytes(0, 0, 0, 120);
            textImageButtonFactory.FixedHeight = buttonHeight;
            textImageButtonFactory.FixedWidth = buttonHeight;
            textImageButtonFactory.AllowThemeToAdjustImage = false;
            textImageButtonFactory.checkedBorderColor = RGBA_Bytes.White;

            string rotateIconPath = Path.Combine("ViewTransformControls", "rotate.png");
            rotateButton = textImageButtonFactory.GenerateRadioButton("", rotateIconPath);
            rotateButton.Margin = new BorderDouble(3);
            AddChild(rotateButton);
            rotateButton.Click += (sender, e) =>
            {
                meshViewerWidget.TrackballTumbleWidget.TransformState = TrackBallController.MouseDownType.Rotation;
            };

            string translateIconPath = Path.Combine("ViewTransformControls", "translate.png");
            translateButton = textImageButtonFactory.GenerateRadioButton("", translateIconPath);
            translateButton.Margin = new BorderDouble(3);
            AddChild(translateButton);
            translateButton.Click += (sender, e) =>
            {
                meshViewerWidget.TrackballTumbleWidget.TransformState = TrackBallController.MouseDownType.Translation;
            };

            string scaleIconPath = Path.Combine("ViewTransformControls", "scale.png");
            scaleButton = textImageButtonFactory.GenerateRadioButton("", scaleIconPath);
            scaleButton.Margin = new BorderDouble(3);
            AddChild(scaleButton);
            scaleButton.Click += (sender, e) =>
            {
                meshViewerWidget.TrackballTumbleWidget.TransformState = TrackBallController.MouseDownType.Scale;
            };

            partSelectSeparator = new GuiWidget(2, 32);
            partSelectSeparator.BackgroundColor = RGBA_Bytes.White;
            partSelectSeparator.Margin = new BorderDouble(3);
            AddChild(partSelectSeparator);

            string partSelectIconPath = Path.Combine("ViewTransformControls", "partSelect.png");
            partSelectButton = textImageButtonFactory.GenerateRadioButton("", partSelectIconPath);
            partSelectButton.Margin = new BorderDouble(3);
            AddChild(partSelectButton);
            partSelectButton.Click += (sender, e) =>
            {
                meshViewerWidget.TrackballTumbleWidget.TransformState = TrackBallController.MouseDownType.None;
            };

            Margin = new BorderDouble(5);
            HAnchor |= Agg.UI.HAnchor.ParentLeft;
            VAnchor = Agg.UI.VAnchor.ParentTop;
            rotateButton.Checked = true;

            SetMeshViewerDisplayTheme();
            partSelectButton.CheckedStateChanged += SetMeshViewerDisplayTheme;

            ActiveTheme.Instance.ThemeChanged.RegisterEvent(ThemeChanged, ref unregisterEvents);
        }

        event EventHandler unregisterEvents;

        public override void OnClosed(EventArgs e)
        {
            if (unregisterEvents != null)
            {
                unregisterEvents(this, null);
            }
            base.OnClosed(e);
        }

        public void ThemeChanged(object sender, EventArgs e)
        {
            SetMeshViewerDisplayTheme(null, null);
        }

        protected void SetMeshViewerDisplayTheme(object sender = null, EventArgs e = null)
        {
            meshViewerWidget.TrackballTumbleWidget.RotationHelperCircleColor = ActiveTheme.Instance.PrimaryBackgroundColor;

            //meshViewerWidget.MaterialColor = RGBA_Bytes.White;
            //meshViewerWidget.SelectedMaterialColor = ActiveTheme.Instance.PrimaryAccentColor;
            meshViewerWidget.BuildVolumeColor = new RGBA_Bytes(ActiveTheme.Instance.PrimaryAccentColor.Red0To255, ActiveTheme.Instance.PrimaryAccentColor.Green0To255, ActiveTheme.Instance.PrimaryAccentColor.Blue0To255, 50);
        }
    }
}
