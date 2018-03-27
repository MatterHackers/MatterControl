using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Csg;
using MatterHackers.VectorMath;

namespace MatterControl.Tests
{
	class RemovedFromProduct
	{
		public static void WriteTestGCodeFile()
		{
			using (StreamWriter file = new StreamWriter("PerformanceTest.gcode"))
			{
				//int loops = 150000;
				int loops = 150;
				int steps = 200;
				double radius = 50;
				Vector2 center = new Vector2(150, 100);

				file.WriteLine("G28 ; home all axes");
				file.WriteLine("G90 ; use absolute coordinates");
				file.WriteLine("G21 ; set units to millimeters");
				file.WriteLine("G92 E0");
				file.WriteLine("G1 F7800");
				file.WriteLine("G1 Z" + (5).ToString());
				WriteMove(file, center);

				for (int loop = 0; loop < loops; loop++)
				{
					for (int step = 0; step < steps; step++)
					{
						Vector2 nextPosition = new Vector2(radius, 0);
						nextPosition.Rotate(MathHelper.Tau / steps * step);
						WriteMove(file, center + nextPosition);
					}
				}

				file.WriteLine("M84     ; disable motors");
			}
		}

		private static void WriteMove(StreamWriter file, Vector2 center)
		{
			file.WriteLine("G1 X" + center.X.ToString() + " Y" + center.Y.ToString());
		}

		private static void HtmlWindowTest()
		{
			try
			{
				SystemWindow htmlTestWindow = new SystemWindow(640, 480);
				string htmlContent = "";
				if (true)
				{
					string releaseNotesFile = Path.Combine("C:\\Users\\lbrubaker\\Downloads", "test1.html");
					htmlContent = File.ReadAllText(releaseNotesFile);
				}
				//else
				//{
				//	WebClient webClient = new WebClient();
				//	htmlContent = webClient.DownloadString("http://www.matterhackers.com/s/store?q=pla");
				//}

				HtmlWidget content = new HtmlWidget(htmlContent, Color.Black);
				content.AddChild(new GuiWidget()
				{
					HAnchor = HAnchor.Absolute,
					VAnchor = VAnchor.Stretch
				});
				content.VAnchor |= VAnchor.Top;
				content.BackgroundColor = Color.White;
				htmlTestWindow.AddChild(content);
				htmlTestWindow.BackgroundColor = Color.Cyan;
				UiThread.RunOnIdle(() =>
				{
					htmlTestWindow.ShowAsSystemWindow();
				}, 1);
			}
			catch
			{
			}
		}

		#region DoBooleanTest
		private bool DoBooleanTest = false;

		Object3D booleanGroup;
		Vector3 offset = new Vector3();
		Vector3 direction = new Vector3(.11, .12, .13);
		Vector3 rotCurrent = new Vector3();
		Vector3 rotChange = new Vector3(.011, .012, .013);
		Vector3 scaleChange = new Vector3(.0011, .0012, .0013);
		Vector3 scaleCurrent = new Vector3(1, 1, 1);

		// TODO: Write test for DoBooleanTest conditional test behavior
		//if (DoBooleanTest)
		//{
		//	BeforeDraw += CreateBooleanTestGeometry;
		//	AfterDraw += RemoveBooleanTestGeometry;
		//}

		private void CreateBooleanTestGeometry(object sender, DrawEventArgs e)
		{
			try
			{
				booleanGroup = new Object3D();

				booleanGroup.Children.Add(new Object3D()
				{
					Mesh = ApplyBoolean(CsgOperations.Union, AxisAlignedBoundingBox.Union, new Vector3(100, 0, 20), "U")
				});

				booleanGroup.Children.Add(new Object3D()
				{
					Mesh = ApplyBoolean(CsgOperations.Subtract, null, new Vector3(100, 100, 20), "S")
				});

				booleanGroup.Children.Add(new Object3D()
				{
					Mesh = ApplyBoolean(CsgOperations.Intersect, AxisAlignedBoundingBox.Intersection, new Vector3(100, 200, 20), "I")
				});

				offset += direction;
				rotCurrent += rotChange;
				scaleCurrent += scaleChange;

				// Create dummy object to fix compilation issues
				IObject3D scene = null;

				scene.Children.Modify(list =>
				{
					list.Add(booleanGroup);
				});
			}
			catch
			{
			}
		}

		private Mesh ApplyBoolean(Func<Mesh, Mesh, Mesh> meshOperation, Func<AxisAlignedBoundingBox, AxisAlignedBoundingBox, AxisAlignedBoundingBox> aabbOperation, Vector3 centering, string opp)
		{
			Mesh boxA = PlatonicSolids.CreateCube(40, 40, 40);
			//boxA = PlatonicSolids.CreateIcosahedron(35);
			boxA.Translate(centering);
			Mesh boxB = PlatonicSolids.CreateCube(40, 40, 40);
			//boxB = PlatonicSolids.CreateIcosahedron(35);

			for (int i = 0; i < 3; i++)
			{
				if (Math.Abs(direction[i] + offset[i]) > 10)
				{
					direction[i] = direction[i] * -1.00073112;
				}
			}

			for (int i = 0; i < 3; i++)
			{
				if (Math.Abs(rotChange[i] + rotCurrent[i]) > 6)
				{
					rotChange[i] = rotChange[i] * -1.000073112;
				}
			}

			for (int i = 0; i < 3; i++)
			{
				if (scaleChange[i] + scaleCurrent[i] > 1.1 || scaleChange[i] + scaleCurrent[i] < .9)
				{
					scaleChange[i] = scaleChange[i] * -1.000073112;
				}
			}

			Vector3 offsetB = offset + centering;
			// switch to the failing offset
			//offsetB = new Vector3(105.240172225344, 92.9716306394062, 18.4619570261172);
			//rotCurrent = new Vector3(4.56890223673623, -2.67874102322035, 1.02768848238523);
			//scaleCurrent = new Vector3(1.07853517569753, 0.964980885267323, 1.09290934544604);
			Debug.WriteLine("t" + offsetB.ToString() + " r" + rotCurrent.ToString() + " s" + scaleCurrent.ToString() + " " + opp);
			Matrix4X4 transformB = Matrix4X4.CreateScale(scaleCurrent) * Matrix4X4.CreateRotation(rotCurrent) * Matrix4X4.CreateTranslation(offsetB);
			boxB.Transform(transformB);

			Mesh meshToAdd = meshOperation(boxA, boxB);

			if (aabbOperation != null)
			{
				AxisAlignedBoundingBox boundsA = boxA.GetAxisAlignedBoundingBox();
				AxisAlignedBoundingBox boundsB = boxB.GetAxisAlignedBoundingBox();
				AxisAlignedBoundingBox boundsAdd = meshToAdd.GetAxisAlignedBoundingBox();

				AxisAlignedBoundingBox boundsResult = aabbOperation(boundsA, boundsB);
			}

			return meshToAdd;
		}

		private void RemoveBooleanTestGeometry(object sender, DrawEventArgs e)
		{
			// Create dummy object to fix compilation issues
			IObject3D scene = null;

			if (scene.Children.Contains(booleanGroup))
			{
				scene.Children.Remove(booleanGroup);

				// TODO: Figure out why this invalidate pump exists and restor
				//UiThread.RunOnIdle(() => Invalidate(), 1.0 / 30.0);
			}
		}
		#endregion DoBooleanTest
	}
}
