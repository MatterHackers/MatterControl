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

using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Threading;
using MatterHackers.VectorMath;

namespace Matter_CAD_Lib.DesignTools.Dxf
{
    /// <summary>
    /// Encapsulates the entire DXF document, containing it's layers and entities.
    /// </summary>
    public class DxfDocument
    {

        //General declarations
        private StreamReader dxfReader;
        private int dxfLinesRead = 0;

        public Header Header { get; set; }
        public List<Layer> Layers { get; set; }
        public List<Line> Lines { get; set; }
        public List<Polyline> Polylines { get; set; }
        public List<Circle> Circles { get; set; }
        public List<Arc> Arcs { get; set; }
        public List<Text> Texts { get; set; }
        public List<Point> Points { get; set; }

        /// <summary>
        /// Initializes a new instance of the <c>DXFDoc</c> class.
        /// </summary>
        /// <param name="dxfFile">The path of the DXF file to load</param>
        public DxfDocument(string dxfFile)
        {
            Header = new Header();
            Layers = new List<Layer>();
            Lines = new List<Line>();
            Polylines = new List<Polyline>();
            Circles = new List<Circle>();
            Arcs = new List<Arc>();
            Texts = new List<Text>();
            Points = new List<Point>();

            //Make sure we read the DXF decimal separator (.) correctly
            CultureInfo cultureInfo = CultureInfo.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            if (File.Exists(dxfFile))
            {
                dxfReader = new StreamReader(dxfFile);
            }
        }

        /// <summary>
        /// Read and parse the DXF file
        /// </summary>
        public void Read()
        {
            bool entitysection = false;

            CodePair code = ReadPair();
            while (code.Value != "EOF" && !dxfReader.EndOfStream)
            {
                if (code.Code == 0)
                {
                    //Have we reached the entities section yet?
                    if (!entitysection)
                    {
                        //No, so keep going until we find the ENTIIES section (and since we are here, let's try to read the layers)
                        switch (code.Value)
                        {
                            case "SECTION":
                                string sec = ReadSection(ref code);
                                if (sec == "ENTITIES")
                                    entitysection = true;
                                break;
                            case "LAYER":
                                Layer layer = ReadLayer(ref code);
                                Layers.Add(layer);
                                break;
                            default:
                                code = ReadPair();
                                break;
                        }
                    }
                    else
                    {
                        //Yes, so let's read the entities
                        switch (code.Value)
                        {
                            case "LINE":
                                Line line = ReadLine(ref code);
                                Lines.Add(line);
                                break;
                            case "CIRCLE":
                                Circle circle = ReadCircle(ref code);
                                Circles.Add(circle);
                                break;
                            case "ARC":
                                Arc arc = ReadArc(ref code);
                                Arcs.Add(arc);
                                break;
                            case "POINT":
                                Point point = ReadPoint(ref code);
                                Points.Add(point);
                                break;
                            case "TEXT":
                                Text text = ReadText(ref code);
                                Texts.Add(text);
                                break;
                            case "POLYLINE":
                                Polyline polyline = ReadPolyline(ref code);
                                Polylines.Add(polyline);
                                break;
                            case "LWPOLYLINE":
                                Polyline lwpolyline = ReadLwPolyline(ref code);
                                Polylines.Add(lwpolyline);
                                break;
                            default:
                                code = ReadPair();
                                break;
                        }
                    }
                }
                else if (code.Code == 9)
                {
                    switch (code.Value)
                    {
                        case "$ACADVER":
                            string version = ReadHeaderValue(ref code, 1);
                            Header.SetAcadVersion(version);
                            break;
                        case "$DWGCODEPAGE":
                            string codepage = ReadHeaderValue(ref code, 3);
                            Header.SetCodepage(codepage);
                            break;
                        case "$LASTSAVEDBY":
                            string lastSavedBy = ReadHeaderValue(ref code, 1);
                            Header.SetLastSavedBy(lastSavedBy);
                            break;
                        default:
                            code = ReadPair();
                            break;
                    }
                }
                else
                {
                    code = ReadPair();
                }
            }
        }

        /// <summary>
        /// Reads a code/value pair at the current line from DXF file
        /// </summary>
        /// <returns>A CodePair object containing code and value for the current line pair</returns>
        private CodePair ReadPair()
        {
            string line, value;
            int code;

            line = dxfReader.ReadLine();
            dxfLinesRead++;

            //Only through an exepction if the code value is not numeric, indicating a corrupted file
            if (!int.TryParse(line, out code))
            {
                throw new Exception("Invalid code (" + line + ") at line " + dxfLinesRead);
            }
            else
            {
                value = dxfReader.ReadLine();
                return new CodePair(code, value);
            }
        }

