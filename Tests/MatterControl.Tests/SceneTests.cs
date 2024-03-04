/*
Copyright (c) 2022, Lars Brubaker, John Lewin
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

using Matter_CAD_Lib.DesignTools.Interfaces;
using Matter_CAD_Lib.DesignTools.Objects3D;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl;
using MatterHackers.MatterControl.PartPreviewWindow;
using MatterHackers.MatterControl.Tests.Automation;
using MatterHackers.VectorMath;
using Xunit;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;


namespace MatterHackers.PolygonMesh.UnitTests
{
    //[TestFixture, Category("Agg.PolygonMesh"), Parallelizable(ParallelScope.Children)]
    public class SceneTests
    {
        private readonly int BlueMaterialIndex = 6;

        private readonly Matrix4X4 BlueMatrix = Matrix4X4.CreateTranslation(20, 0, 0);

        private readonly PrintOutputTypes BlueOutputType = PrintOutputTypes.Solid;

        private readonly int GreenMaterialIndex = 5;

        private readonly Matrix4X4 GreenMatrix = Matrix4X4.CreateTranslation(15, 0, 0);

        private readonly int GroupMaterialIndex = 3;

        private readonly Matrix4X4 GroupMatrix = Matrix4X4.Identity;

        private readonly PrintOutputTypes GroupOutputType = PrintOutputTypes.Solid;

        private readonly int RedMaterialIndex = 4;

        private readonly Matrix4X4 RedMatrix = Matrix4X4.CreateTranslation(10, 0, 0);

        private readonly int RootMaterialIndex = 1;

        private readonly Matrix4X4 RootMatrix = Matrix4X4.Identity;

        private readonly PrintOutputTypes RootOutputType = PrintOutputTypes.Solid;

        private readonly int SuperGroupMaterialIndex = 2;

        private readonly Matrix4X4 SuperGroupMatrix = Matrix4X4.CreateScale(2);

        private readonly PrintOutputTypes SuperGroupOutputType = PrintOutputTypes.Solid;

        public static string GetSceneTempPath(string folder)
        {
            string tempPath = Path.Combine(MatterControlUtilities.RootPath, "Tests", "temp", folder);
            Directory.CreateDirectory(tempPath);

            return tempPath;
        }

        [StaFact]
        public void AmfFilesSaveObjectProperties()
        {
            AssetObject3D.AssetManager = new AssetManager();

            var scene = new InteractiveScene();
            scene.Children.Add(new Object3D
            {
                Mesh = PlatonicSolids.CreateCube(20, 20, 20),
                Name = "test1",
                Color = Color.Red,
            });

            scene.Children.Add(new Object3D
            {
                Mesh = PlatonicSolids.CreateCube(20, 20, 20),
                Name = "test2",
                Matrix = Matrix4X4.CreateTranslation(30, 0, 0)
            });

            string tempPath = GetSceneTempPath("temp_amf");

            Object3D.AssetsPath = Path.Combine(tempPath, "Assets");

            string filePath = Path.Combine(tempPath, "exportTest.amf");
            scene.SetSelection(scene.Children.ToList());
            AmfDocument.Save(scene.SelectedItem, filePath);

            Assert.True(File.Exists(filePath));

            IObject3D loadedItem = Object3D.Load(filePath, CancellationToken.None);
            Assert.True(loadedItem.Children.Count == 2);

            IObject3D item1 = loadedItem.Children.Last();
            Assert.Equal("test1", item1.Name);
            Assert.Equal(Color.Red, item1.Color);
            Assert.Equal(12, item1.Mesh.Faces.Count);
            var aabb1 = item1.GetAxisAlignedBoundingBox();
            Assert.True(new AxisAlignedBoundingBox(-10, -10, -10, 10, 10, 10).Equals(aabb1, .001));

            IObject3D item2 = loadedItem.Children.First();
            Assert.Equal("test2", item2.Name);
            Assert.Equal(Color.White, item2.Color);
            Assert.Equal(12, item2.Mesh.Faces.Count);
            var aabb2 = item2.GetAxisAlignedBoundingBox();
            Assert.True(new AxisAlignedBoundingBox(20, -10, -10, 40, 10, 10).Equals(aabb2, .001));
        }

        [StaFact]
        public async Task AutoArrangeChildrenTests()
        {
            // arrange a single item around the origin
            {
                var scene = new InteractiveScene();
                Object3D cube1;
                scene.Children.Add(cube1 = new Object3D()
                {
                    Mesh = PlatonicSolids.CreateCube(20, 20, 20),
                    Matrix = Matrix4X4.CreateTranslation(34, 22, 10)
                });

                Assert.True(new AxisAlignedBoundingBox(24, 12, 0, 44, 32, 20).Equals(cube1.GetAxisAlignedBoundingBox(), .001));

                await scene.AutoArrangeChildren(Vector3.Zero);

                Assert.True(new AxisAlignedBoundingBox(-10, -10, 0, 10, 10, 20).Equals(cube1.GetAxisAlignedBoundingBox(), .001));
            }

            // arrange a single item around a typical bed center
            {
                var scene = new InteractiveScene();
                Object3D cube1;
                scene.Children.Add(cube1 = new Object3D()
                {
                    Mesh = PlatonicSolids.CreateCube(20, 20, 20),
                    Matrix = Matrix4X4.CreateTranslation(34, 22, 10)
                });

                Assert.True(new AxisAlignedBoundingBox(24, 12, 0, 44, 32, 20).Equals(cube1.GetAxisAlignedBoundingBox(), .001));

                await scene.AutoArrangeChildren(new Vector3(100, 100, 0));

                Assert.True(new AxisAlignedBoundingBox(90, 90, 0, 110, 110, 20).Equals(cube1.GetAxisAlignedBoundingBox(), .001));
            }

            // arrange 4 items
            {
                var scene = new InteractiveScene();
                for (int i = 0; i < 4; i++)
                {
                    scene.Children.Add(new Object3D()
                    {
                        Mesh = PlatonicSolids.CreateCube(20, 20, 20),
                        Matrix = Matrix4X4.CreateTranslation(i * 134, i * -122, 10)
                    });
                }

                var sceneAabb = scene.GetAxisAlignedBoundingBox();
                Assert.True(sceneAabb.XSize > 160);
                Assert.True(sceneAabb.YSize > 160);

                await scene.AutoArrangeChildren(Vector3.Zero);

                sceneAabb = scene.GetAxisAlignedBoundingBox();
                Assert.True(sceneAabb.XSize < 60);
                Assert.True(sceneAabb.YSize < 75);
            }

            // arrange 4 items, starting with 1 selected
            {
                var scene = new InteractiveScene();
                Object3D child = null;
                for (int i = 0; i < 4; i++)
                {
                    scene.Children.Add(child = new Object3D()
                    {
                        Mesh = PlatonicSolids.CreateCube(20, 20, 20),
                        Matrix = Matrix4X4.CreateTranslation(i * 134, i * -122, 10)
                    });
                }

                scene.SelectedItem = child;

                var sceneAabb = scene.GetAxisAlignedBoundingBox();
                Assert.True(sceneAabb.XSize > 160);
                Assert.True(sceneAabb.YSize > 160);

                await scene.AutoArrangeChildren(Vector3.Zero);

                sceneAabb = scene.GetAxisAlignedBoundingBox();
                Assert.True(sceneAabb.XSize < 60);
                Assert.True(sceneAabb.YSize < 75);
            }
        }

        [StaFact]
        public void CreatesAndLinksAmfsForUnsavedMeshes()
        {
            AssetObject3D.AssetManager = new AssetManager();

            var scene = new InteractiveScene();
            scene.Children.Add(new Object3D
            {
                Mesh = PlatonicSolids.CreateCube(20, 20, 20)
            });

            string tempPath = GetSceneTempPath("links_amf");
            string filePath = Path.Combine(tempPath, "some.mcx");

            Object3D.AssetsPath = Path.Combine(tempPath, "Assets");

            scene.Save(filePath);

            Assert.True(File.Exists(filePath));

            IObject3D loadedItem = Object3D.Load(filePath, CancellationToken.None);
            Assert.True(loadedItem.Children.Count == 1);

            IObject3D meshItem = loadedItem.Children.First();

            Assert.True(!string.IsNullOrEmpty(meshItem.MeshPath));

            Assert.True(File.Exists(Path.Combine(tempPath, "Assets", meshItem.MeshPath)));
            Assert.NotNull(meshItem.Mesh);
            Assert.True(meshItem.Mesh.Faces.Count > 0);
        }

        [StaFact]
        public async Task ResavedSceneRemainsConsistent()
        {
            AssetObject3D.AssetManager = new AssetManager();

            var sceneContext = new BedConfig(null);

            var scene = sceneContext.Scene;
            scene.Children.Add(new Object3D
            {
                Mesh = PlatonicSolids.CreateCube(20, 20, 20)
            });

            string tempPath = GetSceneTempPath("resave_scene");
            string filePath = Path.Combine(tempPath, "some.mcx");

            // Set directory for asset resolution
            Object3D.AssetsPath = Path.Combine(tempPath, "Assets");
            if (Directory.Exists(Object3D.AssetsPath))
            {
                Directory.Delete(Object3D.AssetsPath, true);
            }
            Directory.CreateDirectory(Object3D.AssetsPath);

            scene.Save(filePath);
            Assert.Equal(1, Directory.GetFiles(tempPath).Length);//, "Only .mcx file should exists");
            Assert.Equal(1, Directory.GetFiles(Path.Combine(tempPath, "Assets")).Length);//, "Only 1 asset should exist");

            var originalFiles = Directory.GetFiles(tempPath).ToArray();

            // Load the file from disk
            IObject3D loadedItem = Object3D.Load(filePath, CancellationToken.None);
            Assert.Equal(1, loadedItem.Children.Count);

            // Ensure the UI scene is cleared
            scene.Children.Modify(list => list.Clear());

            // Reload the model
            await Task.Run(() =>
            {
                sceneContext.Scene.Load(Object3D.Load(filePath, CancellationToken.None));
            });

            // Serialize and compare the two trees
            string onDiskData = loadedItem.ToJson().Result;
            string inMemoryData = scene.ToJson().Result;

            //File.WriteAllText(@"c:\temp\file-a.txt", onDiskData);
            //File.WriteAllText(@"c:\temp\file-b.txt", inMemoryData);

            Assert.Equal(inMemoryData, onDiskData);//, "Serialized content should match");
            Object3D.AssetsPath = Path.Combine(tempPath, "Assets");

            // Save the scene a second time, validate that things remain the same
            scene.Save(filePath);
            onDiskData = loadedItem.ToJson().Result;

            Assert.True(inMemoryData == onDiskData);

            // Verify that no additional files get created on second save
            Assert.Equal(1, Directory.GetFiles(tempPath).Length);//, "Only .mcx file should exists");
            Assert.Equal(1, Directory.GetFiles(Path.Combine(tempPath, "Assets")).Length);//, "Only 1 asset should exist");
        }

        public InteractiveScene SampleScene()
        {
            Object3D group;

            var scene = new InteractiveScene()
            {
                Color = Color.Black,
                OutputType = this.RootOutputType
            };

            var supergroup = new Object3D()
            {
                Name = "SuperGroup",
                Color = Color.Violet,
                Matrix = this.SuperGroupMatrix,
                OutputType = this.SuperGroupOutputType
            };
            scene.Children.Add(supergroup);

            group = new Object3D()
            {
                Name = "GroupA",
                Color = Color.Pink,
                OutputType = this.GroupOutputType
            };

            supergroup.Children.Add(group);

            group.Children.Add(new Object3D
            {
                Name = nameof(Color.Red),
                Color = Color.Red,
                Matrix = this.RedMatrix,
            });

            group = new Object3D()
            {
                Name = "GroupB",
                Color = Color.Pink,
                OutputType = this.GroupOutputType
            };
            supergroup.Children.Add(group);

            group.Children.Add(new Object3D
            {
                Name = nameof(Color.Green),
                Color = Color.Green,
                Matrix = this.GreenMatrix,
            });

            group = new Object3D()
            {
                Name = "GroupB",
                Color = Color.Pink,
                OutputType = this.GroupOutputType
            };
            supergroup.Children.Add(group);

            group.Children.Add(new Object3D
            {
                Name = nameof(Color.Blue),
                Color = Color.Blue,
                Matrix = this.BlueMatrix,
                OutputType = this.BlueOutputType
            });

            return scene;
        }

        [StaFact]
        public void SaveSimpleScene()
        {
            var scene = new InteractiveScene();
            scene.Children.Add(new Object3D());

            string tempPath = GetSceneTempPath("simple_scene");

            Object3D.AssetsPath = Path.Combine(tempPath, "Assets");

            string filePath = Path.Combine(tempPath, "some.mcx");

            scene.Save(filePath);

            Assert.True(File.Exists(filePath));

            IObject3D loadedItem = Object3D.Load(filePath, CancellationToken.None);
            Assert.True(loadedItem.Children.Count == 1);
        }

        public SceneTests()
        {
            StaticData.RootPath = MatterControlUtilities.StaticDataPath;
            MatterControlUtilities.OverrideAppDataLocation(MatterControlUtilities.RootPath);

            UserSettings.Instance.set(UserSettingsKey.PublicProfilesSha, "0"); //Clears DB so we will download the latest list
        }

        [StaFact]
        public void WorldColorBasicTest()
        {
            var scene = SampleScene();

            var superGroup = scene.DescendantsAndSelf().Where(d => d.Name == "SuperGroup").FirstOrDefault();
            var redItem = scene.DescendantsAndSelf().Where(d => d.Name == nameof(Color.Red)).FirstOrDefault();
            var greenItem = scene.DescendantsAndSelf().Where(d => d.Name == nameof(Color.Green)).FirstOrDefault();
            var blueItem = scene.DescendantsAndSelf().Where(d => d.Name == nameof(Color.Blue)).FirstOrDefault();

            // Validate root
            Assert.Equal(Color.Black, scene.Color);//, "Color property on root should be Black");
            Assert.Equal(Color.Black, scene.WorldColor());//, "WorldColor on root should be Black");

            // Validate red node
            Assert.Equal(Color.Red, redItem.Color);//, "Color property on node should be Red");
            Assert.Equal(Color.Pink, redItem.WorldColor(redItem.Parent));//, "WorldColor on Red up to parent node should be Pink");
            Assert.Equal(Color.Violet, redItem.WorldColor(superGroup));//, "WorldColor on Red up to supergroup should be Violet");

            // Validate green node
            Assert.Equal(Color.Green, greenItem.Color);//, "Color property on node should be Green");
            Assert.Equal(Color.Pink, greenItem.WorldColor(greenItem.Parent));//, "WorldColor on Green up to parent node should be Pink");
            Assert.Equal(Color.Violet, greenItem.WorldColor(superGroup));//, "WorldColor on Green up to supergroup should be Violet");

            // Validate green node
            Assert.Equal(Color.Blue, blueItem.Color);//, "Color property on node should be Green");
            Assert.Equal(Color.Pink, blueItem.WorldColor(blueItem.Parent));//, "WorldColor on Blue up to parent node should be Pink");
            Assert.Equal(Color.Violet, blueItem.WorldColor(superGroup));//, "WorldColor on Blue up to supergroup should be Violet");

            // Validate WorldColor with null param
            Assert.Equal(Color.Black, redItem.WorldColor(null));//, "WorldColor on Red with null param should be root color (Black)");
        }

        [StaFact]
        public void WorldFunctionNonExistingAncestorOverride()
        {
            var scene = SampleScene();
            var redItem = scene.DescendantsAndSelf().Where(d => d.Name == nameof(Color.Red)).FirstOrDefault();
            var nonAncestor = new Object3D();

            // ************************************* WorldColor *************************************
            // Validate root
            Assert.Equal(Color.Black, scene.Color);//, "Color property on root should be Black");
            Assert.Equal(Color.Black, scene.WorldColor());//, "WorldColor on root should be Black");

            // Validate red node
            Assert.Equal(Color.Red, redItem.Color);//, "Color property on node should be Red");

            // Validate WorldColor with non-ancestor param
            Assert.Equal(Color.Black, redItem.WorldColor(nonAncestor));//, "WorldColor on Red with non-ancestor should be root color (Black)");

            // ************************************* WorldMaxtrix *************************************
            // Validate root
            Assert.Equal(this.RootMatrix, scene.Matrix);//, "Matrix property on root should be RootMatrix");
            Assert.Equal(this.RootMatrix, scene.WorldMatrix());//, "WorldMatrix on root should be RootMatrix");

            // Validate red node
            Assert.Equal(this.RedMatrix, redItem.Matrix);//, "Matrix property on node should be RedMatrix");

            // Validate WorldColor with non-ancestor param
            Assert.Equal(this.RedMatrix * this.GroupMatrix * this.SuperGroupMatrix, redItem.WorldMatrix(nonAncestor));//, "WorldMatrix on Red with non-ancestor should be RootMaterialIndex");

            // ************************************* WorldOutputType *************************************
            // Validate root
            Assert.Equal(this.RootOutputType, scene.OutputType);//, "OutputType property on root should be RootOutputType");
            Assert.Equal(this.RootOutputType, scene.WorldOutputType());//, "WorldOutputType on root should be RootOutputType");

            // Validate WorldColor with non-ancestor param
            Assert.Equal(this.RootOutputType, redItem.WorldOutputType(nonAncestor));//, "WorldOutputType on Red with non-ancestor should be RootOutputType");
        }

        [StaFact]
        public void WorldMaterialIndexBasicTest()
        {
            var scene = SampleScene();

            var superGroup = scene.DescendantsAndSelf().Where(d => d.Name == "SuperGroup").FirstOrDefault();
            var redItem = scene.DescendantsAndSelf().Where(d => d.Name == nameof(Color.Red)).FirstOrDefault();
            var greenItem = scene.DescendantsAndSelf().Where(d => d.Name == nameof(Color.Green)).FirstOrDefault();
            var blueItem = scene.DescendantsAndSelf().Where(d => d.Name == nameof(Color.Blue)).FirstOrDefault();
        }

        [StaFact]
        public void WorldMatrixBasicTest()
        {
            var scene = SampleScene();

            var superGroup = scene.DescendantsAndSelf().Where(d => d.Name == "SuperGroup").FirstOrDefault();
            var redItem = scene.DescendantsAndSelf().Where(d => d.Name == nameof(Color.Red)).FirstOrDefault();
            var greenItem = scene.DescendantsAndSelf().Where(d => d.Name == nameof(Color.Green)).FirstOrDefault();
            var blueItem = scene.DescendantsAndSelf().Where(d => d.Name == nameof(Color.Blue)).FirstOrDefault();

            // Validate root
            Assert.Equal(this.RootMatrix, scene.Matrix);//, "Matrix property on root should be RootMatrix");
            Assert.Equal(this.RootMatrix, scene.WorldMatrix());//, "WorldMatrix on root should be RootMatrix");

            // Validate red node
            Assert.Equal(this.RedMatrix, redItem.Matrix);//, "Matrix property on node should be RedMatrix");
            Assert.Equal(redItem.Matrix * this.GroupMatrix, redItem.WorldMatrix(redItem.Parent));//, "WorldMatrix on Red up to parent node should be GroupMatrix");
            Assert.Equal(this.RedMatrix * this.GroupMatrix * this.SuperGroupMatrix, redItem.WorldMatrix(superGroup));//, "WorldMatrix on Red up to supergroup invalid");

            // Validate green node
            Assert.Equal(this.GreenMatrix, greenItem.Matrix);//, "Matrix property on node should be GreenMatrix");
            Assert.Equal(this.GreenMatrix * this.GroupMatrix, greenItem.WorldMatrix(greenItem.Parent));//, "WorldMatrix on Green up to parent node should be GroupMatrix");
            Assert.Equal(this.GreenMatrix * this.GroupMatrix * this.SuperGroupMatrix, greenItem.WorldMatrix(superGroup));//, "WorldMatrix on Green up to supergroup should be SuperGroupMatrix");

            // Validate green node
            Assert.Equal(this.BlueMatrix, blueItem.Matrix);//, "Matrix property on node should be BlueMatrix");
            Assert.Equal(this.BlueMatrix * this.GroupMatrix, blueItem.WorldMatrix(blueItem.Parent));//, "WorldMatrix on Blue up to parent node should be GroupMatrix");
            Assert.Equal(this.BlueMatrix * this.GroupMatrix * this.SuperGroupMatrix, blueItem.WorldMatrix(superGroup));//, "WorldMatrix on Blue up to supergroup should be SuperGroupMatrix");

            // Validate Matrix with null param
            Assert.Equal(this.RedMatrix * this.GroupMatrix * this.SuperGroupMatrix, redItem.WorldMatrix(null));//, "WorldMatrix on Red with null param should be root color (RootMatrix)");
        }

        [StaFact]
        public void WorldOutputTypeBasicTest()
        {
            var scene = SampleScene();

            var superGroup = scene.DescendantsAndSelf().Where(d => d.Name == "SuperGroup").FirstOrDefault();
            var redItem = scene.DescendantsAndSelf().Where(d => d.Name == nameof(Color.Red)).FirstOrDefault();
            var greenItem = scene.DescendantsAndSelf().Where(d => d.Name == nameof(Color.Green)).FirstOrDefault();
            var blueItem = scene.DescendantsAndSelf().Where(d => d.Name == nameof(Color.Blue)).FirstOrDefault();

            // Validate root
            Assert.Equal(this.RootOutputType, scene.OutputType);//, "OutputType property on root should be RootOutputType");
            Assert.Equal(this.RootOutputType, scene.WorldOutputType());//, "WorldOutputType on root should be RootOutputType");

            // Validate red node
            Assert.Equal(this.GroupOutputType, redItem.WorldOutputType(redItem.Parent));//, "WorldOutputType on Red up to parent node should be GroupOutputType");
            Assert.Equal(this.SuperGroupOutputType, redItem.WorldOutputType(superGroup));//, "WorldOutputType on Red up to supergroup should be SuperGroupOutputType");

            // Validate green node
            Assert.Equal(this.GroupOutputType, greenItem.WorldOutputType(greenItem.Parent));//, "WorldOutputType on Green up to parent node should be GroupOutputType");
            Assert.Equal(this.SuperGroupOutputType, greenItem.WorldOutputType(superGroup));//, "WorldOutputType on Green up to supergroup should be SuperGroupOutputType");

            // Validate green node
            Assert.Equal(this.BlueOutputType, blueItem.OutputType);//, "OutputType property on node should be BlueOutputType");
            Assert.Equal(this.GroupOutputType, blueItem.WorldOutputType(blueItem.Parent));//, "WorldOutputType on Blue up to parent node should be GroupOutputType");
            Assert.Equal(this.SuperGroupOutputType, blueItem.WorldOutputType(superGroup));//, "WorldOutputType on Blue up to supergroup should be SuperGroupOutputType");

            // Validate OutputType with null param
            Assert.Equal(this.RootOutputType, redItem.WorldOutputType(null));//, "WorldOutputType on Red with null param should be root color (RootOutputType)");
        }
    }
}