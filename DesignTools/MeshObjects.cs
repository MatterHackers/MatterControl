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
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using MatterHackers.Agg;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl.PartPreviewWindow.View3D;
using MatterHackers.PolygonMesh;
using MatterHackers.RenderOpenGl;
using MatterHackers.VectorMath;
using Newtonsoft.Json;

namespace MatterHackers.MatterControl.DesignTools
{
	public enum Alignment { X, Y, Z, negX, negY, negZ };

	[Flags]
	public enum Face
	{
		Left = 0x01,
		Right = 0x02,
		Front = 0x04,
		Back = 0x08,
		Bottom = 0x10,
		Top = 0x20,
	};

	[Flags]
	public enum Edge
	{
		LeftFront = Face.Left | Face.Front,
		LeftBack = Face.Left | Face.Back,
		LeftBottom = Face.Left | Face.Bottom,
		LeftTop = Face.Left | Face.Top,
		RightFront = Face.Right | Face.Front,
		RightBack = Face.Right | Face.Back,
		RightBottom = Face.Right | Face.Bottom,
		RightTop = Face.Right | Face.Top,
		FrontBottom = Face.Front | Face.Bottom,
		FrontTop = Face.Front | Face.Top,
		BackBottom = Face.Back | Face.Bottom,
		BackTop = Face.Back | Face.Top
	}

	public class BadSubtract : Object3D, IRebuildable
	{
		public override string ActiveEditor => "PublicPropertyEditor";

		public BadSubtract()
		{
			Rebuild();
		}

		public double Sides { get; set; } = 4;

		public void Rebuild()
		{
			int sides = 3;
			IObject3D keep = new Cylinder(20, 20, sides);
			IObject3D subtract = new Cylinder(10, 21, sides);
			subtract = new SetCenter(subtract, keep.GetCenter());
			IObject3D result = keep.Minus(subtract);
			this.SetChildren(result);
		}
	}

	public class SetCenter : Object3D
	{
		public SetCenter()
		{
		}

		public SetCenter(IObject3D item, Vector3 position)
		{
			Matrix *= Matrix4X4.CreateTranslation(position - item.GetCenter());
			Children.Add(item.Clone());
		}

		public SetCenter(IObject3D item, double x, double y, double z)
			: this(item, new Vector3(x, y, z))
		{
		}

		public SetCenter(IObject3D item, Vector3 offset, bool onX = true, bool onY = true, bool onZ = true)
		{
			var center = item.GetAxisAlignedBoundingBox(Matrix4X4.Identity).Center;

			Vector3 consideredOffset = Vector3.Zero; // zero out anything we don't want
			if (onX)
			{
				consideredOffset.X = offset.X - center.X;
			}
			if (onY)
			{
				consideredOffset.Y = offset.Y - center.Y;
			}
			if (onZ)
			{
				consideredOffset.Z = offset.Z - center.Z;
			}

			Matrix *= Matrix4X4.CreateTranslation(consideredOffset);
			Children.Add(item.Clone());
		}
	}

	public class Cylinder : Object3D
	{
		public Cylinder()
		{
		}

		public Cylinder(double radius, double height, int sides, Alignment alignment = Alignment.Z)
			: this(radius, radius, height, sides, alignment)
		{
		}

		public Cylinder(double radiusBottom, double radiusTop, double height, int sides, Alignment alignment = Alignment.Z)
		{
			var path = new VertexStorage();
			path.MoveTo(0, -height/2);
			path.LineTo(radiusBottom, -height/2);
			path.LineTo(radiusTop, height/2);
			path.LineTo(0, height/2);

			Mesh = VertexSourceToMesh.Revolve(path, sides);
			switch (alignment)
			{
				case Alignment.X:
					Matrix = Matrix4X4.CreateRotationY(MathHelper.Tau / 4);
					break;
				case Alignment.Y:
					Matrix = Matrix4X4.CreateRotationX(MathHelper.Tau / 4);
					break;
				case Alignment.Z:
					// This is the natural case (how it was modled)
					break;
				case Alignment.negX:
					Matrix = Matrix4X4.CreateRotationY(-MathHelper.Tau / 4);
					break;
				case Alignment.negY:
					Matrix = Matrix4X4.CreateRotationX(-MathHelper.Tau / 4);
					break;
				case Alignment.negZ:
					Matrix = Matrix4X4.CreateRotationX(MathHelper.Tau / 2);
					break;
			}
		}
	}

