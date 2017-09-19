using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.VectorMath;
using MatterHackers.PolygonMesh;

namespace MatterHackers.MatterControl.SimplePartScripting
{
	public class BendOperation
	{
		[DisplayName("Bend Up")]
		public bool BendCW { get; set; } = true;

		[DisplayName("Angle")]
		public double AngleDegrees { get; set; } = 45;

		IObject3D operationContainer;

		public BendOperation(IObject3D child)
		{
			var scene = ApplicationController.Instance.ActivePrinter.Bed.Scene;

			operationContainer = new Object3D()
			{
				ID = Guid.NewGuid().ToString(),
				Parent = scene
			};
			operationContainer.Children.Add(child);

			scene.Children.Remove(child);
			scene.Children.Add(operationContainer);
			child.Parent = operationContainer;

			scene.SelectedItem = operationContainer;

			// wrap all the meshes with replacement meshes
			foreach(var meshChild in child.Descendants().Where(x => x.Mesh != null).ToList())
			{
				var parent = meshChild.Parent;

				var object3D = meshChild.Clone();

				object3D.OwnerID = operationContainer.ID;
				object3D.Parent = parent;
				object3D.Mesh = Mesh.Copy(meshChild.Mesh, CancellationToken.None);

				object3D.Children.Add(meshChild);

				meshChild.Parent = object3D;
				parent.Children.Remove(meshChild);
				parent.Children.Add(object3D);
			}

			RebuildMeshes();
		}

		public void RebuildMeshes()
		{
			if(AngleDegrees > 0)
			{
				foreach (var meshChild in operationContainer.Descendants().Where(x => x.OwnerID == operationContainer.ID))
				{

					var aabb = meshChild.Children[0].Mesh.GetAxisAlignedBoundingBox();

					// find the radius that will make the x-size sweep out the requested angle
					// c = Tr ; r = c/T
					var angleRadians = MathHelper.DegreesToRadians(AngleDegrees);
					var circumference = aabb.XSize * MathHelper.Tau / angleRadians;
					var radius = circumference / MathHelper.Tau;

					var rotateXyPos = new Vector2(aabb.minXYZ.x, BendCW ? aabb.maxXYZ.y : aabb.minXYZ.y);
					if (!BendCW)
					{
						angleRadians = -angleRadians;
					}

					for (int i = 0; i < meshChild.Mesh.Vertices.Count; i++)
					{
						var pos = meshChild.Children[0].Mesh.Vertices[i].Position;
						var pos2D = new Vector2(pos);
						Vector2 rotateSpace = pos2D - rotateXyPos;
						var rotateRatio = rotateSpace.x / aabb.XSize;

						rotateSpace.x = 0;
						rotateSpace.y += BendCW ? -radius : radius;
						rotateSpace.Rotate(angleRadians * rotateRatio);
						rotateSpace.y += BendCW ? radius : -radius; ;
						rotateSpace += rotateXyPos;

						meshChild.Mesh.Vertices[i].Position = new Vector3(rotateSpace.x, rotateSpace.y, pos.z);
					}

					meshChild.Mesh.MarkAsChanged();
					meshChild.Mesh.CalculateNormals();
				}
			}
			else
			{
				//for (int i = 0; i < transformedMesh.Vertices.Count; i++)
				//{
//					transformedMesh.Vertices[i].Position = inputMesh.Vertices[i].Position;
				//}
			}

			//SetAndInvalidateMesh(transformedMesh);
		}
	}
}
