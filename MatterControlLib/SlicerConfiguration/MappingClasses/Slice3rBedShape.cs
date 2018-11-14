/*
Copyright (c) 2016, Lars Brubaker
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

using MatterHackers.MeshVisualizer;
using MatterHackers.VectorMath;
using System;

namespace MatterHackers.MatterControl.SlicerConfiguration.MappingClasses
{
	public class Slice3rBedShape : MappedSetting
	{
		public Slice3rBedShape(PrinterConfig printer, string canonicalSettingsName)
			: base(printer, canonicalSettingsName, canonicalSettingsName)
		{
		}

		public override string Value
		{
			get
			{
				Vector2 printCenter = printer.Settings.GetValue<Vector2>(SettingsKey.print_center);
				Vector2 bedSize = printer.Settings.GetValue<Vector2>(SettingsKey.bed_size);
				switch (printer.Settings.GetValue<BedShape>(SettingsKey.bed_shape))
				{
					case BedShape.Circular:
						{
							int numPoints = 10;
							double angle = MathHelper.Tau / numPoints;
							string bedString = "";
							bool first = true;
							for (int i = 0; i < numPoints; i++)
							{
								if (!first)
								{
									bedString += ",";
								}
								double x = Math.Cos(angle * i);
								double y = Math.Sin(angle * i);
								bedString += $"{printCenter.X + x * bedSize.X / 2:0.####}x{printCenter.Y + y * bedSize.Y / 2:0.####}";
								first = false;
							}
							return bedString;
						}
					//bed_shape = 99.4522x10.4528,97.8148x20.7912,95.1057x30.9017,91.3545x40.6737,86.6025x50,80.9017x58.7785,74.3145x66.9131,66.9131x74.3145,58.7785x80.9017,50x86.6025,40.6737x91.3545,30.9017x95.1057,20.7912x97.8148,10.4528x99.4522,0x100,-10.4528x99.4522,-20.7912x97.8148,-30.9017x95.1057,-40.6737x91.3545,-50x86.6025,-58.7785x80.9017,-66.9131x74.3145,-74.3145x66.9131,-80.9017x58.7785,-86.6025x50,-91.3545x40.6737,-95.1057x30.9017,-97.8148x20.7912,-99.4522x10.4528,-100x0,-99.4522x - 10.4528,-97.8148x - 20.7912,-95.1057x - 30.9017,-91.3545x - 40.6737,-86.6025x - 50,-80.9017x - 58.7785,-74.3145x - 66.9131,-66.9131x - 74.3145,-58.7785x - 80.9017,-50x - 86.6025,-40.6737x - 91.3545,-30.9017x - 95.1057,-20.7912x - 97.8148,-10.4528x - 99.4522,0x - 100,10.4528x - 99.4522,20.7912x - 97.8148,30.9017x - 95.1057,40.6737x - 91.3545,50x - 86.6025,58.7785x - 80.9017,66.9131x - 74.3145,74.3145x - 66.9131,80.9017x - 58.7785,86.6025x - 50,91.3545x - 40.6737,95.1057x - 30.9017,97.8148x - 20.7912,99.4522x - 10.4528,100x0

					case BedShape.Rectangular:
					default:
						{
							//bed_shape = 0x0,200x0,200x200,0x200
							string bedString = $"{printCenter.X - bedSize.X / 2}x{printCenter.Y - bedSize.Y / 2}";
							bedString += $",{printCenter.X + bedSize.X / 2}x{printCenter.Y - bedSize.Y / 2}";
							bedString += $",{printCenter.X + bedSize.X / 2}x{printCenter.Y + bedSize.Y / 2}";
							bedString += $",{printCenter.X - bedSize.X / 2}x{printCenter.Y + bedSize.Y / 2}";
							return bedString;
						}
				}
			}
		}
	}
}