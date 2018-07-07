/*
Copyright (c) 2018, Lars Brubaker, John Lewin
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

using MatterHackers.MatterControl.Plugins.BrailleBuilder;
using NUnit.Framework;

namespace MatterControl.Tests.MatterControl
{
	[TestFixture, Category("TextCreator")]
	public class BrailleGrade2Tests
	{
		[Test]
		public static void ConvertBrailleText()
		{
			Assert.IsTrue(BrailleGrade2.ConvertWord("taylor") == "taylor");
			Assert.IsTrue(BrailleGrade2.ConvertWord("Taylor") == ",taylor");
			Assert.IsTrue(BrailleGrade2.ConvertWord("TayLor") == ",tay,lor");
			Assert.IsTrue(BrailleGrade2.ConvertWord("energy") == "5}gy");
			Assert.IsTrue(BrailleGrade2.ConvertWord("men") == "m5");
			Assert.IsTrue(BrailleGrade2.ConvertWord("runabout") == "runab");
			Assert.IsTrue(BrailleGrade2.ConvertWord("afternoon") == "afn");
			Assert.IsTrue(BrailleGrade2.ConvertWord("really") == "re,y");
			Assert.IsTrue(BrailleGrade2.ConvertWord("glance") == "gl.e");
			Assert.IsTrue(BrailleGrade2.ConvertWord("station") == "/,n");
			Assert.IsTrue(BrailleGrade2.ConvertWord("as") == "z");
			Assert.IsTrue(BrailleGrade2.ConvertWord("abby") == "a2y");
			Assert.IsTrue(BrailleGrade2.ConvertWord("commitment") == "-mit;t");
			Assert.IsTrue(BrailleGrade2.ConvertWord("mother") == "\"m");
			Assert.IsTrue(BrailleGrade2.ConvertWord("myself") == "myf");
			Assert.IsTrue(BrailleGrade2.ConvertWord("lochness") == "lo*;s");
			Assert.IsTrue(BrailleGrade2.ConvertWord("Seven o'clock") == ",sev5 o'c");

			Assert.IsTrue(BrailleGrade2.ConvertWord("test") == "te/");
			Assert.IsTrue(BrailleGrade2.ConvertWord("that") == "t");
			Assert.IsTrue(BrailleGrade2.ConvertWord("will") == "w");
			Assert.IsTrue(BrailleGrade2.ConvertWord("show") == "%{");
			Assert.IsTrue(BrailleGrade2.ConvertWord("our") == "|r");
			Assert.IsTrue(BrailleGrade2.ConvertWord("with") == ")");
			Assert.IsTrue(BrailleGrade2.ConvertWord("braille") == "brl");
			Assert.IsTrue(BrailleGrade2.ConvertWord("conformance") == "3=m.e");

			Assert.IsTrue(BrailleGrade2.ConvertString("go to sleep") == "g 6sleep");
			Assert.IsTrue(BrailleGrade2.ConvertString("go to") == "g to");
			Assert.IsTrue(BrailleGrade2.ConvertString("here it is") == "\"h x is");
			Assert.IsTrue(BrailleGrade2.ConvertString("test that will show our conformance with braille") == "te/ t w %{ |r 3=m.e ) brl");
			Assert.IsTrue(BrailleGrade2.ConvertString("so we can create some strings and then this gives us the output that is expected") == "s we c cr1te \"s /r+s & !n ? gives u ! |tput t is expect$");

			Assert.IsTrue(BrailleGrade2.ConvertString("Waltz, bad nymph, for quick jigs vex.") == ",waltz1 bad nymph1 = qk jigs vex4");
			Assert.IsTrue(BrailleGrade2.ConvertString("Quick zephyrs blow, vexing daft Jim.") == ",qk zephyrs bl{1 vex+ daft ,jim4");
			Assert.IsTrue(BrailleGrade2.ConvertString("Sphinx of black quartz, judge my vow.") == ",sph9x ( black qu>tz1 judge my v{4");
			Assert.IsTrue(BrailleGrade2.ConvertString("Two driven jocks help fax my big quiz.") == ",two driv5 jocks help fax my big quiz4");
			//				Assert.IsTrue(BrailleGrade2.ConvertString("Five quacking zephyrs jolt my wax bed.") == ",five quack+ zephyrs jolt my wax b$4");
			Assert.IsTrue(BrailleGrade2.ConvertString("The five boxing wizards jump quickly.") == ",! five box+ wiz>ds jump qkly4");
			Assert.IsTrue(BrailleGrade2.ConvertString("Pack my box with five dozen liquor jugs.") == ",pack my box ) five doz5 liquor jugs4");
			Assert.IsTrue(BrailleGrade2.ConvertString("The quick brown fox jumps over the lazy dog.") == ",! qk br{n fox jumps ov} ! lazy dog4");
			Assert.IsTrue(BrailleGrade2.ConvertString("Jinxed wizards pluck ivy from the big quilt.") == ",j9x$ wiz>ds pluck ivy f ! big quilt4");
			Assert.IsTrue(BrailleGrade2.ConvertString("Crazy Fredrick bought many very exquisite opal jewels.") == ",crazy ,fr$rick b\"| _m v exquisite opal jewels4");
			Assert.IsTrue(BrailleGrade2.ConvertString("We promptly judged antique ivory buckles for the next prize.") == ",we promptly judg$ antique ivory buckles =! next prize4");
			Assert.IsTrue(BrailleGrade2.ConvertString("A mad boxer shot a quick, gloved jab to the jaw of his dizzy opponent.") == ",a mad box} %ot a qk1 glov$ jab 6! jaw ( 8 dizzy opp\"ont4");
			Assert.IsTrue(BrailleGrade2.ConvertString("Jaded zombies acted quaintly but kept driving their oxen forward.") == ",jad$ zombies act$ qua9tly b kept driv+ _! ox5 =w>d4");
			Assert.IsTrue(BrailleGrade2.ConvertString("14. The job requires extra pluck and zeal from every young wage earner.") == "#ad4 ,! job requires extra pluck & z1l f e \"y wage e>n}4");

			Assert.IsTrue(BrailleGrade2.ConvertString("Just wanting to put together some more tests to show the effectiveness of our converter.") == ",j want+ 6put tgr \"s m te/s 6%{ ! e6ective;s ( |r 3v}t}4");
		}
	}
}
