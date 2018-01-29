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

using System.Threading;
using MatterHackers.DataConverters3D;
using MatterHackers.PolygonMesh;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.DesignTools
{
	/*

public class ChairFoot2 : MatterCadObject3D
{
	public ChairFoot()
	{
		Rebuild();
	}

	[DisplayName("Angle")]
	public double AngleDegrees { get; set; } = 3;

	// these are the public variables that would be edited
	[DisplayName("Final")]
	public bool FinalPart { get; set; } = true;

	[DisplayName("Height")]
	public double HeightFromFloorToBottomOfLeg { get; set; } = 10;

	[DisplayName("Inner Size")]
	public double InnerSize { get; set; } = 20;

	[DisplayName("Reach")]
	public double InsideReach { get; set; } = 10;

	[DisplayName("Outer Size")]
	public double OuterSize { get; set; } = 22;

	public void Rebuild()
	{
		// This would be better expressed as the desired offset height (height from ground to bottom of chair leg).
		double angleRadians = MathHelper.DegreesToRadians(AngleDegrees);
		double extraHeightForRotation = Math.Sinh(angleRadians) * OuterSize; // get the distance to clip off the extra bottom
		double unclippedFootHeight = HeightFromFloorToBottomOfLeg + extraHeightForRotation;

		if (FinalPart)
		{
			Box chairFootBox = new Box(OuterSize, OuterSize, unclippedFootHeight);
			//chairFootBox.BevelEdge(Edge.LeftBack, 2);
			//chairFootBox.BevelEdge(Edge.LeftFront, 2);
			//chairFootBox.BevelEdge(Edge.RightBack, 2);
			//chairFootBox.BevelEdge(Edge.RightFront, 2);
			IObject3D chairFoot = chairFootBox;

			IObject3D ring = new Cylinder(InnerSize / 2 - 1, InsideReach, 30);
			ring -= new Cylinder(ring.XSize / 2 - 2, ring.ZSize + 1, 30);

			IObject3D fins = new Box(3, 1, ring.ZSize);
			fins = new Translate(fins, 0, 1) + new Translate(fins, 0, -1);
			fins -= new Align(new Rotate(new Box(5, 5, 5), 0, MathHelper.DegreesToRadians(45)), Face.Bottom | Face.Left, fins, Face.Top | Face.Left, 0, 0, -fins.XSize);
			fins = new Translate(fins, InnerSize / 2 - .1);

			ring += new Rotate(fins, 0, 0, MathHelper.DegreesToRadians(45));
			ring += new Rotate(fins, 0, 0, MathHelper.DegreesToRadians(45 + 90));
			ring += new Rotate(fins, 0, 0, MathHelper.DegreesToRadians(45 + 180));
			ring += new Rotate(fins, 0, 0, MathHelper.DegreesToRadians(45 - 90));

			chairFoot += new Align(ring, Face.Bottom, chairFoot, Face.Top, 0, 0, -.1);

			chairFoot = new Rotate(chairFoot, 0, angleRadians, 0);
			IObject3D clipBox = new Align(new Box(OuterSize * 2, OuterSize * 2, unclippedFootHeight), Face.Top, chairFoot, Face.Bottom, 0, 0, extraHeightForRotation);
			chairFoot -= clipBox;
			chairFoot = new Translate(chairFoot, 0, 0, clipBox.GetAxisAlignedBoundingBox().maxXYZ.Z);

			this.Mesh = CsgToMesh.Convert(chairFoot);
		}
		else // fit part
		{
			double baseHeight = 3;
			double insideHeight = 4;
			Box chairFootBox = new Box(OuterSize, OuterSize, baseHeight);
			chairFootBox.BevelEdge(Edge.LeftBack, 2);
			chairFootBox.BevelEdge(Edge.LeftFront, 2);
			chairFootBox.BevelEdge(Edge.RightBack, 2);
			chairFootBox.BevelEdge(Edge.RightFront, 2);
			IObject3D chairFoot = chairFootBox;

			IObject3D ring = new Cylinder(InnerSize / 2 - 1, insideHeight, 30);
			ring -= new Cylinder(ring.XSize / 2 - 2, ring.ZSize + 1, 30);

			IObject3D fins = new Box(3, 1, ring.ZSize);
			fins = new Translate(fins, 0, 1) + new Translate(fins, 0, -1);
			fins -= new Align(new Rotate(new Box(5, 5, 5), 0, MathHelper.DegreesToRadians(45)), Face.Bottom | Face.Left, fins, Face.Top | Face.Left, 0, 0, -fins.XSize);
			fins = new Translate(fins, InnerSize / 2 - .1);

			ring += new Rotate(fins, 0, 0, MathHelper.DegreesToRadians(45));
			ring += new Rotate(fins, 0, 0, MathHelper.DegreesToRadians(45 + 90));
			ring += new Rotate(fins, 0, 0, MathHelper.DegreesToRadians(45 + 180));
			ring += new Rotate(fins, 0, 0, MathHelper.DegreesToRadians(45 - 90));

			chairFoot += new Align(ring, Face.Bottom, chairFoot, Face.Top, 0, 0, -.1);

			this.Mesh = CsgToMesh.Convert(chairFoot);
		}
	}
}
*/

	public class CubeObject3D : Object3D, IRebuildable
	{
		public CubeObject3D()
		{
		}

		public override string ActiveEditor => "PublicPropertyEditor";

		public double Width { get; set; } = 20;
		public double Depth { get; set; } = 20;
		public double Height { get; set; } = 20;

		public static ConeObject3D Create()
		{
			var item = new ConeObject3D();
			item.Rebuild();
			return item;
		}

		public static CubeObject3D Create(double x, double y, double z)
		{
			var item = new CubeObject3D()
			{
				Width = x,
				Depth = y,
				Height = z,
			};

			item.Rebuild();
			return item;
		}

		public void Rebuild()
		{
			var aabb = AxisAlignedBoundingBox.Zero;
			if (Mesh != null)
			{
				// Keep track of the mesh height so it does not move around unexpectedly
				this.GetAxisAlignedBoundingBox();
			}
			Mesh = PlatonicSolids.CreateCube(Width, Depth, Height);
			Mesh.CleanAndMeregMesh(CancellationToken.None);
			PlatingHelper.PlaceMeshAtHeight(this, aabb.minXYZ.Z);
		}
	}
}