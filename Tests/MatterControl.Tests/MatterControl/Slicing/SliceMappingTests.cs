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

using NUnit.Framework;

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
			StaticData.Instance = new MatterHackers.Agg.FileSystemStaticData(Path.Combine("..", "..", "..", "..", "StaticData"));

			var classicProfile = new ClassicSqlitePrinterProfiles();

			// dirrect values work
			{
				classicProfile.SaveValue("primary", "1", 0);
				classicProfile.SaveValue("reference", "10", 0);

				AsPercentOfReferenceOrDirect mapper = new AsPercentOfReferenceOrDirect("primary", "notused", "reference");
				Assert.IsTrue(mapper.Value == "1");

				// and also scaled
				AsPercentOfReferenceOrDirect mapper2 = new AsPercentOfReferenceOrDirect("primary", "notused", "reference", 1000);
				Assert.IsTrue(mapper2.Value == "1000");
			}

			// % reference values work
			{
				classicProfile.SaveValue("primary", "13%", 0);
				classicProfile.SaveValue("reference", "100", 0);

				AsPercentOfReferenceOrDirect mapper = new AsPercentOfReferenceOrDirect("primary", "notused", "reference");
				Assert.IsTrue(mapper.Value == "13");

				// and also scaled
				AsPercentOfReferenceOrDirect mapper2 = new AsPercentOfReferenceOrDirect("primary", "notused", "reference", 1000);
				Assert.IsTrue(mapper2.Value == "13000");
			}

			// and also check for 0
			{
				classicProfile.SaveValue("primary", "0", 0);
				classicProfile.SaveValue("reference", "100", 0);

				AsPercentOfReferenceOrDirect mapper = new AsPercentOfReferenceOrDirect("primary", "notused", "reference");
				Assert.IsTrue(mapper.Value == "100");

				// and also scaled
				AsPercentOfReferenceOrDirect mapper2 = new AsPercentOfReferenceOrDirect("primary", "notused", "reference", 1000);
				Assert.IsTrue(mapper2.Value == "100000");
			}
#endif
		}
	}
}