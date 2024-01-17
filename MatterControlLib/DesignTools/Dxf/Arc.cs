/*
Copyright (c) 2023, Lars Brubaker
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

using MatterHackers.VectorMath;

namespace Matter_CAD_Lib.DesignTools.Dxf
{
    /// <summary>
    /// Defines a DXF arc, with it's layer, center point, radius, start and end angle
    /// </summary>
    public class Arc
    {
        public string Layer { get; set; }
        public Vector2 Center { get; set; }
        public double Radius { get; set; }
        public double StartAngle { get; set; }
        public double EndAngle { get; set; }

        /// <summary>
        /// Initialize a new instance of the Arc object
        /// </summary>
        /// <param name="Center">A Vector2d containg X and Y center coordinates</param>
        /// <param name="Radius">Arc radius</param>
        /// <param name="StartAng">Starting angle, in degrees</param>
        /// <param name="EndAng">Ending angle, in degrees</param>
        /// <param name="Layer">Layer name</param>
        /// <returns>A DXF Arc object</returns>
        public Arc(Vector2 Center, double Radius, double StartAng, double EndAng, string Layer)
        {
            this.Center = Center;
            this.Radius = Radius;
            StartAngle = StartAng;
            EndAngle = EndAng;
            this.Layer = Layer;
        }
    }
}