/*
Copyright (c) 2023, Lars Brubaker
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

using MatterControlLib.PartPreviewWindow.View3D.GeometryNodes.Nodes;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl.DesignTools;
using Newtonsoft.Json;
using NUnit.Framework;
using System.Linq;
using System.Threading.Tasks;

namespace MatterHackers.Agg.Tests
{
    [TestFixture]
    public class JsonSerializationTests
    {
        [Test]
        public async Task CubeObject3D_SerializeDeserializeTest()
        {
            // Arrange: Create an instance of CubeObject3D and set properties
            var cubeObject = new CubeObject3D
            {
                Width = 100,
                Depth = 100,
                Height = 100
            };

            var containerObject = new Object3D()
            {
                Name = "Container",
            };

            containerObject.Children.Add(cubeObject);

            // Act: Serialize the cubeObject to JSON and then Deserialize it back
            var json = await containerObject.ToJson();
            var item = JsonConvert.DeserializeObject(json,
                new JsonSerializerSettings()
                {
                    Converters = { new JsonIObject3DConverter() }
                });
            var deserializedObject = item as Object3D;

            var deserializedCubeObject = deserializedObject.Children.First() as CubeObject3D;

            // Assert: Check if the deserialized object's properties match the original object's properties
            Assert.IsNotNull(deserializedCubeObject);
            Assert.AreEqual(cubeObject.Width.Value(cubeObject), deserializedCubeObject.Width.Value(deserializedCubeObject), "Width should be equal.");
            Assert.AreEqual(cubeObject.Depth.Value(cubeObject), deserializedCubeObject.Depth.Value(deserializedCubeObject), "Depth should be equal.");
            Assert.AreEqual(cubeObject.Height.Value(cubeObject), deserializedCubeObject.Height.Value(deserializedCubeObject), "Height should be equal.");
        }

        [Test]
        public async Task InputObject3DNode_SerializeDeserializeTest()
        {
            var cubeObject = new CubeObject3D
            {
                Width = 100,
                Depth = 100,
                Height = 100
            };

            // Arrange: Create an instance of InputObject3DNode and set properties
            var inputObject = new InputMeshNode(cubeObject);

            // Act: Serialize the inputObject to JSON and then Deserialize it back
            var json = JsonConvert.SerializeObject(inputObject, Formatting.Indented);
            var deserializedInputObject = JsonConvert.DeserializeObject(json) as InputMeshNode;//,
                //new JsonINodeObjectConverter(),
                //new JsonIObject3DConverter());

            // Assert: Check if the deserialized object's properties match the original object's properties
            Assert.IsNotNull(deserializedInputObject);
            var deserializedCubeObject = deserializedInputObject.Children.First() as CubeObject3D;
            Assert.IsNotNull(cubeObject);
            Assert.IsTrue(cubeObject is CubeObject3D);
            // make sure the object is a cube and has the same properties
            Assert.AreEqual(cubeObject.Width.Value(cubeObject), deserializedCubeObject.Width.Value(deserializedCubeObject), "Width should be equal.");
            Assert.AreEqual(cubeObject.Depth.Value(cubeObject), deserializedCubeObject.Depth.Value(deserializedCubeObject), "Depth should be equal.");
            Assert.AreEqual(cubeObject.Height.Value(cubeObject), deserializedCubeObject.Height.Value(deserializedCubeObject), "Height should be equal.");
        }
    }
}
