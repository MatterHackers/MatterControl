﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MatterHackers.VectorMath;
using MatterHackers.RayTracer;
using MatterHackers.GCodeVisualizer;
using MatterHackers.MatterControl.SlicerConfiguration;

namespace MatterHackers.MatterControl
{
    public class PrintLeveling
    {
        Matrix4X4 bedLevelMatrix = Matrix4X4.Identity;

        // private constructor
        private PrintLeveling()
        {
        }

        static private PrintLeveling instance;
        static public PrintLeveling Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new PrintLeveling();
                }

                return instance;
            }
        }

        public void SetPrintLevelingEquation(Vector3 position0, Vector3 position1, Vector3 position2, Vector2 bedCenter)
        {
            if (position0 == position1 || position1 == position2 || position2 == position0)
            {
                return;
            }

            Plane planeOfPoints = new Plane(position0, position1, position2);

            Ray ray = new Ray(new Vector3(bedCenter, 0), Vector3.UnitZ);
            bool inFront;
            double distanceToPlaneAtBedCenter = planeOfPoints.GetDistanceToIntersection(ray, out inFront);

            Matrix4X4 makePointsFlatMatrix = Matrix4X4.CreateTranslation(-bedCenter.x, -bedCenter.y, -distanceToPlaneAtBedCenter);
            makePointsFlatMatrix *= Matrix4X4.CreateRotation(planeOfPoints.planeNormal, Vector3.UnitZ);
            makePointsFlatMatrix *= Matrix4X4.CreateTranslation(bedCenter.x, bedCenter.y, 0);//distanceToPlaneAtBedCenter);

            bedLevelMatrix = Matrix4X4.Invert(makePointsFlatMatrix);

            {
                // test that the points come back as 0 zs
                Vector3 outPosition0 = Vector3.TransformPosition(position0, makePointsFlatMatrix);
                Vector3 outPosition1 = Vector3.TransformPosition(position1, makePointsFlatMatrix);
                Vector3 outPosition2 = Vector3.TransformPosition(position2, makePointsFlatMatrix);

                Vector3 printPosition0 = new Vector3(ActiveSliceSettings.Instance.GetPrintLevelSamplePosition(0), 0);
                Vector3 printPosition1 = new Vector3(ActiveSliceSettings.Instance.GetPrintLevelSamplePosition(1), 0);
                Vector3 printPosition2 = new Vector3(ActiveSliceSettings.Instance.GetPrintLevelSamplePosition(2), 0);

                Vector3 leveledPositon0 = Vector3.TransformPosition(printPosition0, bedLevelMatrix);
                Vector3 leveledPositon1 = Vector3.TransformPosition(printPosition1, bedLevelMatrix);
                Vector3 leveledPositon2 = Vector3.TransformPosition(printPosition2, bedLevelMatrix);
            }
        }

        public Vector3 ApplyLeveling(Vector3 inPosition)
        {
            return Vector3.TransformPosition(inPosition, bedLevelMatrix);
        }

        public Vector3 ApplyLevelingRotation(Vector3 inPosition)
        {
            return Vector3.TransformVector(inPosition, bedLevelMatrix);
        }

        public string ApplyLeveling(Vector3 currentDestination, PrinterMachineInstruction.MovementTypes movementMode, string lineBeingSent, bool addLFCR, bool includeSpaces)
        {
            if (lineBeingSent.StartsWith("G0") || lineBeingSent.StartsWith("G1"))
            {
                double extruderDelta = 0;
                GCodeFile.GetFirstNumberAfter("E", lineBeingSent, ref extruderDelta);
                double feedRate = 0;
                GCodeFile.GetFirstNumberAfter("F", lineBeingSent, ref feedRate);

                string newLine = "G1 ";

                if (lineBeingSent.Contains('X') || lineBeingSent.Contains('Y') || lineBeingSent.Contains('Z'))
                {
                    Vector3 outPosition = PrintLeveling.Instance.ApplyLeveling(currentDestination);
                    if (movementMode == PrinterMachineInstruction.MovementTypes.Relative)
                    {
                        Vector3 relativeMove = Vector3.Zero;
                        GCodeFile.GetFirstNumberAfter("X", lineBeingSent, ref relativeMove.x);
                        GCodeFile.GetFirstNumberAfter("Y", lineBeingSent, ref relativeMove.y);
                        GCodeFile.GetFirstNumberAfter("Z", lineBeingSent, ref relativeMove.z);
                        outPosition = PrintLeveling.Instance.ApplyLevelingRotation(relativeMove);
                    }

                    if (includeSpaces)
                    {
                        newLine = newLine + String.Format("X{0:0.##} Y{1:0.##} Z{2:0.##}", outPosition.x, outPosition.y, outPosition.z);
                    }
                    else
                    {
                        newLine = newLine + String.Format("X{0:0.##}Y{1:0.##}Z{2:0.##}", outPosition.x, outPosition.y, outPosition.z);
                    }
                }

                if (extruderDelta != 0)
                {
                    newLine = newLine + String.Format(" E{0:0.###}", extruderDelta);
                }
                if (feedRate != 0)
                {
                    newLine = newLine + String.Format(" F{0:0.##}", feedRate);
                }

                lineBeingSent = newLine;

                if (addLFCR)
                {
                    lineBeingSent += "\r\n";
                }
            }

            return lineBeingSent;
        }

        public void ApplyLeveling(GCodeFile unleveledGCode)
        {
            foreach (PrinterMachineInstruction instruction in unleveledGCode.GCodeCommandQueue)
            {
                Vector3 currentDestination = instruction.Position;
                instruction.Line = ApplyLeveling(currentDestination, instruction.movementType, instruction.Line, false, false);
            }
        }
    }
}
