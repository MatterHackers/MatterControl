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
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.DesignTools.Operations
{
	public class Align2D : VertexSourceApplyTransform
	{
		public Align2D()
		{
		}

		public Align2D(IVertexSource objectToAlign, Side2D boundingFacesToAlign, IVertexSource objectToAlignTo, Side2D boundingFacesToAlignTo, double offsetX = 0, double offsetY = 0, string name = "")
			: this(objectToAlign, boundingFacesToAlign, GetPositionToAlignTo(objectToAlignTo, boundingFacesToAlignTo, new Vector2(offsetX, offsetY)), name)
		{
			if (objectToAlign == objectToAlignTo)
			{
				throw new Exception("You cannot align an object to itself.");
			}
		}

		public Align2D(IVertexSource objectToAlign, Side2D boundingFacesToAlign, double positionToAlignToX = 0, double positionToAlignToY = 0, string name = "")
			: this(objectToAlign, boundingFacesToAlign, new Vector2(positionToAlignToX, positionToAlignToY), name)
		{
		}

		public Align2D(IVertexSource objectToAlign, Side2D boundingFacesToAlign, Vector2 positionToAlignTo, double offsetX, double offsetY, string name = "")
			: this(objectToAlign, boundingFacesToAlign, positionToAlignTo + new Vector2(offsetX, offsetY), name)
		{
		}

		public Align2D(IVertexSource item, Side2D boundingFacesToAlign, Vector2 positionToAlignTo, string name = "")
		{
			var bounds = item.GetBounds();

			if (IsSet(boundingFacesToAlign, Side2D.Left, Side2D.Right))
			{
				positionToAlignTo.X = positionToAlignTo.X - bounds.Left;
			}
			if (IsSet(boundingFacesToAlign, Side2D.Right, Side2D.Left))
			{
				positionToAlignTo.X = positionToAlignTo.X - bounds.Left - (bounds.Right - bounds.Left);
			}
			if (IsSet(boundingFacesToAlign, Side2D.Bottom, Side2D.Top))
			{
				positionToAlignTo.Y = positionToAlignTo.Y - bounds.Bottom;
			}
			if (IsSet(boundingFacesToAlign, Side2D.Top, Side2D.Bottom))
			{
				positionToAlignTo.Y = positionToAlignTo.Y - bounds.Bottom - (bounds.Top - bounds.Bottom);
			}

			Transform = Affine.NewTranslation(positionToAlignTo);
			VertexSource = item;
		}

		public static Vector2 GetPositionToAlignTo(IVertexSource objectToAlignTo, Side2D boundingFacesToAlignTo, Vector2 extraOffset)
		{
			Vector2 positionToAlignTo = new Vector2();
			if (IsSet(boundingFacesToAlignTo, Side2D.Left, Side2D.Right))
			{
				positionToAlignTo.X = objectToAlignTo.GetBounds().Left;
			}
			if (IsSet(boundingFacesToAlignTo, Side2D.Right, Side2D.Left))
			{
				positionToAlignTo.X = objectToAlignTo.GetBounds().Right;
			}
			if (IsSet(boundingFacesToAlignTo, Side2D.Bottom, Side2D.Top))
			{
				positionToAlignTo.Y = objectToAlignTo.GetBounds().Bottom;
			}
			if (IsSet(boundingFacesToAlignTo, Side2D.Top, Side2D.Bottom))
			{
				positionToAlignTo.Y = objectToAlignTo.GetBounds().Top;
			}

			return positionToAlignTo + extraOffset;
		}

		private static bool IsSet(Side2D variableToCheck, Side2D faceToCheckFor, Side2D faceToAssertNot)
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