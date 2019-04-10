/*
Copyright (c) 2019, Lars Brubaker, John Lewin
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
using MatterHackers.Agg;

namespace MatterHackers.MeshVisualizer
{
	public static class MaterialRendering
	{
		/// <summary>
		/// Get the color for a given extruder, falling back to extruder 0 color on -1 (unassigned)
		/// </summary>
		/// <param name="materialIndex">The extruder/material index to resolve</param>
		/// <returns>The color for the given extruder</returns>
		public static Color Color(int materialIndex)
		{
			return ColorF.FromHSL(Math.Max(materialIndex, 0) / 10.0, .99, .49).ToColor();
		}

		/// <summary>
		/// Get the color for a given extruder, falling back to the supplied color on -1 (unassigned)
		/// </summary>
		/// <param name="materialIndex">The extruder/material index to resolve</param>
		/// <param name="unassignedColor">The color to use when the extruder/material has not been assigned</param>
		/// <returns>The color for the given extruder</returns>
		public static Color Color(int materialIndex, Color unassignedColor)
		{
			return (materialIndex == -1) ? unassignedColor : ColorF.FromHSL(materialIndex / 10.0, .99, .49).ToColor();
		}
	}
}
