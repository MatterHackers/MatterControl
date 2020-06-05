/*
Copyright (c) 2017, John Lewin
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
using System.Reflection;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl.Library;
using MatterHackers.PolygonMesh;

namespace MatterHackers.MatterControl.Plugins.SvgConverter
{
	/// <summary>
	/// Loads IObject3D and thumbnails for SVG based ILibraryItem objects
	/// </summary>
	public class SvgContentProvider : ISceneContentProvider
	{
		private static ImageBuffer imageBuffer;

		static SvgContentProvider()
		{
			using (var stream = Assembly.GetExecutingAssembly()?.GetManifestResourceStream("Experimental.Svg.Logo.png"))
			{
				imageBuffer = new ImageBuffer();
				AggContext.ImageIO.LoadImageData(stream, imageBuffer);
			}
		}

		public async Task<IObject3D> CreateItem(ILibraryItem item, Action<double, string> reporter)
		{
			const double DefaultSizeMm = 40;
			return await Task.Run(async () =>
			{

				if (imageBuffer != null)
				{
					// Build an ImageBuffer from some svg content
					double scaleMmPerPixels = Math.Min(DefaultSizeMm / imageBuffer.Width, DefaultSizeMm / imageBuffer.Height);

					// Create texture mesh
					double width = scaleMmPerPixels * imageBuffer.Width;
					double height = scaleMmPerPixels * imageBuffer.Height;

					Mesh textureMesh = PlatonicSolids.CreateCube(width, height, 0.2);
					textureMesh.PlaceTextureOnFaces(0, imageBuffer);

					string assetPath = null;

					if (item is ILibraryAssetStream assetStream)
					{
						assetPath = assetStream.AssetPath;

						if (string.IsNullOrWhiteSpace(assetPath))
						{
							using (var streamAndLength = await assetStream.GetStream(null))
							{
								string assetID = AssetObject3D.AssetManager.StoreStream(streamAndLength.Stream, ".svg");
								assetPath = assetID;
							}

						}
					}

					var svgObject = new SvgObject3D()
					{
						DString = "", // How to acquire?
						SvgPath = assetPath
					};

					svgObject.Children.Add(new Object3D()
					{
						Mesh = textureMesh
					});

					await svgObject.Rebuild();

					return svgObject;
				}
				else
				{
					return null;
				}
			});
		}


		public Task<ImageBuffer> GetThumbnail(ILibraryItem item, int width, int height)
		{
			return Task.FromResult<ImageBuffer>(this.DefaultImage);
		}

		public ImageBuffer DefaultImage => AggContext.StaticData.LoadIcon(Path.Combine("Library", "svg.png"));
	}
}