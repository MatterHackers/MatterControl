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

using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using Newtonsoft.Json;
using System.Linq;

namespace MatterHackers.MatterControl.DesignTools
{
	using MatterHackers.Agg.UI;
	using MatterHackers.DataConverters3D.UndoCommands;
	using MatterHackers.Localizations;
	using MatterHackers.MatterControl.DesignTools.Operations;
	using MatterHackers.PolygonMesh;
	using System.Collections.Generic;
	using System.Threading;

	public class LinearExtrudeObject3D : Object3D
	{
		public double Height { get; set; } = 5;

		public override bool CanApply => true;

		[JsonIgnore]
		private IVertexSource VertexSource
		{
			get
			{
				var item = this.Descendants().Where((d) => d is IPathObject).FirstOrDefault();
				if (item is IPathObject pathItem)
				{
					return pathItem.VertexSource;
				}

				return null;
			}
		}

		public override void Apply(UndoBuffer undoBuffer)
		{
			// only keep the mesh and get rid of everything else
			using (RebuildLock())
			{
				var meshOnlyItem = new Object3D()
				{
					Mesh = this.Mesh.Copy(CancellationToken.None)
				};

				meshOnlyItem.CopyProperties(this, Object3DPropertyFlags.All);

				// and replace us with the children 
				undoBuffer.AddAndDo(new ReplaceCommand(new List<IObject3D> { this }, new List<IObject3D> { meshOnlyItem }));
			}

			Invalidate(new InvalidateArgs(this, InvalidateType.Content));
		}

		public LinearExtrudeObject3D()
		{
			Name = "Linear Extrude".Localize();
		}

		public override void OnInvalidate(InvalidateArgs invalidateType)
		{
			if ((invalidateType.InvalidateType == InvalidateType.Content
				|| invalidateType.InvalidateType == InvalidateType.Matrix
				|| invalidateType.InvalidateType == InvalidateType.Mesh
				|| invalidateType.InvalidateType == InvalidateType.Path)
				&& invalidateType.Source != this
				&& !RebuildLocked)
			{
				Rebuild(null);
			}
			else if (invalidateType.InvalidateType == InvalidateType.Properties
				&& invalidateType.Source == this)
			{
				Rebuild(null);
			}
			else
			{
				base.OnInvalidate(invalidateType);
			}
		}

		private void Rebuild(UndoBuffer undoBuffer)
		{
			using (RebuildLock())
			{
				var vertexSource = this.VertexSource;
				Mesh = VertexSourceToMesh.Extrude(this.VertexSource, Height);
				if (Mesh.Vertices.Count == 0)
				{
					Mesh = null;
				}
			}

			Invalidate(new InvalidateArgs(this, InvalidateType.Mesh));
		}
	}
}