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

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.DataConverters3D;
using MatterHackers.PolygonMesh;

namespace MatterHackers.MatterControl.PartPreviewWindow.View3D
{
	public class IntersectGroup : Object3D
	{
		public IntersectGroup()
		{
		}

		public IntersectGroup(SafeList<IObject3D> children)
		{
			Children.Modify((list) =>
			{
				foreach (var child in children)
				{
					list.Add(child);
				}
			});

			bool first = true;
			// Wrap every first descendant that has a mesh
			foreach (var child in this.VisibleMeshes().ToList())
			{
				// wrap the child in a IntersectItem
				child.Parent.Children.Modify((list) =>
				{
					list.Remove(child);
					list.Add(new IntersectItem(child, this.ID, !first));
					first = false;
				});
			}

			ProcessBooleans();
		}

		private async void ProcessBooleans()
		{
			// spin up a task to remove holes from the objects in the group
			await Task.Run(() =>
			{
				var container = this;
				var participants = this.VisibleMeshes().Where((obj) => obj.OwnerID == this.ID);
				var removeObjects = participants.Where((obj) => obj.OutputType == PrintOutputTypes.Hole);
				var keepObjects = participants.Where((obj) => obj.OutputType != PrintOutputTypes.Hole);

				if (removeObjects.Any()
					&& keepObjects.Any())
				{
					foreach (var remove in removeObjects)
					{
						foreach (var keep in keepObjects)
						{
							var transformedRemove = Mesh.Copy(remove.Mesh, CancellationToken.None);
							transformedRemove.Transform(remove.WorldMatrix());

							var transformedKeep = Mesh.Copy(keep.Mesh, CancellationToken.None);
							transformedKeep.Transform(keep.WorldMatrix());

							transformedKeep = PolygonMesh.Csg.CsgOperations.Intersect(transformedKeep, transformedRemove);
							var inverse = keep.WorldMatrix();
							inverse.Invert();
							transformedKeep.Transform(inverse);
							keep.Mesh = transformedKeep;
						}

						remove.Visible = false;
					}
				}
			});
		}
	}

	public class IntersectItem : MeshWrapper
	{
		public IntersectItem()
		{
		}

		public IntersectItem(IObject3D child, string ownerId, bool makeHole)
			: base(child, ownerId)
		{
			if (makeHole)
			{
				OutputType = PrintOutputTypes.Hole;
			}
		}
	}
}