	public class CardHolder : Object3D, IRebuildable
	{
		public override string ActiveEditor => "PublicPropertyEditor";

		public CardHolder()
		{
			Rebuild();
		}

		[DisplayName("Name")]
		public string NameToWrite { get; set; } = "MatterHackers";

		public void Rebuild()
		{
			IObject3D plainCardHolder = Object3D.Load("C:/Temp/CardHolder.stl");

			//TypeFace typeFace = TypeFace.LoadSVG("Viking_n.svg");

			var letterPrinter = new TypeFacePrinter(NameToWrite);//, new StyledTypeFace(typeFace, 12));

			IObject3D nameMesh = new Object3D()
			{
				Mesh = VertexSourceToMesh.Extrude(letterPrinter, 5)
			};

			AxisAlignedBoundingBox textBounds = nameMesh.GetAxisAlignedBoundingBox(Matrix4X4.Identity);
			var textArea = new Vector2(90, 20);

			// test the area that the names will go to
			// nameMesh = new Box(textArea.X, textArea.Y, 5);

			double scale = Math.Min(textArea.X / textBounds.XSize, textArea.Y / textBounds.YSize);
			nameMesh = new Scale(nameMesh, scale, scale, 1);
			nameMesh = new Align(nameMesh, Face.Bottom | Face.Front, plainCardHolder, Face.Bottom | Face.Front);
			nameMesh = new SetCenter(nameMesh, plainCardHolder.GetCenter(), true, false, false);

			nameMesh = new Rotate(nameMesh, MathHelper.DegreesToRadians(-16));
			nameMesh = new Translate(nameMesh, 0, 4, 2);

			// output two meshes for card holder and text
			this.Children.Modify(list =>
			{
				list.Clear();
				list.Add(plainCardHolder);
				list.Add(nameMesh);
			});
		}
	}

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

	public class CubePrimitive : Object3D, IRebuildable
	{
		public override string ActiveEditor => "PublicPropertyEditor";

		public CubePrimitive()
		{
			Rebuild();
		}

		public double Width { get; set; } = 20;
		public double Depth { get; set; } = 20;
		public double Height { get; set; } = 20;

		public void Rebuild()
		{
			var aabb = AxisAlignedBoundingBox.Zero;
			if (Mesh != null)
			{
				this.GetAxisAlignedBoundingBox();
			}
			Mesh = PlatonicSolids.CreateCube(Width, Depth, Height);
			Mesh.CleanAndMergMesh(CancellationToken.None);
			PlatingHelper.PlaceMeshAtHeight(this, aabb.minXYZ.Z);
		}
	}

	public class CylinderPrimitive : Object3D, IRebuildable
	{
		public override string ActiveEditor => "PublicPropertyEditor";

		public CylinderPrimitive()
		{
			Rebuild();
		}

		public double Diameter { get; set; } = 20;
		public double Height { get; set; } = 20;
		public int Sides { get; set; } = 30;

		public void Rebuild()
		{
			var aabb = AxisAlignedBoundingBox.Zero;
			if (Mesh != null)
			{
				this.GetAxisAlignedBoundingBox();
			}
			var path = new VertexStorage();
			path.MoveTo(0, 0);
			path.LineTo(Diameter / 2, 0);
			path.LineTo(Diameter / 2, Height);
			path.LineTo(0, Height);

			Mesh = VertexSourceToMesh.Revolve(path, Sides);
			Mesh.CleanAndMergMesh(CancellationToken.None);
			PlatingHelper.PlaceMeshAtHeight(this, aabb.minXYZ.Z);
		}
	}

	public enum NamedTypeFace { Liberation_Sans, Liberation_Sans_Bold, Liberation_Mono, Titillium, Damion };

	public static class NamedTypeFaceCache
	{
		public static TypeFace GetTypeFace(NamedTypeFace Name)
		{
			switch (Name)
			{
				case NamedTypeFace.Liberation_Sans:
					return LiberationSansFont.Instance;

				case NamedTypeFace.Liberation_Sans_Bold:
					return LiberationSansBoldFont.Instance;

				case NamedTypeFace.Liberation_Mono:
					return ApplicationController.MonoSpacedTypeFace;

				case NamedTypeFace.Titillium:
					return ApplicationController.TitilliumTypeFace;

				case NamedTypeFace.Damion:
					return ApplicationController.DamionTypeFace;

				default:
					return LiberationSansFont.Instance;
			}
		}
	}

