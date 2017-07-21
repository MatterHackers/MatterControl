using System;
using System.Collections.Generic;
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.UI;
using MatterHackers.Csg;
using MatterHackers.Csg.Solids;
using MatterHackers.Csg.Transform;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.RenderOpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.SimplePartScripting
{
	public abstract class MatterCadObject3D : Object3D
	{
		public override string ActiveEditor { get; set; } = "MatterCadEditor";

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

		public bool Unlocked { get; } = true;

		public IEnumerable<Type> SupportedTypes() => new Type[]
		{
			typeof(MatterCadObject3D),
		};

		public GuiWidget Create(IObject3D item, View3DWidget view3DWidget, ThemeConfig theme)
		{
			this.view3DWidget = view3DWidget;
			this.item = item;

			var mainContainer = new FlowLayoutWidget(FlowDirection.TopToBottom);

			var tabContainer = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.AbsolutePosition,
				Visible = true,
				Width = theme.WhiteButtonFactory.FixedWidth
			};
			mainContainer.AddChild(tabContainer);

			if (item is MatterCadObject3D)
			{
				ModifyCadObject(view3DWidget, tabContainer, theme);
			}
			return mainContainer;
		}

		private void ModifyCadObject(View3DWidget view3DWidget, FlowLayoutWidget tabContainer, ThemeConfig theme)
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

			var updateButton = theme.textImageButtonFactory.Generate("Update".Localize());
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

	public class TestPart : MatterCadObject3D, IMappingType
	{
		public double XOffset { get; set; } = -.4;

		public TestPart()
		{
			Mesh = Create();
		}

		public override PolygonMesh.Mesh Create()
		{
			CsgObject boxCombine = new Box(10, 10, 10);
			boxCombine -= new Translate(new Box(10, 10, 10), XOffset, -3, 2);
			return CsgToMesh.Convert(boxCombine);
		}
	}
	
	public class CardHolder : MatterCadObject3D, IMappingType
	{
		public string NameToWrite { get; set; }
		public CardHolder()
		{
			Mesh = Create();
		}

		public override PolygonMesh.Mesh Create()
		{
			CsgObject plainCardHolder = new MeshContainer("PlainBusinessCardHolder.stl");

			TypeFace typeFace = TypeFace.LoadSVG("Viking_n.svg");

			var letterPrinter = new TypeFacePrinter(NameToWrite, new StyledTypeFace(typeFace, 12));
			PolygonMesh.Mesh textMesh = VertexSourceToMesh.Extrude(letterPrinter, 5);

			CsgObject nameMesh = new MeshContainer(textMesh);

			AxisAlignedBoundingBox textBounds = textMesh.GetAxisAlignedBoundingBox();
			var textArea = new Vector2(85, 20);

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

	public class ChairFoot : MatterCadObject3D, IMappingType
	{
		// these are the public variables that would be edited
		public bool FinalPart { get; set; } = true;

		public double HeightFromFloorToBottomOfLeg { get; set; } = 10;

		public double OuterSize { get; set; } = 22;

		public double InnerSize { get; set; } = 20;
		public double InsideReach { get; set; } = 10;

		public double AngleDegrees { get; set; } = 3;

		public ChairFoot()
		{
			Mesh = Create();
		}

		public override MatterHackers.PolygonMesh.Mesh Create()
		{
			// This would be better expressed as the desired offset height (height from ground to bottom of chair leg).
			double angleRadians = MathHelper.DegreesToRadians(AngleDegrees);
			double extraHeightForRotation = Math.Sinh(angleRadians) * OuterSize; // get the distance to clip off the extra bottom
			double unclippedFootHeight = HeightFromFloorToBottomOfLeg + extraHeightForRotation;

			if(FinalPart)
			{
				Box chairFootBox = new Box(OuterSize, OuterSize, unclippedFootHeight);
				//chairFootBox.BevelEdge(Edge.LeftBack, 2);
				//chairFootBox.BevelEdge(Edge.LeftFront, 2);
				//chairFootBox.BevelEdge(Edge.RightBack, 2);
				//chairFootBox.BevelEdge(Edge.RightFront, 2);
				CsgObject chairFoot = chairFootBox;

				CsgObject ring = new Cylinder(InnerSize / 2 - 1, InsideReach);
				ring -= new Cylinder(ring.XSize / 2 - 2, ring.ZSize + 1);

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
				chairFoot = new Translate(chairFoot, 0, 0, clipBox.GetAxisAlignedBoundingBox().maxXYZ.z);

				return CsgToMesh.Convert(chairFoot);
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

				CsgObject ring = new Cylinder(InnerSize / 2 - 1, insideHeight);
				ring -= new Cylinder(ring.XSize / 2 - 2, ring.ZSize + 1);

				CsgObject fins = new Box(3, 1, ring.ZSize);
				fins = new Translate(fins, 0, 1) + new Translate(fins, 0, -1);
				fins -= new Align(new Rotate(new Box(5, 5, 5), 0, MathHelper.DegreesToRadians(45)), Face.Bottom | Face.Left, fins, Face.Top | Face.Left, 0, 0, -fins.XSize);
				fins = new Translate(fins, InnerSize / 2 - .1);

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
