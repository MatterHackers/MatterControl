/*
Copyright (c) 2015, Kevin Pope
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

using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using System.Collections.Generic;
using MatterHackers.MatterControl.SettingsManagement;
using System.IO;
using System.Linq;
using MatterHackers.Agg;
using System;

namespace MatterHackers.MatterControl
{
	public class BoundDropList : DropDownList
	{
		private List<KeyValuePair<string, string>> listSource;

		public BoundDropList(string noSelectionString, int maxHeight = 0)
			: base(noSelectionString, maxHeight: maxHeight)
		{
		}

		public List<KeyValuePair<string, string>> ListSource
		{
			get
			{
				return listSource;
			}
			set
			{
				if (listSource == value)
				{
					return;
				}

				MenuItems.Clear();
				SelectedIndex = -1;

				listSource = value;

				foreach (var keyValue in listSource)
				{
					AddItem(keyValue.Key, keyValue.Value);
				}

				Invalidate();
			}
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			base.OnDraw(graphics2D);

			if (Focused)
			{
				graphics2D.Rectangle(LocalBounds, RGBA_Bytes.Orange);
			}
		}
	}
}
 