	public class TextPrimitive : Object3D, IRebuildable
	{
		[DisplayName("Name")]
		public string NameToWrite { get; set; } = "Text";

		public NamedTypeFace Font { get; set; } = new NamedTypeFace();

		public double PointSize { get; set; } = 24;

		public double Height { get; set; } = 5;

		public override string ActiveEditor => "PublicPropertyEditor";

		public TextPrimitive()
		{
			Rebuild();
		}

		public void Rebuild()
		{
			var letterPrinter = new TypeFacePrinter(NameToWrite, new StyledTypeFace(NamedTypeFaceCache.GetTypeFace(Font), PointSize * 0.352778));

			IObject3D nameMesh = new Object3D()
			{
				Mesh = VertexSourceToMesh.Extrude(letterPrinter, Height)
			};

			// output two meshes for card holder and text
			this.Children.Modify(list =>
			{
				list.Clear();
				list.Add(nameMesh);
			});
		}
	}

	public class ConePrimitive : Object3D, IRebuildable
	{
		public override string ActiveEditor => "PublicPropertyEditor";

		public ConePrimitive()
		{
			Rebuild();
		}

		[DisplayName("Diameter")]
		public double Diameter { get; set; } = 20;
		//[DisplayName("Top")]
		//public double TopDiameter { get; set; } = 0;
		public double Height { get; set; } = 20;
		public int Sides { get; set; } = 30;

		public void Rebuild()
		{
			var aabb = AxisAlignedBoundingBox.Zero;
			if (Mesh != null)
			{
				this.GetAxisAlignedBoundingBox();
			}
			var path = new VertexStorage();
			path.MoveTo(0, 0);
			path.LineTo(Diameter / 2, 0);
			path.LineTo(0, Height);

			Mesh = VertexSourceToMesh.Revolve(path, Sides);
			Mesh.CleanAndMergMesh(CancellationToken.None);
			PlatingHelper.PlaceMeshAtHeight(this, aabb.minXYZ.Z);
		}
	}

	public class TorusPrimitive : Object3D, IRebuildable
	{
		public override string ActiveEditor => "PublicPropertyEditor";

		public TorusPrimitive()
		{
			Rebuild();
		}

		[DisplayName("Inner Diameter")]
		public double InnerDiameter { get; set; } = 10;
		[DisplayName("Outer Diameter")]
		public double OuterDiameter { get; set; } = 20;
		[DisplayName("Toroid Sides")]
		public int ToroidSides { get; set; } = 20;
		[DisplayName("Ring Sides")]
		public int PoleSides { get; set; } = 16;

		public void Rebuild()
		{
			var aabb = AxisAlignedBoundingBox.Zero;
			if (Mesh != null)
			{
				this.GetAxisAlignedBoundingBox();
			}
			var poleRadius = (OuterDiameter / 2 - InnerDiameter / 2) / 2;
			var toroidRadius = InnerDiameter / 2 + poleRadius;
			var path = new VertexStorage();
			var angleDelta = MathHelper.Tau / PoleSides;
			var angle = 0.0;
			var circleCenter = new Vector2(toroidRadius, 0);
			path.MoveTo(circleCenter + new Vector2(poleRadius * Math.Cos(angle), poleRadius * Math.Sin(angle)));
			for (int i = 0; i < PoleSides; i++)
			{
				angle += angleDelta;
				path.LineTo(circleCenter + new Vector2(poleRadius * Math.Cos(angle), poleRadius * Math.Sin(angle)));
			}

			Mesh = VertexSourceToMesh.Revolve(path, ToroidSides);
			Mesh.CleanAndMergMesh(CancellationToken.None);
			PlatingHelper.PlaceMeshAtHeight(this, aabb.minXYZ.Z);
		}
	}
	public class SpherePrimitive : Object3D, IRebuildable
	{
		public override string ActiveEditor => "PublicPropertyEditor";

		public SpherePrimitive()
		{
			Rebuild();
		}

