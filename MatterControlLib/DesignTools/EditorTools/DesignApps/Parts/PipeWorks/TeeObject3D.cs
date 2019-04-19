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

using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.DesignTools
{
	public class TeeObject3D : PipeWorksBaseObject3D
	{
		public TeeObject3D()
		{
		}

		public static async Task<TeeObject3D> Create()
		{
			var item = new TeeObject3D();
			await item.Rebuild();

			return item;
		}

		[DisplayName("Inner Radius")]
		public double InnerDiameter { get; set; }

		[DisplayName("Outer Radius")]
		public double OuterDiameter { get; set; }

		public double BottomReach { get; set; }

		public double FrontReach { get; set; }

		public double TopReach { get; set; }

		public async override Task Rebuild()
		{
			using (RebuildLock())
			{
				using (new CenterAndHeightMaintainer(this))
				{
					// validate the some of the values and store in user data if changed
					InnerDiameter = ValidateValue(InnerDiameter, "PipeWorksInnerDiameter", 15);
					OuterDiameter = ValidateValue(OuterDiameter, "PipeWorksOuterDiameter", 20);
					BottomReach = ValidateValue(BottomReach, "PipeWorksBottomReach", 30);
					FrontReach = ValidateValue(FrontReach, "PipeWorksFrontReach", 25);
					TopReach = ValidateValue(TopReach, "PipeWorksTopReach", 30);

					IObject3D topBottomConnect = await CylinderObject3D.Create(OuterDiameter, OuterDiameter, Sides, Alignment.Y);
					IObject3D frontConnect = await CylinderObject3D.Create(OuterDiameter, OuterDiameter, Sides, Alignment.X);
					frontConnect = new AlignObject3D(frontConnect, FaceAlign.Right, topBottomConnect, FaceAlign.Right);

					IObject3D bottomReach = new RotateObject3D(CreateReach(BottomReach, InnerDiameter), -MathHelper.Tau / 4);
					bottomReach = new AlignObject3D(bottomReach, FaceAlign.Back, topBottomConnect, FaceAlign.Front, 0, .02);

					IObject3D topReach = new RotateObject3D(CreateReach(TopReach, InnerDiameter), MathHelper.Tau / 4);
					topReach = new AlignObject3D(topReach, FaceAlign.Front, topBottomConnect, FaceAlign.Back, 0, -.02);

					IObject3D frontReach = new RotateObject3D(CreateReach(FrontReach, InnerDiameter), 0, -MathHelper.Tau / 4);
					frontReach = new AlignObject3D(frontReach, FaceAlign.Left, topBottomConnect, FaceAlign.Right, -.02);

					// output multiple meshes for pipe connector
					this.Children.Modify(list =>
					{
						list.Clear();
						list.Add(topBottomConnect);
						list.Add(frontConnect);
						list.Add(bottomReach);
						list.Add(topReach);
						list.Add(frontReach);
					});

					this.Color = Color.Transparent;
					this.Mesh = null;
				}
			}

			Invalidate(InvalidateType.Children);
		}
	}
}