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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.MatterControl.Tests.Automation;
using MatterHackers.PolygonMesh.Csg;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.VectorMath;
using Net3dBool;
using NUnit.Framework;

namespace MatterHackers.PolygonMesh.UnitTests
{
	[TestFixture, Category("Agg.PolygonMesh.Rebuild")]
	public class MeshRebuildTests
	{
		[Test]
		public void PinchChangesMesh()
		{
			AggContext.StaticData = new FileSystemStaticData(TestContext.CurrentContext.ResolveProjectPath(4, "StaticData"));
			MatterControlUtilities.OverrideAppDataLocation(TestContext.CurrentContext.ResolveProjectPath(4));

			var root = new Object3D();
			var cube = new CubeObject3D();
			root.Children.Add(cube);
			cube.Invalidate(new InvalidateArgs(cube, InvalidateType.Properties));
			Assert.AreEqual(1, root.Descendants().Count());

			// now add a pinch
			var pinch1 = new PinchObject3D();
			pinch1.WrapItems(new List<IObject3D>() { cube });
			root.Children.Remove(cube);
			root.Children.Add(pinch1);
			Assert.AreEqual(3, root.Descendants().Count());
		}

		[Test, Ignore("Unstable test failing after unrelated changes")]
		public void PinchTextMaintainsWrapping()
		{
			AggContext.StaticData = new FileSystemStaticData(TestContext.CurrentContext.ResolveProjectPath(4, "StaticData"));
			MatterControlUtilities.OverrideAppDataLocation(TestContext.CurrentContext.ResolveProjectPath(4));

			var root = new Object3D();
			var text = new TextObject3D();
			root.Children.Add(text);
			text.Invalidate(new InvalidateArgs(text, InvalidateType.Properties, null));
			Assert.AreEqual(5, root.Descendants().Count());

			// now add a pinch
			var pinch1 = new PinchObject3D();
			pinch1.WrapItems(new List<IObject3D>() { text });
			root.Children.Remove(text);
			root.Children.Add(pinch1);
			Assert.AreEqual(10, root.Descendants().Count());

			// now remove pinch
			pinch1.Remove(null);
			Assert.AreEqual(5, root.Descendants().Count());
			Assert.AreEqual(1, root.Children.Count());

			// now add it again
			var first = root.Children.First(); // the wrap made a copy so set text to be the current text
			Assert.IsTrue(first is TextObject3D);
			pinch1 = new PinchObject3D();
			pinch1.WrapItems(new List<IObject3D>() { first });
			root.Children.Remove(first);
			root.Children.Add(pinch1);
			Assert.AreEqual(10, root.Descendants().Count());
		}
	}
}