/*
Copyright (c) 2017, John Lewin
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
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.ImageProcessing;

namespace MatterHackers.MatterControl.CustomWidgets
{
	public class SimpleButton : Button
	{
		private bool mouseInBounds = false;

		public SimpleButton(ThemeConfig theme)
		{
			this.HoverColor = theme.SlightShade;
			this.MouseDownColor = theme.MinimalShade;
			this.Margin = 0;
		}

		public RGBA_Bytes HoverColor { get; set; } = RGBA_Bytes.Transparent;

		public RGBA_Bytes MouseDownColor { get; set; } = RGBA_Bytes.Transparent;

		public override void OnMouseEnterBounds(MouseEventArgs mouseEvent)
		{
			mouseInBounds = true;
			base.OnMouseEnterBounds(mouseEvent);
			this.Invalidate();
		}

		public override void OnMouseLeaveBounds(MouseEventArgs mouseEvent)
		{
			mouseInBounds = false;
			base.OnMouseLeaveBounds(mouseEvent);
			this.Invalidate();
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			base.OnMouseDown(mouseEvent);
			this.Invalidate();
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			base.OnMouseUp(mouseEvent);
			this.Invalidate();
		}

		public override RGBA_Bytes BackgroundColor
		{
			get
			{
				if (this.MouseCaptured
					&& mouseInBounds
					&& this.Enabled)
				{
					return this.MouseDownColor;
				}
				else if (this.mouseInBounds
					&& this.Enabled)
				{
					return this.HoverColor;
				}
				else
				{
					return base.BackgroundColor;
				}
			}
			set => base.BackgroundColor = value;
		}
	}

	public class IconButton : SimpleButton
	{
		private ImageWidget imageWidget;

		private ImageBuffer image;

		public IconButton(ImageBuffer icon, ThemeConfig theme)
			: base(theme)
		{
			this.image = icon;
			this.HAnchor = HAnchor.Absolute;
			this.VAnchor = VAnchor.Absolute | VAnchor.Center;
			this.Height = theme.ButtonHeight;
			this.Width = theme.ButtonHeight;

			imageWidget = new ImageWidget(icon)
			{
				HAnchor = HAnchor.Center,
				VAnchor = VAnchor.Center,
				Selectable = false
			};

			this.AddChild(imageWidget);
		}

		public override bool Enabled
		{
			get => base.Enabled;
			set
			{
				base.Enabled = value;
				imageWidget.Image = (value) ? image : this.DisabledImage;
			}
		}

		private ImageBuffer _disabledImage;
		public ImageBuffer DisabledImage
		{
			get
			{
				// Lazy construct on first access
				if (_disabledImage == null)
				{
					_disabledImage = image.AjustAlpha(0.2);
				}

				return _disabledImage;
			}
		}
	}

	public class TextButton : SimpleButton
	{
		private TextWidget textWidget;

		public TextButton(string text, ThemeConfig theme)
			: base(theme)
		{
			this.HAnchor = HAnchor.Fit;
			this.VAnchor = VAnchor.Absolute | VAnchor.Center;
			this.Height = theme.ButtonFactory.Options.FixedHeight;
			this.Padding = theme.ButtonFactory.Options.Margin;

			this.AddChild(textWidget = new TextWidget(text, pointSize: theme.ButtonFactory.Options.FontSize, textColor: theme.ButtonFactory.Options.NormalTextColor)
			{
				HAnchor = HAnchor.Center,
				VAnchor = VAnchor.Center
			});
		}

		public override string Text
		{
			get => base.Text;
			set
			{
				this.textWidget.Text = value;
				base.Text = value;
			}
		}

		public override bool Enabled
		{
			get => base.Enabled;
			set
			{
				base.Enabled = value;
				textWidget.Enabled = value;
			}
		}
	}

}