        /// <summary>
        /// Reads the SECTION name from the DXF file
        /// </summary>
        /// <param name="code">A reference to the current CodePair read</param>
        /// <returns>A string containing the section name</returns>
        private string ReadSection(ref CodePair code)
        {
            string returnval = "";

            code = ReadPair();
            while (code.Code != 0)
            {
                if (code.Code == 2)
                {
                    returnval = code.Value;
                    break;
                }
                code = ReadPair();
            }

            return returnval;
        }

        /// <summary>
        /// Reads the LINE data from the DXF file
        /// </summary>
        /// <param name="code">A reference to the current CodePair read</param>
        /// <returns>A Line object with layer and two point data</returns>
        private Line ReadLine(ref CodePair code)
        {
            Line returnval = new Line(Vector2.Zero, Vector2.Zero, "0");

            code = ReadPair();
            while (code.Code != 0)
            {
                switch (code.Code)
                {
                    case 8:
                        returnval.Layer = code.Value;
                        break;
                    case 10:
                        returnval.P1 = new Vector2(double.Parse(code.Value), returnval.P1.Y);
                        break;
                    case 20:
                        returnval.P1 = new Vector2(returnval.P1.X, double.Parse(code.Value));
                        break;
                    case 11:
                        returnval.P2 = new Vector2(double.Parse(code.Value), returnval.P2.Y);
                        break;
                    case 21:
                        returnval.P2 = new Vector2(returnval.P2.X, double.Parse(code.Value));
                        break;
                }
                code = ReadPair();
            }

            return returnval;
        }

        /// <summary>
        /// Reads the ARC data from the DXF file
        /// </summary>
        /// <param name="code">A reference to the current CodePair read</param>
        /// <returns>An Arc object with layer, center point, radius, start angle and end angle data</returns>
        private Arc ReadArc(ref CodePair code)
        {
            Arc returnval = new Arc(Vector2.Zero, 0, 0, 0, "0");

            code = ReadPair();
            while (code.Code != 0)
            {
                switch (code.Code)
                {
                    case 8:
                        returnval.Layer = code.Value;
                        break;
                    case 10:
                        returnval.Center = new Vector2(double.Parse(code.Value), returnval.Center.Y);
                        break;
                    case 20:
                        returnval.Center = new Vector2(returnval.Center.X, double.Parse(code.Value));
                        break;
                    case 40:
                        returnval.Radius = double.Parse(code.Value);
                        break;
                    case 50:
                        returnval.StartAngle = double.Parse(code.Value);
                        break;
                    case 51:
                        returnval.EndAngle = double.Parse(code.Value);
                        break;
                }
                code = ReadPair();
            }

            return returnval;
        }

        /// <summary>
        /// Reads the LWPOLYLINE data from the DXF file
        /// </summary>
        /// <param name="code">A reference to the current CodePair read</param>
        /// <returns>A Polyline object with layer, closed flag and vertex list data</returns>
        private Polyline ReadLwPolyline(ref CodePair code)
        {
            Polyline returnval = new Polyline(new List<Vertex>(), "0", false);
            Vertex vtx = new Vertex(Vector2.Zero);
            int flags = 0;

            code = ReadPair();
            while (code.Code != 0)
            {
                switch (code.Code)
                {
                    case 8:
                        returnval.Layer = code.Value;
                        break;
                    case 70:
                        flags = int.Parse(code.Value);
                        break;
                    case 10:
                        vtx = new Vertex(Vector2.Zero);
                        vtx.Position = new Vector2(double.Parse(code.Value), vtx.Position.Y);
                        break;
                    case 20:
                        vtx.Position = new Vector2(vtx.Position.X, double.Parse(code.Value));
                        returnval.Vertexes.Add(vtx);
                        break;
                    case 42:
                        vtx.Bulge = double.Parse(code.Value);
                        break;
                }
                code = ReadPair();
            }

            if ((flags & 1) == 1)
                returnval.Closed = true;

            return returnval;
        }

        /// <summary>
        /// Reads the POLYLINE data from the DXF file
        /// </summary>
        /// <param name="code">A reference to the current CodePair read</param>
        /// <returns>A Polyline object with layer, closed flag and vertex list data</returns>
        private Polyline ReadPolyline(ref CodePair code)
        {
            Polyline returnval = new Polyline(new List<Vertex>(), "0", false);
            int flags = 0;

            code = ReadPair();
            while (code.Code != 0)
            {
                switch (code.Code)
                {
                    case 8:
                        returnval.Layer = code.Value;
                        break;
                    case 70:
                        flags = int.Parse(code.Value);
                        break;
                }
                code = ReadPair();
            }

            while (code.Value != "SEQEND")
            {
                if (code.Value == "VERTEX")
                {
                    Vertex vtx = ReadVertex(ref code);
                    returnval.Vertexes.Add(vtx);
                }
                else
                {
                    code = ReadPair();
                }
            }

            if ((flags & 1) == 1)
                returnval.Closed = true;

            return returnval;
        }

