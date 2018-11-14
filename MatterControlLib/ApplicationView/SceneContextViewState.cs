/*
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

using System;

namespace MatterHackers.MatterControl
{
	using MatterHackers.DataConverters3D;
	using MatterHackers.RenderOpenGl;

	public class SceneContextViewState
	{
		private BedConfig sceneContext;
		private RenderTypes renderType = RenderTypes.Outlines;

		public SceneContextViewState(BedConfig sceneContext)
		{
			this.sceneContext = sceneContext;

			// Make sure the render mode is set correctly
			string renderTypeString = UserSettings.Instance.get(UserSettingsKey.defaultRenderSetting);
			if (renderTypeString == null)
			{
				renderTypeString = "Outlines";
				UserSettings.Instance.set(UserSettingsKey.defaultRenderSetting, renderTypeString);
			}

			if (Enum.TryParse(renderTypeString, out renderType))
			{
				this.RenderType = renderType;
			}
		}

		public bool ModelView { get; set; } = true;

		public RenderTypes RenderType
		{
			get => this.ModelView ? renderType : RenderTypes.Shaded;
			set
			{
				if (renderType != value)
				{
					renderType = value;

					// Persist value
					UserSettings.Instance.set(UserSettingsKey.defaultRenderSetting, renderType.ToString());

					foreach (var renderTransfrom in sceneContext.Scene.VisibleMeshes())
					{
						renderTransfrom.Mesh.MarkAsChanged();
					}
				}
			}
		}

		public double SceneTreeRatio
		{
			get
			{
				if (double.TryParse(UserSettings.Instance.get(UserSettingsKey.SceneTreeRatio), out double treeRatio))
				{
					return treeRatio;
				}

				return .75;
			}
			set
			{
				UserSettings.Instance.set(UserSettingsKey.SceneTreeRatio, value.ToString());
			}
		}

		public double SelectedObjectEditorHeight
		{
			get
			{
				if (double.TryParse(UserSettings.Instance.get(UserSettingsKey.SelectedObjectEditorHeight), out double controlHeight))
				{
					return Math.Max(controlHeight, 35);
				}

				return 120;
			}
			set
			{
				var minimumValue = Math.Max(value, 35);
				UserSettings.Instance.set(UserSettingsKey.SelectedObjectEditorHeight, minimumValue.ToString());
			}
		}
	}
}