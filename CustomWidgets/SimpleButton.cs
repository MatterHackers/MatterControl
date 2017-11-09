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

using System;
using System.Collections.Generic;
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.ImageProcessing;

namespace MatterHackers.MatterControl.CustomWidgets
{
	public class SimpleButton : Button
	{
		private bool mouseInBounds = false;

		protected ThemeConfig theme;

		public SimpleButton(ThemeConfig theme)
		{
			this.theme = theme;
			this.HoverColor = theme.SlightShade;
			this.MouseDownColor = theme.MinimalShade;
			this.Margin = 0;
		}

		public Color HoverColor { get; set; } = Color.Transparent;

		public Color MouseDownColor { get; set; } = Color.Transparent;

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

		public override Color BackgroundColor
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

	public class RadioIconButton : IconButton, IRadioButton
	{
		public IEnumerable<GuiWidget> SiblingRadioButtonList { get; set; }

		public event EventHandler CheckedStateChanged;

		public RadioIconButton(ImageBuffer icon, ThemeConfig theme)
			: base(icon, theme)
		{
			this.BorderColor = theme.ButtonFactory.Options.NormalTextColor;
		}

		public Color BorderColor { get; set; } = Color.White;

		public override void OnClick(MouseEventArgs mouseEvent)
		{
			base.OnClick(mouseEvent);
			this.Checked = true;
		}

		private bool _checked;
		public bool Checked
		{
			get => _checked;
			set
			{
				if (_checked != value)
				{
					_checked = value;
					if (_checked)
					{
						UncheckAllOtherRadioButtons();
					}

					OnCheckStateChanged();
					Invalidate();
				}
			}
		}

		public virtual void OnCheckStateChanged()
		{
			CheckedStateChanged?.Invoke(this, null);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			if (this.Checked)
			{

				graphics2D.Rectangle(LocalBounds, this.BorderColor);
			}

			base.OnDraw(graphics2D);
		}

		private void UncheckAllOtherRadioButtons()
		{
			if (SiblingRadioButtonList != null)
			{
				foreach (GuiWidget child in SiblingRadioButtonList.Distinct())
				{
					var radioButton = child as IRadioButton;
					if (radioButton != null && radioButton != this)
					{
						radioButton.Checked = false;
					}
				}
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