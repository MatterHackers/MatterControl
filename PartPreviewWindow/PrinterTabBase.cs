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
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.PrinterCommunication;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.MeshVisualizer;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class PrinterTabBase : TabPage
	{
		internal View3DWidget modelViewer;

		protected ViewControls3D viewControls3D;

		protected BedConfig sceneContext;
		protected ThemeConfig theme;

		protected GuiWidget view3DContainer;
		protected FlowLayoutWidget topToBottom;
		protected FlowLayoutWidget leftToRight;

		public PrinterTabBase(BedConfig sceneContext, ThemeConfig theme, string tabTitle)
			: base (tabTitle)
		{
			this.sceneContext = sceneContext;
			this.theme = theme;
			this.BackgroundColor = theme.TabBodyBackground;
			this.Padding = 0;

			viewControls3D = new ViewControls3D(theme, sceneContext.Scene.UndoBuffer)
			{
				PartSelectVisible = false,
				VAnchor = VAnchor.Top | VAnchor.Fit | VAnchor.Absolute,
				HAnchor = HAnchor.Left | HAnchor.Fit,
				Visible = true,
				Margin = new BorderDouble(6, 0, 0, 43)
			};
			viewControls3D.ResetView += (sender, e) =>
			{
				if (modelViewer.Visible)
				{
					this.modelViewer.ResetView();
				}
			};
			viewControls3D.OverflowButton.DynamicPopupContent = () =>
			{
				return this.GetViewControls3DOverflowMenu();
			};

			bool isPrinterType = this.GetType() == typeof(PrinterTabPage);

			// The 3D model view
			modelViewer = new View3DWidget(
				sceneContext,
				View3DWidget.AutoRotate.Disabled,
				viewControls3D,
				theme,
				View3DWidget.OpenMode.Editing,
				editorType: (isPrinterType) ? MeshViewerWidget.EditorType.Printer : MeshViewerWidget.EditorType.Part);

			topToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
			topToBottom.AnchorAll();
			this.AddChild(topToBottom);

			leftToRight = new FlowLayoutWidget();
			leftToRight.Name = "View3DContainerParent";
			leftToRight.AnchorAll();
			topToBottom.AddChild(leftToRight);

			view3DContainer = new GuiWidget();
			view3DContainer.AnchorAll();
			view3DContainer.AddChild(modelViewer);

			leftToRight.AddChild(view3DContainer);

			modelViewer.BackgroundColor = ActiveTheme.Instance.TertiaryBackgroundColor;

			if (sceneContext.World.RotationMatrix == Matrix4X4.Identity)
			{
				this.modelViewer.ResetView();
			}

			this.AddChild(viewControls3D);

			this.AnchorAll();
		}

		protected virtual GuiWidget GetViewControls3DOverflowMenu()
		{
			return modelViewer.ShowOverflowMenu();
		}

		public override void OnLoad(EventArgs args)
		{
			ApplicationController.Instance.ActiveView3DWidget = modelViewer;

			base.OnLoad(args);
		}
	}
}
