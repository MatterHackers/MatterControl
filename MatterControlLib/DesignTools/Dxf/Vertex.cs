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
    /// Defines a DXF vertex, with position, bulge and layer
    /// </summary>
    public class Vertex
    {
        public Vector2 Position { get; set; }
        public double Bulge { get; set; }
        public string Layer { get; set; }

        /// <summary>
        /// Initialize a new instance of the Vertex object. Bulge and Layer are optional (defaults to 0).
        /// </summary>
        /// <param name="Location">A Vector2d containg X and Y coordinates</param>
        /// <param name="Bulge">The tangent of 1/4 the included angle for an arc segment. Negative if the arc goes clockwise from the start point to the endpoint.</param>
        /// <param name="Layer">Layer name</param>
        /// <returns>A DXF Vertex object</returns>
        public Vertex(Vector2 Location, double Bulge = 0, string Layer = "0")
        {
            Position = Location;
            this.Bulge = Bulge;
            this.Layer = Layer;
        }

        /// <summary>
        /// Initialize a new instance of the Vertex object. Bulge and Layer are optional (defaults to 0).
        /// </summary>
        /// <param name="X">X coordinate</param>
        /// <param name="Y">Y coordinate</param>
        /// <param name="Bulge">The tangent of 1/4 the included angle for an arc segment. Negative if the arc goes clockwise from the start point to the endpoint.</param>
        /// <param name="Layer">Layer name</param>
        /// <returns>A DXF Vertex object</returns>
        public Vertex(double X, double Y, double Bulge = 0, string Layer = "0")
        {
            Position = new Vector2(0, 0)
            {
                X = X,
                Y = Y
            };
            this.Bulge = Bulge;
            this.Layer = Layer;
        }
    }
}