		public double Diameter { get; set; } = 20;
		[DisplayName("Longitude Sides")]
		public int LongitudeSides { get; set; } = 30;
		[DisplayName("Latitude Sides")]
		public int LatitudeSides { get; set; } = 20;

		public void Rebuild()
		{
			var aabb = AxisAlignedBoundingBox.Zero;
			if (Mesh != null)
			{
				this.GetAxisAlignedBoundingBox();
			}
			var path = new VertexStorage();
			var angleDelta = MathHelper.Tau / 2 / LatitudeSides;
			var angle = -MathHelper.Tau / 4;
			var radius = Diameter / 2;
			path.MoveTo(new Vector2(radius * Math.Cos(angle), radius * Math.Sin(angle)));
			for (int i = 0; i < LatitudeSides; i++)
			{
				angle += angleDelta;
				path.LineTo(new Vector2(radius * Math.Cos(angle), radius * Math.Sin(angle)));
			}

			Mesh = VertexSourceToMesh.Revolve(path, LongitudeSides);
			PlatingHelper.PlaceMeshAtHeight(this, aabb.minXYZ.Z);
		}
	}

	public class CurveTest : Object3D, IRebuildable
	{
		public override string ActiveEditor => "PublicPropertyEditor";

		private PolygonMesh.Mesh inputMesh;

		private PolygonMesh.Mesh transformedMesh;

		public CurveTest()
		{
			var letterPrinter = new TypeFacePrinter("MatterHackers");
			inputMesh = VertexSourceToMesh.Extrude(letterPrinter, 5);
			transformedMesh = PolygonMesh.Mesh.Copy(inputMesh, CancellationToken.None);

			Rebuild();
		}

		[DisplayName("Angle")]
		public double AngleDegrees { get; set; } = 0;

		[DisplayName("Bend Up")]
		public bool BendCW { get; set; } = true;

		public void Rebuild()
		{
			if (AngleDegrees > 0)
			{
				var aabb = inputMesh.GetAxisAlignedBoundingBox();

				// find the radius that will make the x-size sweep out the requested angle
				// c = Tr ; r = c/T
				var angleRadians = MathHelper.DegreesToRadians(AngleDegrees);
				var circumference = aabb.XSize * MathHelper.Tau / angleRadians;
				var radius = circumference / MathHelper.Tau;

				var rotateXyPos = new Vector2(aabb.minXYZ.X, BendCW ? aabb.maxXYZ.Y : aabb.minXYZ.Y);
				if (!BendCW)
				{
					angleRadians = -angleRadians;
				}

				for (int i = 0; i < transformedMesh.Vertices.Count; i++)
				{
					var pos = inputMesh.Vertices[i].Position;
					var pos2D = new Vector2(pos);
					Vector2 rotateSpace = pos2D - rotateXyPos;
					var rotateRatio = rotateSpace.X / aabb.XSize;

					rotateSpace.X = 0;
					rotateSpace.Y += BendCW ? -radius : radius;
					rotateSpace.Rotate(angleRadians * rotateRatio);
					rotateSpace.Y += BendCW ? radius : -radius; ;
					rotateSpace += rotateXyPos;

					transformedMesh.Vertices[i].Position = new Vector3(rotateSpace.X, rotateSpace.Y, pos.Z);
				}
			}
			else
			{
				for (int i = 0; i < transformedMesh.Vertices.Count; i++)
				{
					transformedMesh.Vertices[i].Position = inputMesh.Vertices[i].Position;
				}
			}

			transformedMesh.MarkAsChanged();
			transformedMesh.CalculateNormals();

			this.Mesh = transformedMesh;
		}
	}

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

	public class PvcT : Object3D, IRebuildable
	{
		public override string ActiveEditor => "PublicPropertyEditor";

		private int sides = 50;

		public PvcT()
		{
			Rebuild();
		}

		[DisplayName("Inner Radius")]
		public double InnerDiameter { get; set; } = 15;

		[DisplayName("Outer Radius")]
		public double OuterDiameter { get; set; } = 20;

		public double BottomReach { get; set; } = 30;

		public double FrontReach { get; set; } = 25;

		public double TopReach { get; set; } = 30;

