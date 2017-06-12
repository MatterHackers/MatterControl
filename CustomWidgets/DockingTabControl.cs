/*
Copyright (c) 2017, Lars Brubaker
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
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.CustomWidgets
{
	public class DockingTabControl : GuiWidget
	{
		private bool controlIsPinned = false;
		private TabControl tabControl;
		private GuiWidget topToBottom;

		public DockingTabControl()
		{
			// load up the state data for this control and printer
			// ActiveSliceSettings.Instance.PrinterSelected
			controlIsPinned = true;
		}

		public void AddPage(string name, GuiWidget widget)
		{
			if (controlIsPinned)
			{
				if (tabControl == null)
				{
					var resizePage = new ResizeContainer()
					{
						Width = 640,
						VAnchor = VAnchor.ParentBottomTop,
					};
					tabControl = new TabControl();
					resizePage.AddChild(tabControl);

					topToBottom.AddChild(resizePage);
				}

				var content = new DockWindowContent(widget, name);
				tabControl.AddTab(new TabPage(content, name), name);
			}
			else // control is floating
			{
				TypeFacePrinter stringPrinter = new TypeFacePrinter(name, 12);

				var stringPrinter2 = new VertexSourceApplyTransform(stringPrinter, Affine.NewTranslation(new Vector2(200, 200)));
				//graphics2D.Render(stringPrinter2, RGBA_Bytes.Black);

				var stringPrinter3 = new VertexSourceApplyTransform(stringPrinter, Affine.NewRotation(MathHelper.DegreesToRadians(-90)));
				var bounds = stringPrinter3.Bounds();
				stringPrinter3.Transform = ((Affine)stringPrinter3.Transform) * Affine.NewTranslation(new Vector2(0, -bounds.Bottom + 0));

				GuiWidget optionsText = new GuiWidget(bounds.Width, bounds.Height)
				{
					DoubleBuffer = true,
					//BackgroundColor = RGBA_Bytes.Green,
				};

				optionsText.AfterDraw += (s, e) =>
				{
					e.graphics2D.Render(stringPrinter3, RGBA_Bytes.Black);
					//e.graphics2D.DrawString(name, 0, 0);
				};

				PopupButton settingsButton = new PopupButton(optionsText)
				{
					AlignToRightEdge = true,
				};

				settingsButton.PopupContent = new DockWindowContent(widget, name);

				topToBottom.AddChild(settingsButton);
			}
		}

		public override void Initialize()
		{
			base.Initialize();

			Width = 30;
			VAnchor = VAnchor.ParentBottomTop;
			HAnchor = HAnchor.FitToChildren;
			BackgroundColor = RGBA_Bytes.Red;
			topToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.FitToChildren,
				VAnchor = VAnchor.ParentBottomTop
			};
			AddChild(topToBottom);
		}

		internal class ResizeContainer : FlowLayoutWidget
		{
			private double downWidth = 0;
			private bool mouseDownOnBar = false;
			private double mouseDownX;
			private int resizeWidth = 10;

			internal ResizeContainer()
			{
				Padding = new BorderDouble(resizeWidth, 0, 0, 0);
				HAnchor = HAnchor.AbsolutePosition;
			}

			public override void OnDraw(Graphics2D graphics2D)
			{
				graphics2D.FillRectangle(LocalBounds.Left, LocalBounds.Bottom, LocalBounds.Left + resizeWidth, LocalBounds.Top, RGBA_Bytes.Black);
				base.OnDraw(graphics2D);
			}

			public override void OnMouseDown(MouseEventArgs mouseEvent)
			{
				if (mouseEvent.Position.x < resizeWidth)
				{
					mouseDownOnBar = true;
					mouseDownX = TransformToScreenSpace(mouseEvent.Position).x;
					downWidth = Width;
				}
				base.OnMouseDown(mouseEvent);
			}

			public override void OnMouseMove(MouseEventArgs mouseEvent)
			{
				if (mouseDownOnBar)
				{
					int currentMouseX = (int)TransformToScreenSpace(mouseEvent.Position).x;
					Width = downWidth + mouseDownX - currentMouseX;
				}
				base.OnMouseMove(mouseEvent);
			}

			public override void OnMouseUp(MouseEventArgs mouseEvent)
			{
				mouseDownOnBar = false;
				base.OnMouseUp(mouseEvent);
			}
		}

		private class DockWindowContent : GuiWidget, IIgnoredPopupChild
		{
			internal DockWindowContent(GuiWidget child, string title)
			{
				FlowLayoutWidget topToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom)
				{
					VAnchor = VAnchor.ParentBottomTop,
					HAnchor = HAnchor.ParentLeftRight
				};

				FlowLayoutWidget titleBar = new FlowLayoutWidget()
				{
					HAnchor = HAnchor.ParentLeftRight
				};
				titleBar.AddChild(new TextWidget(title));
				titleBar.AddChild(new GuiWidget() { HAnchor = HAnchor.ParentLeftRight });
				titleBar.AddChild(new CheckBox("[pin icon]"));
				topToBottom.AddChild(titleBar);

				Width = 500;
				Height = 640;
				topToBottom.AddChild(child);

				AddChild(topToBottom);
			}
		}
	}
}