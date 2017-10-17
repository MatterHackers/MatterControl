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

namespace MatterHackers.MatterControl.PartPreviewWindow.View3D
{
	public class DifferenceItem : Object3D
	{
		public DifferenceItem(IObject3D child, bool keep)
		{
			this.Keep = keep;
			Children.Add(child);
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
					list.Add(new DifferenceItem(child, first));
					first = false;
				});
			}
		}

		async void ProcessBooleans()
		{
			// spin up a task to remove holes from the objects in the group
			await Task.Run(() =>
			{
				var holes = this.Children.Where(obj => obj.OutputType == PrintOutputTypes.Hole).ToList();
				if (holes.Any())
				{
					var itemsToReplace = new List<(IObject3D object3D, Mesh newMesh)>();
					foreach (var hole in holes)
					{
						var transformedHole = Mesh.Copy(hole.Mesh, CancellationToken.None);
						transformedHole.Transform(hole.Matrix);

						var stuffToModify = this.Children.Where(obj => obj.OutputType != PrintOutputTypes.Hole && obj.Mesh != null).ToList();
						foreach (var object3D in stuffToModify)
						{
							var transformedObject = Mesh.Copy(object3D.Mesh, CancellationToken.None);
							transformedObject.Transform(object3D.Matrix);

							var newMesh = PolygonMesh.Csg.CsgOperations.Subtract(transformedObject, transformedHole);
							if (newMesh != object3D.Mesh)
							{
								itemsToReplace.Add((object3D, newMesh));
							}
						}

						foreach (var x in itemsToReplace)
						{
							this.Children.Remove(x.object3D);

							var newItem = new Object3D()
							{
								Mesh = x.newMesh,

								// Copy over child properties...
								OutputType = x.object3D.OutputType,
								Color = x.object3D.Color,
								MaterialIndex = x.object3D.MaterialIndex
							};
							newItem.Children.Add(x.object3D);

							this.Children.Add(newItem);
						}
					}
				}
			});
		}
	}
}
