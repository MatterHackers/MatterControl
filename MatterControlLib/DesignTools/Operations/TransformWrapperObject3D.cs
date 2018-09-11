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
using System.ComponentModel;
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MeshVisualizer;
using MatterHackers.RenderOpenGl;
using MatterHackers.RenderOpenGl.OpenGl;
using MatterHackers.VectorMath;
using Newtonsoft.Json;

namespace MatterHackers.MatterControl.DesignTools.Operations
{
	public abstract class TransformWrapperObject3D : Object3D
	{
		protected IObject3D TransformItem
		{
			get
			{
				if (Children.Count > 0)
				{
					return Children.First();
				}

				return null;
			}
		}

		[JsonIgnore]
		public IObject3D SourceItem
		{
			get
			{
				if (TransformItem?.Children.Count > 0)
				{
					return TransformItem.Children.First();
				}

				return null;
			}
		}

		public TransformWrapperObject3D()
		{
			Name = "Transform Wrapper".Localize();
		}

		public override void Apply(UndoBuffer undoBuffer)
		{
			using (RebuildLock())
			{
				// push our matrix into our children
				foreach (var child in this.Children)
				{
					child.Matrix *= this.Matrix;
				}

				// push child into children
				SourceItem.Matrix *= TransformItem.Matrix;

				// add our children to our parent and remove from parent
				this.Parent.Children.Modify(list =>
				{
					list.Remove(this);
					list.AddRange(TransformItem.Children);
				});
			}
			Invalidate(new InvalidateArgs(this, InvalidateType.Content));
		}

		public override void Remove(UndoBuffer undoBuffer)
		{
			using (RebuildLock())
			{
				// push our matrix into inner children
				foreach (var child in TransformItem.Children)
				{
					child.Matrix *= this.Matrix;
				}

				// add inner children to our parent and remove from parent
				this.Parent.Children.Modify(list =>
				{
					list.Remove(this);
					list.AddRange(TransformItem.Children);
				});
			}

			Invalidate(new InvalidateArgs(this, InvalidateType.Content));
		}
	}
}