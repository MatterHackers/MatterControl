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
using System;
using System.Collections.Generic;

namespace Matter_CAD_Lib.DesignTools.Dxf
{
    /// <summary>
    /// Helper class to create polygonal vertexes for circles, arcs and polylines.
    /// </summary>
    public class VertexConverter
    {

        /// <summary>
        /// Multiply this by an angle in degress to get the result in radians
        /// </summary>
        public const double DegToRad = Math.PI / 180.0;

        /// <summary>
        /// Multiply this by an angle in radians to get the result in degrees
        /// </summary>
        public const double RadToDeg = 180.0 / Math.PI;

        /// <summary>
        /// Get circle vertexes using a given precision. Higher the precision, smoother the circle shape, with an increase in vertex count.
        /// </summary>
        /// <param name="entity">The circle entity</param>
        /// <param name="precision">Shape precision (number of edges). Must be equal or higher than 3</param>
        /// <returns>A 2D vector list containing the circle shape</returns>
        public static List<Vector2> GetCircleVertexes(Circle entity, int precision = 3)
        {
            List<Vector2> coords = new List<Vector2>();
            double X, Y, R, increment;

            X = entity.Center.X;
            Y = entity.Center.Y;
            R = entity.Radius;

            if (precision < 3)
                precision = 3;

            //High-school unit circle math ;)
            increment = Math.PI * 2 / precision;
            for (int i = 0; i < precision; i++)
            {
                double sin = Math.Sin(increment * i) * R;
                double cos = Math.Cos(increment * i) * R;

                coords.Add(new Vector2(X + cos, Y + sin));
            }

            return coords;
        }

        /// <summary>
        /// Get arc vertexes using a given precision. Higher the precision, smoother the arc curve, with an increase in vertex count.
        /// </summary>
        /// <param name="entity">The arc entity</param>
        /// <param name="precision">Arc precision (number of segments). Must be equal or higher than 2</param>
        /// <returns>A 2D vector list containing the arc shape</returns>
        public static List<Vector2> GetArcVertexes(Arc entity, int precision = 2)
        {
            List<Vector2> coords = new List<Vector2>();

            double start = entity.StartAngle * DegToRad;
            double end = entity.EndAngle * DegToRad;
            double angle;

            if (precision < 2)
                precision = 2;

            //Gets the angle increment for the given precision
            if (start > end)
                angle = (end + (2 * Math.PI - start)) / precision;
            else
                angle = (end - start) / precision;

            //Basic unit circle math to calculate arc vertex coordinate for a given angle and radius
            for (int i = 0; i <= precision; i++)
            {
                double sine = entity.Radius * Math.Sin(start + angle * i);
                double cosine = entity.Radius * Math.Cos(start + angle * i);
                coords.Add(new Vector2(cosine + entity.Center.X, sine + entity.Center.Y));
            }

            return coords;
        }

