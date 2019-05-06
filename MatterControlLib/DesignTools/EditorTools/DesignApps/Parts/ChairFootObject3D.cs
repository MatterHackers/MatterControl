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

using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.MatterControl.DesignTools.Operations;
using MatterHackers.VectorMath;
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace MatterHackers.MatterControl.DesignTools
{
	public class ChairFootObject3D : Object3D
	{
		public enum OutputMode { Final_Part, Fit_Test };

		//private static string permissionKey = "ag1zfm1oLWRmcy10ZXN0chgLEgtEaWdpdGFsSXRlbRiAgICA3pCBCgw";

		public ChairFootObject3D()
		{
			//PermissionCheckRequest.CheckPermission(permissionKey);
		}

		//[Icons(new string[] { "424.png", "align_left.png", "align_center_x.png", "align_right.png" })]
		public OutputMode Output { get; set; } = OutputMode.Final_Part;

		[DisplayName("Angle")]
		public double AngleDegrees { get; set; } = 3;

		[DisplayName("Height")]
		public double HeightFromFloorToBottomOfLeg { get; set; } = 10;

		[DisplayName("Inner Size")]
		public double InnerSize { get; set; } = 20;

		[DisplayName("Reach")]
		public double InsideReach { get; set; } = 10;

		[DisplayName("Outer Size")]
		public double OuterSize { get; set; } = 22;

		public double BaseBevel { get; set; } = 2;

		//public override bool Persistable { get => PermissionCheckRequest.UserAuthorized(permissionKey); }

		public override bool CanFlatten => Persistable;

		public override bool CanEdit => Persistable;

		public static async Task<ChairFootObject3D> Create()
		{
			var chairFoot = new ChairFootObject3D();
			await chairFoot.Rebuild();
			return chairFoot;
		}

		private object locker = new object();
		private bool inRebuild;

		public override PolygonMesh.Mesh Mesh
		{
			get
			{
				lock (locker)
				{
					// Check a known condition for have persisted meshes or need to rebuild
					if (!inRebuild
						&& Children.Count > 0
						&& !this.Descendants<RingObject3D>().Where((d) => d.Mesh != null).Any())
					{
						Rebuild();
					}
					return base.Mesh;
				}
			}

			set => base.Mesh = value;
		}

		public async override Task Rebuild()
		{
			using (RebuildLock())
			{
				inRebuild = true;
				if (AngleDegrees > 45)
				{
					AngleDegrees = 45;
				}
				using (new CenterAndHeightMaintainer(this))
				{
					// This would be better expressed as the desired offset height (height from ground to bottom of chair leg).
					double angleRadians = MathHelper.DegreesToRadians(AngleDegrees);

					var insideReach = InsideReach;
					var heightFromFloorToBottomOfLeg = HeightFromFloorToBottomOfLeg;
					if (Output == OutputMode.Fit_Test)
					{
						insideReach = 4;
						angleRadians = 0;
						heightFromFloorToBottomOfLeg = 4;
					}

					double extraHeightForRotation = Math.Sinh(angleRadians) * OuterSize; // get the distance to clip off the extra bottom
					double unclippedFootHeight = heightFromFloorToBottomOfLeg + extraHeightForRotation;

					var baseBevelClamped = Math.Max(0, Math.Min(OuterSize / 2, BaseBevel));
					RoundedRect footBase = new RoundedRect(-OuterSize / 2, -OuterSize / 2, OuterSize / 2, OuterSize / 2, baseBevelClamped)
					{
						ResolutionScale = 1000
					};
					IObject3D chairFoot = new Object3D()
					{
						Mesh = VertexSourceToMesh.Extrude(footBase, unclippedFootHeight)
					};

					IObject3D ring = new RingObject3D(InnerSize - 2, InnerSize - 6, insideReach, 60);
					ring.Translate(0, 0, -insideReach / 2 - .02);

					VertexStorage finShape = new VertexStorage();
					finShape.MoveTo(0, 0);
					finShape.LineTo(3, 0);
					finShape.LineTo(3, ring.ZSize());
					finShape.LineTo(0, ring.ZSize() - 3);
					IObject3D fins = new Object3D()
					{
						Mesh = VertexSourceToMesh.Extrude(finShape, 1)
					};
					fins.Rotate(Vector3.Zero, Vector3.UnitX, -MathHelper.Tau / 4);
					fins.Rotate(Vector3.Zero, Vector3.UnitZ, -MathHelper.Tau / 2);
					fins = (new TranslateObject3D(fins, 1.48, 1, -ring.ZSize() - .02)).Plus(new TranslateObject3D(fins, 1.48, -1, -ring.ZSize() - .02));
					fins = new TranslateObject3D(fins, InnerSize / 2 - .1);

					ring = ring.Plus(new RotateObject3D(fins, 0, 0, MathHelper.DegreesToRadians(45)));
					ring = ring.Plus(new RotateObject3D(fins, 0, 0, MathHelper.DegreesToRadians(45 + 90)));
					ring = ring.Plus(new RotateObject3D(fins, 0, 0, MathHelper.DegreesToRadians(45 + 180)));
					ring = ring.Plus(new RotateObject3D(fins, 0, 0, MathHelper.DegreesToRadians(45 - 90)));

					chairFoot = chairFoot.Plus(new AlignObject3D(ring, FaceAlign.Bottom, chairFoot, FaceAlign.Top, 0, 0, -.1));

					chairFoot = new RotateObject3D(chairFoot, 0, angleRadians, 0);
					if (unclippedFootHeight != heightFromFloorToBottomOfLeg)
					{
						IObject3D clipBox = new AlignObject3D(await CubeObject3D.Create(OuterSize * 2, OuterSize * 2, unclippedFootHeight), FaceAlign.Top, chairFoot, FaceAlign.Bottom, 0, 0, extraHeightForRotation);
						chairFoot = chairFoot.Minus(clipBox);
						chairFoot = new TranslateObject3D(chairFoot, 0, 0, clipBox.GetAxisAlignedBoundingBox().MaxXYZ.Z);
					}

					this.Children.Modify(list =>
					{
						list.Clear();
						list.Add(chairFoot);
					});
				}
				inRebuild = false;
			}
		}
	}
}