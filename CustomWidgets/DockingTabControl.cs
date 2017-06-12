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

using System.Collections.Generic;
using MatterHackers.Agg;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.ImageProcessing;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.CustomWidgets
{
	public class DockingTabControl : GuiWidget
	{
		// TODO: Pinned state should preferably come from MCWS, default to local data if guest and be per user not printer
		public bool ControlIsPinned { get; set; } = false;
		private GuiWidget topToBottom;

		Dictionary<string, GuiWidget> allTabs = new Dictionary<string, GuiWidget>();

		public DockingTabControl()
		{
		}

		public void AddPage(string name, GuiWidget widget)
		{
			allTabs.Add(name, widget);
			Rebuild();
		}

		void Rebuild()
		{
			foreach(var nameWidget in allTabs)
			{
				nameWidget.Value.Parent?.RemoveChild(nameWidget.Value);
				nameWidget.Value.ClearRemovedFlag();
			}

			topToBottom.RemoveAllChildren();

			var tabControl = new TabControl();

			if (ControlIsPinned)
			{
				var resizePage = new ResizeContainer()
				{
					Width = 640,
					VAnchor = VAnchor.ParentBottomTop,
					BorderColor = ApplicationController.Instance.Theme.SplitterBackground
				};

				tabControl = ApplicationController.Instance.Theme.CreateTabControl();
				resizePage.AddChild(tabControl);

				topToBottom.AddChild(resizePage);
			}

			foreach (var nameWidget in allTabs)
			{
				if (ControlIsPinned)
				{
					var content = new DockWindowContent(this, nameWidget.Value, nameWidget.Key, ControlIsPinned);

					var tabPage = new TabPage(content, nameWidget.Key);

					tabControl.AddTab(new SimpleTextTabWidget(
						tabPage,
						nameWidget.Key + " Tab",
						12,
						ActiveTheme.Instance.TabLabelSelected,
						RGBA_Bytes.Transparent,
						ActiveTheme.Instance.TabLabelUnselected,
						RGBA_Bytes.Transparent));
				}
				else // control is floating
				{
					var rotatedLabel = new VertexSourceApplyTransform(
						new TypeFacePrinter(nameWidget.Key, 12),
						Affine.NewRotation(MathHelper.DegreesToRadians(-90)));

					var bounds = rotatedLabel.Bounds();
					rotatedLabel.Transform = ((Affine)rotatedLabel.Transform) * Affine.NewTranslation(new Vector2(0, -bounds.Bottom + 0));

					var optionsText = new GuiWidget(bounds.Width, bounds.Height)
					{
						DoubleBuffer = true,
					};
					optionsText.AfterDraw += (s, e) =>
					{
						e.graphics2D.Render(rotatedLabel, ActiveTheme.Instance.PrimaryTextColor);
					};

					var settingsButton = new PopupButton(optionsText)
					{
						AlignToRightEdge = true,
					};
					settingsButton.PopupContent = new DockWindowContent(this, nameWidget.Value, nameWidget.Key, ControlIsPinned)
					{
						BackgroundColor = ActiveTheme.Instance.PrimaryBackgroundColor
					};
					topToBottom.AddChild(settingsButton);
				}
			}

			if (ControlIsPinned)
			{
				tabControl.TabBar.AddChild(new HorizontalSpacer());

				var icon = StaticData.Instance.LoadIcon("Pushpin_16x.png", 16, 16).InvertLightness();
				var imageWidget = new ImageWidget(icon)
				{
					VAnchor = VAnchor.ParentCenter
				};
				imageWidget.Margin = new BorderDouble(right: 25, top: 6);
				imageWidget.DebugShowBounds = true;
				imageWidget.MinimumSize = new Vector2(16, 16);
				imageWidget.Click += (s, e) =>
				{
					ControlIsPinned = !ControlIsPinned;
					UiThread.RunOnIdle(() => Rebuild());
				};
				tabControl.TabBar.AddChild(imageWidget);
			}
		}

		public override void Initialize()
		{
			base.Initialize();

			Width = 30;
			VAnchor = VAnchor.ParentBottomTop;
			HAnchor = HAnchor.FitToChildren;
			//BackgroundColor = RGBA_Bytes.Red;
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
				this.Padding = new BorderDouble(resizeWidth, 0, 0, 0);
				this.HAnchor = HAnchor.AbsolutePosition;
				this.Cursor = Cursors.WaitCursor;
			}

			public override void OnDraw(Graphics2D graphics2D)
			{
				graphics2D.FillRectangle(LocalBounds.Left, LocalBounds.Bottom, LocalBounds.Left + resizeWidth, LocalBounds.Top, this.BorderColor);
				base.OnDraw(graphics2D);
			}

			public RGBA_Bytes BorderColor { get; set; } = ActiveTheme.Instance.TertiaryBackgroundColor;

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
					UiThread.RunOnIdle(() => Width = downWidth + mouseDownX - currentMouseX);
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
			internal DockWindowContent(DockingTabControl parent, GuiWidget child, string title, bool isDocked)
			{
				var topToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom)
				{
					VAnchor = VAnchor.ParentBottomTop,
					HAnchor = HAnchor.ParentLeftRight
				};

				if (!isDocked)
				{
					var titleBar = new FlowLayoutWidget()
					{
						HAnchor = HAnchor.ParentLeftRight,
						VAnchor = VAnchor.FitToChildren,
					};

					titleBar.AddChild(new TextWidget(title, textColor: ActiveTheme.Instance.PrimaryTextColor)
					{
						Margin = new BorderDouble(left: 12)
					});


					titleBar.AddChild(new HorizontalSpacer() { Height = 5, DebugShowBounds = false });

					var icon = StaticData.Instance.LoadIcon((isDocked) ? "Pushpin_16x.png" : "PushpinUnpin_16x.png", 16, 16).InvertLightness();
					var imageWidget = new ImageWidget(icon);
					imageWidget.Margin = new BorderDouble(right: 25, top: 6);
					imageWidget.MinimumSize = new Vector2(16, 16);
					imageWidget.Click += (s, e) =>
					{
						parent.ControlIsPinned = !parent.ControlIsPinned;
						UiThread.RunOnIdle(() => parent.Rebuild());
					};
					titleBar.AddChild(imageWidget);

					topToBottom.AddChild(titleBar);
				}

				Width = 500;
				Height = 640;
				topToBottom.AddChild(child);

				AddChild(topToBottom);
			}
		}
	}
}