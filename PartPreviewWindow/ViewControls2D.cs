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
using System.IO;
using MatterHackers.Agg;
using MatterHackers.Agg.ImageProcessing;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class ViewControlsBase : FlowLayoutWidget
	{
		protected int buttonHeight = UserSettings.Instance.IsTouchScreen ? 40 : 20;
		public const int overlayAlpha = 50;
	}

	public class ViewControls2D : ViewControlsBase
	{
		private Button resetViewButton;

		public RadioButton translateButton;
		public RadioButton scaleButton;

		public event EventHandler ResetView;

		public ViewControls2D(TextImageButtonFactory buttonFactory)
		{
			this.BackgroundColor = new RGBA_Bytes(0, 0, 0, overlayAlpha);
			string resetViewIconPath = Path.Combine("ViewTransformControls", "reset.png");
			resetViewButton = buttonFactory.Generate("", StaticData.Instance.LoadIcon(resetViewIconPath, 32, 32).InvertLightness());
			resetViewButton.ToolTipText = "Reset View".Localize();
			AddChild(resetViewButton);
			resetViewButton.Click += (sender, e) =>
			{
				UiThread.RunOnIdle(() => ResetView?.Invoke(this, null));
			};

			string translateIconPath = Path.Combine("ViewTransformControls", "translate.png");
			translateButton = buttonFactory.GenerateRadioButton("", StaticData.Instance.LoadIcon(translateIconPath, 32, 32));
			translateButton.ToolTipText = "Move".Localize();
			translateButton.Margin = new BorderDouble(3);
			AddChild(translateButton);

			string scaleIconPath = Path.Combine("ViewTransformControls", "scale.png");
			scaleButton = buttonFactory.GenerateRadioButton("", StaticData.Instance.LoadIcon(scaleIconPath, 32, 32));
			scaleButton.ToolTipText = "Zoom".Localize();
			scaleButton.Margin = new BorderDouble(3);
			AddChild(scaleButton);

			Margin = new BorderDouble(5);
			HAnchor |= HAnchor.ParentLeft;
			VAnchor = VAnchor.ParentTop;
			translateButton.Checked = true;
		}
	}

	public enum PartViewMode
	{
		Layers2D,
		Layers3D,
		Model
	}

	public class ViewModeChangedEventArgs : EventArgs
	{
		public PartViewMode ViewMode { get; set; }
	}

	public class ViewControlsToggle : ViewControlsBase
	{
		public event EventHandler<ViewModeChangedEventArgs> ViewModeChanged;

		public ViewControlsToggle(TextImageButtonFactory buttonFactory, PartViewMode initialViewMode)
		{
			this.BackgroundColor = new RGBA_Bytes(0, 0, 0, overlayAlpha);

			var layers2DButton = buttonFactory.GenerateRadioButton("", Path.Combine("ViewTransformControls", "2d.png"));
			layers2DButton.Name = "Layers2D Button";
			layers2DButton.Margin = new BorderDouble(3);
			layers2DButton.Click += SwitchModes_Click;
			this.AddChild(layers2DButton);

			var layers3DButton = buttonFactory.GenerateRadioButton("", Path.Combine("ViewTransformControls", "3d.png"));
			layers3DButton.Click += SwitchModes_Click;
			layers3DButton.Margin = new BorderDouble(3);
		
			if (!UserSettings.Instance.IsTouchScreen)
			{
				this.AddChild(layers3DButton);

				// Change to always start in 3D view on desktop
				layers3DButton.Checked = initialViewMode == PartViewMode.Layers3D;
			}
			else
			{
				layers2DButton.Checked = true;
			}

			this.Margin = new BorderDouble(5, 5, 200, 5);
			this.HAnchor |= HAnchor.ParentRight;
			this.VAnchor = VAnchor.ParentTop;
		}

		private void SwitchModes_Click(object sender, MouseEventArgs e)
		{
			var widget = sender as GuiWidget;
			ViewModeChanged?.Invoke(this, new ViewModeChangedEventArgs()
			{
				ViewMode = widget.Name == "Layers2D Button" ? PartViewMode.Layers2D : PartViewMode.Layers3D
			});
		}
	}
}