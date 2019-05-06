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

using System;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl.CustomWidgets;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.DesignTools
{
	[HideChildrenFromTreeView]
	[WebPageLink("About Braille", "https://en.wikipedia.org/wiki/Braille")]
	public class BrailleCardObject3D : Object3D
	{
		public BrailleCardObject3D()
		{
		}

		public static async Task<BrailleCardObject3D> Create()
		{
			var item = new BrailleCardObject3D();

			await item.Rebuild();
			return item;
		}

		public override bool CanFlatten => true;

		public char Letter { get; set; } = 'a';

		public double BaseHeight { get; set; } = 4;

		public override async void OnInvalidate(InvalidateArgs invalidateType)
		{
			if (invalidateType.InvalidateType.HasFlag(InvalidateType.Properties)
				&& invalidateType.Source == this)
			{
				await Rebuild();
			}

			base.OnInvalidate(invalidateType);
		}

		public override async Task Rebuild()
		{
			using (RebuildLock())
			{
				using (new CenterAndHeightMaintainer(this))
				{
					this.Children.Modify(list =>
				{
					list.Clear();
				});

					var brailleLetter = new BrailleObject3D()
					{
						TextToEncode = Letter.ToString(),
						BaseHeight = BaseHeight,
					};
					await brailleLetter.Rebuild();
					this.Children.Add(brailleLetter);

					var textObject = new TextObject3D()
					{
						PointSize = 46,
						Color = Color.LightBlue,
						NameToWrite = Letter.ToString(),
						Height = BaseHeight
					};

					await textObject.Rebuild();
					IObject3D letterObject = new RotateObject3D_2(textObject, Vector3.UnitX, -90);
					await letterObject.Rebuild();
					var scaleRatio = Math.Max(letterObject.XSize() / 17, letterObject.ZSize() / 17);
					if (scaleRatio > 1)
					{
						letterObject = new ScaleObject3D(letterObject, 1.0/scaleRatio, 1, 1.0/scaleRatio);
					}
					letterObject = new AlignObject3D(letterObject, FaceAlign.Bottom | FaceAlign.Front, brailleLetter, FaceAlign.Top | FaceAlign.Front, 0, 0, 3.5);
					letterObject = new SetCenterObject3D(letterObject, brailleLetter.GetCenter(), true, false, false);
					this.Children.Add(letterObject);

					var basePath = new RoundedRect(0, 0, 22, 34, 3)
					{
						ResolutionScale = 10
					};

					IObject3D basePlate = new Object3D()
					{
						Mesh = VertexSourceToMesh.Extrude(basePath, BaseHeight),
						Matrix = Matrix4X4.CreateRotationX(MathHelper.Tau / 4)
					};

					basePlate = new AlignObject3D(basePlate, FaceAlign.Bottom | FaceAlign.Back, brailleLetter, FaceAlign.Bottom | FaceAlign.Back);
					basePlate = new SetCenterObject3D(basePlate, brailleLetter.GetCenter(), true, false, false);
					this.Children.Add(basePlate);

					IObject3D underline = await CubeObject3D.Create(basePlate.XSize(), .2, 1);
					underline = new AlignObject3D(underline, FaceAlign.Bottom, brailleLetter, FaceAlign.Top);
					underline = new AlignObject3D(underline, FaceAlign.Back | FaceAlign.Left, basePlate, FaceAlign.Front | FaceAlign.Left, 0, .01);
					this.Children.Add(underline);
				}
			}

			Parent?.Invalidate(new InvalidateArgs(this, InvalidateType.Children));
		}
	}
}