		public void Rebuild()
		{
			IObject3D topBottomConnect = new Cylinder(OuterDiameter / 2, OuterDiameter, sides, Alignment.Y);
			IObject3D frontConnect = new Cylinder(OuterDiameter / 2, OuterDiameter / 2, sides, Alignment.X);
			frontConnect = new Align(frontConnect, Face.Right, topBottomConnect, Face.Right);

			IObject3D bottomReach = new Rotate(CreateReach(BottomReach), -MathHelper.Tau / 4);
			bottomReach = new Align(bottomReach, Face.Back, topBottomConnect, Face.Front, 0, .1);

			IObject3D topReach = new Rotate(CreateReach(TopReach), MathHelper.Tau / 4);
			topReach = new Align(topReach, Face.Front, topBottomConnect, Face.Back, 0, -.1);

			IObject3D frontReach = new Rotate(CreateReach(FrontReach), 0, -MathHelper.Tau / 4);
			frontReach = new Align(frontReach, Face.Left, topBottomConnect, Face.Right, -.1);

			// output multiple meshes for pipe connector
			this.Children.Modify(list =>
			{
				list.Clear();
				list.Add(topBottomConnect);
				list.Add(frontConnect);
				list.Add(bottomReach);
				list.Add(topReach);
				list.Add(frontReach);
			});

			this.Color = Color.Transparent;
			this.Mesh = null;
		}

		private IObject3D CreateReach(double reach)
		{
			var finWidth = 4.0;
			var finLength = InnerDiameter;

			var pattern = new VertexStorage();
			pattern.MoveTo(0, 0);
			pattern.LineTo(finLength/2, 0);
			pattern.LineTo(finLength/2, reach - finLength / 8);
			pattern.LineTo(finLength/2 - finLength / 8, reach);
			pattern.LineTo(-finLength/2 + finLength / 8, reach);
			pattern.LineTo(-finLength/2, reach - finLength / 8);
			pattern.LineTo(-finLength/2, 0);

			var fin1 = new Object3D()
			{
				Mesh = VertexSourceToMesh.Extrude(pattern, finWidth)
			};
			fin1 = new Translate(fin1, 0, 0, -finWidth / 2);
			//fin1.ChamferEdge(Face.Top | Face.Back, finLength / 8);
			//fin1.ChamferEdge(Face.Top | Face.Front, finLength / 8);
			fin1 = new Rotate(fin1, -MathHelper.Tau / 4);
			var fin2 = new SetCenter(new Rotate(fin1, 0, 0, MathHelper.Tau / 4), fin1.GetCenter());

			return new Object3D().SetChildren(new List<IObject3D>() { fin1, fin2 });
		}
	}

	public class RibonWithName : Object3D, IRebuildable
	{
		public override string ActiveEditor => "PublicPropertyEditor";

		public RibonWithName()
		{
			Rebuild();
		}

		[DisplayName("Name")]
		public string NameToWrite { get; set; } = "MatterHackers";

		public NamedTypeFace Font { get; set; } = new NamedTypeFace();

		public void Rebuild()
		{
			IObject3D cancerRibonStl = Object3D.Load("Cancer_Ribbon.stl", CancellationToken.None);

			cancerRibonStl = new Rotate(cancerRibonStl, MathHelper.DegreesToRadians(90));

			var letterPrinter = new TypeFacePrinter(NameToWrite.ToUpper(), new StyledTypeFace(NamedTypeFaceCache.GetTypeFace(Font), 12));

			IObject3D nameMesh = new Object3D()
			{
				Mesh = VertexSourceToMesh.Extrude(letterPrinter, 5)
			};

			AxisAlignedBoundingBox textBounds = nameMesh.GetAxisAlignedBoundingBox();
			var textArea = new Vector2(25, 6);

			double scale = Math.Min(textArea.X / textBounds.XSize, textArea.Y / textBounds.YSize);
			nameMesh = new Scale(nameMesh, scale, scale, 2 / textBounds.ZSize);
			nameMesh = new Align(nameMesh, Face.Bottom | Face.Front, cancerRibonStl, Face.Top | Face.Front, 0, 0, -1);
			nameMesh = new SetCenter(nameMesh, cancerRibonStl.GetCenter(), true, false, false);

			nameMesh = new Rotate(nameMesh, 0, 0, MathHelper.DegreesToRadians(50));
			nameMesh = new Translate(nameMesh, -37, -14, -1);

			// output two meshes for card holder and text
			this.Children.Modify(list =>
			{
				list.Clear();
				list.Add(cancerRibonStl);
				list.Add(nameMesh);
			});

			this.Mesh = null;
		}
	}

