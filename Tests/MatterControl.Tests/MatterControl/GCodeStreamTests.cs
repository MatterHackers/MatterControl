/*
Copyright (c) 2014, Kevin Pope
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

using MatterHackers.MatterControl;
using NUnit.Framework;
using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Linq;
using MatterHackers.MatterControl.PrinterCommunication.Io;

namespace MatterControl.Tests.MatterControl
{
    public class TestGCodeStream : GCodeStream
    {
        string[] lines;
        int index = 0;

        public TestGCodeStream(string[] lines)
        {
            this.lines = lines;
        }

        public override string ReadLine()
        {
            return lines[index++];
        }
    }

    [TestFixture]
    public class GCodeStreamTests
    {
        [Test, Category("GCodeStream")]
        public void BabyStepsStreamTets()
        {
        }

        [Test, Category("GCodeStream")]
        public void MaxLengthStreamTests()
        {
            string[] lines = new string[]
            {
                "G1 X0 Y0 Z0 E0 F500",
                "M105",
                "G1 X18 Y0 Z0 F2500",
                "G28",
                "G1 X0 Y0 Z0 E0 F500",
                null,
            };

            string[] expected = new string[]
            {
                "G1 X0 Y0 Z0 E0 F500",
                "M105",
                "G1 X6 F2500",
                "G1 X12",
                "G1 X18",
                "G28",
                "G1 X12 F500",
                "G1 X6",
                "G1 X0",
                null,
            };

            MaxLengthStream maxLengthStream = new MaxLengthStream(new TestGCodeStream(lines), 6);

            int expectedIndex = 0;
            string correctedLine = maxLengthStream.ReadLine();
            Assert.IsTrue(correctedLine == expected[expectedIndex++]);
            while (correctedLine != null)
            {
                correctedLine = maxLengthStream.ReadLine();
                Assert.IsTrue(correctedLine == expected[expectedIndex++]);
            }
        }
    }
}
