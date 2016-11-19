using System;
using System.Collections.Generic;
using System.Diagnostics;
using MatterHackers.Csg;
using MatterHackers.Agg.Font;
using MatterHackers.Csg.Solids;
using MatterHackers.Csg.Transform;
using MatterHackers.Csg.Processors;
using MatterHackers.VectorMath;
using MatterHackers.RenderOpenGl;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.Agg.UI;
using MatterHackers.MatterControl;
using MatterHackers.Localizations;
using MatterHackers.Agg;
using MatterHackers.MatterControl.CustomWidgets;
using System.Linq;

namespace MatterHackers.MatterControl.SimplePartScripting
{
	public class MatterCadObject3D : Object3D
	{
		public override string ActiveEditor { get; set; } = "MatterCadEditor";
		public string TypeName { get; } = nameof(MatterCadObject3D);

		public Dictionary<string, string> Settings { get; set; } = new Dictionary<string, string>();

		public virtual MatterHackers.PolygonMesh.Mesh Create()
		{
			return Mesh;
		}
	}

	public class MatterCadEditor : IObject3DEditor
	{
		private View3DWidget view3DWidget;
		private IObject3D item;

		public string Name => "MatterCad";

		public IEnumerable<Type> SupportedTypes() => new Type[]
		{
			typeof(MatterCadObject3D),
		};

		public GuiWidget Create(IObject3D item, View3DWidget view3DWidget)
		{
			this.view3DWidget = view3DWidget;
			this.item = item;

			var mainContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);

			var tabContainer = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.AbsolutePosition,
				Visible = true,
				Width = view3DWidget.WhiteButtonFactory.FixedWidth
			};
			mainContainer.AddChild(tabContainer);

