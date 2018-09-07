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

using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.DataConverters3D.UndoCommands;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DesignTools.Operations;
using System.Collections.Generic;
using MatterHackers.Localizations;

namespace MatterHackers.MatterControl.DesignTools
{
	public class ComponentObject3D : Object3D, IVisualLeafNode
	{
		public ComponentObject3D()
		{
		}

		public ComponentObject3D(IEnumerable<IObject3D> children)
			: base(children)
		{
		}

		public override bool CanApply => Finalized;
		public bool Finalized { get; set; } = true;
		public List<string> SurfacedEditors { get; set; } = new List<string>();
		public string ComponentID { get; set; }

		public override void Apply(UndoBuffer undoBuffer)
		{
			// we want to end up with just a group of all the visible mesh objects
			using (RebuildLock())
			{
				List<IObject3D> newChildren = new List<IObject3D>();

				// push our matrix into a copy of our visible children
				foreach (var child in this.VisibleMeshes())
				{
					var meshOnlyItem = new Object3D();
					meshOnlyItem.Matrix = child.WorldMatrix(this);
					meshOnlyItem.Color = child.WorldColor(this);
					meshOnlyItem.MaterialIndex = child.WorldMaterialIndex(this);
					meshOnlyItem.OutputType = child.WorldOutputType(this);
					meshOnlyItem.Mesh = child.Mesh;
					meshOnlyItem.Name = "Mesh".Localize();
					newChildren.Add(meshOnlyItem);
				}

				if(newChildren.Count > 1)
				{
					var group = new GroupObject3D();
					group.Name = this.Name;
					group.Children.Modify(list =>
					{
						list.AddRange(newChildren);
					});
					newChildren.Clear();
					newChildren.Add(group);
				}
				else if(newChildren.Count == 1)
				{
					newChildren[0].Name = this.Name;
				}

				// and replace us with the children
				undoBuffer.AddAndDo(new ReplaceCommand(new List<IObject3D> { this }, newChildren));
			}

			Invalidate(new InvalidateArgs(this, InvalidateType.Content, undoBuffer));
		}
	}
}