/*
Copyright (c) 2016, Lars Brubaker, John Lewin
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

using System.Collections.Generic;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl.DesignTools;
using NUnit.Framework;

namespace MatterControl.Tests.MatterControl
{
	[TestFixture]
	public class SupportGeneratorTests
	{
		[Test, Category("Support Generator")]
		public void SupportExtentsTests()
		{
			// make a cube in the air and ensure that no mater where it is placed, support is always under the entire extents
			InteractiveScene scene = new InteractiveScene();
			var supportGenerator = new SupportGenerator(scene);
		}

		[Test, Category("Support Generator")]
		public void TopBottomWalkingTest()
		{
			{
				var planes = new List<(double z, bool bottom)>()
				{
					(0, false),
					(5, true),
					(10, false),
				};

				int bottom = SupportGenerator.GetNextBottom(0, planes);
				Assert.AreEqual(1, bottom);

				int bottom1 = SupportGenerator.GetNextBottom(1, planes);
				Assert.AreEqual(0, bottom);
			}

			{
				var planes = new List<(double z, bool bottom)>()
				{
					(10, false),
					(10, true),
					(20, false)
				};

				int bottom = SupportGenerator.GetNextBottom(0, planes);
				Assert.AreEqual(0, bottom);
			}
		}
	}
}
