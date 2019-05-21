/*
Copyright (c) 2019, Lars Brubaker, John Lewin
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
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.RayTracer;
using MatterHackers.RayTracer.Traceable;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PartPreviewWindow
{
	public class ItemTraceDataDrawable : IDrawableItem
	{
		private InteractiveScene scene;

		public ItemTraceDataDrawable(ISceneContext sceneContext)
		{
			this.scene = sceneContext.Scene;
		}

		public bool Enabled { get; set; } = false;

		public string Title { get; } = "Item TraceData Render";

		public string Description { get; } = "Render TraceData for the selected item";

		public DrawStage DrawStage { get; } = DrawStage.Last;

		public void Draw(GuiWidget sender, IObject3D item, bool isSelected, DrawEventArgs e, Matrix4X4 itemMaxtrix, WorldView world)
		{
			if (isSelected)
			{
				var center = item.GetAxisAlignedBoundingBox().Center;

				var traceData = item.TraceData();

				var xy = traceData.Contains(center);

				var items = traceData.FilterX(p =>
				{
					var worldToAxis = Matrix4X4.Invert(p.WorldMatrix);

					var localPoint = Vector3Ex.Transform(center, worldToAxis);
					return p.BvhItem.GetAxisAlignedBoundingBox().Contains(localPoint);
				}).ToArray();

				if (items.Any())
				{
					int i = 0;
					foreach(var p in items)
					{
						InteractionLayer.RenderBounds(e, world, p.Matrix, p.BvhItem, i++);
					}
				}
			}
		}
	}

	public class BvhItemView
	{
		public IBvhItem BvhItem { get; }
		public Matrix4X4 WorldMatrix { get; }

		public BvhItemView(IBvhItem item, Matrix4X4 matrix)
		{
			this.BvhItem = item;
			this.WorldMatrix = matrix;
		}
	}


	public static class PrimitivesExtensions
	{
		public static IEnumerable<(IBvhItem BvhItem, Matrix4X4 Matrix)> FilterX(this IPrimitive source, Predicate<BvhItemView> filter)
		{
			var items = new Stack<(IBvhItem BvhItem, Matrix4X4 Matrix)>();
			items.Push((source, Matrix4X4.Identity));

			while (items.Count > 0)
			{
				var context = items.Pop();

				// If the node passes the predicate, yield it and push its children to the stack for processing
				if (filter.Invoke(new BvhItemView(context.BvhItem, context.Matrix)))
				{
					switch (context.BvhItem)
					{
						case Transform transform:
							items.Push((transform.Child, transform.AxisToWorld * context.Matrix));
							break;

						case UnboundCollection unboundCollection:
							foreach (var item in unboundCollection.Items)
							{
								items.Push((item, context.Matrix));
							}
							break;
					}
				}

				//Console.WriteLine("Yield: {0}/{1}", context.GetType().Name, string.Join(",", worldMatrix.GetAsDoubleArray()));
				yield return (context.BvhItem, context.Matrix);
			}
		}
	}
}