﻿/*
Copyright (c) 2018, Lars Brubaker, John Lewin
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

using MatterHackers.Agg.UI;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
	public class TextField : UIField
	{
		protected ThemedTextEditWidget textEditWidget;
		private ThemeConfig theme;

		public TextField(ThemeConfig theme)
		{
			this.theme = theme;
		}

		public string TextValue
		{
			get { return textEditWidget.Text; }
			set
			{
				if (textEditWidget.Text != value)
				{
					textEditWidget.Text = value;
				}
			}
		}

		public override void Initialize(int tabIndex)
		{
			textEditWidget = new ThemedTextEditWidget("", theme, pixelWidth: ControlWidth, tabIndex: tabIndex)
			{
				ToolTipText = this.HelpText,
				SelectAllOnFocus = true,
				Name = this.Name,
			};
			textEditWidget.ActualTextEditWidget.EditComplete += (s, e) =>
			{
				if (this.Value != textEditWidget.Text)
				{
					this.SetValue(
						textEditWidget.Text,
						userInitiated: true);
				}
			};

			this.Content = textEditWidget;
		}

		protected override void OnValueChanged(FieldChangedEventArgs fieldChangedEventArgs)
		{
			if (this.Value != textEditWidget.Text)
			{
				textEditWidget.Text = this.Value;
			}

			base.OnValueChanged(fieldChangedEventArgs);
		}
	}

	public class ReadOnlyTextField : UIField
	{
		TextWidget textWidget;
		private ThemeConfig theme;

		public ReadOnlyTextField(ThemeConfig theme)
		{
			this.theme = theme;
		}

		public override void Initialize(int tabIndex)
		{
			textWidget = new TextWidget("", textColor: theme.TextColor, pointSize: theme.DefaultFontSize)
			{
				TabIndex = tabIndex,
				ToolTipText = this.HelpText,
				AutoExpandBoundsToText = true,
				Name = this.Name,
			};

			this.Content = textWidget;
		}

		protected override void OnValueChanged(FieldChangedEventArgs fieldChangedEventArgs)
		{
			if (this.Value != textWidget.Text)
			{
				textWidget.Text = this.Value;
			}

			base.OnValueChanged(fieldChangedEventArgs);
		}
	}
}
