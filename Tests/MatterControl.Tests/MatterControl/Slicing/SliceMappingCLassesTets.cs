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

using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.VectorMath;
using NUnit.Framework;
using System.IO;

namespace MatterHackers.MatterControl.SlicerConfiguration.Tests
{
	[TestFixture, Category("MatterControl.SlicerConfiguration")]
	public class SliceMappingTests
	{
		[Test]
		public void AsPercentOfReferenceOrDirectTests()
		{
#if !__ANDROID__
			// Set the static data to point to the directory of MatterControl
			StaticData.Instance = new MatterHackers.Agg.FileSystemStaticData(Path.Combine("..", "..", "..", ".."));

			// dirrect values work
			{
				ActiveSliceSettings.Instance.SaveValue("primary", "1", 0);
				ActiveSliceSettings.Instance.SaveValue("reference", "10", 0);

				AsPercentOfReferenceOrDirect mapper = new AsPercentOfReferenceOrDirect("notused", "primary", "reference");
				Assert.IsTrue(mapper.MappedValue == "1");

				// and also scaled
				AsPercentOfReferenceOrDirect mapper2 = new AsPercentOfReferenceOrDirect("notused", "primary", "reference", 1000);
				Assert.IsTrue(mapper2.MappedValue == "1000");
			}

			// % reference values work
			{
				ActiveSliceSettings.Instance.SaveValue("primary", "13%", 0);
				ActiveSliceSettings.Instance.SaveValue("reference", "100", 0);

				AsPercentOfReferenceOrDirect mapper = new AsPercentOfReferenceOrDirect("notused", "primary", "reference");
				Assert.IsTrue(mapper.MappedValue == "13");

				// and also scaled
				AsPercentOfReferenceOrDirect mapper2 = new AsPercentOfReferenceOrDirect("notused", "primary", "reference", 1000);
				Assert.IsTrue(mapper2.MappedValue == "13000");
			}

			// and also check for 0
			{
				ActiveSliceSettings.Instance.SaveValue("primary", "0", 0);
				ActiveSliceSettings.Instance.SaveValue("reference", "100", 0);

				AsPercentOfReferenceOrDirect mapper = new AsPercentOfReferenceOrDirect("notused", "primary", "reference");
				Assert.IsTrue(mapper.MappedValue == "100");

				// and also scaled
				AsPercentOfReferenceOrDirect mapper2 = new AsPercentOfReferenceOrDirect("notused", "primary", "reference", 1000);
				Assert.IsTrue(mapper2.MappedValue == "100000");
			}
#endif
		}
	}
}