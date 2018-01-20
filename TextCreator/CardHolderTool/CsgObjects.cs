using System;
using System.ComponentModel;
using System.IO;
using MatterHackers.Agg;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Platform;
using MatterHackers.Csg;
using MatterHackers.Csg.Solids;
using MatterHackers.Csg.Transform;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl.MatterCad;
using MatterHackers.RenderOpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.SimplePartScripting
{
	public class BadSubtract : MatterCadObject3D
	{
		public BadSubtract()
		{
			RebuildMeshes();
		}

		public double Sides { get; set; } = 4;

		public override void RebuildMeshes()
		{
			int sides = 3;
			CsgObject keep = new Cylinder(20, 20, sides);
			CsgObject subtract = new Cylinder(10, 21, sides);
			subtract = new SetCenter(subtract, keep.GetCenter());
			CsgObject result = keep - subtract;
			this.Mesh = CsgToMesh.Convert(result);
		}
	}

	public class CardHolder : MatterCadObject3D
	{
		public CardHolder()
		{
			RebuildMeshes();
		}

		[DisplayName("Name")]
		public string NameToWrite { get; set; } = "MatterHackers";

		public override void RebuildMeshes()
		{
			CsgObject plainCardHolder = new MeshContainer("PlainBusinessCardHolder.stl");

			//TypeFace typeFace = TypeFace.LoadSVG("Viking_n.svg");

			var letterPrinter = new TypeFacePrinter(NameToWrite);//, new StyledTypeFace(typeFace, 12));
			PolygonMesh.Mesh textMesh = VertexSourceToMesh.Extrude(letterPrinter, 5);

			CsgObject nameMesh = new MeshContainer(textMesh);

			AxisAlignedBoundingBox textBounds = textMesh.GetAxisAlignedBoundingBox();
			var textArea = new Vector2(85, 20);

			// test the area that the names will go to
			//nameMesh = new Box(textArea.x, textArea.y, 5);

			double scale = Math.Min(textArea.X / textBounds.XSize, textArea.Y / textBounds.YSize);
			nameMesh = new Scale(nameMesh, scale, scale, 1);
			nameMesh = new Align(nameMesh, Face.Top | Face.Front, plainCardHolder, Face.Bottom | Face.Front);
			nameMesh = new SetCenter(nameMesh, plainCardHolder.GetCenter(), true, false, false);

			nameMesh = new Rotate(nameMesh, MathHelper.DegreesToRadians(18));
			nameMesh = new Translate(nameMesh, 0, 2, 16);

			// output one combined mesh
			//plainCardHolder += nameMesh;
			//SetAndInvalidateMesh(CsgToMesh.Convert(plainCardHolder));

			// output two meshes for card holder and text
			this.Children.Modify(list =>
			{
				list.Clear();
				list.AddRange(new[]
				{
					new Object3D()
					{
						Mesh = CsgToMesh.Convert(plainCardHolder)
					},
					new Object3D()
					{
						Mesh = CsgToMesh.Convert(nameMesh)
					}
				});
			});

			this.Mesh = null;
		}
	}

	public class ChairFoot : MatterCadObject3D
	{
		public ChairFoot()
		{
			RebuildMeshes();
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

		public override void RebuildMeshes()
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
				CsgObject chairFoot = chairFootBox;

				CsgObject ring = new Cylinder(InnerSize / 2 - 1, InsideReach, 30);
				ring -= new Cylinder(ring.XSize / 2 - 2, ring.ZSize + 1, 30);

				CsgObject fins = new Box(3, 1, ring.ZSize);
				fins = new Translate(fins, 0, 1) + new Translate(fins, 0, -1);
				fins -= new Align(new Rotate(new Box(5, 5, 5), 0, MathHelper.DegreesToRadians(45)), Face.Bottom | Face.Left, fins, Face.Top | Face.Left, 0, 0, -fins.XSize);
				fins = new Translate(fins, InnerSize / 2 - .1);

				ring += new Rotate(fins, 0, 0, MathHelper.DegreesToRadians(45));
				ring += new Rotate(fins, 0, 0, MathHelper.DegreesToRadians(45 + 90));
				ring += new Rotate(fins, 0, 0, MathHelper.DegreesToRadians(45 + 180));
				ring += new Rotate(fins, 0, 0, MathHelper.DegreesToRadians(45 - 90));

				chairFoot += new Align(ring, Face.Bottom, chairFoot, Face.Top, 0, 0, -.1);

				chairFoot = new Rotate(chairFoot, 0, angleRadians, 0);
				CsgObject clipBox = new Align(new Box(OuterSize * 2, OuterSize * 2, unclippedFootHeight), Face.Top, chairFoot, Face.Bottom, 0, 0, extraHeightForRotation);
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
				CsgObject chairFoot = chairFootBox;

				CsgObject ring = new Cylinder(InnerSize / 2 - 1, insideHeight, 30);
				ring -= new Cylinder(ring.XSize / 2 - 2, ring.ZSize + 1, 30);

				CsgObject fins = new Box(3, 1, ring.ZSize);
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

	public class PvcT : MatterCadObject3D
	{
		private int sides = 50;

		public PvcT()
		{
			RebuildMeshes();
		}

		public double BottomReach { get; set; } = 30;

		public double FrontReach { get; set; } = 25;

		[DisplayName("Inner Radius")]
		public double InnerDiameter { get; set; } = 15;

		[DisplayName("Outer Radius")]
		public double OuterDiameter { get; set; } = 20;

		public double TopReach { get; set; } = 30;

		public override void RebuildMeshes()
		{
			CsgObject topBottomConnect = new Cylinder(OuterDiameter / 2, OuterDiameter, sides, Alignment.y);
			CsgObject frontConnect = new Cylinder(OuterDiameter / 2, OuterDiameter / 2, sides, Alignment.x);
			frontConnect = new Align(frontConnect, Face.Right, topBottomConnect, Face.Right);

			CsgObject bottomReach = new Rotate(CreateReach(BottomReach), -MathHelper.Tau / 4);
			bottomReach = new Align(bottomReach, Face.Back, topBottomConnect, Face.Front, 0, 1);

			CsgObject topReach = new Rotate(CreateReach(TopReach), MathHelper.Tau / 4);
			topReach = new Align(topReach, Face.Front, topBottomConnect, Face.Back, 0, -1);

			CsgObject frontReach = new Rotate(CreateReach(FrontReach), 0, -MathHelper.Tau / 4);
			frontReach = new Align(frontReach, Face.Left, topBottomConnect, Face.Right, -1);

			// output multiple meshes for pipe connector
			this.Children.Modify(list =>
			{
				list.Clear();
				list.AddRange(new[]
				{
					new Object3D()
					{
						Mesh = CsgToMesh.Convert(topBottomConnect),
						Color = Color.LightGray
					},
					new Object3D()
					{
						Mesh = CsgToMesh.Convert(frontConnect),
						Color = Color.LightGray
					},
					new Object3D()
					{
						Mesh = CsgToMesh.Convert(bottomReach),
						Color = Color.White
					},
					new Object3D()
					{
						Mesh = CsgToMesh.Convert(topReach),
						Color = Color.White
					},
					new Object3D()
					{
						Mesh = CsgToMesh.Convert(frontReach),
						Color = Color.White
					}
				});
			});

			this.Color = Color.Transparent;
			this.Mesh = null;
		}

		private CsgObject CreateReach(double reach)
		{
			var finWidth = 4.0;
			var finLength = InnerDiameter;
			var fin1 = new Box(finWidth, finLength, reach);
			fin1.ChamferEdge(Face.Top | Face.Back, finLength / 8);
			fin1.ChamferEdge(Face.Top | Face.Front, finLength / 8);
			CsgObject fin2 = new Rotate(fin1, 0, 0, MathHelper.Tau / 4);

			return fin1 + fin2;
		}
	}

	public class RibonWithName : MatterCadObject3D
	{
		private static TypeFace typeFace = null;

		public RibonWithName()
		{
			RebuildMeshes();
		}

		[DisplayName("Name")]
		public string NameToWrite { get; set; } = "MatterHackers";

		public override void RebuildMeshes()
		{
			CsgObject cancerRibonStl = new MeshContainer("Cancer_Ribbon.stl");

			cancerRibonStl = new Rotate(cancerRibonStl, MathHelper.DegreesToRadians(90));

			if (typeFace == null)
			{
				typeFace = TypeFace.LoadFrom(AggContext.StaticData.ReadAllText(Path.Combine("Fonts", "TitilliumWeb-Black.svg")));
			}

			var letterPrinter = new TypeFacePrinter(NameToWrite.ToUpper(), new StyledTypeFace(typeFace, 12));
			PolygonMesh.Mesh textMesh = VertexSourceToMesh.Extrude(letterPrinter, 5);

			CsgObject nameMesh = new MeshContainer(textMesh);

			AxisAlignedBoundingBox textBounds = textMesh.GetAxisAlignedBoundingBox();
			var textArea = new Vector2(25, 6);

			double scale = Math.Min(textArea.X / textBounds.XSize, textArea.Y / textBounds.YSize);
			nameMesh = new Scale(nameMesh, scale, scale, 2 / textBounds.ZSize);
			nameMesh = new Align(nameMesh, Face.Bottom | Face.Front, cancerRibonStl, Face.Top | Face.Front, 0, 0, -1);
			nameMesh = new SetCenter(nameMesh, cancerRibonStl.GetCenter(), true, false, false);

			nameMesh = new Rotate(nameMesh, 0, 0, MathHelper.DegreesToRadians(50));
			nameMesh = new Translate(nameMesh, -37, -14, -1);

			// output two meshes

			this.Children.Modify(list =>
			{
				list.Clear();
				list.AddRange(new[]
				{
				new Object3D()
				{
					Mesh = CsgToMesh.Convert(cancerRibonStl)
				},
				new Object3D()
				{
					Mesh = CsgToMesh.Convert(nameMesh)
				}
				});
			});

			this.Mesh = null;
		}
	}
}