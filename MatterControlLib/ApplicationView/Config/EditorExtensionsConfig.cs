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

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Matter_CAD_Lib.DesignTools._Object3D;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl.Library;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MeshVisualizer;

[assembly: InternalsVisibleTo("MatterControl.Tests")]
[assembly: InternalsVisibleTo("MatterControl.AutomationTests")]
[assembly: InternalsVisibleTo("CloudServices.Tests")]

namespace MatterHackers.MatterControl
{
    public class EditorExtensionsConfig
	{
		private LibraryConfig libraryConfig;

		// private List<IObject3DEditor> _IObject3DEditorProviders = new List<IObject3DEditor>()
		// {
		// 	new IntersectionEditor(),
		// 	new SubtractEditor(),
		// 	new SubtractAndReplace()
		// };

		public EditorExtensionsConfig(LibraryConfig libraryConfig)
		{
			this.libraryConfig = libraryConfig;

			objectEditorsByType = new Dictionary<Type, HashSet<Func<ThemeConfig, UndoBuffer, IObjectEditor>>>();
		}

		private void MapTypesToEditorFactory(Func<ThemeConfig, UndoBuffer, IObjectEditor> object3DEditorFactory)
		{
			var editor = object3DEditorFactory.Invoke(null, null);

            foreach (Type type in editor.SupportedTypes())
			{
				if (!objectEditorsByType.TryGetValue(type, out HashSet<Func<ThemeConfig, UndoBuffer, IObjectEditor>> mappedEditorsFactories))
				{
					mappedEditorsFactories = new HashSet<Func<ThemeConfig, UndoBuffer, IObjectEditor>>();
					objectEditorsByType.Add(type, mappedEditorsFactories);
				}

				mappedEditorsFactories.Add(object3DEditorFactory);
			}
		}

		public void RegisterFactory(Func<ThemeConfig, UndoBuffer, IObjectEditor> object3DEditorFactory)
		{
			this.MapTypesToEditorFactory(object3DEditorFactory);
		}

		private Dictionary<Type, HashSet<Func<ThemeConfig, UndoBuffer, IObjectEditor>>> objectEditorsByType;

		public HashSet<Func<ThemeConfig, UndoBuffer, IObjectEditor>> GetEditorsForType(Type selectedItemType)
		{
			HashSet<Func<ThemeConfig, UndoBuffer, IObjectEditor>> mappedEditors;
			objectEditorsByType.TryGetValue(selectedItemType, out mappedEditors);

			if (mappedEditors == null)
			{
				foreach (var kvp in objectEditorsByType)
				{
					var editorType = kvp.Key;

					if (editorType.IsAssignableFrom(selectedItemType)
						&& selectedItemType != typeof(Object3D))
					{
						mappedEditors = kvp.Value;
						break;
					}
				}
			}

			return mappedEditors;
		}
	}
}
