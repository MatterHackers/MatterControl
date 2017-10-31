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
using System.Diagnostics;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.MeshVisualizer;

namespace MatterHackers.MatterControl.CustomWidgets
{
	public class ValueDisplayInfo : GuiWidget
	{
		private TextWidget numberDisplay;
		private NumberEdit numberEdit;

		public ValueDisplayInfo(string defaultSizeString = "-0000.00", Agg.Font.Justification justification = Agg.Font.Justification.Left)
		{
			double pointSize = 12;
			numberDisplay = new TextWidget(defaultSizeString, 0, 0, pointSize, justification: justification)
			{
				VAnchor = VAnchor.Bottom,
				HAnchor = HAnchor.Left,
				Text = "0",
			};
			AddChild(numberDisplay);
			numberEdit = new NumberEdit(0, 50, 50, pointSize, pixelWidth: numberDisplay.Width, allowNegatives: true, allowDecimals: true)
			{
				Visible = false,
				VAnchor = VAnchor.Bottom,
				HAnchor = HAnchor.Left,
				SelectAllOnFocus = true,
			};
			numberEdit.InternalNumberEdit.TextChanged += (s, e) =>
			{
				numberDisplay.Text = GetDisplayString == null ? "None" : GetDisplayString.Invoke(Value);
				base.OnTextChanged(e);
			};
			numberEdit.InternalNumberEdit.MaxDecimalsPlaces = 2;

			numberEdit.EditComplete += (s, e) =>
			{
				EditComplete?.Invoke(this, e);
				timeSinceMouseUp.Restart();
				numberEdit.Visible = false;
				numberDisplay.Visible = true;
			};

			AddChild(numberEdit);

			VAnchor = VAnchor.Fit;
			HAnchor = HAnchor.Fit;

			UiThread.RunOnIdle(CheckControlsVisibility, .1);
		}

		public event EventHandler EditComplete;

		public bool Editing
		{
			get
			{
				return this.Visible && (numberEdit.Visible || this.UnderMouseState != UnderMouseState.NotUnderMouse);
			}
		}

		public Func<bool> ForceHide { get; set; }

		Func<double, string> _GetDisplayString = (value) => "{0:0.0}".FormatWith(value);
		public Func<double, string> GetDisplayString
		{
			get { return _GetDisplayString; }
			set
			{
				_GetDisplayString = value;
				if (GetDisplayString != null)
				{
					numberDisplay.Text = GetDisplayString?.Invoke(Value);
				}
			}
		}

		public double Value
		{
			get
			{
				return numberEdit.Value;
			}
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
					numberEdit.Visible = false;
					numberDisplay.Visible = true;
				}
			}
		}

		protected double SecondsToShowNumberEdit { get; private set; } = 4;
		protected Stopwatch timeSinceMouseUp { get; private set; } = new Stopwatch();

		public override void OnDraw(Graphics2D graphics2D)
		{
			if (UnderMouseState == UnderMouseState.UnderMouseNotFirst
				|| UnderMouseState == UnderMouseState.FirstUnderMouse)
			{
				numberDisplay.TextColor = Color.Red;
			}
			else
			{
				numberDisplay.TextColor = Color.Black;
			}
			base.OnDraw(graphics2D);
		}

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

		private void CheckControlsVisibility()
		{
			if (!this.Editing)
			{
				if (timeSinceMouseUp.IsRunning)
				{
					if (timeSinceMouseUp.ElapsedMilliseconds > SecondsToShowNumberEdit * 1000)
					{
						if (this.Editing)
						{
						}
						else if (timeSinceMouseUp.IsRunning)
						{
							Visible = false;
						}
					}
				}
			}

			if (Visible && ForceHide?.Invoke() == true)
			{
				// If the user is hovering on a different control
				Visible = false;
			}

			UiThread.RunOnIdle(CheckControlsVisibility, .1);
		}
	}
}