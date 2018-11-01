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

using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.DataConverters3D.UndoCommands;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace MatterHackers.MatterControl.PartPreviewWindow.View3D
{
	public abstract class MeshWrapperObject3D : Object3D
	{
		public MeshWrapperObject3D()
		{
		}

		public override bool CanFlatten => true;

		public override void Flatten(UndoBuffer undoBuffer)
		{
			var meshWrappers = this.Descendants().Where(o => o.OwnerID == this.ID).ToList();

			// remove all the meshWrappers (collapse the children)
			foreach (var meshWrapper in meshWrappers)
			{
				var parent = meshWrapper.Parent;
				if (meshWrapper.Visible)
				{
					var newMesh = new Object3D()
					{
						Mesh = meshWrapper.Mesh
					};
					newMesh.CopyProperties(meshWrapper, Object3DPropertyFlags.All);
					newMesh.Name = this.Name;
					parent.Children.Add(newMesh);
				}

				// remove it
				parent.Children.Remove(meshWrapper);
			}

			base.Flatten(undoBuffer);
		}

		/// <summary>
		/// MeshWrapperObject3D overrides GetAabb so that it can only check the geometry that it has created
		/// </summary>
		/// <param name="matrix"></param>
		/// <returns></returns>
		public override AxisAlignedBoundingBox GetAxisAlignedBoundingBox(Matrix4X4 matrix)
		{
			AxisAlignedBoundingBox totalBounds = AxisAlignedBoundingBox.Empty();

			// This needs to be Descendants because we need to move past the first visible mesh to our owned objects
			foreach (var child in this.Descendants().Where(i => i.OwnerID == this.ID && i.Visible))
			{
				var childMesh = child.Mesh;
				if (childMesh != null)
				{
					// Add the bounds of each child object
					var childBounds = childMesh.GetAxisAlignedBoundingBox(child.WorldMatrix(this) * matrix);
					// Check if the child actually has any bounds
					if (childBounds.XSize > 0)
					{
						totalBounds += childBounds;
					}
				}
			}

			return totalBounds;
		}

		public IEnumerable<(IObject3D original, IObject3D meshCopy)> WrappedObjects()
		{
			return this.Descendants()
				.Where((obj) => obj.OwnerID == this.ID)
				.Select((mw) => (mw.Children.First(), mw));
		}

		public override void Remove(UndoBuffer undoBuffer)
		{
			using (RebuildLock())
			{
				// remove all the mesh wrappers that we own
				var meshWrappers = this.Descendants().Where(o => o.OwnerID == this.ID).ToList();
				foreach (var meshWrapper in meshWrappers)
				{
					meshWrapper.Remove(null);
				}
				foreach (var child in Children)
				{
					child.OutputType = PrintOutputTypes.Default;
				}

				// collapse our children into our parent
				base.Remove(null);
			}

			Invalidate(new InvalidateArgs(this, InvalidateType.Content));
		}

		public void ResetMeshWrapperMeshes(Object3DPropertyFlags flags, CancellationToken cancellationToken)
		{
			using (RebuildLock())
			{
				this.DebugDepth("Reset MWM");

				// Remove everything above the objects that have the meshes we are wrapping that are mesh wrappers
				var wrappers = this.Descendants().Where(o => o.OwnerID == this.ID).ToList();
				foreach (var wrapper in wrappers)
				{
					using (wrapper.RebuildLock())
					{
						var remove = wrapper.Parent;
						while (remove is ModifiedMeshObject3D)
						{
							var hold = remove;
							remove.Remove(null);
							remove = hold.Parent;
						}
					}
				}

				// if there are not already, wrap all meshes with our id (some inner object may have changed it's meshes)
				AddMeshWrapperToAllChildren();

				this.Mesh = null;
				var participants = this.Descendants().Where(o => o.OwnerID == this.ID).ToList();
				foreach (var item in participants)
				{
					var firstChild = item.Children.First();
					using (item.RebuildLock())
					{
						// set the mesh back to a copy of the child mesh
						item.Mesh = firstChild.Mesh.Copy(cancellationToken);
						// and reset the properties
						item.CopyProperties(firstChild, flags & (~Object3DPropertyFlags.Matrix));
					}
				}
			}
		}

		public void WrapSelectedItemAndSelect(InteractiveScene scene)
		{
			using (RebuildLock())
			{
				var selectedItems = scene.GetSelectedItems();

				if (selectedItems.Count > 0)
				{
					// clear the selected item
					scene.SelectedItem = null;

					WrapItems(selectedItems);

					scene.UndoBuffer.AddAndDo(
						new ReplaceCommand(
							new List<IObject3D>(selectedItems),
							new List<IObject3D> { this }));

					// and select this
					scene.SelectedItem = this;
				}
			}

			Invalidate(new InvalidateArgs(this, InvalidateType.Properties, null));
		}

		public void WrapItems(List<IObject3D> items)
		{
			using (RebuildLock())
			{
				var clonedItemsToAdd = new List<IObject3D>(items.Select((i) => i.Clone()));

				Children.Modify((list) =>
				{
					list.Clear();

					foreach (var child in clonedItemsToAdd)
					{
						list.Add(child);
					}
				});

				AddMeshWrapperToAllChildren();

				this.MakeNameNonColliding();
			}

			Invalidate(new InvalidateArgs(this, InvalidateType.Properties, null));
		}

		private void AddMeshWrapperToAllChildren()
		{
			// Wrap every first descendant that has a mesh
			foreach (var child in this.VisibleMeshes().ToList())
			{
				// have to check that NO child of the visible mesh has us as the parent id
				if (!child.DescendantsAndSelf().Where((c) => c.OwnerID == this.ID).Any())
				{
					// wrap the child
					child.Parent.Children.Modify((System.Action<List<IObject3D>>)((List<IObject3D> list) =>
					{
						list.Remove(child);
						list.Add((IObject3D)new ModifiedMeshObject3D(child, this.ID));
					}));
				}
			}
		}
	}
}