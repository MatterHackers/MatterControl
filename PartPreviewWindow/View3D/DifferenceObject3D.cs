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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.DataConverters3D;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow.View3D
{
	public class MeshObjectWrapper : Object3D
	{
		public MeshObjectWrapper()
		{
		}

		public MeshObjectWrapper(IObject3D child, string ownerId)
		{
			Children.Add(child);

			this.OwnerID = ownerId;
			this.MaterialIndex = child.MaterialIndex;
			this.ItemType = child.ItemType;
			this.OutputType = child.OutputType;
			this.Color = child.Color;
			this.Mesh = child.Mesh;

			this.Matrix = child.Matrix;
			child.Matrix = Matrix4X4.Identity;
		}
	}


	public class DifferenceItem : MeshObjectWrapper
	{
		public DifferenceItem()
		{
		}

		public DifferenceItem(IObject3D child, string ownerId, bool keep)
			: base(child, ownerId)
		{
			this.Keep = keep;
			if (!keep)
			{
				OutputType = PrintOutputTypes.Hole;
			}
		}

		public bool Keep { get; set; } = true;
	}

	public class DifferenceGroup : Object3D
	{
		public DifferenceGroup(SafeList<IObject3D> children)
		{
			Children.Modify((list) =>
			{
				foreach (var child in children)
				{
					list.Add(child);
				}
			});

			bool first = true;
			// now wrap every decendant that has a mesh
			foreach (var child in this.Descendants().Where((o) => o.Mesh != null))
			{
				// wrap the child in a DifferenceItem
				child.Parent.Children.Modify((list) =>
				{
					list.Remove(child);
					list.Add(new DifferenceItem(child, this.ID, first));
					first = false;
				});
			}

			ProcessBooleans();
		}

		async void ProcessBooleans()
		{
			// spin up a task to remove holes from the objects in the group
			await Task.Run(() =>
			{
				var container = this;
				var participants = this.Descendants().Where((obj) => obj.OwnerID == this.ID);
				var removeObjects = participants.Where((obj) => ((DifferenceItem)obj).Keep == false);
				var keepObjects = participants.Where((obj) => ((DifferenceItem)obj).Keep == true);

				if (removeObjects.Any()
					&& keepObjects.Any())
				{
					foreach (var keep in keepObjects)
					{
						foreach (var remove in removeObjects)
						{
							var transformedRemove = Mesh.Copy(remove.Mesh, CancellationToken.None);
							transformedRemove.Transform(GetMatrixToWrapper(remove));

							var transformedKeep = Mesh.Copy(keep.Mesh, CancellationToken.None);
							transformedKeep.Transform(GetMatrixToWrapper(keep));

							transformedKeep = PolygonMesh.Csg.CsgOperations.Subtract(transformedKeep, transformedRemove);
							var inverse = GetMatrixToWrapper(keep);
							inverse.Invert();
							transformedKeep.Transform(inverse);
							keep.Mesh = transformedKeep;
						}
					}
				}
			});
		}

		private Matrix4X4 GetMatrixToWrapper(IObject3D child)
		{
			var matrix = child.Matrix;
			var parent = child.Parent;
			while(parent.ID != child.OwnerID)
			{
				matrix *= parent.Matrix;
				parent = parent.Parent;
			}

			return matrix;
		}
	}
}
