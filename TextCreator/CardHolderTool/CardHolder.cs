using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
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

		public abstract void RebuildMeshes();
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

			var mainContainer = new FlowLayoutWidget(FlowDirection.TopToBottom)
			{
				HAnchor = HAnchor.ParentLeftRight
			};

			if (item is MatterCadObject3D)
			{
				ModifyCadObject(view3DWidget, mainContainer, theme);
			}

			mainContainer.MinimumSize = new Vector2(250, 0);
			return mainContainer;
		}

		private void ModifyCadObject(View3DWidget view3DWidget, FlowLayoutWidget tabContainer, ThemeConfig theme)
		{
			var allowedTypes = new Type[] { typeof(double), typeof(string), typeof(bool) };

			var ownedPropertiesOnly = System.Reflection.BindingFlags.Public
				| System.Reflection.BindingFlags.Instance
				| System.Reflection.BindingFlags.DeclaredOnly;

			var editableProperties = this.item.GetType().GetProperties(ownedPropertiesOnly)
				.Where(pi => allowedTypes.Contains(pi.PropertyType)
					&& pi.GetGetMethod() != null)
				.Select(p => new
				{
					Value = p.GetGetMethod().Invoke(this.item, null),
					DisplayName = GetDisplayName(p),
					PropertyInfo = p
				});

			foreach (var property in editableProperties)
			{
				// create a double editor
				if (property.Value is double doubleValue)
				{
					FlowLayoutWidget rowContainer = CreateSettingsRow(property.DisplayName.Localize());
					var doubleEditWidget = new MHNumberEdit(doubleValue, pixelWidth: 50 * GuiWidget.DeviceScale, allowNegatives: true, allowDecimals: true, increment: .05)
					{
						SelectAllOnFocus = true,
						VAnchor = VAnchor.ParentCenter
					};
					doubleEditWidget.ActuallNumberEdit.EditComplete += (s, e) =>
					{
						double editValue;
						if (double.TryParse(doubleEditWidget.Text, out editValue))
						{
							property.PropertyInfo.GetSetMethod().Invoke(this.item, new Object[] { editValue });
						}
						((MatterCadObject3D)item).RebuildMeshes();
					};
					rowContainer.AddChild(doubleEditWidget);
					tabContainer.AddChild(rowContainer);
				}
				// create a bool editor
				else if (property.Value is bool boolValue)
				{
					FlowLayoutWidget rowContainer = CreateSettingsRow(property.DisplayName.Localize());

					var doubleEditWidget = new CheckBox("");
					doubleEditWidget.Checked = boolValue;
					doubleEditWidget.CheckedStateChanged += (s, e) =>
					{
						property.PropertyInfo.GetSetMethod().Invoke(this.item, new Object[] { doubleEditWidget.Checked });
						((MatterCadObject3D)item).RebuildMeshes();
					};
					rowContainer.AddChild(doubleEditWidget);
					tabContainer.AddChild(rowContainer);
				}
				// create a bool editor
				else if (property.Value is string stringValue)
				{
					FlowLayoutWidget rowContainer = CreateSettingsRow(property.DisplayName.Localize());
					var textEditWidget = new MHTextEditWidget(stringValue, pixelWidth: 150 * GuiWidget.DeviceScale)
					{
						SelectAllOnFocus = true,
						VAnchor = VAnchor.ParentCenter
					};
					textEditWidget.ActualTextEditWidget.EditComplete += (s, e) =>
					{
						property.PropertyInfo.GetSetMethod().Invoke(this.item, new Object[] { textEditWidget.Text });
						((MatterCadObject3D)item).RebuildMeshes();
					};
					rowContainer.AddChild(textEditWidget);
					tabContainer.AddChild(rowContainer);
				}
			}

			var updateButton = theme.textImageButtonFactory.Generate("Update".Localize());
			updateButton.Margin = new BorderDouble(5);
			updateButton.HAnchor = HAnchor.ParentRight;
			updateButton.Click += (s, e) =>
			{
				((MatterCadObject3D)item).RebuildMeshes();
			};
			tabContainer.AddChild(updateButton);
		}

		private string GetDisplayName(PropertyInfo prop)
		{
			var nameAttribute = prop.GetCustomAttributes(true).OfType<DisplayNameAttribute>().FirstOrDefault();
			return nameAttribute?.DisplayName ?? prop.Name;
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
			RebuildMeshes();
		}

		public override void RebuildMeshes()
		{
			CsgObject boxCombine = new Box(10, 10, 10);
			boxCombine -= new Translate(new Box(10, 10, 10), XOffset, -3, 2);
			SetAndInvalidateMesh(CsgToMesh.Convert(boxCombine));
		}
	}
	
	public class CardHolder : MatterCadObject3D, IMappingType
	{
		[DisplayName("Name")]
		public string NameToWrite { get; set; } = "MatterHackers";

		public CardHolder()
		{
			RebuildMeshes();
		}

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

			double scale = Math.Min(textArea.x / textBounds.XSize, textArea.y / textBounds.YSize);
			nameMesh = new Scale(nameMesh, scale, scale, 1);
			nameMesh = new Align(nameMesh, Face.Top | Face.Front, plainCardHolder, Face.Bottom | Face.Front);
			nameMesh = new SetCenter(nameMesh, plainCardHolder.GetCenter(), true, false, false);

			nameMesh = new Rotate(nameMesh, MathHelper.DegreesToRadians(18));
			nameMesh = new Translate(nameMesh, 0, 2, 16);

			// output one combined mesh
			//plainCardHolder += nameMesh;
			//SetAndInvalidateMesh(CsgToMesh.Convert(plainCardHolder));

			// output two meshes for card holder and text
			this.Children.Clear();

			this.Children.AddRange(new[]
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


			this.SetAndInvalidateMesh(null);
		}
	}

	public class ChairFoot : MatterCadObject3D, IMappingType
	{
		// these are the public variables that would be edited
		[DisplayName("Final")]
		public bool FinalPart { get; set; } = true;

		[DisplayName("Height")]
		public double HeightFromFloorToBottomOfLeg { get; set; } = 10;

		[DisplayName("Outer Size")]
		public double OuterSize { get; set; } = 22;

		[DisplayName("Inner Size")]
		public double InnerSize { get; set; } = 20;
		[DisplayName("Reach")]
		public double InsideReach { get; set; } = 10;

		[DisplayName("Angle")]
		public double AngleDegrees { get; set; } = 3;

		public ChairFoot()
		{
			RebuildMeshes();
		}

		public override void RebuildMeshes()
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

				SetAndInvalidateMesh(CsgToMesh.Convert(chairFoot));
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

				SetAndInvalidateMesh(CsgToMesh.Convert(chairFoot));
			}
		}
	}
}
