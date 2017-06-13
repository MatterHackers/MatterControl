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
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class OverflowDropdown : PopupButton
	{
		public OverflowDropdown(bool allowLightnessInvert)
			: base(LoadThemedIcon(allowLightnessInvert))
		{
			this.ToolTipText = "More...".Localize();
		}

		public BorderDouble MenuPadding { get; set; } = new BorderDouble(40, 8, 20, 8);

		public static ImageWidget LoadThemedIcon(bool allowLightnessInvert)
		{
			var imageBuffer = StaticData.Instance.LoadIcon(Path.Combine("ViewTransformControls", "overflow.png"), 32, 32);
			if (!ActiveTheme.Instance.IsDarkTheme && allowLightnessInvert)
			{
				imageBuffer.InvertLightness();
			}

			return new ImageWidget(imageBuffer);
		}

		public MenuItem CreateHorizontalLine()
		{
			return new MenuItem(new GuiWidget()
			{
				HAnchor = HAnchor.ParentLeftRight,
				Height = 1,
				BackgroundColor = RGBA_Bytes.LightGray,
				Margin = new BorderDouble(10, 1),
				VAnchor = VAnchor.ParentCenter,
			}, "HorizontalLine");
		}

		public MenuItem CreateMenuItem(string name, string value = null, double pointSize = 12)
		{
			var menuStatesView = new MenuItemColorStatesView(name)
			{
				NormalBackgroundColor = RGBA_Bytes.White,
				OverBackgroundColor = RGBA_Bytes.Gray,
				NormalTextColor = RGBA_Bytes.Black,
				OverTextColor = RGBA_Bytes.Black,
				DisabledTextColor = RGBA_Bytes.Gray,
				PointSize = pointSize,
				Padding = this.MenuPadding,
			};

			return new MenuItem(menuStatesView, value ?? name)
			{
				Text = name,
				Name = name + " Menu Item"
			};
		}

		protected override void BeforeShowPopup()
		{
			if (this.PopupContent.BackgroundColor == RGBA_Bytes.Transparent)
			{
				this.PopupContent.BackgroundColor = RGBA_Bytes.White;
			}
		}
	}

	public class PopupButton : GuiWidget, IIgnoredPopupChild
	{
		private static readonly RGBA_Bytes slightShade = new RGBA_Bytes(0, 0, 0, 40);

		private GuiWidget buttonView;
		private bool menuVisibileAtMouseDown = false;
		private bool menuVisible = false;
		private PopupWidget popupWidget;

		//private GuiWidget buttonView;
		public PopupButton(GuiWidget buttonView)
		{
			this.Margin = 3;
			this.HAnchor = HAnchor.FitToChildren;
			this.VAnchor = VAnchor.FitToChildren;
			this.buttonView = buttonView;

			this.AddChild(buttonView);
		}

		public bool AlignToRightEdge { get; set; }
		public RGBA_Bytes BorderColor { get; set; } = RGBA_Bytes.Gray;
		public Func<GuiWidget> DynamicPopupContent { get; set; }
		public IPopupLayoutEngine PopupLayoutEngine { get; set; }
		public Direction PopDirection { get; set; } = Direction.Down;
		public bool MakeScrollable { get; set; } = true;

		public GuiWidget PopupContent { get; set; }

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			// Store the menu state at the time of mousedown
			menuVisibileAtMouseDown = menuVisible;
			base.OnMouseDown(mouseEvent);
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			// Only show the popup if the menu was hidden as the mouse events started
			if ((buttonView.MouseCaptured || this.MouseCaptured)
				&& !menuVisibileAtMouseDown)
			{
				ShowPopup();
				this.BackgroundColor = slightShade;
			}

			base.OnMouseUp(mouseEvent);
		}

		public void ShowPopup()
		{
			if (PopupLayoutEngine == null)
			{
				PopupLayoutEngine = new PopupLayoutEngine(this.PopupContent, this, Vector2.Zero, this.PopDirection, 0, this.AlignToRightEdge);
			}
			menuVisible = true;

			this.PopupContent?.ClearRemovedFlag();

			if (this.DynamicPopupContent != null)
			{
				this.PopupContent = this.DynamicPopupContent();
			}

			if (this.PopupContent == null)
			{
				return;
			}

			this.BeforeShowPopup();

			popupWidget = new PopupWidget(this.PopupContent, PopupLayoutEngine, MakeScrollable)
			{
				BorderWidth = 1,
				BorderColor = this.BorderColor,
			};

			popupWidget.Closed += (s, e) =>
			{
				menuVisible = false;
				popupWidget = null;
			};
			popupWidget.Focus();
		}

		protected virtual void BeforeShowPopup()
		{
		}
	}
}