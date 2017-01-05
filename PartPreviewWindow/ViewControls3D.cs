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

using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.MeshVisualizer;
using MatterHackers.VectorMath;
using System;
using System.IO;
using MatterHackers.Localizations;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.ImageProcessing;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public enum ViewControls3DButtons
	{
		Rotate,
		Scale,
		Translate,
		PartSelect
	}

	public class ViewControls3D : FlowLayoutWidget
	{
		private GuiWidget partSelectSeparator;
		private MeshViewerWidget meshViewerWidget;

		private Button resetViewButton;

		private RadioButton translateButton;
		private RadioButton rotateButton;
		private RadioButton scaleButton;
		private RadioButton partSelectButton;

		private EventHandler unregisterEvents;

		private int buttonHeight;

		public event EventHandler ResetView;

		public bool PartSelectVisible
		{
			get { return partSelectSeparator.Visible; }
			set
			{
				partSelectSeparator.Visible = value;
				partSelectButton.Visible = value;
			}
		}

		private ViewControls3DButtons activeTransformState = ViewControls3DButtons.Rotate;

		public ViewControls3DButtons ActiveButton
		{
			get
			{
				return activeTransformState;
			}
			set
			{
				this.activeTransformState = value;

				switch(value)
				{
					case ViewControls3DButtons.Rotate:
						meshViewerWidget.TrackballTumbleWidget.TransformState = TrackBallController.MouseDownType.Rotation;
						rotateButton.Checked = true;
						break;

					case ViewControls3DButtons.Translate:
						meshViewerWidget.TrackballTumbleWidget.TransformState = TrackBallController.MouseDownType.Translation;
						translateButton.Checked = true;
						break;

					case ViewControls3DButtons.Scale:
						meshViewerWidget.TrackballTumbleWidget.TransformState = TrackBallController.MouseDownType.Scale;
						scaleButton.Checked = true;
						break;

					case ViewControls3DButtons.PartSelect:
						meshViewerWidget.TrackballTumbleWidget.TransformState = TrackBallController.MouseDownType.None;
						partSelectButton.Checked = true;
						break;
				}
			}
		}

		public ViewControls3D(MeshViewerWidget meshViewerWidget)
		{
			if (UserSettings.Instance.DisplayMode == ApplicationDisplayType.Touchscreen)
			{
				buttonHeight = 40;
			}
			else
			{
				buttonHeight = 0;
			}

			this.meshViewerWidget = meshViewerWidget;
			var textImageButtonFactory = new TextImageButtonFactory()
			{
				normalTextColor = ActiveTheme.Instance.PrimaryTextColor,
				hoverTextColor = ActiveTheme.Instance.PrimaryTextColor,
				disabledTextColor = ActiveTheme.Instance.PrimaryTextColor,
				pressedTextColor = ActiveTheme.Instance.PrimaryTextColor,
				FixedHeight = buttonHeight,
				FixedWidth = buttonHeight,
				AllowThemeToAdjustImage = false,
				checkedBorderColor = RGBA_Bytes.White
			};

			
			string iconPath;

			iconPath = Path.Combine("ViewTransformControls", "reset.png");
			resetViewButton = textImageButtonFactory.Generate("", StaticData.Instance.LoadIcon(iconPath,32,32).InvertLightness());
			resetViewButton.ToolTipText = "Reset View".Localize();
			AddChild(resetViewButton);
			resetViewButton.Click += (s, e) => ResetView?.Invoke(this, null);

			iconPath = Path.Combine("ViewTransformControls", "rotate.png");
			rotateButton = textImageButtonFactory.GenerateRadioButton("", StaticData.Instance.LoadIcon(iconPath,32,32));
			rotateButton.ToolTipText = "Rotate (Alt + Left Mouse)".Localize();
			rotateButton.Margin = new BorderDouble(3);
			AddChild(rotateButton);
			rotateButton.Click += (s, e) => this.ActiveButton = ViewControls3DButtons.Rotate;

			iconPath = Path.Combine("ViewTransformControls", "translate.png");
			translateButton = textImageButtonFactory.GenerateRadioButton("", StaticData.Instance.LoadIcon(iconPath,32,32));
			translateButton.ToolTipText = "Move (Shift + Left Mouse)".Localize();
			translateButton.Margin = new BorderDouble(3);
			AddChild(translateButton);
			translateButton.Click += (s, e) => this.ActiveButton = ViewControls3DButtons.Translate;

			iconPath = Path.Combine("ViewTransformControls", "scale.png");
			scaleButton = textImageButtonFactory.GenerateRadioButton("", StaticData.Instance.LoadIcon(iconPath,32,32));
			scaleButton.ToolTipText = "Zoom (Ctrl + Left Mouse)".Localize();
			scaleButton.Margin = new BorderDouble(3);
			AddChild(scaleButton);
			scaleButton.Click += (s, e) => this.ActiveButton = ViewControls3DButtons.Scale;

			partSelectSeparator = new GuiWidget(2, 32);
			partSelectSeparator.BackgroundColor = RGBA_Bytes.White;
			partSelectSeparator.Margin = new BorderDouble(3);
			AddChild(partSelectSeparator);

			iconPath = Path.Combine("ViewTransformControls", "partSelect.png");
			partSelectButton = textImageButtonFactory.GenerateRadioButton("", StaticData.Instance.LoadIcon(iconPath,32,32));
            partSelectButton.ToolTipText = "Select Part".Localize();
            partSelectButton.Margin = new BorderDouble(3);
			AddChild(partSelectButton);
			partSelectButton.Click += (s, e) => this.ActiveButton = ViewControls3DButtons.PartSelect;

			Margin = new BorderDouble(5);
			HAnchor |= Agg.UI.HAnchor.ParentLeft;
			VAnchor = Agg.UI.VAnchor.ParentTop;
			rotateButton.Checked = true;
			BackgroundColor = new RGBA_Bytes(0, 0, 0, 120);

			SetMeshViewerDisplayTheme();
			partSelectButton.CheckedStateChanged += SetMeshViewerDisplayTheme;

			ActiveTheme.ThemeChanged.RegisterEvent(ThemeChanged, ref unregisterEvents);
		}

		public override void OnClosed(EventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
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