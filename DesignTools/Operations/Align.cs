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
using MatterHackers.DataConverters3D;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.DesignTools.Operations
{
	public enum Alignment { X, Y, Z, negX, negY, negZ };

	[Flags]
	public enum Edge
	{
		LeftFront = Face.Left | Face.Front,
		LeftBack = Face.Left | Face.Back,
		LeftBottom = Face.Left | Face.Bottom,
		LeftTop = Face.Left | Face.Top,
		RightFront = Face.Right | Face.Front,
		RightBack = Face.Right | Face.Back,
		RightBottom = Face.Right | Face.Bottom,
		RightTop = Face.Right | Face.Top,
		FrontBottom = Face.Front | Face.Bottom,
		FrontTop = Face.Front | Face.Top,
		BackBottom = Face.Back | Face.Bottom,
		BackTop = Face.Back | Face.Top
	}

	[Flags]
	public enum Face
	{
		Left = 0x01,
		Right = 0x02,
		Front = 0x04,
		Back = 0x08,
		Bottom = 0x10,
		Top = 0x20,
	};

	public class Align : Object3D
	{
		public Align()
		{
		}

		public Align(IObject3D objectToAlign, Face boundingFacesToAlign, IObject3D objectToAlignTo, Face boundingFacesToAlignTo, double offsetX = 0, double offsetY = 0, double offsetZ = 0, string name = "")
			: this(objectToAlign, boundingFacesToAlign, GetPositionToAlignTo(objectToAlignTo, boundingFacesToAlignTo, new Vector3(offsetX, offsetY, offsetZ)), name)
		{
			if (objectToAlign == objectToAlignTo)
			{
				throw new Exception("You cannot align an object ot itself.");
			}
		}

		public Align(IObject3D objectToAlign, Face boundingFacesToAlign, double offsetX = 0, double offsetY = 0, double offsetZ = 0, string name = "")
			: this(objectToAlign, boundingFacesToAlign, new Vector3(offsetX, offsetY, offsetZ), name)
		{
		}

		public Align(IObject3D objectToAlign, Face boundingFacesToAlign, Vector3 positionToAlignTo, double offsetX, double offsetY, double offsetZ, string name = "")
			: this(objectToAlign, boundingFacesToAlign, positionToAlignTo + new Vector3(offsetX, offsetY, offsetZ), name)
		{
		}

		public Align(IObject3D item, Face boundingFacesToAlign, Vector3 positionToAlignTo, string name = "")
		{
			AxisAlignedBoundingBox bounds = item.GetAxisAlignedBoundingBox();

			if (IsSet(boundingFacesToAlign, Face.Left, Face.Right))
			{
				positionToAlignTo.X = positionToAlignTo.X - bounds.minXYZ.X;
			}
			if (IsSet(boundingFacesToAlign, Face.Right, Face.Left))
			{
				positionToAlignTo.X = positionToAlignTo.X - bounds.minXYZ.X - (bounds.maxXYZ.X - bounds.minXYZ.X);
			}
			if (IsSet(boundingFacesToAlign, Face.Front, Face.Back))
			{
				positionToAlignTo.Y = positionToAlignTo.Y - bounds.minXYZ.Y;
			}
			if (IsSet(boundingFacesToAlign, Face.Back, Face.Front))
			{
				positionToAlignTo.Y = positionToAlignTo.Y - bounds.minXYZ.Y - (bounds.maxXYZ.Y - bounds.minXYZ.Y);
			}
			if (IsSet(boundingFacesToAlign, Face.Bottom, Face.Top))
			{
				positionToAlignTo.Z = positionToAlignTo.Z - bounds.minXYZ.Z;
			}
			if (IsSet(boundingFacesToAlign, Face.Top, Face.Bottom))
			{
				positionToAlignTo.Z = positionToAlignTo.Z - bounds.minXYZ.Z - (bounds.maxXYZ.Z - bounds.minXYZ.Z);
			}

			Matrix *= Matrix4X4.CreateTranslation(positionToAlignTo);
			Children.Add(item.Clone());
		}

		public static Vector3 GetPositionToAlignTo(IObject3D objectToAlignTo, Face boundingFacesToAlignTo, Vector3 extraOffset)
		{
			Vector3 positionToAlignTo = new Vector3();
			if (IsSet(boundingFacesToAlignTo, Face.Left, Face.Right))
			{
				positionToAlignTo.X = objectToAlignTo.GetAxisAlignedBoundingBox().minXYZ.X;
			}
			if (IsSet(boundingFacesToAlignTo, Face.Right, Face.Left))
			{
				positionToAlignTo.X = objectToAlignTo.GetAxisAlignedBoundingBox().maxXYZ.X;
			}
			if (IsSet(boundingFacesToAlignTo, Face.Front, Face.Back))
			{
				positionToAlignTo.Y = objectToAlignTo.GetAxisAlignedBoundingBox().minXYZ.Y;
			}
			if (IsSet(boundingFacesToAlignTo, Face.Back, Face.Front))
			{
				positionToAlignTo.Y = objectToAlignTo.GetAxisAlignedBoundingBox().maxXYZ.Y;
			}
			if (IsSet(boundingFacesToAlignTo, Face.Bottom, Face.Top))
			{
				positionToAlignTo.Z = objectToAlignTo.GetAxisAlignedBoundingBox().minXYZ.Z;
			}
			if (IsSet(boundingFacesToAlignTo, Face.Top, Face.Bottom))
			{
				positionToAlignTo.Z = objectToAlignTo.GetAxisAlignedBoundingBox().maxXYZ.Z;
			}
			return positionToAlignTo + extraOffset;
		}

		private static bool IsSet(Face variableToCheck, Face faceToCheckFor, Face faceToAssertNot)
		{
			if ((variableToCheck & faceToCheckFor) != 0)
			{
				if ((variableToCheck & faceToAssertNot) != 0)
				{
					throw new Exception("You cannot have both " + faceToCheckFor.ToString() + " and " + faceToAssertNot.ToString() + " set when calling Align.  The are mutually exclusive.");
				}
				return true;
			}

			return false;
		}
	}
}