        /// <summary>
        /// Reads the VERTEX data from the DXF file
        /// </summary>
        /// <param name="code">A reference to the current CodePair read</param>
        /// <returns>A Vertex object with layer, position and bulge data</returns>
        private Vertex ReadVertex(ref CodePair code)
        {
            Vertex returnval = new Vertex(0, 0, 0, "0");

            code = ReadPair();
            while (code.Code != 0)
            {
                switch (code.Code)
                {
                    case 8:
                        returnval.Layer = code.Value;
                        break;
                    case 10:
                        returnval.Position = new Vector2(double.Parse(code.Value), returnval.Position.Y);
                        break;
                    case 20:
                        returnval.Position = new Vector2(returnval.Position.X, double.Parse(code.Value));
                        break;
                    case 42:
                        returnval.Bulge = double.Parse(code.Value);
                        break;
                }
                code = ReadPair();
            }

            return returnval;
        }

        /// <summary>
        /// Reads the CIRCLE data from the DXF file
        /// </summary>
        /// <param name="code">A reference to the current CodePair read</param>
        /// <returns>A Circle object with layer, center point and radius data</returns>
        private Circle ReadCircle(ref CodePair code)
        {
            Circle returnval = new Circle(Vector2.Zero, 0, "0");

            code = ReadPair();
            while (code.Code != 0)
            {
                switch (code.Code)
                {
                    case 8:
                        returnval.Layer = code.Value;
                        break;
                    case 10:
                        returnval.Center = new Vector2(double.Parse(code.Value), returnval.Center.Y);
                        break;
                    case 20:
                        returnval.Center = new Vector2(returnval.Center.X, double.Parse(code.Value));
                        break;
                    case 40:
                        returnval.Radius = double.Parse(code.Value);
                        break;
                }
                code = ReadPair();
            }

            return returnval;
        }

        /// <summary>
        /// Reads the POINT data from the DXF file
        /// </summary>
        /// <param name="code">A reference to the current CodePair read</param>
        /// <returns>A Point object with layer and position data</returns>
        private Point ReadPoint(ref CodePair code)
        {
            Point returnval = new Point(Vector2.Zero, "0");

            code = ReadPair();
            while (code.Code != 0)
            {
                switch (code.Code)
                {
                    case 8:
                        returnval.Layer = code.Value;
                        break;
                    case 10:
                        returnval.Position = new Vector2(double.Parse(code.Value), returnval.Position.Y);
                        break;
                    case 20:
                        returnval.Position = new Vector2(returnval.Position.X, double.Parse(code.Value));
                        break;
                }
                code = ReadPair();
            }

            return returnval;
        }

        /// <summary>
        /// Reads the TEXT data from the DXF file
        /// </summary>
        /// <param name="code">A reference to the current CodePair read</param>
        /// <returns>A Text object with layer, value (text) and position data</returns>
        private Text ReadText(ref CodePair code)
        {
            Text returnval = new Text(Vector2.Zero, "", "0");

            code = ReadPair();
            while (code.Code != 0)
            {
                switch (code.Code)
                {
                    case 1:
                        returnval.Value = code.Value;
                        break;
                    case 8:
                        returnval.Layer = code.Value;
                        break;
                    case 10:
                        returnval.Position = new Vector2(double.Parse(code.Value), returnval.Position.Y);
                        break;
                    case 20:
                        returnval.Position = new Vector2(returnval.Position.X, double.Parse(code.Value));
                        break;
                }
                code = ReadPair();
            }

            return returnval;
        }

        /// <summary>
        /// Reads the LAYER data from the DXF file
        /// </summary>
        /// <param name="code">A reference to the current CodePair read</param>
        /// <returns>A Layer object with name and AciColor index</returns>
        private Layer ReadLayer(ref CodePair code)
        {
            Layer returnval = new Layer("0", 0);

            code = ReadPair();
            while (code.Code != 0)
            {
                switch (code.Code)
                {
                    case 2:
                        returnval.Name = code.Value;
                        break;
                    case 62:
                        returnval.ColorIndex = int.Parse(code.Value);
                        break;
                }
                code = ReadPair();
            }

            return returnval;
        }

        private string ReadHeaderValue(ref CodePair code, int groupcode)
        {
            string returnval = "";

            code = ReadPair();
            while (code.Code != 0)
            {
                if (code.Code == groupcode)
                {
                    returnval = code.Value;
                    break;
                }
                code = ReadPair();
            }
            return returnval;
        }
    }
}