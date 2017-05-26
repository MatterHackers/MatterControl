/*
Copyright (c) 2017, Lars Brubaker, John Lewin
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
using System.IO;
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.ImageProcessing;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public enum ViewControls3DButtons
	{
		Rotate,
		Scale,
		Translate,
		PartSelect
	}

	public class TransformStateChangedEventArgs : EventArgs
	{
		public ViewControls3DButtons TransformMode { get; set; }
	}

	public class ViewControls3D : ViewControlsBase
	{
		public event EventHandler ResetView;
		public event EventHandler<TransformStateChangedEventArgs> TransformStateChanged;

		internal OverflowDropdown OverflowButton;

		private GuiWidget partSelectSeparator;
		private Button resetViewButton;

		private RadioButton translateButton;
		private RadioButton rotateButton;
		private RadioButton scaleButton;
		private RadioButton partSelectButton;

		private EventHandler unregisterEvents;

		private ViewControls3DButtons activeTransformState = ViewControls3DButtons.Rotate;
		
		public bool PartSelectVisible
		{
			get { return partSelectSeparator.Visible; }
			set
			{
				partSelectSeparator.Visible = value;
				partSelectButton.Visible = value;
			}
		}

		public ViewControls3DButtons ActiveButton
		{
			get
			{
				return activeTransformState;
			}
			set
			{
				this.activeTransformState = value;
				switch (this.activeTransformState)
				{
					case ViewControls3DButtons.Rotate:
						rotateButton.Checked = true;
						break;

					case ViewControls3DButtons.Translate:
						translateButton.Checked = true;
						break;

					case ViewControls3DButtons.Scale:
						scaleButton.Checked = true;
						break;

					case ViewControls3DButtons.PartSelect:
						partSelectButton.Checked = true;
						break;
				}

				TransformStateChanged?.Invoke(this, new TransformStateChangedEventArgs()
				{
					TransformMode = activeTransformState
				});
			}
		}

		public ViewControls3D(TextImageButtonFactory buttonFactory)
		{
			this.BackgroundColor = new RGBA_Bytes(0, 0, 0, overlayAlpha);
			this.HAnchor |= HAnchor.ParentLeft;
			this.VAnchor = VAnchor.ParentTop;

			string iconPath;

			iconPath = Path.Combine("ViewTransformControls", "reset.png");
			resetViewButton = buttonFactory.Generate("", StaticData.Instance.LoadIcon(iconPath,32,32).InvertLightness());
			resetViewButton.ToolTipText = "Reset View".Localize();
			resetViewButton.Click += (s, e) => ResetView?.Invoke(this, null);
			AddChild(resetViewButton);

			iconPath = Path.Combine("ViewTransformControls", "rotate.png");
			rotateButton = buttonFactory.GenerateRadioButton("", StaticData.Instance.LoadIcon(iconPath,32,32));
			rotateButton.ToolTipText = "Rotate (Alt + Left Mouse)".Localize();
			rotateButton.Margin = new BorderDouble(3);
			rotateButton.Click += (s, e) => this.ActiveButton = ViewControls3DButtons.Rotate;
			AddChild(rotateButton);

			iconPath = Path.Combine("ViewTransformControls", "translate.png");
			translateButton = buttonFactory.GenerateRadioButton("", StaticData.Instance.LoadIcon(iconPath,32,32));
			translateButton.ToolTipText = "Move (Shift + Left Mouse)".Localize();
			translateButton.Margin = new BorderDouble(3);
			translateButton.Click += (s, e) => this.ActiveButton = ViewControls3DButtons.Translate;
			AddChild(translateButton);

			iconPath = Path.Combine("ViewTransformControls", "scale.png");
			scaleButton = buttonFactory.GenerateRadioButton("", StaticData.Instance.LoadIcon(iconPath,32,32));
			scaleButton.ToolTipText = "Zoom (Ctrl + Left Mouse)".Localize();
			scaleButton.Margin = 3;
			scaleButton.Click += (s, e) => this.ActiveButton = ViewControls3DButtons.Scale;
			AddChild(scaleButton);

			partSelectSeparator = new GuiWidget(2, 32);
			partSelectSeparator.BackgroundColor = RGBA_Bytes.White;
			partSelectSeparator.Margin = 3;
			AddChild(partSelectSeparator);

			iconPath = Path.Combine("ViewTransformControls", "partSelect.png");
			partSelectButton = buttonFactory.GenerateRadioButton("", StaticData.Instance.LoadIcon(iconPath,32,32));
			partSelectButton.ToolTipText = "Select Part".Localize();
			partSelectButton.Margin = new BorderDouble(3);
			partSelectButton.Click += (s, e) => this.ActiveButton = ViewControls3DButtons.PartSelect;
			AddChild(partSelectButton);

			iconPath = Path.Combine("ViewTransformControls", "layers.png");
			var layersButton = buttonFactory.Generate("", StaticData.Instance.LoadIcon(iconPath, 32, 32).InvertLightness());
			layersButton.ToolTipText = "Layers".Localize();
			layersButton.Margin = 3;
			layersButton.Click += (s, e) =>
			{
				var parentTabPage = this.Parents<PartPreviewContent.PrinterTabPage>().First();
				parentTabPage.ToggleView();
			};
			AddChild(layersButton);

			OverflowButton = new OverflowDropdown(allowLightnessInvert: false);
			OverflowButton.ToolTipText = "More...".Localize();
			OverflowButton.Margin = 3;
			AddChild(OverflowButton);

			rotateButton.Checked = true;
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			unregisterEvents?.Invoke(this, null);
			base.OnClosed(e);
		}
	}
}