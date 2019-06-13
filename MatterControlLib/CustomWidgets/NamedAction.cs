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
using MatterHackers.Agg.Image;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl;

namespace MatterHackers.Agg.UI
{
	public class SceneSelectionOperation
	{
		public Action<ISceneContext> Action { get; set; }

		public Func<InteractiveScene, bool> IsEnabled { get; set; }

		public Type OperationType { get; set; }

		public Func<bool, ImageBuffer> Icon { get; set; }

		public Func<string> TitleResolver { get; set; }

		public string Title => this.TitleResolver?.Invoke();

		public Func<string> HelpTextResolver { get; set; }

		public string HelpText => this.HelpTextResolver?.Invoke();
	}

	public class SceneSelectionSeparator : SceneSelectionOperation
	{
	}

	public class OperationGroup : SceneSelectionOperation
	{
		public List<SceneSelectionOperation> Operations { get; set; } = new List<SceneSelectionOperation>();

		public bool StickySelection { get; internal set; }

		public string GroupName { get; set; }
	}

}