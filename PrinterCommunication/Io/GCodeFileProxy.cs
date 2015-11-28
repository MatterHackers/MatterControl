/*
Copyright (c) 2015, Lars Brubaker
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
using MatterHackers.GCodeVisualizer;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.PrinterCommunication.Io
{
	public class GCodeFileProxy : GCodeFile
	{
        private GCodeFile source;

        protected GCodeFile Source { get { return source; } }

		public GCodeFileProxy(GCodeFile source)
		{
			this.source = source;
		}

        #region Abstract Functions
        public override void Add(PrinterMachineInstruction printerMachineInstruction)
        {
            source.Add(printerMachineInstruction);
        }

        public override void Clear()
        {
            source.Clear();
        }

        public override RectangleDouble GetBounds()
        {
            return source.GetBounds();
        }

        public override double GetFilamentCubicMm(double filamentDiameter)
        {
            return source.GetFilamentCubicMm(filamentDiameter);
        }

        public override double GetFilamentDiameter()
        {
            return source.GetFilamentDiameter();
        }

        public override double GetFilamentUsedMm(double filamentDiameter)
        {
            return source.GetFilamentUsedMm(filamentDiameter);
        }

        public override double GetFilamentWeightGrams(double filamentDiameterMm, double density)
        {
            return source.GetFilamentWeightGrams(filamentDiameterMm, density);
        }

        public override double GetFirstLayerHeight()
        {
            return source.GetFirstLayerHeight();
        }

        public override int GetInstructionIndexAtLayer(int layerIndex)
        {
            return source.GetInstructionIndexAtLayer(layerIndex);
        }

        public override double GetLayerHeight()
        {
            return source.GetLayerHeight();
        }

        public override int GetLayerIndex(int instructionIndex)
        {
            return source.GetLayerIndex(instructionIndex);
        }

        public override Vector2 GetWeightedCenter()
        {
            return source.GetWeightedCenter();
        }

        public override void Insert(int indexToStartInjection, PrinterMachineInstruction printerMachineInstruction)
        {
            source.Insert(indexToStartInjection, printerMachineInstruction);
        }

        public override PrinterMachineInstruction Instruction(int i)
        {
            return source.Instruction(i);
        }

        public override bool IsExtruding(int instructionIndexToCheck)
        {
            return source.IsExtruding(instructionIndexToCheck);
        }

        public override double PercentComplete(int instructionIndex)
        {
            return source.PercentComplete(instructionIndex);
        }

        public override double Ratio0to1IntoContainedLayer(int instructionIndex)
        {
            return source.Ratio0to1IntoContainedLayer(instructionIndex);
        }

        public override int LineCount
        {
            get
            {
                return source.LineCount;
            }
        }

        public override int NumChangesInZ
        {
            get
            {
                return source.NumChangesInZ;
            }
        }

        public override double TotalSecondsInPrint
        {
            get
            {
                return source.TotalSecondsInPrint;
            }
        }

        #endregion Abstract Functions
    }
}