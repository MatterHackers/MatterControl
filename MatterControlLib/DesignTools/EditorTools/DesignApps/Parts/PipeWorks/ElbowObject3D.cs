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
	public class ElbowObject3D : PipeWorksBaseObject3D
	{
		public ElbowObject3D()
		{
		}

		public static async Task<ElbowObject3D> Create()
		{
			var item = new ElbowObject3D();
			await item.Rebuild();

			return item;
		}

		[DisplayName("Inner Radius")]
		public double InnerDiameter { get; set; }

		[DisplayName("Outer Radius")]
		public double OuterDiameter { get; set; }

		public double Angle { get; set; } = 90;

		public double BottomReach { get; set; }

		public double FrontReach { get; set; }

		public override async Task Rebuild()
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

					IObject3D bottomReach = new RotateObject3D(CreateReach(BottomReach, InnerDiameter), -MathHelper.Tau / 4);
					IObject3D frontReach = null;
					IObject3D elbowConnect = null;
					if (Angle < 90)
					{
						frontReach = bottomReach.Clone();

						var translate = new Vector3(-OuterDiameter / 2, 0, 0);
						bottomReach.Translate(translate);

						frontReach = new RotateObject3D(frontReach, 0, 0, -MathHelper.DegreesToRadians(Angle));
						translate = Vector3Ex.Transform(-translate, Matrix4X4.CreateRotationZ(MathHelper.DegreesToRadians(Angle)));
						frontReach.Translate(translate);

						var torus = new TorusObject3D();

						using (torus.RebuildLock())
						{
							torus.Advanced = true;
							torus.InnerDiameter = 0;
							OuterDiameter = OuterDiameter * 2;
							torus.RingSides = Sides;
							torus.Sides = Sides;
							torus.StartingAngle = Angle;
							torus.EndingAngle = 180;
						}

						torus.Invalidate(new InvalidateArgs(torus, InvalidateType.Properties));
						elbowConnect = torus;
					}
					else if (Angle < 270)
					{
						bottomReach.Translate(0, -OuterDiameter / 2, 0);
						IObject3D reachConnect = await CylinderObject3D.Create(OuterDiameter, OuterDiameter, Sides, Alignment.Y);
						reachConnect = new AlignObject3D(reachConnect, FaceAlign.Front, bottomReach, FaceAlign.Back);
						reachConnect = new SetCenterObject3D(reachConnect, bottomReach.GetCenter(), true, false, true);
						bottomReach = bottomReach.Plus(reachConnect);

						frontReach = bottomReach.Clone();
						frontReach = new RotateObject3D(frontReach, 0, 0, -MathHelper.DegreesToRadians(Angle));

						elbowConnect = new SphereObject3D(OuterDiameter, Sides);
					}

					// output multiple meshes for pipe connector
					this.Children.Modify(list =>
					{
						list.Clear();
						list.Add(elbowConnect);
						list.Add(bottomReach);
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