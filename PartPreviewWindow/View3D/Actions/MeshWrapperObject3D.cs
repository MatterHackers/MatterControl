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

using System.Collections.Generic;
using System.Linq;
using MatterHackers.DataConverters3D;
using MatterHackers.DataConverters3D.UndoCommands;
using MatterHackers.PolygonMesh;

namespace MatterHackers.MatterControl.PartPreviewWindow.View3D
{
	public class MeshWrapperObject3D : Object3D
	{
		public MeshWrapperObject3D()
		{
		}

		public override bool CanBake => true;
		public override bool CanRemove => true;

		public override void Remove()
		{
			// remove all the mesh wrappers that we own
			var meshWrappers = this.Descendants().Where(o => o.OwnerID == this.ID).ToList();
			foreach(var meshWrapper in meshWrappers)
			{
				meshWrapper.Remove();
			}
			foreach(var child in Children)
			{
				child.OutputType = PrintOutputTypes.Default;
			}
			// collapes our children into our parent
			base.Remove();
		}

		public override void Bake()
		{
			var meshWrappers = this.Descendants().Where(o => o.OwnerID == this.ID).ToList();

			// remove all the meshWrappers (collapse the children)
			foreach(var meshWrapper in meshWrappers)
			{
				if (meshWrapper.Visible)
				{
					// clear the children
					meshWrapper.Children.Modify(list =>
					{
						list.Clear();
					});
					meshWrapper.OwnerID = null;
				}
				else
				{
					// remove it
					meshWrapper.Parent.Children.Remove(meshWrapper);
				}
			}

			base.Bake();
		}

		public static void WrapSelection(MeshWrapperObject3D meshWrapper, InteractiveScene scene)
		{
			if (scene.HasSelection)
			{
				var selectedItem = scene.SelectedItem;
				scene.SelectedItem = null;

				List<IObject3D> originalItems;

				if (selectedItem is SelectionGroup)
				{
					originalItems = selectedItem.Children.ToList();
				}
				else
				{
					originalItems = new List<IObject3D> { selectedItem };
				}

				var itemsToAdd = new List<IObject3D>(originalItems.Select((i) => i.Clone()));
				meshWrapper.WrapAndAddAsChildren(itemsToAdd);

				scene.UndoBuffer.AddAndDo(
					new ReplaceCommand(
						new List<IObject3D>(originalItems),
						new List<IObject3D> { meshWrapper }));

				meshWrapper.MakeNameNonColliding();
				scene.SelectedItem = meshWrapper;
			}
		}

		public void WrapAndAddAsChildren(List<IObject3D> children)
		{
			Children.Modify((list) =>
			{
				list.Clear();

				foreach (var child in children)
				{
					list.Add(child);
				}
			});

			// Wrap every first descendant that has a mesh
			foreach (var child in this.VisibleMeshes().ToList())
			{
				// wrap the child
				child.object3D.Parent.Children.Modify((list) =>
				{
					list.Remove(child.object3D);
					list.Add(new MeshWrapper(child.object3D, this.ID));
				});
			}
		}

		public void ResetMeshWrappers()
		{
			this.Mesh = null;
			var participants = this.Descendants().Where(o => o.OwnerID == this.ID).ToList();
			foreach (var item in participants)
			{
				item.Visible = true;
				// set the mesh back to the child mesh
				item.Mesh = item.Children.First().Mesh;
				// and set the color back
				item.Color = item.Children.First().Color;
			}
		}
	}
}