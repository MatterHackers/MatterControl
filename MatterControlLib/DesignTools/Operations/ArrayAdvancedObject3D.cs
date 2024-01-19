﻿/*
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

using Matter_CAD_Lib.DesignTools.Interfaces;
using Matter_CAD_Lib.DesignTools._Object3D;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;
using MatterHackers.VectorMath;
using System.Linq;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.DesignTools.Operations
{
    public class ArrayAdvancedObject3D : ArrayObject3D
	{
		public ArrayAdvancedObject3D()
		{
			Name = "Advanced Array".Localize();
		}

		public override bool CanApply => true;

		public override IntOrExpression Count { get; set; } = 3;

		public Vector3 Offset { get; set; } = new Vector3(30, 0, 0);

		public DoubleOrExpression Rotate { get; set; } = -15;

		public bool RotatePart { get; set; } = true;

		public DoubleOrExpression Scale { get; set; } = .9;

		public bool ScaleOffset { get; set; } = true;

		public override async Task Rebuild()
		{
			var rebuildLock = this.RebuildLock();
			SourceContainer.Visible = true;

			await ApplicationController.Instance.Tasks.Execute(
				"Advanced Array".Localize(),
				null,
				(reporter, cancellationToken) =>
				{
					this.DebugDepth("Rebuild");
					var sourceContainer = SourceContainer;
					this.Children.Modify(list =>
					{
						list.Clear();
						list.Add(sourceContainer);
						var lastChild = sourceContainer.Children.First();
						list.Add(lastChild.DeepCopy());
						var offset = Offset;
						var count = Count.Value(this);
						var rotate = Rotate.Value(this);
						var scale = Scale.Value(this);
						for (int i = 1; i < count; i++)
						{
							var rotateRadians = MathHelper.DegreesToRadians(rotate);
							if (ScaleOffset)
							{
								offset *= scale;
							}

							var next = lastChild.DeepCopy();
							offset = Vector3Ex.Transform(offset, Matrix4X4.CreateRotationZ(rotateRadians));
							next.Matrix *= Matrix4X4.CreateTranslation(offset);

							if (RotatePart)
							{
								next.Matrix = next.ApplyAtBoundsCenter(Matrix4X4.CreateRotationZ(rotateRadians));
							}

							next.Matrix = next.ApplyAtBoundsCenter(Matrix4X4.CreateScale(scale));
							list.Add(next);
							lastChild = next;
						}
					});

					ProcessIndexExpressions();

					SourceContainer.Visible = false;
					UiThread.RunOnIdle(() =>
					{
						rebuildLock.Dispose();
						this.DoRebuildComplete();
						Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Children));
					});
					return Task.CompletedTask;
				});
		}
	}
}