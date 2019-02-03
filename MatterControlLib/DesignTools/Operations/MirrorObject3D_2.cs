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

using System;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.MatterControl.PartPreviewWindow.View3D;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.DesignTools
{
	public class MirrorObject3D_2 : OperationSourceContainerObject3D
	{
		public MirrorObject3D_2()
		{
			Name = "Mirror".Localize();
		}

		public enum MirrorAxis { X_Axis, Y_Axis, Z_Axis };

		public MirrorAxis MirrorOn { get; set; } = MirrorAxis.X_Axis;

		public override Task Rebuild()
		{
			this.DebugDepth("Rebuild");

			var rebuildLock = this.RebuildLock();

			return ApplicationController.Instance.Tasks.Execute(
				"Mirror".Localize(),
				null,
				(reporter, cancellationToken) =>
				{
					SourceContainer.Visible = true;
					RemoveAllButSource();

					var oldMatrix = this.Matrix;
					this.Matrix = Matrix4X4.Identity;

					var mirrorMatrix = Matrix4X4.Identity;
					switch (MirrorOn)
					{
						case MirrorAxis.X_Axis:
							mirrorMatrix = this.ApplyAtBoundsCenter(Matrix4X4.CreateScale(-1, 1, 1));
							break;

						case MirrorAxis.Y_Axis:
							mirrorMatrix = this.ApplyAtBoundsCenter(Matrix4X4.CreateScale(1, -1, 1));
							break;

						case MirrorAxis.Z_Axis:
							mirrorMatrix = this.ApplyAtBoundsCenter(Matrix4X4.CreateScale(1, 1, -1));
							break;
					}

					foreach (var sourceItem in SourceContainer.VisibleMeshes())
					{
						var originalMesh = sourceItem.Mesh;
						var transformedMesh = originalMesh.Copy(CancellationToken.None);

						var sourceToThisMatrix = sourceItem.WorldMatrix(this);

						// move it to us then mirror then move it back
						transformedMesh.Transform(sourceToThisMatrix * mirrorMatrix * sourceToThisMatrix.Inverted);

						transformedMesh.ReverseFaces();

						var newMesh = new Object3D()
						{
							Mesh = transformedMesh
						};
						newMesh.CopyWorldProperties(sourceItem, this, Object3DPropertyFlags.All);
						this.Children.Add(newMesh);
					}

					this.Matrix = oldMatrix;
					SourceContainer.Visible = false;
					rebuildLock.Dispose();
					base.OnInvalidate(new InvalidateArgs(this, InvalidateType.Children));
					return Task.CompletedTask;
				});
		}
	}
}