/*
Copyright (c) 2014, Lars Brubaker
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
#define DEBUG_INTO_TGAS

using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.MatterControl.Tests.Automation;
using NUnit.Framework;

namespace MatterHackers.PolygonMesh.UnitTests
{
	[TestFixture, Category("Agg.PolygonMesh.Rebuild")]
	public class MeshRebuildTests
	{
		public void SetupEvnironment()
		{
			StaticData.RootPath = MatterControlUtilities.StaticDataPath;
			MatterControlUtilities.OverrideAppDataLocation(MatterControlUtilities.RootPath);

			// Automation runner must do as much as program.cs to spin up platform
			string platformFeaturesProvider = "MatterHackers.MatterControl.WindowsPlatformsFeatures, MatterControl.Winforms";
			AppContext.Platform = AggContext.CreateInstanceFrom<INativePlatformFeatures>(platformFeaturesProvider);
			AppContext.Platform.InitPluginFinder();
			AppContext.Platform.ProcessCommandline();
		}

		[Test]
		public async Task PinchChangesMesh()
		{
			SetupEvnironment();

			var root = new Object3D();

			// now add a pinch
			var pinch1 = new PinchObject3D_3();
			pinch1.Children.Add(new CubeObject3D());
			await pinch1.Rebuild();
			root.Children.Add(pinch1);
			Assert.AreEqual(3, root.Descendants().Count());
		}
	}

	[TestFixture, Category("Agg.Scene.Rebuild")]
	public class SceenSheetTests
	{
		public void SetupEvnironment()
		{
			StaticData.RootPath = MatterControlUtilities.StaticDataPath;
			MatterControlUtilities.OverrideAppDataLocation(MatterControlUtilities.RootPath);

			// Automation runner must do as much as program.cs to spin up platform
			string platformFeaturesProvider = "MatterHackers.MatterControl.WindowsPlatformsFeatures, MatterControl.Winforms";
			AppContext.Platform = AggContext.CreateInstanceFrom<INativePlatformFeatures>(platformFeaturesProvider);
			AppContext.Platform.InitPluginFinder();
			AppContext.Platform.ProcessCommandline();
		}

		[Test]
		public async Task TestSheetFunctions()
		{
			SetupEvnironment();
			var root = new Object3D();

			// Create the scene (a cube and a sheet)
			var cube1 = new CubeObject3D();
			root.Children.Add(cube1);
			var sheet = await SheetObject3D.Create();
			root.Children.Add(sheet);
			// set the sheet A1 to 33
			sheet.SheetData[0, 0].Expression = "=33";
			// rebuild cube without a reference to sheet
			cube1.Invalidate(InvalidateType.Properties);
			Assert.AreEqual(20, cube1.Width.Value(cube1), "cube1 should be the default 20mm");
			// set the cube width to the sheet value, but with a bad description (needs an equals to work)
			cube1.Width = "A1";
			cube1.Invalidate(InvalidateType.Properties);
			Assert.AreEqual(CubeObject3D.MinEdgeSize, cube1.Width.Value(cube1), "Should be the minimum cube value as the reference is bad");
			// now fix the reference
			cube1.Width = "=A1";
			cube1.Invalidate(InvalidateType.Properties);
			Assert.AreEqual(33, cube1.Width.Value(cube1), "Should now be the value ad A1");
			// Change the sheet value
			sheet.SheetData[0, 0].Expression = "=43";
			sheet.SheetData.Recalculate();
			// and rebuild the references
			sheet.Invalidate(InvalidateType.SheetUpdated);
			Assert.AreEqual(43, cube1.Width.Value(cube1));
		}
	}
}