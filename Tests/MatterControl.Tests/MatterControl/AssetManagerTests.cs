using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.Tests.Automation;
using MatterHackers.PolygonMesh;
using NUnit.Framework;

namespace MatterControl.Tests.MatterControl
{
	[TestFixture]
	public class AssetManagerTests
	{

		[Test]
		public async Task StoreAssetFile()
		{
			AggContext.StaticData = new FileSystemStaticData(TestContext.CurrentContext.ResolveProjectPath(4, "StaticData"));
			MatterControlUtilities.OverrideAppDataLocation(TestContext.CurrentContext.ResolveProjectPath(4));

			// Create sample asset file
			string tempFile = ApplicationDataStorage.Instance.GetNewLibraryFilePath(".txt");
			Directory.CreateDirectory(Path.GetDirectoryName(tempFile));
			File.WriteAllText(tempFile, "Sample Text");

			// Set directory for asset resolution
			Object3D.AssetsPath = ApplicationDataStorage.Instance.LibraryAssetsPath;
			Directory.CreateDirectory(Object3D.AssetsPath);

			foreach (var file in Directory.GetFiles(Object3D.AssetsPath))
			{
				File.Delete(file);
			}
			Assert.AreEqual(0, Directory.GetFiles(Object3D.AssetsPath).Length);

			// Create AssetManager
			var assetManager = new MockAssetManager();
			AssetObject3D.AssetManager = assetManager;

			// Store
			string result = await AssetObject3D.AssetManager.StoreFile(tempFile, false, CancellationToken.None, null);

			// Validate
			Assert.AreEqual(1, Directory.GetFiles(Object3D.AssetsPath).Length, "Unexpected asset file count");
			Assert.AreEqual("8FB7B108E5F0A7FAE84DF849DDE830FED5B5F786.txt", result, "Unexpected asset name");
			Assert.AreEqual(0, assetManager.PublishCount, "No files should have been published");
		}

		[Test]
		public async Task StoreAsset()
		{
			AggContext.StaticData = new FileSystemStaticData(TestContext.CurrentContext.ResolveProjectPath(4, "StaticData"));
			MatterControlUtilities.OverrideAppDataLocation(TestContext.CurrentContext.ResolveProjectPath(4));

			// Create sample asset file
			string tempFile = ApplicationDataStorage.Instance.GetNewLibraryFilePath(".txt");
			Directory.CreateDirectory(Path.GetDirectoryName(tempFile));
			File.WriteAllText(tempFile, "Sample Text");

			var object3D = new AssetObject3D()
			{
				AssetPath = tempFile
			};

			// Set directory for asset resolution
			Object3D.AssetsPath = ApplicationDataStorage.Instance.LibraryAssetsPath;
			Directory.CreateDirectory(Object3D.AssetsPath);

			foreach(var file in Directory.GetFiles(Object3D.AssetsPath))
			{
				File.Delete(file);
			}
			Assert.AreEqual(0, Directory.GetFiles(Object3D.AssetsPath).Length);

			// Create AssetManager
			var assetManager = new MockAssetManager();
			AssetObject3D.AssetManager = assetManager;


			// Store
			await AssetObject3D.AssetManager.StoreAsset(object3D, false, CancellationToken.None, null);

			// Validate
			Assert.AreEqual(1, Directory.GetFiles(Object3D.AssetsPath).Length, "Unexpected asset file count");
			Assert.AreEqual("8FB7B108E5F0A7FAE84DF849DDE830FED5B5F786", object3D.AssetID, "Unexpected AssetID");
			Assert.AreEqual("8FB7B108E5F0A7FAE84DF849DDE830FED5B5F786.txt", object3D.AssetPath, "Unexpected asset name");
			Assert.AreEqual(0, assetManager.PublishCount, "No files should have been published");
		}

		[Test]
		public async Task StoreMesh()
		{
			AggContext.StaticData = new FileSystemStaticData(TestContext.CurrentContext.ResolveProjectPath(4, "StaticData"));
			MatterControlUtilities.OverrideAppDataLocation(TestContext.CurrentContext.ResolveProjectPath(4));

			// Create sample asset file
			string tempFile = ApplicationDataStorage.Instance.GetNewLibraryFilePath(".txt");
			Directory.CreateDirectory(Path.GetDirectoryName(tempFile));
			File.WriteAllText(tempFile, "Sample Text");

			var object3D = new Object3D()
			{
				Mesh = PlatonicSolids.CreateCube(1, 1, 1)
			};

			// Set directory for asset resolution
			Object3D.AssetsPath = ApplicationDataStorage.Instance.LibraryAssetsPath;
			Directory.CreateDirectory(Object3D.AssetsPath);

			foreach (var file in Directory.GetFiles(Object3D.AssetsPath))
			{
				File.Delete(file);
			}
			Assert.AreEqual(0, Directory.GetFiles(Object3D.AssetsPath).Length);

			// Create AssetManager
			var assetManager = new MockAssetManager();
			AssetObject3D.AssetManager = assetManager;

			// Store
			await AssetObject3D.AssetManager.StoreMesh(object3D, false, CancellationToken.None, null);

			// Validate
			Assert.AreEqual(1, Directory.GetFiles(Object3D.AssetsPath).Length, "Unexpected asset file count");
			Assert.AreEqual("CF58E6637ED36311082F66EC5FA3A279E0513FE6.stl", object3D.MeshPath, "Unexpected MeshPath");
			Assert.AreEqual(0, assetManager.PublishCount, "No files should have been published");
		}

		// Create AssetManager
		public class MockAssetManager : AssetManager
		{
			public int PublishCount { get; private set; } = 0;

			public override Task PublishAsset(string assetID, CancellationToken cancellationToken, Action<double, string> progress)
			{
				this.PublishCount++;
				return Task.CompletedTask;
			}
		}
	}
}
