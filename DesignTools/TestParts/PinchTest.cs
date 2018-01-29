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

using System.ComponentModel;
using System.Linq;
using System.Threading;
using MatterHackers.Agg.Font;
using MatterHackers.DataConverters3D;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.DesignTools
{
	public class PinchTest : Object3D, IRebuildable
	{
		public override string ActiveEditor => "PublicPropertyEditor";

		private PolygonMesh.Mesh inputMesh;

		private PolygonMesh.Mesh transformedMesh;

		public PinchTest()
		{
			var letterPrinter = new TypeFacePrinter("MatterHackers");
			inputMesh = VertexSourceToMesh.Extrude(letterPrinter, 5);
			transformedMesh = PolygonMesh.Mesh.Copy(inputMesh, CancellationToken.None);

			Rebuild();
		}

		[DisplayName("Back Ratio")]
		public double PinchRatio { get; set; } = 1;

		public void Rebuild()
		{
			var aabb = inputMesh.GetAxisAlignedBoundingBox();
			for (int i = 0; i < transformedMesh.Vertices.Count; i++)
			{
				var pos = inputMesh.Vertices[i].Position;

				var ratioToApply = PinchRatio;

				var distFromCenter = pos.X - aabb.Center.X;
				var distanceToPinch = distFromCenter * (1 - PinchRatio);
				var delta = (aabb.Center.X + distFromCenter * ratioToApply) - pos.X;

				// find out how much to pinch based on y position
				var amountOfRatio = (pos.Y - aabb.minXYZ.Y) / aabb.YSize;
				transformedMesh.Vertices[i].Position = new Vector3(pos.X + delta * amountOfRatio, pos.Y, pos.Z);
			}

			transformedMesh.MarkAsChanged();
			transformedMesh.CalculateNormals();

			this.Mesh = transformedMesh;
		}
	}
}