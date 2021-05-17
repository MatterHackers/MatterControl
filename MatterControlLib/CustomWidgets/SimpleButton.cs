/*
Copyright (c) 2019, John Lewin
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
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.ImageProcessing;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.CustomWidgets
{
	public class SimpleButton : GuiWidget
	{
		protected ThemeConfig theme;

		private bool hasKeyboardFocus;

		public SimpleButton(ThemeConfig theme)
		{
			this.theme = theme;
			this.HoverColor = theme.SlightShade;
			this.MouseDownColor = theme.MinimalShade;
			this.Margin = 0;

			this.TabStop = true;
		}

		public Color HoverColor { get; set; } = Color.Transparent;

		public Color MouseDownColor { get; set; } = Color.Transparent;

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

		protected override void OnClick(MouseEventArgs mouseEvent)
		{
			if (mouseEvent.Button == MouseButtons.Left)
			{
				base.OnClick(mouseEvent);
			}
		}

		public override void OnKeyUp(KeyEventArgs keyEvent)
		{
			if (keyEvent.KeyCode == Keys.Enter
				|| keyEvent.KeyCode == Keys.Space)
			{
				UiThread.RunOnIdle(this.InvokeClick);
			}

			base.OnKeyUp(keyEvent);
		}

		public override void OnMouseEnterBounds(MouseEventArgs mouseEvent)
		{
			Invalidate();
			base.OnMouseEnterBounds(mouseEvent);
		}

		public override void OnMouseLeaveBounds(MouseEventArgs mouseEvent)
		{
			Invalidate();
			base.OnMouseLeaveBounds(mouseEvent);
		}

		public override Color BackgroundColor
		{
			get
			{
				var firstWidgetUnderMouse = ContainsFirstUnderMouseRecursive();
				if (this.MouseCaptured
					&& firstWidgetUnderMouse
					&& this.Enabled)
				{
					return this.MouseDownColor;
				}
				else if (firstWidgetUnderMouse
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

		public override void OnFocusChanged(EventArgs e)
		{
			hasKeyboardFocus = this.Focused && !ContainsFirstUnderMouseRecursive();
			this.Invalidate();

			base.OnFocusChanged(e);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			base.OnDraw(graphics2D);

			if (this.TabStop
				&& hasKeyboardFocus)
			{
				var bounds = this.LocalBounds;
				var stroke = 1 * GuiWidget.DeviceScale;
				var expand = stroke / 2;
				var rect = new RoundedRect(bounds.Left + expand,
					bounds.Bottom + expand,
					bounds.Right - expand,
					bounds.Top - expand);
				rect.radius(BackgroundRadius.SW,
					BackgroundRadius.SE,
					BackgroundRadius.NE,
					BackgroundRadius.NW);

				var rectOutline = new Stroke(rect, stroke);

				graphics2D.Render(rectOutline, theme.EditFieldColors.Focused.BorderColor);
			}
		}
	}

	public class SimpleFlowButton : FlowLayoutWidget
	{
		private bool mouseInBounds = false;

		protected ThemeConfig theme;

		public SimpleFlowButton(ThemeConfig theme)
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
				else if (mouseInBounds
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
		protected ImageWidget imageWidget;

		protected ImageBuffer image;

		private IconButton(ThemeConfig theme)
			: base(theme)
		{
		}

		public IconButton(ImageBuffer icon, ThemeConfig theme)
			: base(theme)
		{
			image = icon;
			this.HAnchor = HAnchor.Absolute;
			this.VAnchor = VAnchor.Absolute | VAnchor.Center;
			this.Height = theme.ButtonHeight;
			this.Width = theme.ButtonHeight;
			this.BackgroundRadius = theme.ButtonRadius;

			imageWidget = new ImageWidget(icon, listenForImageChanged: false)
			{
				HAnchor = HAnchor.Center,
				VAnchor = VAnchor.Center,
				Selectable = false
			};

			this.AddChild(imageWidget);
		}

		public ImageBuffer IconImage => this.Enabled ? image : this.DisabledImage;

		internal void SetIcon(ImageBuffer icon)
		{
			image = icon;
			imageWidget.Image = icon;
			_disabledImage = null;
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

		public override void OnEnabledChanged(EventArgs e)
		{
			imageWidget.Image = this.Enabled ? image : this.DisabledImage;
			this.Invalidate();

			base.OnEnabledChanged(e);
		}
	}

	public class RadioIconButton : IconButton, IRadioButton
	{
		public IList<GuiWidget> SiblingRadioButtonList { get; set; }

		public event EventHandler CheckedStateChanged;

		public bool ToggleButton { get; set; } = false;

		public RadioIconButton(ImageBuffer icon, ThemeConfig theme)
			: base(icon, theme)
		{
		}

		protected override void OnClick(MouseEventArgs mouseEvent)
		{
			base.OnClick(mouseEvent);

			bool newValue = this.ToggleButton ? !this.Checked : true;

			bool checkStateChanged = newValue != this.Checked;

			this.Checked = newValue;

			// After setting CheckedState, fire event if different
			if (checkStateChanged)
			{
				OnCheckStateChanged();
			}
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
						this.UncheckSiblings();
					}

					this.BackgroundColor = _checked ? theme.MinimalShade : Color.Transparent;

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
				if (BackgroundRadius.SW + BackgroundRadius.NW == Width)
				{
					void Render(double startRatio)
					{
						var stroke = 4 * GuiWidget.DeviceScale;
						var angle = MathHelper.Tau / 4;
						var start = MathHelper.Tau * startRatio - angle / 2;
						var end = MathHelper.Tau * startRatio + angle / 2;
						var arc = new Arc(Width / 2, Height / 2, Width / 2 - stroke / 2, Height / 2 - stroke / 2, start, end);
						var background = new Stroke(arc, stroke);
						graphics2D.Render(background, theme.PrimaryAccentColor.WithAlpha(100));
					}

					Render(1.0 / 3.0 + .75);
					Render(2.0 / 3.0 + .75);
					Render(1.0 + .75);
				}
				else
				{
					graphics2D.Rectangle(0, 0, LocalBounds.Right, 2 * DeviceScale, theme.PrimaryAccentColor);
				}
			}

			base.OnDraw(graphics2D);
		}
	}

	public class RadioTextButton : TextButton, IRadioButton
	{
		public IList<GuiWidget> SiblingRadioButtonList { get; set; }

		public event EventHandler CheckedStateChanged;

		public RadioTextButton(string text, ThemeConfig theme, double pointSize = -1)
			: base(text, theme, pointSize)
		{
			this.SelectedBackgroundColor = theme.SlightShade;
		}

		public override Color BackgroundColor
		{
			get
			{
				var firstWidgetUnderMouse = ContainsFirstUnderMouseRecursive();
				if (this.MouseCaptured
					&& firstWidgetUnderMouse
					&& this.Enabled)
				{
					if (Checked)
					{
						return SelectedBackgroundColor.AdjustLightness(.9).ToColor();
					}

					return this.MouseDownColor;
				}
				else if (firstWidgetUnderMouse
					&& this.Enabled)
				{
					if (Checked)
					{
						return SelectedBackgroundColor.AdjustLightness(.8).ToColor();
					}

					return this.HoverColor;
				}
				else
				{
					return base.BackgroundColor;
				}
			}
			set => base.BackgroundColor = value;
		}

		protected override void OnClick(MouseEventArgs mouseEvent)
		{
			base.OnClick(mouseEvent);

			bool newValue = true;

			bool checkStateChanged = newValue != this.Checked;

			this.Checked = newValue;

			// After setting CheckedState, fire event if different
			if (checkStateChanged)
			{
				OnCheckStateChanged();
			}
		}

		public Color SelectedBackgroundColor { get; set; }

		public Color UnselectedBackgroundColor { get; set; }

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
						this.UncheckSiblings();
					}

					OnCheckStateChanged();
				}

				this.BackgroundColor = _checked ? this.SelectedBackgroundColor : this.UnselectedBackgroundColor;
			}
		}

		public bool DrawUnderline { get; set; } = true;

		public override void OnMouseEnterBounds(MouseEventArgs mouseEvent)
		{
			base.OnMouseEnterBounds(mouseEvent);
			this.Invalidate();
		}

		public override void OnMouseLeaveBounds(MouseEventArgs mouseEvent)
		{
			base.OnMouseLeaveBounds(mouseEvent);
			this.Invalidate();
		}

		public virtual void OnCheckStateChanged()
		{
			CheckedStateChanged?.Invoke(this, null);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			if (this.Checked && DrawUnderline)
			{
				graphics2D.Rectangle(LocalBounds.Left, 0, LocalBounds.Right, 2, theme.PrimaryAccentColor);
			}

			base.OnDraw(graphics2D);
		}
	}

	public class TextButton : SimpleButton
	{
		private readonly TextWidget textWidget;

		public TextButton(string text, ThemeConfig theme, double pointSize = -1)
			: base(theme)
		{
			this.HAnchor = HAnchor.Fit;
			this.VAnchor = VAnchor.Absolute | VAnchor.Center;
			this.Height = theme.ButtonHeight;
			this.Padding = theme.TextButtonPadding;
			this.TabStop = true;

			this.BackgroundRadius = theme.ButtonRadius;

			var textSize = (pointSize != -1) ? pointSize : theme.DefaultFontSize;

			this.AddChild(textWidget = new TextWidget(text, pointSize: textSize, textColor: theme.TextColor)
			{
				HAnchor = HAnchor.Center,
				VAnchor = VAnchor.Center,
				AutoExpandBoundsToText = true
			});
		}

		public Color TextColor
		{
			get => textWidget.TextColor;
			set => textWidget.TextColor = value;
		}

		public override string Text
		{
			get => textWidget.Text;
			set => textWidget.Text = value;
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

	public class TextIconButton : SimpleFlowButton
	{
		private TextWidget textWidget;

		public bool DrawIconOverlayOnDisabled { get; set; } = false;

		public TextIconButton(string text, ImageBuffer icon, ThemeConfig theme)
			: base(theme)
		{
			this.HAnchor = HAnchor.Fit;
			this.VAnchor = VAnchor.Absolute | VAnchor.Center;
			this.Height = theme.ButtonHeight;
			this.Padding = theme.TextButtonPadding;

			this.BackgroundRadius = theme.ButtonRadius;

			this.AddChild(ImageWidget = new ImageWidget(icon)
			{
				VAnchor = VAnchor.Center,
				Selectable = false
			});

			// TODO: Only needed because TextWidget violates normal padding/margin rules
			var textContainer = new GuiWidget()
			{
				Padding = new BorderDouble(8, 4, 2, 4),
				HAnchor = HAnchor.Fit,
				VAnchor = VAnchor.Center | VAnchor.Fit,
				Selectable = false
			};
			this.AddChild(textContainer);

			textContainer.AddChild(textWidget = new TextWidget(text, pointSize: theme.DefaultFontSize, textColor: theme.TextColor));
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			base.OnDraw(graphics2D);

			// now draw an overlay on the image if it is disabled
			if (DrawIconOverlayOnDisabled && !ImageWidget.Enabled)
			{
				graphics2D.Render(new RoundedRect(ImageWidget.TransformToParentSpace(this, ImageWidget.LocalBounds), 0),
					theme.BackgroundColor.WithAlpha(200));
			}
		}

		public void SetIcon(ImageBuffer imageBuffer)
		{
			ImageWidget.Image = imageBuffer;
		}

		public ImageWidget ImageWidget { get; }

		public override string Text { get => textWidget.Text; set => textWidget.Text = value; }
	}

	public class HoverIconButton : IconButton
	{
		private ImageBuffer normalImage;

		private ImageBuffer hoverImage;

		// Single ImageBuffer constructor creates a grayscale copy for use as the normal image
		// and uses the original as the hover image
		public HoverIconButton(ImageBuffer icon, ThemeConfig theme)
			: this(MakeGrayscale(icon), icon, theme)
		{
		}

		public HoverIconButton(ImageBuffer icon, ImageBuffer hoverIcon, ThemeConfig theme)
			: base(icon, theme)
		{
			normalImage = icon;
			hoverImage = hoverIcon;

			this.HAnchor = HAnchor.Absolute;
			this.VAnchor = VAnchor.Absolute | VAnchor.Center;
			this.Height = theme.ButtonHeight;
			this.Width = theme.ButtonHeight;

			imageWidget = new ImageWidget(icon, listenForImageChanged: false)
			{
				HAnchor = HAnchor.Center,
				VAnchor = VAnchor.Center,
			};

			this.AddChild(imageWidget);
		}

		public static ImageBuffer MakeGrayscale(ImageBuffer icon)
		{
			var hoverIcon = new ImageBuffer(icon);
			ApplicationController.Instance.MakeGrayscale(hoverIcon);

			return hoverIcon;
		}

		public override void OnMouseEnterBounds(MouseEventArgs mouseEvent)
		{
			imageWidget.Image = hoverImage;

			base.OnMouseEnterBounds(mouseEvent);
			this.Invalidate();
		}

		public override void OnMouseLeaveBounds(MouseEventArgs mouseEvent)
		{
			imageWidget.Image = normalImage;

			base.OnMouseLeaveBounds(mouseEvent);
			this.Invalidate();
		}
	}
}