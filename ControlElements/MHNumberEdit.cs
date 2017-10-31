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

using MatterHackers.Agg;
using MatterHackers.Agg.UI;

namespace MatterHackers.MatterControl
{
	public class MHNumberEdit : GuiWidget
	{
		public NumberEdit ActuallNumberEdit { get; }

		public MHNumberEdit(double startingValue, double x = 0, double y = 0, double pointSize = 12, double pixelWidth = 0, double pixelHeight = 0, bool allowNegatives = false, bool allowDecimals = false, double minValue = int.MinValue, double maxValue = int.MaxValue, double increment = 1, int tabIndex = 0)
		{
			this.Padding = new BorderDouble(3);
			this.BackgroundColor = Color.White;
			this.HAnchor = HAnchor.Fit;
			this.VAnchor = VAnchor.Fit;

			this.ActuallNumberEdit = new NumberEdit(startingValue, x, y, pointSize, pixelWidth, pixelHeight, allowNegatives, allowDecimals, minValue, maxValue, increment, tabIndex)
			{
				VAnchor = VAnchor.Bottom
			};
			this.AddChild(this.ActuallNumberEdit);
		}

		public override int TabIndex
		{
			// TODO: This looks invalid - setter and getter should use same context
			get => base.TabIndex;
			set => this.ActuallNumberEdit.TabIndex = value;
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			base.OnDraw(graphics2D);
			if (ContainsFocus)
			{
				graphics2D.Rectangle(LocalBounds, Color.Orange);
			}
		}

		public double Value
		{
			get => this.ActuallNumberEdit.Value;
			set => this.ActuallNumberEdit.Value = value;
		}

		public override string Text
		{
			get => this.ActuallNumberEdit.Text;
			set => this.ActuallNumberEdit.Text = value;
		}

		public bool SelectAllOnFocus
		{
			get => this.ActuallNumberEdit.InternalNumberEdit.SelectAllOnFocus;
			set => this.ActuallNumberEdit.InternalNumberEdit.SelectAllOnFocus = value;
		}
	}
}