/*
Copyright (c) 2019, John Lewin
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using MatterHackers.Agg.Platform;
using MatterHackers.DataConverters3D;
using MatterHackers.Localizations;
using MatterHackers.MatterControl.DesignTools;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl.Library
{
	public class PrimitivesContainer : LibraryContainer
	{
		public PrimitivesContainer()
		{
			Name = "Primitives".Localize();
		}

		public override void Load()
		{
			var library = ApplicationController.Instance.Library;

			long index = DateTime.Now.Ticks;
			var libraryItems = new List<GeneratorItem>()
			{
				new GeneratorItem(
					() => "Cube".Localize(),
					async () => await CubeObject3D.Create())
					{ DateCreated = new System.DateTime(index++) },
				new GeneratorItem(
					() => "Pyramid".Localize(),
					async () => await PyramidObject3D.Create())
					{ DateCreated = new System.DateTime(index++) },
				new GeneratorItem(
					() => "Wedge".Localize(),
					async () => await WedgeObject3D.Create())
					{ DateCreated = new System.DateTime(index++) },
				new GeneratorItem(
					() => "Half Wedge".Localize(),
					async () => await HalfWedgeObject3D.Create())
					{ DateCreated = new System.DateTime(index++) },
				new GeneratorItem(
					() => "Text".Localize(),
					async () => await TextObject3D.Create())
					{ DateCreated = new System.DateTime(index++) },

				new GeneratorItem(
					() => "Cylinder".Localize(),
					async () => await CylinderObject3D.Create())
					{ DateCreated = new System.DateTime(index++) },
				new GeneratorItem(
					() => "Cone".Localize(),
					async () => await ConeObject3D.Create())
					{ DateCreated = new System.DateTime(index++) },
				new GeneratorItem(
					() => "Half Cylinder".Localize(),
					async () => await HalfCylinderObject3D.Create())
					{ DateCreated = new System.DateTime(index++) },
				new GeneratorItem(
					() => "Torus".Localize(),
					async () => await TorusObject3D.Create())
					{ DateCreated = new System.DateTime(index++) },
				new GeneratorItem(
					() => "Ring".Localize(),
					async () => await RingObject3D.Create())
					{ DateCreated = new System.DateTime(index++) },
				new GeneratorItem(
					() => "Sphere".Localize(),
					async () => await SphereObject3D.Create())
					{ DateCreated = new System.DateTime(index++) },
				new GeneratorItem(
					() => "Half Sphere".Localize(),
					async () => await HalfSphereObject3D.Create())
					{ DateCreated = new System.DateTime(index++) },
				new GeneratorItem(
					() => "Image Converter".Localize(),
					() =>
					{
						// Construct an image
						var imageObject = new ImageObject3D()
						{
							AssetPath = AggContext.StaticData.ToAssetPath(Path.Combine("Images", "mh-logo.png"))
						};

						// Construct a scene
						var tempScene = new InteractiveScene();
						tempScene.Children.Add(imageObject);
						tempScene.SelectedItem = imageObject;

						// Invoke ImageConverter operation, passing image and scene
						ApplicationController.Instance.Graph.Operations["ImageConverter"].Operation(imageObject, tempScene);

						// Return replacement object constructed in ImageConverter operation
						var constructedComponent = tempScene.SelectedItem;
						tempScene.Children.Remove(constructedComponent);

						return Task.FromResult<IObject3D>(constructedComponent);
					})
					{ DateCreated = new System.DateTime(index++) },
			};

			string title = "Primitive Shapes".Localize();

			foreach (var item in libraryItems)
			{
				item.Category = title;
				Items.Add(item);
			}
		}
	}
}
