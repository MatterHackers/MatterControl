/*
Copyright (c) 2016, John Lewin
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

using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl;
using MatterHackers.MatterControl.Library;
using MatterHackers.MatterControl.Tests.Automation;
using MatterHackers.MeshVisualizer;
using MatterHackers.VectorMath;
using Newtonsoft.Json;
using NUnit.Framework;

namespace MatterHackers.PolygonMesh.UnitTests
{
	[TestFixture, Category("Agg.PolygonMesh"), RunInApplicationDomain]
	public class SceneTests
	{
		[Test]
		public void SaveSimpleScene()
		{
			var scene = new InteractiveScene();
			scene.Children.Add(new Object3D());

			string tempPath = GetSceneTempPath();

			Object3D.AssetsPath = Path.Combine(tempPath, "Assets");

			string filePath = Path.Combine(tempPath, "some.mcx");

			scene.Save(filePath);

			Assert.IsTrue(File.Exists(filePath));

			IObject3D loadedItem = Object3D.Load(filePath, CancellationToken.None);
			Assert.IsTrue(loadedItem.Children.Count == 1);
		}

		[Test]
		public void CreatesAndLinksAmfsForUnsavedMeshes()
		{
			var scene = new InteractiveScene();
			scene.Children.Add(new Object3D
			{
				Mesh = PlatonicSolids.CreateCube(20, 20, 20)
			});

			string tempPath = GetSceneTempPath();
			string filePath = Path.Combine(tempPath, "some.mcx");

			Object3D.AssetsPath = Path.Combine(tempPath, "Assets");

			scene.Save(filePath);

			Assert.IsTrue(File.Exists(filePath));

			IObject3D loadedItem = Object3D.Load(filePath, CancellationToken.None);
			Assert.IsTrue(loadedItem.Children.Count == 1);

			IObject3D meshItem = loadedItem.Children.First();

			Assert.IsTrue(!string.IsNullOrEmpty(meshItem.MeshPath));

			Assert.IsTrue(File.Exists(Path.Combine(tempPath, "Assets", meshItem.MeshPath)));
			Assert.IsNotNull(meshItem.Mesh);
			Assert.IsTrue(meshItem.Mesh.Faces.Count > 0);
		}

		[Test]
		public async Task ResavedSceneRemainsConsistent()
		{
#if !__ANDROID__
			// Set the static data to point to the directory of MatterControl
			AggContext.StaticData = new FileSystemStaticData(TestContext.CurrentContext.ResolveProjectPath(4, "StaticData"));
			MatterControlUtilities.OverrideAppDataLocation(TestContext.CurrentContext.ResolveProjectPath(4));
#endif

			var sceneContext = new BedConfig(null);

			var scene = sceneContext.Scene;
			scene.Children.Add(new Object3D
			{
				Mesh = PlatonicSolids.CreateCube(20, 20, 20)
			});

			string tempPath = GetSceneTempPath();
			string filePath = Path.Combine(tempPath, "some.mcx");

			// Set directory for asset resolution
			Object3D.AssetsPath = Path.Combine(tempPath, "Assets");
			Directory.CreateDirectory(Object3D.AssetsPath);

			// Empty temp folder
			foreach (string tempFile in Directory.GetFiles(tempPath).ToList())
			{
				File.Delete(tempFile);
			}

			scene.Save(filePath);
			Assert.AreEqual(1, Directory.GetFiles(tempPath).Length, "Only .mcx file should exists");
			Assert.AreEqual(1, Directory.GetFiles(Path.Combine(tempPath, "Assets")).Length, "Only 1 asset should exist");

			var originalFiles = Directory.GetFiles(tempPath).ToArray();

			// Load the file from disk
			IObject3D loadedItem = Object3D.Load(filePath, CancellationToken.None);
			Assert.AreEqual(1, loadedItem.Children.Count);

			// Ensure the UI scene is cleared
			scene.Children.Modify(list => list.Clear());

			// Reload the model
			await Task.Run(() =>
			{
				sceneContext.Scene.Load(Object3D.Load(filePath, CancellationToken.None));
			});

			// Serialize and compare the two trees
			string onDiskData = loadedItem.ToJson();
			string inMemoryData = scene.ToJson();

			//File.WriteAllText(@"c:\temp\file-a.txt", onDiskData);
			//File.WriteAllText(@"c:\temp\file-b.txt", inMemoryData);

			Assert.AreEqual(inMemoryData, onDiskData, "Serialized content should match");
			Object3D.AssetsPath = tempPath;

			// Save the scene a second time, validate that things remain the same
			scene.Save(filePath);
			onDiskData = loadedItem.ToJson();

			Assert.IsTrue(inMemoryData == onDiskData);

			// Verify that no additional files get created on second save
			Assert.AreEqual(1, Directory.GetFiles(tempPath).Length, "Only .mcx file should exists");
			Assert.AreEqual(1, Directory.GetFiles(Path.Combine(tempPath, "Assets")).Length, "Only 1 asset should exist");
		}

		private readonly int RootMaterialIndex = 1;
		private readonly int SuperGroupMaterialIndex = 2;
		private readonly int GroupMaterialIndex = 3;
		private readonly int RedMaterialIndex = 4;
		private readonly int GreenMaterialIndex = 5;
		private readonly int BlueMaterialIndex = 6;

		private readonly Matrix4X4 RootMatrix = Matrix4X4.Identity;
		private readonly Matrix4X4 SuperGroupMatrix = Matrix4X4.CreateScale(2);
		private readonly Matrix4X4 GroupMatrix = Matrix4X4.Identity;

		private readonly Matrix4X4 RedMatrix = Matrix4X4.CreateTranslation(10, 0, 0);
		private readonly Matrix4X4 GreenMatrix = Matrix4X4.CreateTranslation(15, 0, 0);
		private readonly Matrix4X4 BlueMatrix = Matrix4X4.CreateTranslation(20, 0, 0);

		private readonly PrintOutputTypes RootOutputType = PrintOutputTypes.Solid;
		private readonly PrintOutputTypes SuperGroupOutputType = PrintOutputTypes.Hole;
		private readonly PrintOutputTypes GroupOutputType = PrintOutputTypes.Solid;

		private readonly PrintOutputTypes RedOutputType = PrintOutputTypes.Support;
		private readonly PrintOutputTypes GreenOutputType = PrintOutputTypes.Support;
		private readonly PrintOutputTypes BlueOutputType = PrintOutputTypes.Hole;

		public InteractiveScene SampleScene()
		{
			Object3D group;

			var scene = new InteractiveScene()
			{
				Color = Color.Black,
				MaterialIndex = this.RootMaterialIndex,
				OutputType = this.RootOutputType
			};

			var supergroup = new Object3D()
			{
				Name = "SuperGroup",
				Color = Color.Violet,
				MaterialIndex = this.SuperGroupMaterialIndex,
				Matrix = this.SuperGroupMatrix,
				OutputType = this.SuperGroupOutputType
			};
			scene.Children.Add(supergroup);

			group = new Object3D()
			{
				Name = "GroupA",
				Color = Color.Pink,
				MaterialIndex = this.GroupMaterialIndex,
				OutputType = this.GroupOutputType
			}; 

			supergroup.Children.Add(group);

			group.Children.Add(new Object3D
			{
				Name = nameof(Color.Red),
				Color = Color.Red,
				MaterialIndex = this.RedMaterialIndex,
				Matrix = this.RedMatrix,
				OutputType = this.RedOutputType
			});

			group = new Object3D()
			{
				Name = "GroupB",
				Color = Color.Pink,
				MaterialIndex = this.GroupMaterialIndex,
				OutputType = this.GroupOutputType
			};
			supergroup.Children.Add(group);

			group.Children.Add(new Object3D
			{
				Name = nameof(Color.Green),
				Color = Color.Green,
				MaterialIndex = this.GreenMaterialIndex,
				Matrix = this.GreenMatrix,
				OutputType = this.GreenOutputType
			});

			group = new Object3D()
			{
				Name = "GroupB",
				Color = Color.Pink,
				MaterialIndex = this.GroupMaterialIndex,
				OutputType = this.GroupOutputType
			};
			supergroup.Children.Add(group);

			group.Children.Add(new Object3D
			{
				Name = nameof(Color.Blue),
				Color = Color.Blue,
				MaterialIndex = this.BlueMaterialIndex,
				Matrix = this.BlueMatrix,
				OutputType = this.BlueOutputType
			});

			return scene;
		}

		[Test]
		public void WorldColorBasicTest()
		{
			var scene = SampleScene();

			var superGroup = scene.Descendants().Where(d => d.Name == "SuperGroup").FirstOrDefault();
			var redItem = scene.Descendants().Where(d => d.Name == nameof(Color.Red)).FirstOrDefault();
			var greenItem = scene.Descendants().Where(d => d.Name == nameof(Color.Green)).FirstOrDefault();
			var blueItem = scene.Descendants().Where(d => d.Name == nameof(Color.Blue)).FirstOrDefault();

			// Validate root
			Assert.AreEqual(Color.Black, scene.Color, "Color property on root should be Black");
			Assert.AreEqual(Color.Black, scene.WorldColor(), "WorldColor on root should be Black");

			// Validate red node
			Assert.AreEqual(Color.Red, redItem.Color, "Color property on node should be Red");
			Assert.AreEqual(Color.Pink, redItem.WorldColor(redItem.Parent), "WorldColor on Red up to parent node should be Pink");
			Assert.AreEqual(Color.Violet, redItem.WorldColor(superGroup), "WorldColor on Red up to supergroup should be Violet");

			// Validate green node
			Assert.AreEqual(Color.Green, greenItem.Color, "Color property on node should be Green");
			Assert.AreEqual(Color.Pink, greenItem.WorldColor(greenItem.Parent), "WorldColor on Green up to parent node should be Pink");
			Assert.AreEqual(Color.Violet, greenItem.WorldColor(superGroup), "WorldColor on Green up to supergroup should be Violet");

			// Validate green node
			Assert.AreEqual(Color.Blue, blueItem.Color, "Color property on node should be Green");
			Assert.AreEqual(Color.Pink, blueItem.WorldColor(blueItem.Parent), "WorldColor on Blue up to parent node should be Pink");
			Assert.AreEqual(Color.Violet, blueItem.WorldColor(superGroup), "WorldColor on Blue up to supergroup should be Violet");

			// Validate WorldColor with null param
			Assert.AreEqual(Color.Black, redItem.WorldColor(null), "WorldColor on Red with null param should be root color (Black)");
		}

		[Test]
		public void WorldMaterialIndexBasicTest()
		{
			var scene = SampleScene();

			var superGroup = scene.Descendants().Where(d => d.Name == "SuperGroup").FirstOrDefault();
			var redItem = scene.Descendants().Where(d => d.Name == nameof(Color.Red)).FirstOrDefault();
			var greenItem = scene.Descendants().Where(d => d.Name == nameof(Color.Green)).FirstOrDefault();
			var blueItem = scene.Descendants().Where(d => d.Name == nameof(Color.Blue)).FirstOrDefault();

			// Validate root
			Assert.AreEqual(this.RootMaterialIndex, scene.MaterialIndex, "MaterialIndex property on root should be RootMaterialIndex");
			Assert.AreEqual(this.RootMaterialIndex, scene.WorldMaterialIndex(), "WorldMaterialIndex on root should be RootMaterialIndex");

			// Validate red node
			Assert.AreEqual(this.RedMaterialIndex, redItem.MaterialIndex, "MaterialIndex property on node should be RedMaterialIndex");
			Assert.AreEqual(this.GroupMaterialIndex, redItem.WorldMaterialIndex(redItem.Parent), "WorldMaterialIndex on Red up to parent node should be GroupMaterialIndex");
			Assert.AreEqual(this.SuperGroupMaterialIndex, redItem.WorldMaterialIndex(superGroup), "WorldMaterialIndex on Red up to supergroup should be SuperGroupMaterialIndex");

			// Validate green node
			Assert.AreEqual(this.GreenMaterialIndex, greenItem.MaterialIndex, "MaterialIndex property on node should be GreenMaterialIndex");
			Assert.AreEqual(this.GroupMaterialIndex, greenItem.WorldMaterialIndex(greenItem.Parent), "WorldMaterialIndex on Green up to parent node should be GroupMaterialIndex");
			Assert.AreEqual(this.SuperGroupMaterialIndex, greenItem.WorldMaterialIndex(superGroup), "WorldMaterialIndex on Green up to supergroup should be SuperGroupMaterialIndex");

			// Validate green node
			Assert.AreEqual(this.BlueMaterialIndex, blueItem.MaterialIndex, "MaterialIndex property on node should be BlueMaterialIndex");
			Assert.AreEqual(this.GroupMaterialIndex, blueItem.WorldMaterialIndex(blueItem.Parent), "WorldMaterialIndex on Blue up to parent node should be GroupMaterialIndex");
			Assert.AreEqual(this.SuperGroupMaterialIndex, blueItem.WorldMaterialIndex(superGroup), "WorldMaterialIndex on Blue up to supergroup should be SuperGroupMaterialIndex");

			// Validate MaterialIndex with null param
			Assert.AreEqual(this.RootMaterialIndex, redItem.WorldMaterialIndex(null), "WorldMaterialIndex on Red with null param should be root color (RootMaterialIndex)");
		}

		[Test]
		public void WorldMatrixBasicTest()
		{
			var scene = SampleScene();

			var superGroup = scene.Descendants().Where(d => d.Name == "SuperGroup").FirstOrDefault();
			var redItem = scene.Descendants().Where(d => d.Name == nameof(Color.Red)).FirstOrDefault();
			var greenItem = scene.Descendants().Where(d => d.Name == nameof(Color.Green)).FirstOrDefault();
			var blueItem = scene.Descendants().Where(d => d.Name == nameof(Color.Blue)).FirstOrDefault();

			// Validate root
			Assert.AreEqual(this.RootMatrix, scene.Matrix, "Matrix property on root should be RootMatrix");
			Assert.AreEqual(this.RootMatrix, scene.WorldMatrix(), "WorldMatrix on root should be RootMatrix");

			// Validate red node
			Assert.AreEqual(this.RedMatrix, redItem.Matrix, "Matrix property on node should be RedMatrix");
			Assert.AreEqual(redItem.Matrix * this.GroupMatrix, redItem.WorldMatrix(redItem.Parent), "WorldMatrix on Red up to parent node should be GroupMatrix");
			Assert.AreEqual(this.RedMatrix * this.GroupMatrix * this.SuperGroupMatrix, redItem.WorldMatrix(superGroup), "WorldMatrix on Red up to supergroup invalid");

			// Validate green node
			Assert.AreEqual(this.GreenMatrix, greenItem.Matrix, "Matrix property on node should be GreenMatrix");
			Assert.AreEqual(this.GreenMatrix * this.GroupMatrix, greenItem.WorldMatrix(greenItem.Parent), "WorldMatrix on Green up to parent node should be GroupMatrix");
			Assert.AreEqual(this.GreenMatrix * this.GroupMatrix * this.SuperGroupMatrix, greenItem.WorldMatrix(superGroup), "WorldMatrix on Green up to supergroup should be SuperGroupMatrix");

			// Validate green node
			Assert.AreEqual(this.BlueMatrix, blueItem.Matrix, "Matrix property on node should be BlueMatrix");
			Assert.AreEqual(this.BlueMatrix * this.GroupMatrix, blueItem.WorldMatrix(blueItem.Parent), "WorldMatrix on Blue up to parent node should be GroupMatrix");
			Assert.AreEqual(this.BlueMatrix * this.GroupMatrix * this.SuperGroupMatrix, blueItem.WorldMatrix(superGroup), "WorldMatrix on Blue up to supergroup should be SuperGroupMatrix");

			// Validate Matrix with null param
			Assert.AreEqual(this.RedMatrix * this.GroupMatrix * this.SuperGroupMatrix, redItem.WorldMatrix(null), "WorldMatrix on Red with null param should be root color (RootMatrix)");
		}

		[Test]
		public void WorldOutputTypeBasicTest()
		{
			var scene = SampleScene();

			var superGroup = scene.Descendants().Where(d => d.Name == "SuperGroup").FirstOrDefault();
			var redItem = scene.Descendants().Where(d => d.Name == nameof(Color.Red)).FirstOrDefault();
			var greenItem = scene.Descendants().Where(d => d.Name == nameof(Color.Green)).FirstOrDefault();
			var blueItem = scene.Descendants().Where(d => d.Name == nameof(Color.Blue)).FirstOrDefault();

			// Validate root
			Assert.AreEqual(this.RootOutputType, scene.OutputType, "OutputType property on root should be RootOutputType");
			Assert.AreEqual(this.RootOutputType, scene.WorldOutputType(), "WorldOutputType on root should be RootOutputType");

			// Validate red node
			Assert.AreEqual(this.RedOutputType, redItem.OutputType, "OutputType property on node should be RedOutputType");
			Assert.AreEqual(this.GroupOutputType, redItem.WorldOutputType(redItem.Parent), "WorldOutputType on Red up to parent node should be GroupOutputType");
			Assert.AreEqual(this.SuperGroupOutputType, redItem.WorldOutputType(superGroup), "WorldOutputType on Red up to supergroup should be SuperGroupOutputType");

			// Validate green node
			Assert.AreEqual(this.GreenOutputType, greenItem.OutputType, "OutputType property on node should be GreenOutputType");
			Assert.AreEqual(this.GroupOutputType, greenItem.WorldOutputType(greenItem.Parent), "WorldOutputType on Green up to parent node should be GroupOutputType");
			Assert.AreEqual(this.SuperGroupOutputType, greenItem.WorldOutputType(superGroup), "WorldOutputType on Green up to supergroup should be SuperGroupOutputType");

			// Validate green node
			Assert.AreEqual(this.BlueOutputType, blueItem.OutputType, "OutputType property on node should be BlueOutputType");
			Assert.AreEqual(this.GroupOutputType, blueItem.WorldOutputType(blueItem.Parent), "WorldOutputType on Blue up to parent node should be GroupOutputType");
			Assert.AreEqual(this.SuperGroupOutputType, blueItem.WorldOutputType(superGroup), "WorldOutputType on Blue up to supergroup should be SuperGroupOutputType");

			// Validate OutputType with null param
			Assert.AreEqual(this.RootOutputType, redItem.WorldOutputType(null), "WorldOutputType on Red with null param should be root color (RootOutputType)");
		}

		[Test]
		public void WorldFunctionNonExistingAncestorOverride()
		{
			var scene = SampleScene();
			var redItem = scene.Descendants().Where(d => d.Name == nameof(Color.Red)).FirstOrDefault();
			var nonAncestor = new Object3D();

			// ************************************* WorldColor *************************************
			// Validate root
			Assert.AreEqual(Color.Black, scene.Color, "Color property on root should be Black");
			Assert.AreEqual(Color.Black, scene.WorldColor(), "WorldColor on root should be Black");

			// Validate red node
			Assert.AreEqual(Color.Red, redItem.Color, "Color property on node should be Red");

			// Validate WorldColor with non-ancestor param
			Assert.AreEqual(Color.Black, redItem.WorldColor(nonAncestor), "WorldColor on Red with non-ancestor should be root color (Black)");

			// ************************************* MaterialIndex *************************************
			// Validate root
			Assert.AreEqual(this.RootMaterialIndex, scene.MaterialIndex, "MaterialIndex property on root should be RootMaterialIndex");
			Assert.AreEqual(this.RootMaterialIndex, scene.WorldMaterialIndex(), "WorldMaterialIndex on root should be RootMaterialIndex");

			// Validate red node
			Assert.AreEqual(this.RedMaterialIndex, redItem.MaterialIndex, "Color property on node should be Red");

			// Validate WorldColor with non-ancestor param
			Assert.AreEqual(this.RootMaterialIndex, redItem.WorldMaterialIndex(nonAncestor), "WorldMaterialIndex on Red with non-ancestor should be RootMaterialIndex");

			// ************************************* WorldMaxtrix *************************************
			// Validate root
			Assert.AreEqual(this.RootMatrix, scene.Matrix, "Matrix property on root should be RootMatrix");
			Assert.AreEqual(this.RootMatrix, scene.WorldMatrix(), "WorldMatrix on root should be RootMatrix");

			// Validate red node
			Assert.AreEqual(this.RedMatrix, redItem.Matrix, "Matrix property on node should be RedMatrix");

			// Validate WorldColor with non-ancestor param
			Assert.AreEqual(this.RedMatrix * this.GroupMatrix * this.SuperGroupMatrix, redItem.WorldMatrix(nonAncestor), "WorldMatrix on Red with non-ancestor should be RootMaterialIndex");

			// ************************************* WorldOutputType *************************************
			// Validate root
			Assert.AreEqual(this.RootOutputType, scene.OutputType, "OutputType property on root should be RootOutputType");
			Assert.AreEqual(this.RootOutputType, scene.WorldOutputType(), "WorldOutputType on root should be RootOutputType");


			// Validate red node
			Assert.AreEqual(this.RedOutputType, redItem.OutputType, "Color property on node should be Red");

			// Validate WorldColor with non-ancestor param
			Assert.AreEqual(this.RootOutputType, redItem.WorldOutputType(nonAncestor), "WorldOutputType on Red with non-ancestor should be RootOutputType");
		}

		public static string GetSceneTempPath()
		{
			string tempPath = TestContext.CurrentContext.ResolveProjectPath(4, "Tests", "temp", "scenetests");
			Directory.CreateDirectory(tempPath);

			return tempPath;
		}
	}
}