        /// <summary>
        /// Get polyline vertexes using a given precision. Higher precision, smoother the polyline curves will be, with an increase in vertex count.
        /// </summary>
        /// <param name="entity">The polyline entity</param>
        /// <param name="precision">Curve precision (number of segments). Must be equal or higher than 2</param>
        /// <returns>A 2D vector list containing all the polyline vertexes, including straight and curved segments</returns>
        public static List<Vector2> GetPolyVertexes(Polyline entity, int precision = 2)
        {
            List<Vector2> coords = new List<Vector2>();

            if (precision < 2)
                precision = 2;

            for (int i = 0; i < entity.Vertexes.Count; i++)
            {

                if (entity.Vertexes[i].Bulge == 0)
                {
                    coords.Add(new Vector2(entity.Vertexes[i].Position.X, entity.Vertexes[i].Position.Y));
                }
                else
                {
                    if (i != entity.Vertexes.Count - 1)
                    {
                        double bulge = entity.Vertexes[i].Bulge;
                        double p1x = entity.Vertexes[i].Position.X;
                        double p1y = entity.Vertexes[i].Position.Y;
                        double p2x = entity.Vertexes[i + 1].Position.X;
                        double p2y = entity.Vertexes[i + 1].Position.Y;

                        //Definition of bulge, from Autodesk DXF fileformat specs
                        double angulo = Math.Abs(Math.Atan(bulge) * 4);
                        bool girou = false;

                        //For my method, this angle should always be less than 180. 
                        if (angulo >= Math.PI)
                        {
                            angulo = Math.PI * 2 - angulo;
                            girou = true;
                        }

                        //Distance between the two vertexes, the angle between Center-P1 and P1-P2 and the arc radius
                        double distancia = Math.Sqrt(Math.Pow(p1x - p2x, 2) + Math.Pow(p1y - p2y, 2));
                        double alfa = (Math.PI - angulo) / 2;
                        double raio = distancia * Math.Sin(alfa) / Math.Sin(angulo);

                        double xc, yc, angulo1, angulo2, multiplier, incr;

                        //Used to invert the signal of the calculations below
                        if (bulge < 0)
                            multiplier = 1;
                        else
                            multiplier = -1;

                        //Calculates the arc center
                        if (!girou)
                        {
                            xc = (p1x + p2x) / 2 - multiplier * ((p1y - p2y) / 2) * Math.Sqrt(Math.Pow(2 * raio / distancia, 2) - 1);
                            yc = (p1y + p2y) / 2 + multiplier * ((p1x - p2x) / 2) * Math.Sqrt(Math.Pow(2 * raio / distancia, 2) - 1);
                        }
                        else
                        {
                            xc = (p1x + p2x) / 2 + multiplier * ((p1y - p2y) / 2) * Math.Sqrt(Math.Pow(2 * raio / distancia, 2) - 1);
                            yc = (p1y + p2y) / 2 - multiplier * ((p1x - p2x) / 2) * Math.Sqrt(Math.Pow(2 * raio / distancia, 2) - 1);
                        }

                        //Invert start and end angle, depending on the bulge (clockwise or counter-clockwise)
                        if (bulge < 0)
                        {
                            angulo1 = Math.PI + Math.Atan2(yc - entity.Vertexes[i + 1].Position.Y, xc - entity.Vertexes[i + 1].Position.X);
                            angulo2 = Math.PI + Math.Atan2(yc - entity.Vertexes[i].Position.Y, xc - entity.Vertexes[i].Position.X);
                        }
                        else
                        {
                            angulo1 = Math.PI + Math.Atan2(yc - entity.Vertexes[i].Position.Y, xc - entity.Vertexes[i].Position.X);
                            angulo2 = Math.PI + Math.Atan2(yc - entity.Vertexes[i + 1].Position.Y, xc - entity.Vertexes[i + 1].Position.X);
                        }

                        //If it's more than 360, subtract 360 to keep it in the 0~359 range
                        if (angulo1 >= Math.PI * 2) angulo1 -= Math.PI * 2;
                        if (angulo2 >= Math.PI * 2) angulo2 -= Math.PI * 2;

                        //Calculate the angle increment for each vertex for the given precision
                        if (angulo1 > angulo2)
                            incr = (angulo2 + (2 * Math.PI - angulo1)) / precision;
                        else
                            incr = (angulo2 - angulo1) / precision;

                        //Gets the arc coordinates. If bulge is negative, invert the order
                        if (bulge > 0)
                        {
                            for (int a = 0; a <= precision; a++)
                            {
                                double sine = Math.Abs(raio) * Math.Sin(angulo1 + incr * a);
                                double cosine = Math.Abs(raio) * Math.Cos(angulo1 + incr * a);
                                coords.Add(new Vector2(cosine + xc, sine + yc));
                            }
                        }
                        else
                        {
                            for (int a = precision; a >= 0; a--)
                            {
                                double sine = Math.Abs(raio) * Math.Sin(angulo1 + incr * a);
                                double cosine = Math.Abs(raio) * Math.Cos(angulo1 + incr * a);
                                coords.Add(new Vector2(cosine + xc, sine + yc));
                            }
                        }
                    }

                }
            }

            return coords;
        }

    }
}