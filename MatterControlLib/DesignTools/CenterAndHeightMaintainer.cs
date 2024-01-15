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
using Matter_CAD_Lib.DesignTools.Interfaces;
using MatterHackers.VectorMath;

namespace MatterHackers.DataConverters3D
{
    public enum MaintainFlags
	{
		Center = 1,
		Bottom = 2,
		Origin = 4,
		Default = Center | Bottom,
	}

	public class CenterAndHeightMaintainer : IDisposable
	{
		private MaintainFlags flags;
		private IObject3D item;
		private AxisAlignedBoundingBox aabb;
		private Vector3 originRelParent;

		public CenterAndHeightMaintainer(IObject3D item, MaintainFlags flags = MaintainFlags.Default)
		{
			this.flags = flags;
			this.item = item;
			aabb = item.GetAxisAlignedBoundingBox();
			originRelParent = Vector3.Zero.Transform(item.Matrix);
		}

		public void Dispose()
		{
			// make sure we have some size in z
			if (aabb.ZSize > 0)
			{
				// get the current bounds
				var newAabbb = item.GetAxisAlignedBoundingBox();

				if (flags.HasFlag(MaintainFlags.Center))
				{
					// move our center back to where our center was
					item.Matrix *= Matrix4X4.CreateTranslation(aabb.Center - newAabbb.Center);

					// update the bounds again
					newAabbb = item.GetAxisAlignedBoundingBox();
				}
				else if (flags.HasFlag(MaintainFlags.Origin))
				{
					var newOriginRelParent = Vector3.Zero.Transform(item.Matrix);
					
					// move our center back to where our center was
					item.Matrix *= Matrix4X4.CreateTranslation(originRelParent - newOriginRelParent);

					// update the bounds again
					newAabbb = item.GetAxisAlignedBoundingBox();
				}

				if (flags.HasFlag(MaintainFlags.Bottom))
				{
					// Make sure we also maintain our height
					item.Matrix *= Matrix4X4.CreateTranslation(new Vector3(0, 0, aabb.MinXYZ.Z - newAabbb.MinXYZ.Z));
				}
			}
		}
	}
}