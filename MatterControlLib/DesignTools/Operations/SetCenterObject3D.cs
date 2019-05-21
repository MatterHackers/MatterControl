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

using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters3D;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.DesignTools.Operations
{
	public class SetCenterObject3D : Object3D
	{
		public SetCenterObject3D()
		{
		}

		public SetCenterObject3D(IObject3D item, Vector3 position)
		{
			Matrix = Matrix4X4.CreateTranslation(position - item.GetCenter());
			Children.Add(item.Clone());
		}

		public SetCenterObject3D(IObject3D item, double x, double y, double z)
			: this(item, new Vector3(x, y, z))
		{
		}

		public SetCenterObject3D(IObject3D item, Vector3 offset, bool onX = true, bool onY = true, bool onZ = true)
		{
			var center = item.GetAxisAlignedBoundingBox().Center;

			Vector3 consideredOffset = Vector3.Zero; // zero out anything we don't want
			if (onX)
			{
				consideredOffset.X = offset.X - center.X;
			}
			if (onY)
			{
				consideredOffset.Y = offset.Y - center.Y;
			}
			if (onZ)
			{
				consideredOffset.Z = offset.Z - center.Z;
			}

			Matrix = Matrix4X4.CreateTranslation(consideredOffset);
			Children.Add(item.Clone());
		}
	}

	public class SetCenter2D : VertexSourceApplyTransform
	{
		public SetCenter2D()
		{
		}

		public SetCenter2D(IVertexSource item, Vector2 position)
		{
			Transform = Affine.NewTranslation(position - item.GetBounds().Center);
			VertexSource = item;
		}

		public SetCenter2D(IVertexSource item, double x, double y)
			: this(item, new Vector2(x, y))
		{
		}

		public SetCenter2D(IVertexSource item, Vector2 offset, bool onX = true, bool onY = true)
		{
			var center = item.GetBounds().Center;

			Vector2 consideredOffset = Vector2.Zero; // zero out anything we don't want
			if (onX)
			{
				consideredOffset.X = offset.X - center.X;
			}
			if (onY)
			{
				consideredOffset.Y = offset.Y - center.Y;
			}

			Transform = Affine.NewTranslation(consideredOffset);
			VertexSource = item;
		}
	}
}