			if (item is MatterCadObject3D)
			{
				ModifyCadObject(view3DWidget, tabContainer);
			}
			return mainContainer;
		}

		private void ModifyCadObject(View3DWidget view3DWidget, FlowLayoutWidget tabContainer)
		{
			var stringPropertyNamesAndValues = this.item.GetType()
				.GetProperties()
				.Where(pi => pi.PropertyType == typeof(Double) && pi.GetGetMethod() != null)
				.Select(pi => new
				{
					Name = pi.Name,
					Value = pi
				});

			foreach (var nameValue in stringPropertyNamesAndValues)
			{
				FlowLayoutWidget rowContainer = CreateSettingsRow(nameValue.Name.Localize());
				var doubleEditWidget = new MHNumberEdit((double)nameValue.Value.GetGetMethod().Invoke(this.item, null), pixelWidth: 50 * GuiWidget.DeviceScale, allowNegatives: true, allowDecimals: true, increment: .05)
				{
					SelectAllOnFocus = true,
					VAnchor = VAnchor.ParentCenter
				};
				doubleEditWidget.ActuallNumberEdit.EditComplete += (s, e) =>
				{
					double editValue;
					if (double.TryParse(doubleEditWidget.Text, out editValue))
					{
						nameValue.Value.GetSetMethod().Invoke(this.item, new Object[] { editValue });
					}
					this.item.SetAndInvalidateMesh(((MatterCadObject3D)item).Create());
				};
				rowContainer.AddChild(doubleEditWidget);
				tabContainer.AddChild(rowContainer);
			}

			var updateButton = view3DWidget.textImageButtonFactory.Generate("Update".Localize());
			updateButton.Margin = new BorderDouble(5);
			updateButton.HAnchor = HAnchor.ParentRight;
			updateButton.Click += (s, e) =>
			{
				this.item.SetAndInvalidateMesh(((MatterCadObject3D)item).Create());
			};
			tabContainer.AddChild(updateButton);
		}

		private static FlowLayoutWidget CreateSettingsRow(string labelText)
		{
			var rowContainer = new FlowLayoutWidget(FlowDirection.LeftToRight)
			{
				HAnchor = HAnchor.ParentLeftRight,
				Padding = new BorderDouble(5)
			};

			var label = new TextWidget(labelText + ":", textColor: ActiveTheme.Instance.PrimaryTextColor)
			{
				Margin = new BorderDouble(0, 0, 3, 0),
				VAnchor = VAnchor.ParentCenter
			};
			rowContainer.AddChild(label);

			rowContainer.AddChild(new HorizontalSpacer());

			return rowContainer;
		}
	}

	public class TestPart : MatterCadObject3D
	{
		public double XOffset { get; set; } = -5;

		public TestPart()
		{
			Mesh = Create();
		}

		public override MatterHackers.PolygonMesh.Mesh Create()
		{
			CsgObject boxCombine = new Box(10, 10, 10);
			boxCombine -= new Translate(new Box(10, 10, 10), XOffset, -3, 2);
			return CsgToMesh.Convert(boxCombine);
		}
	}
	
	public class CardHolder : MatterCadObject3D
	{
		// these are the public variables that would be edited
		public string Name { get; set; } = "Name";

		public MatterHackers.PolygonMesh.Mesh Create()
		{
			CsgObject plainCardHolder = new MeshContainer("PlainBusinessCardHolder.stl");

			TypeFace typeFace = TypeFace.LoadSVG("Viking_n.svg");

			TypeFacePrinter letterPrinter = new TypeFacePrinter(Name, new StyledTypeFace(typeFace, 12));
			MatterHackers.PolygonMesh.Mesh textMesh = VertexSourceToMesh.Extrude(letterPrinter, 5);

			CsgObject nameMesh = new MeshContainer(textMesh);

			AxisAlignedBoundingBox textBounds = textMesh.GetAxisAlignedBoundingBox();
			Vector2 textArea = new Vector2(85, 20);

			nameMesh = new Box(textArea.x, textArea.y, 5);


			double scale = Math.Min(textArea.x / textBounds.XSize, textArea.y / textBounds.YSize);
			nameMesh = new Scale(nameMesh, scale, scale, 1);
			nameMesh = new Align(nameMesh, Face.Top | Face.Front, plainCardHolder, Face.Bottom | Face.Front);
			nameMesh = new SetCenter(nameMesh, plainCardHolder.GetCenter(), true, false, false);

			nameMesh = new Rotate(nameMesh, MathHelper.DegreesToRadians(18));
			nameMesh = new Translate(nameMesh, 0, 2, 16);

			plainCardHolder += nameMesh;

			return CsgToMesh.Convert(plainCardHolder);
		}
	}

	public class ChairFoot : MatterCadObject3D
	{
		// these are the public variables that would be edited
		double heightFromFloorToBottomOfLeg = 10;

		double outerSize = 22;

		double innerSize = 20;
		double insideReach = 10;

		double angleDegrees = 3;

		public MatterHackers.PolygonMesh.Mesh Create()
		{
			// This would be better expressed as the desired offset height (height from ground to bottom of chair leg).
			double angleRadians = MathHelper.DegreesToRadians(angleDegrees);
			double extraHeightForRotation = Math.Sinh(angleRadians) * outerSize; // get the distance to clip off the extra bottom
			double unclippedFootHeight = heightFromFloorToBottomOfLeg + extraHeightForRotation;

			{
				Box chairFootBox = new Box(outerSize, outerSize, unclippedFootHeight);
				chairFootBox.BevelEdge(Edge.LeftBack, 2);
				chairFootBox.BevelEdge(Edge.LeftFront, 2);
				chairFootBox.BevelEdge(Edge.RightBack, 2);
				chairFootBox.BevelEdge(Edge.RightFront, 2);
				CsgObject chairFoot = chairFootBox;

				CsgObject ring = new Cylinder(innerSize / 2 - 1, insideReach);
				ring -= new Cylinder(ring.XSize / 2 - 2, ring.ZSize + 1);

				CsgObject fins = new Box(3, 1, ring.ZSize);
				fins = new Translate(fins, 0, 1) + new Translate(fins, 0, -1);
				fins -= new Align(new Rotate(new Box(5, 5, 5), 0, MathHelper.DegreesToRadians(45)), Face.Bottom | Face.Left, fins, Face.Top | Face.Left, 0, 0, -fins.XSize);
				fins = new Translate(fins, innerSize / 2 - .1);

				ring += new Rotate(fins, 0, 0, MathHelper.DegreesToRadians(45));
				ring += new Rotate(fins, 0, 0, MathHelper.DegreesToRadians(45 + 90));
				ring += new Rotate(fins, 0, 0, MathHelper.DegreesToRadians(45 + 180));
				ring += new Rotate(fins, 0, 0, MathHelper.DegreesToRadians(45 - 90));

				chairFoot += new Align(ring, Face.Bottom, chairFoot, Face.Top, 0, 0, -.1);

				chairFoot = new Rotate(chairFoot, 0, angleRadians, 0);
				chairFoot -= new Align(new Box(outerSize * 2, outerSize * 2, unclippedFootHeight), Face.Top, chairFoot, Face.Bottom, 0, 0, extraHeightForRotation);
				OpenSCadOutput.Save(chairFoot, "Chair Foot.scad");

				MatterHackers.PolygonMesh.Mesh mesh = CsgToMesh.Convert(chairFoot);
				MatterHackers.PolygonMesh.Processors.StlProcessing.Save(mesh, "Chair Foot.stl");
			}

			{
				double baseHeight = 3;
				double insideHeight = 4;
				Box chairFootBox = new Box(outerSize, outerSize, baseHeight);
				chairFootBox.BevelEdge(Edge.LeftBack, 2);
				chairFootBox.BevelEdge(Edge.LeftFront, 2);
				chairFootBox.BevelEdge(Edge.RightBack, 2);
				chairFootBox.BevelEdge(Edge.RightFront, 2);
				CsgObject chairFoot = chairFootBox;

				CsgObject ring = new Cylinder(innerSize / 2 - 1, insideHeight);
				ring -= new Cylinder(ring.XSize / 2 - 2, ring.ZSize + 1);

				CsgObject fins = new Box(3, 1, ring.ZSize);
				fins = new Translate(fins, 0, 1) + new Translate(fins, 0, -1);
				fins -= new Align(new Rotate(new Box(5, 5, 5), 0, MathHelper.DegreesToRadians(45)), Face.Bottom | Face.Left, fins, Face.Top | Face.Left, 0, 0, -fins.XSize);
				fins = new Translate(fins, innerSize / 2 - .1);

				ring += new Rotate(fins, 0, 0, MathHelper.DegreesToRadians(45));
				ring += new Rotate(fins, 0, 0, MathHelper.DegreesToRadians(45 + 90));
				ring += new Rotate(fins, 0, 0, MathHelper.DegreesToRadians(45 + 180));
				ring += new Rotate(fins, 0, 0, MathHelper.DegreesToRadians(45 - 90));

				chairFoot += new Align(ring, Face.Bottom, chairFoot, Face.Top, 0, 0, -.1);

				return CsgToMesh.Convert(chairFoot);
			}
		}
	}
}
