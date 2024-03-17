/*
Involute Spur Gear Builder (c) 2014 Dr. Rainer Hessmer
ported to C# 2019 by Lars Brubaker

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
using ClipperLib;
using MatterHackers.Agg;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters2D;
using MatterHackers.VectorMath;
using Polygons = System.Collections.Generic.List<System.Collections.Generic.List<ClipperLib.IntPoint>>;

public static class VertexSourceExtensions
{
	public static IVertexSource Minus(this IVertexSource a, IVertexSource b)
	{
		return MergePaths(a, b, ClipType.ctDifference);
	}

    public static IVertexSource Plus(this IVertexSource a, IVertexSource b)
	{
		return MergePaths(a, b, ClipType.ctUnion);
	}

	public static IVertexSource RotateZDegrees(this IVertexSource a, double angle)
	{
		return new VertexSourceApplyTransform(a, Affine.NewRotation(MathHelper.DegreesToRadians(angle)));
	}

	public static IVertexSource Subtract(this IVertexSource a, IVertexSource b)
	{
		return a.Minus(b);
	}

	public static IVertexSource Translate(this IVertexSource a, Vector2 delta)
	{
		return new VertexSourceApplyTransform(a, Affine.NewTranslation(delta));
	}

	public static IVertexSource Union(this IVertexSource a, IVertexSource b)
	{
		return a.Plus(b);
	}

	public static VertexStorage MergePaths(this IVertexSource a, IVertexSource b, ClipType clipType, PolyFillType polyFillType = PolyFillType.pftEvenOdd, bool cleanPaths = true)
	{
        var aPolys = a.CreatePolygons();
        var bPolys = b.CreatePolygons();

		var outputPolys = ApplyClipping(aPolys, bPolys, clipType, polyFillType);
		VertexStorage output = outputPolys.CreateVertexStorage();

		output.Add(0, 0, FlagsAndCommand.Stop);

		return output;
	}

    public static Polygons ApplyClipping(this Polygons a, Polygons b, ClipType clipType, PolyFillType polyFillType = PolyFillType.pftEvenOdd, bool cleanPaths = true)
    {
        var clipper = new Clipper();
        clipper.AddPaths(a, PolyType.ptSubject, true);
        clipper.AddPaths(b, PolyType.ptClip, true);

        var outputPolys = new Polygons();
        clipper.Execute(clipType, outputPolys, polyFillType);

		if (cleanPaths)
		{
            Clipper.CleanPolygons(outputPolys);
        }

        return outputPolys;
    }
}