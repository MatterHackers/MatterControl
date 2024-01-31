/*
Copyright (c) 2018, John Lewin
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

using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Matter_CAD_Lib.DesignTools.Objects3D;
using Matter_CAD_Lib.DesignTools.Interfaces;
using MatterHackers.DataConverters3D;
using MatterHackers.PolygonMesh.Processors;
using Xunit;

namespace MatterHackers.Agg.Tests
{
    
	public class AmfDocumentTests
	{
		[Fact]
		public void NoElementWhitespaceTest()
		{
			// Amf xml lacking whitespace between elements
			string xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?><amf unit=\"millimeter\" version=\"1.1\"><object id=\"1\"><mesh><vertices><vertex><coordinates><x>-5</x><y>-2.5</y><z>1</z></coordinates></vertex><vertex><coordinates><x>-5</x><y>2.5</y><z>1</z></coordinates></vertex><vertex><coordinates><x>-5</x><y>-2.5</y><z>-1</z></coordinates></vertex><vertex><coordinates><x>-5</x><y>2.5</y><z>-1</z></coordinates></vertex><vertex><coordinates><x>5</x><y>-2.5</y><z>1</z></coordinates></vertex><vertex><coordinates><x>5</x><y>2.5</y><z>1</z></coordinates></vertex><vertex><coordinates><x>5</x><y>-2.5</y><z>-1</z></coordinates></vertex><vertex><coordinates><x>5</x><y>2.5</y><z>-1</z></coordinates></vertex></vertices><volume><triangle><v1>0</v1><v2>4</v2><v3>5</v3></triangle><triangle><v1>0</v1><v2>5</v2><v3>1</v3></triangle><triangle><v1>2</v1><v2>0</v2><v3>1</v3></triangle><triangle><v1>2</v1><v2>1</v2><v3>3</v3></triangle><triangle><v1>4</v1><v2>6</v2><v3>7</v3></triangle><triangle><v1>4</v1><v2>7</v2><v3>5</v3></triangle><triangle><v1>2</v1><v2>3</v2><v3>7</v3></triangle><triangle><v1>2</v1><v2>7</v2><v3>6</v3></triangle><triangle><v1>1</v1><v2>5</v2><v3>7</v3></triangle><triangle><v1>1</v1><v2>7</v2><v3>3</v3></triangle><triangle><v1>2</v1><v2>6</v2><v3>4</v3></triangle><triangle><v1>2</v1><v2>4</v2><v3>0</v3></triangle></volume></mesh></object></amf>";

			// Wrap xml with MemoryStream, load, validate
			using (var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(xml)))
			{
				var amfObject3D = AmfDocument.Load(memoryStream, CancellationToken.None);

				Assert.Equal(1, amfObject3D.Children.Count);
				Assert.NotNull(amfObject3D.Children.First().Mesh);
			}
		}

		[Fact]
		public void LoadAmfFullyIndented()
		{
			var testContext = new TestContext();
            // Fully indented AMF file
            string amfPath = testContext.CurrentContext.ResolveProjectPath(new string[] { "..", "TestData", "FullyIndented.amf" });

			// Load AMF file and validate result
			var amfObject = AmfDocument.Load(amfPath, CancellationToken.None);

			Assert.Equal(1, amfObject.Children.Count);

			var child = amfObject.Children.First();
			Assert.NotNull(child.Mesh);
			Assert.Equal("Cube", child.Name);
			Assert.Equal(new Color(0, 128, 255), child.Color);
			Assert.Equal(PrintOutputTypes.Solid, child.OutputType);
		}

		[Fact]
		public void SaveAmfContainingXmlSpecialCharacters()
		{
			const string XmlSpecialCharacters = "&<>'\"";

			// Create an object whose name contains XML special characters
			var inputObject = new Object3D()
			{
				Children =
				{
					new Object3D()
					{
						Name = XmlSpecialCharacters,
						Mesh = PolygonMesh.PlatonicSolids.CreateCube()
					}
				}
			};

			// Add meta data containing XML special characters
			var outputSettings = new MeshOutputSettings()
			{
				MetaDataKeyValue = new()
				{
					{ XmlSpecialCharacters, XmlSpecialCharacters }
				}
			};

			using var memoryStream = new MemoryStream();

			// Save and load as AMF to validate that XML special characters are correctly escaped
			AmfDocument.Save(inputObject, memoryStream, outputSettings);
			var outputObject = AmfDocument.Load(memoryStream, CancellationToken.None);

			Assert.Equal(outputObject.Children.Count, 1);
			Assert.Equal(XmlSpecialCharacters, outputObject.Children.First().Name);
		}

		[Fact]
		public void SaveAmfWithSpecificCulture()
		{
			var originalCultureInfo = CultureInfo.CurrentCulture;

			// Change current culture
			CultureInfo.CurrentCulture = CultureInfo.CreateSpecificCulture("fr-FR");

			try
			{
				var inputObject = new Object3D()
				{
					Children =
					{
						new Object3D()
						{
							Name = "Cube",
							Mesh = PolygonMesh.PlatonicSolids.CreateCube()
						}
					}
				};

				using var memoryStream = new MemoryStream();

				// Save and load as AMF to validate XML format
				AmfDocument.Save(inputObject, memoryStream, new MeshOutputSettings());
				var outputObject = AmfDocument.Load(memoryStream, CancellationToken.None);

				Assert.Equal(outputObject.Children.Count, 1);
				Assert.Equal("Cube", outputObject.Children.First().Name);
			}
			finally
			{
				// Restore original culture
				CultureInfo.CurrentCulture = originalCultureInfo;
			}
		}

		private void CreateSampleFile()
		{
			var root = new Object3D();

			root.Children.Add(new Object3D()
			{
				Mesh = PolygonMesh.PlatonicSolids.CreateCube(10, 5, 2)
			});

			AmfDocument.Save(
				root,
				@"c:\temp\sample.amf");
		}
	}
}