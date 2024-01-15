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
using System.Collections.Generic;
using System.Linq;
using Matter_CAD_Lib.DesignTools._Object3D;
using Matter_CAD_Lib.DesignTools.Interfaces;
using MatterHackers.Agg;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace MatterHackers.DataConverters3D
{
    public static class Object3DHelperExtensions
	{
		public static void AddRange(this IList<IObject3D> list, IEnumerable<IObject3D> addItems)
		{
			list.AddRange(addItems);
		}

		public static void CopyWorldProperties(this IObject3D copyTo,
			IObject3D copyFrom,
			IObject3D root,
			Object3DPropertyFlags flags,
			bool includingRoot = true)
		{
			if (flags.HasFlag(Object3DPropertyFlags.Matrix))
			{
				copyTo.Matrix = copyFrom.WorldMatrix(root, includingRoot);
			}

			if (flags.HasFlag(Object3DPropertyFlags.Color))
			{
				copyTo.Color = copyFrom.WorldColor(root, includingRoot);
			}

			if (flags.HasFlag(Object3DPropertyFlags.Name))
			{
				copyTo.Name = copyFrom.Name;
			}

			if (flags.HasFlag(Object3DPropertyFlags.OutputType))
			{
				copyTo.OutputType = copyFrom.WorldOutputType(root, includingRoot);
			}

			if (flags.HasFlag(Object3DPropertyFlags.Visible))
			{
				copyTo.Visible = copyFrom.WorldVisible(root, includingRoot);
			}
		}

		public static void CopyProperties(this IObject3D copyTo, IObject3D copyFrom, Object3DPropertyFlags flags)
		{
			if (flags.HasFlag(Object3DPropertyFlags.Matrix))
			{
				copyTo.Matrix = copyFrom.Matrix;
			}

			if (flags.HasFlag(Object3DPropertyFlags.Color))
			{
				copyTo.Color = copyFrom.Color;
			}

			if (flags.HasFlag(Object3DPropertyFlags.Name))
			{
				copyTo.Name = copyFrom.Name;
			}

			if (flags.HasFlag(Object3DPropertyFlags.OutputType))
			{
				copyTo.OutputType = copyFrom.OutputType;
			}

			if (flags.HasFlag(Object3DPropertyFlags.Visible))
			{
				copyTo.Visible = copyFrom.Visible;
			}
		}

		public static bool HasChildren(this IObject3D object3D)
		{
			return object3D.Children.Count > 0;
		}

		public static AxisAlignedBoundingBox GetAxisAlignedBoundingBox(this IObject3D object3D)
		{
			return object3D.GetAxisAlignedBoundingBox(Matrix4X4.Identity);
		}

		/// <summary>
		/// Got the top of this objects parent tree and get change the name of the object if
		/// required to make sure it is not the same as any other descendant
		/// </summary>
		/// <param name="root"></param>
		public static void MakeNameNonColliding(this IObject3D item)
		{
			var topParent = item.Parents().LastOrDefault();
			if (topParent != null)
			{
				var names = new HashSet<string>(topParent.DescendantsAndSelf().Where((i) => i != item).Select((i2) => i2.Name));

				if (string.IsNullOrEmpty(item.Name))
				{
					// Object3D authors should give their objects a simplified name, but if they fail to do so,
					// fallback to a sane default before calling into GetNonCollidingName
					item.Name = item.TypeName;
				}

				item.Name = Util.GetNonCollidingName(item.Name, names);
			}
		}

		public static ulong MeshRenderId(this IObject3D root)
		{
			ulong hash = 14695981039346656037;
			using (root.RebuildLock())
			{
				var oldMatrix = root.Matrix;
				root.Matrix = Matrix4X4.Identity;

				foreach (var item in root.VisibleMeshes())
				{
					unchecked
					{
						hash = item.Mesh.LongHashBeforeClean.GetLongHashCode(hash);
						hash = item.WorldMatrix(root).GetLongHashCode(hash);
						hash = item.WorldColor(root).GetLongHashCode(hash);
					}
				}

				root.Matrix = oldMatrix;
			}

			return hash;
		}

		/// <summary>
		/// Enumerator to get the currently visible object that has a mesh for rendering.
		/// The returned set may include placeholder or proxy data while
		/// long operations are happening such as loading or mesh processing.
		/// </summary>
		/// <returns></returns>
		public static IEnumerable<IObject3D> VisibleMeshes(this IObject3D root, Func<IObject3D, bool> consider = null)
		{
			var items = new Stack<IObject3D>(new[] { root });
			while (items.Count > 0)
			{
				var item = items.Pop();

				// store the mesh so we are thread safe regarding having a valid object (not null)
				var mesh = item.Mesh;
				if (mesh != null)
				{
					// there is a mesh return the object
					yield return item;
				}
				else // there is no mesh go into the object and iterate its children
				{
					foreach (var childItem in item.Children)
					{
						if (childItem.Visible
							&& (consider == null || consider(childItem)))
						{
							items.Push(childItem);
						}
					}
				}
			}
		}

		/// <summary>
		/// Enumerator to get the currently visible object that has a VertexSource.
		/// The returned set may include placeholder or proxy data while
		/// long operations are happening such as loading or mesh processing.
		/// </summary>
		/// <returns></returns>
		public static IEnumerable<IPathProvider> VisiblePathProviders(this IObject3D root, Func<IObject3D, bool> consider = null, bool onlyChildren = false)
		{
			var items = new Stack<IObject3D>(new[] { root });
			while (items.Count > 0)
			{
				var item = items.Pop();

				// store the mesh so we are thread safe regarding having a valid object (not null)
                if ((!onlyChildren || item != root)
                    && item is IPathProvider pathObject3D
					&& pathObject3D.GetRawPath() != null)
				{
					// there is a VertexSource return the object
					yield return pathObject3D;
				}
				else // there is no mesh go into the object and iterate its children
				{
					foreach (var childItem in item.Children)
					{
						if (childItem.Visible
							&& (consider == null || consider(childItem)))
						{
							items.Push(childItem);
						}
					}
				}
			}
		}

        public static IVertexSource CombinedVisibleChildrenPaths(this IObject3D item)
        {
			var visibleChildPaths = item.VisiblePathProviders(onlyChildren: true);
			if (visibleChildPaths != null
				&& visibleChildPaths.Any())
			{
				return new CombinePaths(visibleChildPaths.Select(i => i.GetTransformedPath(item)));
			}

			return null;
		}
	}
}