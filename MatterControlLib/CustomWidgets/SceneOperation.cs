﻿/*
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
using MatterHackers.MatterControl;

namespace MatterHackers.Agg.UI
{
	public class SceneOperation
	{
		public SceneOperation(string id)
		{
			this.Id = id;
		}

		public string Id { get; }

		public Action<ISceneContext> Action { get; set; }

		/// <summary>
		/// Gets or sets the type this operation outputs
		/// </summary>
		public Type ResultType { get; set; }

		public Func<ThemeConfig, ImageBuffer> Icon { get; set; }

		public Func<ISceneContext, bool> IsEnabled { get; set; } = (sceneContext) => true;

		public Func<string> HelpTextGetter { get; set; }

		public string HelpText => this.HelpTextGetter?.Invoke();

		public string UiHint { get; set; }

		public Func<string> TitleGetter { get; set; }

		public string Title => this.TitleGetter?.Invoke();

		/// <summary>
		/// Gets or sets the type that this operation can be applied to
		/// </summary>
		public Type OperationType { get; set; }

		/// <summary>
		/// Gets or sets if this operation should be shown in right click and modify menu.
		/// </summary>
		public Func<ISceneContext, bool> ShowInModifyMenu { get; set; }
	}

	public class SceneSelectionSeparator : SceneOperation
	{
		public SceneSelectionSeparator()
			: base(null)
		{
		}
	}

	public class OperationGroup : SceneOperation
	{
		public OperationGroup(string id)
			: base(id)
		{
		}

		public List<SceneOperation> Operations { get; set; } = new List<SceneOperation>();

		public int InitialSelectionIndex { get; set; } = 0;

		public string GroupRecordId => $"ActiveButton_{this.Id}_Group";

		public EventHandler CollapseChanged;

		private static string CollapseKey(string operationGroupName)
		{
			return $"scene_operation_collapse_{operationGroupName}";
		}

		public bool Collapse
		{
			get
			{
				return UserSettings.Instance.Fields.GetBool(CollapseKey(this.Id), true);
			}

			set
			{
				if (Collapse != value)
				{
					UserSettings.Instance.Fields.SetBool(CollapseKey(Id), value);
					CollapseChanged?.Invoke(this, null);
				}
			}
		}

		public EventHandler VisibleChanged;

		private static string VisibleKey(string operationGroupName)
		{
			return $"scene_operation_visible_{operationGroupName}";
		}

		public static bool GetVisible(string id, bool defaultIfNotSet)
		{
			return UserSettings.Instance.Fields.GetBool(VisibleKey(id), defaultIfNotSet);
		}

		public bool Visible
		{
			get
			{
				return GetVisible(this.Id, true);
			}

			set
			{
				if (Visible != value)
				{
					UserSettings.Instance.Fields.SetBool(VisibleKey(Id), value);
					VisibleChanged?.Invoke(this, null);
				}
			}
		}

		public SceneOperation GetDefaultOperation()
		{
			if (!int.TryParse(UserSettings.Instance.get(GroupRecordId), out int activeButtonID))
			{
				activeButtonID = InitialSelectionIndex;
			}

			activeButtonID = agg_basics.Clamp(activeButtonID, 0, this.Operations.Count - 1);

			return this.Operations[activeButtonID];
		}
	}
}