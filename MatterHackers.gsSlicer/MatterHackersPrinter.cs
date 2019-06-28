/*
Copyright (c) 2019, John Lewin
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

using gs;
using MatterHackers.Agg;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.VectorMath;

namespace MatterHackers.gsBundle
{
	public class MatterHackersPrinter : GenericRepRapSettings
	{
		public MatterHackersPrinter(PrinterSettings settings)
		{
			string make = settings.GetValue<string>(SettingsKey.make);
			string model = settings.GetValue<string>(SettingsKey.model);

			var printCenter = settings.GetValue<Vector2>(SettingsKey.print_center);
			var bedSize = settings.GetValue<Vector2>(SettingsKey.bed_size);

			machineInfo = new FFFMachineInfo()
			{
				ManufacturerName = make,
				ManufacturerUUID = agg_basics.GetLongHashCode(make).ToString(),
				ModelIdentifier = model,
				ModelUUID = agg_basics.GetLongHashCode(model).ToString(),
				Class = MachineClass.PlasticFFFPrinter,

				BedSizeXMM = (int)settings.BedBounds.Width,
				BedSizeYMM = (int)settings.BedBounds.Height,
				// BedSizeZMM = settings.GetValue<int>(SettingsKey.build_height),
				MaxHeightMM = settings.GetValue<int>(SettingsKey.build_height),

				NozzleDiamMM = settings.GetValue<double>(SettingsKey.nozzle_diameter),
				FilamentDiamMM = settings.GetValue<double>(SettingsKey.filament_diameter),

				// Set bed origin factors based on printer center/bedsize
				BedOriginFactorX = printCenter.X / bedSize.X,
				BedOriginFactorY = printCenter.Y / bedSize.Y,

				HasHeatedBed = settings.GetValue<bool>(SettingsKey.has_heated_bed),
				HasAutoBedLeveling = settings.GetValue<bool>(SettingsKey.has_hardware_leveling),
				EnableAutoBedLeveling = false,

				// TODO: Consider how to adapt and/or update for MatterControl printers
				MaxExtruderTempC = 250,
				MaxBedTempC = 80,

				MaxExtrudeSpeedMMM = 80 * 60,
				MaxTravelSpeedMMM = 120 * 60,
				MaxZTravelSpeedMMM = 100 * 60,
				MaxRetractSpeedMMM = 45 * 60,
				MinLayerHeightMM = 0.05,
				MaxLayerHeightMM = 0.3,
			};

			LayerHeightMM = settings.GetValue<double>(SettingsKey.layer_height);

			ExtruderTempC = (int)settings.GetValue<double>(SettingsKey.temperature1);
			HeatedBedTempC = (int)settings.GetValue<double>(SettingsKey.bed_temperature);

			RoofLayers = settings.GetValue<int>(SettingsKey.top_solid_layers);
			FloorLayers = settings.GetValue<int>(SettingsKey.bottom_solid_layers);

			SolidFillNozzleDiamStepX = 1.0;
			RetractDistanceMM = settings.GetValue<double>(SettingsKey.retract_length);

			RetractSpeed = settings.GetValue<double>(SettingsKey.retract_speed) * 60;
			ZTravelSpeed = settings.Helpers.GetMovementSpeeds()["z"] * 60;
			RapidTravelSpeed = settings.GetValue<double>(SettingsKey.travel_speed) * 60;
			CarefulExtrudeSpeed = settings.GetValue<double>(SettingsKey.first_layer_speed) * 60;
			RapidExtrudeSpeed = Machine.MaxExtrudeSpeedMMM;

			// Looks like fractional of perimeter speed
			var outerSpeed = settings.GetValue<double>(SettingsKey.external_perimeter_speed);
			var innerSpeed = settings.GetValue<double>(SettingsKey.external_perimeter_speed);

			OuterPerimeterSpeedX = outerSpeed / innerSpeed;
		}

		public override AssemblerFactoryF AssemblerType()
		{
			return (GCodeBuilder builder, SingleMaterialFFFSettings settings) =>
			{
				return new RepRapAssembler(builder, settings)
				{
					HeaderCustomizerF = HeaderCustomF
				};
			};
		}

		protected void HeaderCustomF(RepRapAssembler.HeaderState state, GCodeBuilder Builder)
		{
			if (state == RepRapAssembler.HeaderState.BeforePrime)
			{
				if (Machine.HasAutoBedLeveling && Machine.EnableAutoBedLeveling)
				{
					Builder.BeginGLine(29, "auto-level bed");
				}
			}
		}
	}
}