	public class Box : Object3D
	{
		public Box(double x, double y, double z)
		{
			Mesh = PlatonicSolids.CreateCube(x, y, z);
			Mesh.CleanAndMergMesh(CancellationToken.None);
		}
	}

	public static class Object3DExtensions
	{
		public static IObject3D Translate(this IObject3D objectToTranslate, double x = 0, double y = 0, double z = 0, string name = "")
		{
			return objectToTranslate.Translate(new Vector3(x, y, z), name);
		}

		public static IObject3D Translate(this IObject3D objectToTranslate, Vector3 translation, string name = "")
		{
			objectToTranslate.Matrix *= Matrix4X4.CreateTranslation(translation);
			return objectToTranslate;
		}

		public static IObject3D Minus(this IObject3D a, IObject3D b)
		{
			var resultsA = a.Clone();
			SubtractEditor.Subtract(resultsA.VisibleMeshes().ToList(), b.VisibleMeshes().ToList());
			return resultsA;
		}

		public static Vector3 GetCenter(this IObject3D item)
		{
			return item.GetAxisAlignedBoundingBox(Matrix4X4.Identity).Center;
		}

		public static IObject3D SetChildren(this IObject3D parent, IEnumerable<IObject3D> newChildren)
		{
			parent.Children.Modify((list) =>
			{
				list.Clear();
				list.AddRange(newChildren);
			});

			return parent;
		}

		public static void SetChildren(this IObject3D parent, IObject3D newChild)
		{
			parent.Children.Modify((list) =>
			{
				list.Clear();
				list.Add(newChild);
			});
		}
	}

	public class Translate : Object3D
	{
		public Translate()
		{ }

		public Translate(IObject3D item, double x = 0, double y = 0, double z = 0)
			: this(item, new Vector3(x, y, z))
		{
		}

		public Translate(IObject3D item, Vector3 translation)
		{
			Matrix *= Matrix4X4.CreateTranslation(translation);
			Children.Add(item.Clone());
		}
	}

	public class Align : Object3D
	{
		public Align()
		{
		}

		public Align(IObject3D objectToAlign, Face boundingFacesToAlign, IObject3D objectToAlignTo, Face boundingFacesToAlignTo, double offsetX = 0, double offsetY = 0, double offsetZ = 0, string name = "")
			: this(objectToAlign, boundingFacesToAlign, GetPositionToAlignTo(objectToAlignTo, boundingFacesToAlignTo, new Vector3(offsetX, offsetY, offsetZ)), name)
		{
			if (objectToAlign == objectToAlignTo)
			{
				throw new Exception("You cannot align an object ot itself.");
			}
		}

		public Align(IObject3D objectToAlign, Face boundingFacesToAlign, double offsetX = 0, double offsetY = 0, double offsetZ = 0, string name = "")
			: this(objectToAlign, boundingFacesToAlign, new Vector3(offsetX, offsetY, offsetZ), name)
		{
		}

		public Align(IObject3D objectToAlign, Face boundingFacesToAlign, Vector3 positionToAlignTo, double offsetX, double offsetY, double offsetZ, string name = "")
			: this(objectToAlign, boundingFacesToAlign, positionToAlignTo + new Vector3(offsetX, offsetY, offsetZ), name)
		{
		}

		public Align(IObject3D item, Face boundingFacesToAlign, Vector3 positionToAlignTo, string name = "")
		{
			AxisAlignedBoundingBox bounds = item.GetAxisAlignedBoundingBox();

			if (IsSet(boundingFacesToAlign, Face.Left, Face.Right))
			{
				positionToAlignTo.X = positionToAlignTo.X - bounds.minXYZ.X;
			}
			if (IsSet(boundingFacesToAlign, Face.Right, Face.Left))
			{
				positionToAlignTo.X = positionToAlignTo.X - bounds.minXYZ.X - (bounds.maxXYZ.X - bounds.minXYZ.X);
			}
			if (IsSet(boundingFacesToAlign, Face.Front, Face.Back))
			{
				positionToAlignTo.Y = positionToAlignTo.Y - bounds.minXYZ.Y;
			}
			if (IsSet(boundingFacesToAlign, Face.Back, Face.Front))
			{
				positionToAlignTo.Y = positionToAlignTo.Y - bounds.minXYZ.Y - (bounds.maxXYZ.Y - bounds.minXYZ.Y);
			}
			if (IsSet(boundingFacesToAlign, Face.Bottom, Face.Top))
			{
				positionToAlignTo.Z = positionToAlignTo.Z - bounds.minXYZ.Z;
			}
			if (IsSet(boundingFacesToAlign, Face.Top, Face.Bottom))
			{
				positionToAlignTo.Z = positionToAlignTo.Z - bounds.minXYZ.Z - (bounds.maxXYZ.Z - bounds.minXYZ.Z);
			}

			Matrix *= Matrix4X4.CreateTranslation(positionToAlignTo);
			Children.Add(item.Clone());
		}

