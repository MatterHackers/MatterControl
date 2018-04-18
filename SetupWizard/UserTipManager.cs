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

using System;
using System.Linq;
using MatterHackers.Agg.UI;

namespace MatterHackers.MatterControl.SetupWizard
{
	public class UserTipManager
	{
		private GuiWidget widgetToExplain;
		private static UserTipManager _instance;
		public static UserTipManager Instance
		{
			get
			{
				if (_instance == null)
				{
					_instance = new UserTipManager();
				}
				return _instance;
			}
		}

		private UserTipManager()
		{

		}

		public void ShowTip(SystemWindow systemWindow, string widgetName, string extruder0TipMessage)
		{
			widgetToExplain = systemWindow.Descendants().Where((w) => w.Name == widgetName).FirstOrDefault();
#if DEBUG
			if(widgetToExplain == null)
			{
				throw new Exception("Can't find the named widget");
			}
#endif
			if (widgetToExplain != null)
			{
				widgetToExplain.AfterDraw -= DoShowTip;
			}
			// hook the widget draw and wait for it to draw so that we know it is visible
			widgetToExplain.AfterDraw += DoShowTip;
			widgetToExplain.Invalidate();
		}

		private void DoShowTip(object sender, DrawEventArgs e)
		{
			if (widgetToExplain != null)
			{
				widgetToExplain.AfterDraw -= DoShowTip;
			}
		}
	}
}