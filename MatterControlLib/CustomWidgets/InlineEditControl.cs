/*
Copyright (c) 2019, Lars Brubaker, John Lewin
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
using System.Diagnostics;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;

namespace MatterHackers.MatterControl.CustomWidgets
{
	public class InlineEditControl : GuiWidget
	{
		private TextWidget numberDisplay;
		private MHNumberEdit numberEdit;
		private Func<double, string> _getDisplayString = (value) => "{0:0.0}".FormatWith(value);
		private RunningInterval runningInterval;
		private ThemeConfig theme;

		public InlineEditControl(string defaultSizeString = "-0000.00", Agg.Font.Justification justification = Agg.Font.Justification.Left)
		{
			theme = AppContext.Theme;
			base.Visible = false;

			double pointSize = 12;

			numberDisplay = new TextWidget(defaultSizeString, 0, 0, pointSize, justification: justification, textColor: theme.TextColor)
			{
				Visible = false,
				VAnchor = VAnchor.Bottom,
				HAnchor = HAnchor.Left,
				Text = "0",
			};
			AddChild(numberDisplay);

			numberEdit = new MHNumberEdit(0, theme, pixelWidth: numberDisplay.Width, allowNegatives: true, allowDecimals: true)
			{
				Visible = false,
				VAnchor = VAnchor.Bottom,
				HAnchor = HAnchor.Left,
				SelectAllOnFocus = true,
			};
			numberEdit.ActuallNumberEdit.InternalNumberEdit.TextChanged += (s, e) =>
			{
				numberDisplay.Text = GetDisplayString == null ? "None" : GetDisplayString.Invoke(Value);
				base.OnTextChanged(e);
			};
			numberEdit.ActuallNumberEdit.InternalNumberEdit.MaxDecimalsPlaces = 2;

			numberEdit.ActuallNumberEdit.EditComplete += (s, e) =>
			{
				EditComplete?.Invoke(this, e);
				timeSinceMouseUp.Restart();
				numberEdit.Visible = false;
				numberDisplay.Visible = true;
			};

			AddChild(numberEdit);

			VAnchor = VAnchor.Fit;
			HAnchor = HAnchor.Fit;

			runningInterval = UiThread.SetInterval(HideIfApplicable, .1);
		}

		public event EventHandler EditComplete;

		public bool Editing
		{
			get
			{
				return this.Visible && ((numberEdit.Visible && numberEdit.ContainsFocus) || this.UnderMouseState != UnderMouseState.NotUnderMouse);
			}
		}

		public Func<bool> ForceHide { get; set; }

		public Func<double, string> GetDisplayString
		{
			get => _getDisplayString;
			set
			{
				_getDisplayString = value;
				if (_getDisplayString != null)
				{
					numberDisplay.Text = _getDisplayString?.Invoke(Value);
				}
			}
		}

		public double Value
		{
			get => numberEdit.Value;
			set
			{
				if (!numberEdit.ContainsFocus
					&& numberEdit.Value != value)
				{
					timeSinceMouseUp.Restart();
					numberEdit.Value = value;
				}
			}
		}

		public override bool Visible
		{
			get => base.Visible;
			set
			{
				if (value)
				{
					timeSinceMouseUp.Restart();
				}
				else
				{
					timeSinceMouseUp.Reset();
				}

				if (base.Visible != value)
				{
					base.Visible = value;
					this.StopEditing();
				}
			}
		}

		public void StopEditing()
		{
			numberEdit.Visible = false;
			numberDisplay.Visible = true;
			Invalidate();
		}

		protected double SecondsToShowNumberEdit { get; private set; } = 4;

		protected Stopwatch timeSinceMouseUp { get; private set; } = new Stopwatch();

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			if (mouseEvent.Button == MouseButtons.Left
				&& (UnderMouseState == UnderMouseState.UnderMouseNotFirst
				|| UnderMouseState == UnderMouseState.FirstUnderMouse))
			{
				numberEdit.Visible = true;
				numberDisplay.Visible = false;
				numberEdit.Focus();
			}

			base.OnMouseDown(mouseEvent);
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			if (UnderMouseState == UnderMouseState.UnderMouseNotFirst
				|| UnderMouseState == UnderMouseState.FirstUnderMouse)
			{
				if (numberDisplay.TextColor != theme.PrimaryAccentColor)
				{
					numberDisplay.TextColor = theme.PrimaryAccentColor;
				}
			}
			else
			{
				if (numberDisplay.TextColor != theme.TextColor)
				{
					numberDisplay.TextColor = theme.TextColor;
				}
			}

			base.OnMouseMove(mouseEvent);
		}

		public override void OnClosed(EventArgs e)
		{
			// Unregister listeners
			UiThread.ClearInterval(runningInterval);

			base.OnClosed(e);
		}

		private void HideIfApplicable()
		{
			if (this.Visible)
			{
				if (!this.Editing
					&& timeSinceMouseUp.IsRunning
					&& timeSinceMouseUp.ElapsedMilliseconds > SecondsToShowNumberEdit * 1000)
				{
					Visible = false;
				}
				else if (this.ForceHide?.Invoke() == true)
				{
					// Hide if custom ForceHide implementations say to do so
					this.Visible = false;
				}
			}
		}
	}
}