		public static Vector3 GetPositionToAlignTo(IObject3D objectToAlignTo, Face boundingFacesToAlignTo, Vector3 extraOffset)
		{
			Vector3 positionToAlignTo = new Vector3();
			if (IsSet(boundingFacesToAlignTo, Face.Left, Face.Right))
			{
				positionToAlignTo.X = objectToAlignTo.GetAxisAlignedBoundingBox().minXYZ.X;
			}
			if (IsSet(boundingFacesToAlignTo, Face.Right, Face.Left))
			{
				positionToAlignTo.X = objectToAlignTo.GetAxisAlignedBoundingBox().maxXYZ.X;
			}
			if (IsSet(boundingFacesToAlignTo, Face.Front, Face.Back))
			{
				positionToAlignTo.Y = objectToAlignTo.GetAxisAlignedBoundingBox().minXYZ.Y;
			}
			if (IsSet(boundingFacesToAlignTo, Face.Back, Face.Front))
			{
				positionToAlignTo.Y = objectToAlignTo.GetAxisAlignedBoundingBox().maxXYZ.Y;
			}
			if (IsSet(boundingFacesToAlignTo, Face.Bottom, Face.Top))
			{
				positionToAlignTo.Z = objectToAlignTo.GetAxisAlignedBoundingBox().minXYZ.Z;
			}
			if (IsSet(boundingFacesToAlignTo, Face.Top, Face.Bottom))
			{
				positionToAlignTo.Z = objectToAlignTo.GetAxisAlignedBoundingBox().maxXYZ.Z;
			}
			return positionToAlignTo + extraOffset;
		}

		private static bool IsSet(Face variableToCheck, Face faceToCheckFor, Face faceToAssertNot)
		{
			if ((variableToCheck & faceToCheckFor) != 0)
			{
				if ((variableToCheck & faceToAssertNot) != 0)
				{
					throw new Exception("You cannot have both " + faceToCheckFor.ToString() + " and " + faceToAssertNot.ToString() + " set when calling Align.  The are mutually exclusive.");
				}
				return true;
			}

			return false;
		}
	}

	public class Rotate : Object3D
	{
		public Rotate()
		{
		}

		public Rotate(IObject3D item, double x = 0, double y = 0, double z = 0, string name = "")
			: this(item, new Vector3(x, y, z), name)
		{
		}

		public Rotate(IObject3D item, Vector3 translation, string name = "")
		{
			Matrix *= Matrix4X4.CreateRotation(translation);
			Children.Add(item.Clone());
		}
	}

	public class Scale : Object3D
	{
		public Scale()
		{
		}

		public Scale(IObject3D item, double x = 0, double y = 0, double z = 0, string name = "")
			: this(item, new Vector3(x, y, z), name)
		{
		}

		public Scale(IObject3D item, Vector3 translation, string name = "")
		{
			Matrix *= Matrix4X4.CreateScale(translation);
			Children.Add(item.Clone());
		}
	}

	public class TestPart : Object3D, IRebuildable
	{
		public override string ActiveEditor => "PublicPropertyEditor";

		public TestPart()
		{
			Rebuild();
		}

		public double XOffset { get; set; } = -.4;

		public void Rebuild()
		{
			IObject3D boxCombine = new Box(10, 10, 10);
			boxCombine = boxCombine.Minus(new Translate(new Box(10, 10, 10), XOffset, -3, 2));
			this.SetChildren(boxCombine);
		}
	}
}