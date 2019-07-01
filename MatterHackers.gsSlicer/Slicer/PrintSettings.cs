

using System;
using gs;

namespace cotangent
{
	public class PrintSettings
	{
		protected SingleMaterialFFFSettings Active;

		protected bool bValueModified = false;
		public bool HasModifiedValues
		{
			get { return bValueModified; }
		}


		protected double layer_height = 0.2;
		public double LayerHeightMM
		{
			get { return layer_height; }
			set { if (layer_height != value) { layer_height = value; setting_modified(true, true); } }
		}
		public bool LayerHeightMM_Modified { get { return layer_height != Active.LayerHeightMM; } }


		public enum OpenMeshMode
		{
			Clipped = 0, Embedded = 1, Ignored = 2
		}
		OpenMeshMode open_mode = OpenMeshMode.Clipped;
		public OpenMeshMode OpenMode
		{
			get { return open_mode; }
		}
		public int OpenModeInt
		{
			get { return (int)open_mode; }
			set
			{
				OpenMeshMode newmode = (OpenMeshMode)value;
				if (open_mode != newmode)
				{
					open_mode = newmode; setting_modified(true, true);
				}
			}
		}


		protected int outer_shells = 2;
		public int OuterShells
		{
			get { return outer_shells; }
			set { if (outer_shells != value) { outer_shells = value; setting_modified(false, true); } }
		}
		public bool OuterShells_Modified { get { return outer_shells != Active.Shells; } }


		protected int roof_layers = 2;
		public int RoofLayers
		{
			get { return roof_layers; }
			set { if (roof_layers != value) { roof_layers = value; setting_modified(false, true); } }
		}
		public bool RoofLayers_Modified { get { return roof_layers != Active.RoofLayers; } }


		protected int floor_layers = 2;
		public int FloorLayers
		{
			get { return floor_layers; }
			set { if (floor_layers != value) { floor_layers = value; setting_modified(false, true); } }
		}
		public bool FloorLayers_Modified { get { return floor_layers != Active.FloorLayers; } }


		protected double infill_step = 3.0;
		public double InfillStepX
		{
			get { return infill_step; }
			set { if (infill_step != value) { infill_step = value; setting_modified(false, true); } }
		}
		public bool InfillStepX_Modified { get { return infill_step != Active.SparseLinearInfillStepX; } }


		protected int interior_solid_region_shells = 0;
		public int InteriorSolidRegionShells
		{
			get { return interior_solid_region_shells; }
			set { if (interior_solid_region_shells != value) { interior_solid_region_shells = value; setting_modified(false, true); } }
		}
		public bool InteriorSolidRegionShells_Modified { get { return interior_solid_region_shells != Active.InteriorSolidRegionShells; } }


		protected bool clip_self_overlaps = false;
		public bool ClipSelfOverlaps
		{
			get { return clip_self_overlaps; }
			set { if (clip_self_overlaps != value) { clip_self_overlaps = value; setting_modified(false, true); } }
		}
		public bool ClipSelfOverlaps_Modified { get { return clip_self_overlaps != Active.ClipSelfOverlaps; } }




		/*
         * Start layer settings
         */
		protected int start_layers = 0;
		public int StartLayers
		{
			get { return start_layers; }
			set { if (start_layers != value) { start_layers = value; setting_modified(true, true); } }
		}
		public bool StartLayers_Modified { get { return start_layers != Active.StartLayers; } }


		protected double start_layer_height = 0.2;
		public double StartLayerHeightMM
		{
			get { return start_layer_height; }
			set { if (start_layer_height != value) { start_layer_height = value; setting_modified(true, true); } }
		}
		public bool StartLayerHeightMM_Modified { get { return start_layer_height != Active.StartLayerHeightMM; } }



		/*
         *  Support settings
         */

		protected bool generate_support = false;
		public bool GenerateSupport
		{
			get { return generate_support; }
			set { if (generate_support != value) { generate_support = value; setting_modified(true, true); } }
		}
		public bool GenerateSupport_Modified { get { return generate_support != Active.GenerateSupport; } }

		protected double overhang_angle = 35.0;
		public double OverhangAngleDeg
		{
			get { return overhang_angle; }
			set { if (overhang_angle != value) { overhang_angle = value; setting_modified(false, true); } }
		}
		public bool OverhangAngleDeg_Modified { get { return overhang_angle != Active.SupportOverhangAngleDeg; } }

		protected bool support_minz_tips = true;
		public bool SupportMinZTips
		{
			get { return support_minz_tips; }
			set { if (support_minz_tips != value) { support_minz_tips = value; setting_modified(false, true); } }
		}
		public bool SupportMinZTips_Modified { get { return support_minz_tips != Active.SupportMinZTips; } }

		protected double support_step = 3.0;
		public double SupportStepX
		{
			get { return support_step; }
			set { if (support_step != value) { support_step = value; setting_modified(false, true); } }
		}
		public bool SupportStepX_Modified { get { return support_step != Active.SupportSpacingStepX; } }


		protected bool enable_support_shell = false;
		public bool EnableSupportShell
		{
			get { return enable_support_shell; }
			set { if (enable_support_shell != value) { enable_support_shell = value; setting_modified(false, true); } }
		}
		public bool EnableSupportShell_Modified { get { return enable_support_shell != Active.EnableSupportShell; } }


		protected bool enable_support_release_opt = false;
		public bool EnableSupportReleaseOpt
		{
			get { return enable_support_release_opt; }
			set { if (enable_support_release_opt != value) { enable_support_release_opt = value; setting_modified(false, true); } }
		}
		public bool EnableSupportReleaseOpt_Modified { get { return enable_support_release_opt != Active.EnableSupportReleaseOpt; } }


		protected double support_solid_space = 0.3;
		public double SupportSolidSpace
		{
			get { return support_solid_space; }
			set { if (support_solid_space != value) { support_solid_space = value; setting_modified(false, true); } }
		}
		public bool SupportSolidSpace_Modified { get { return support_solid_space != Active.SupportSolidSpace; } }


		/*
         *  Bridging settings
         */

		protected bool enable_bridging = false;
		public bool EnableBridging
		{
			get { return enable_bridging; }
			set { if (enable_bridging != value) { enable_bridging = value; setting_modified(false, true); } }
		}
		public bool EnableBridging_Modified { get { return enable_bridging != Active.EnableBridging; } }

		protected double max_bridge_dist = 10.0;
		public double MaxBridgeDistanceMM
		{
			get { return max_bridge_dist; }
			set { if (max_bridge_dist != value) { max_bridge_dist = value; setting_modified(false, true); } }
		}
		public bool MaxBridgeDistanceMM_Modified { get { return max_bridge_dist != Active.MaxBridgeWidthMM; } }



		/*
         *  Extra settings
         */

		protected int layer_range_min = 1;
		public int LayerRangeMin
		{
			get { return layer_range_min; }
			set { if (layer_range_min != value) { layer_range_min = value; setting_modified(false, true); } }
		}
		public bool LayerRangeMin_Modified { get { return layer_range_min != (Active.LayerRangeFilter.a + 1); } }


		protected int layer_range_max = 999999999;
		public int LayerRangeMax
		{
			get { return layer_range_max; }
			set { if (layer_range_max != value) { layer_range_max = value; setting_modified(false, true); } }
		}
		public bool LayerRangeMax_Modified { get { return layer_range_max != (Active.LayerRangeFilter.b + 1); } }



		/*
         * Machine settings
         */


		protected double nozzle_diam = 0.4;
		public double NozzleDiameterMM
		{
			get { return nozzle_diam; }
			set { if (nozzle_diam != value) { nozzle_diam = value; setting_modified(true, true); } }
		}
		public bool NozzleDiameterMM_Modified { get { return nozzle_diam != Active.Machine.NozzleDiamMM; } }

		protected double filament_diam = 0.4;
		public double FilamentDiameterMM
		{
			get { return filament_diam; }
			set { if (filament_diam != value) { filament_diam = value; setting_modified(true, true); } }
		}
		public bool FilamentDiameterMM_Modified { get { return filament_diam != Active.Machine.FilamentDiamMM; } }


		protected int extruder_temp = 230;
		public int ExtruderTempC
		{
			get { return extruder_temp; }
			set { if (extruder_temp != value) { extruder_temp = value; setting_modified(false, true); } }
		}
		public bool ExtruderTempC_Modified { get { return extruder_temp != Active.ExtruderTempC; } }


		public bool HasHeatedBed
		{
			get { return Active.Machine.HasHeatedBed; }
		}

		protected int bed_temp = 0;
		public int BedTempC
		{
			get { return bed_temp; }
			set { if (bed_temp != value) { bed_temp = value; setting_modified(false, true); } }
		}
		public bool BedTempC_Modified { get { return bed_temp != Active.HeatedBedTempC; } }


		protected int print_speed = 90;
		public int PrintSpeedMMS
		{
			get { return print_speed; }
			set { if (print_speed != value) { print_speed = value; setting_modified(false, true); } }
		}
		public bool PrintSpeedMMS_Modified { get { return print_speed != (int)Math.Round(Active.RapidExtrudeSpeed / 60, 0); } }


		protected int travel_speed = 150;
		public int TravelSpeedMMS
		{
			get { return travel_speed; }
			set { if (travel_speed != value) { travel_speed = value; setting_modified(false, true); } }
		}
		public bool TravelSpeedMMS_Modified { get { return travel_speed != (int)Math.Round(Active.RapidTravelSpeed / 60, 0); } }


		protected int fan_speed_x = 100;
		public int FanSpeedX
		{
			get { return fan_speed_x; }
			set { if (fan_speed_x != value) { fan_speed_x = value; setting_modified(false, true); } }
		}
		public bool FanSpeedX_Modified { get { return fan_speed_x != (int)Math.Round(Active.FanSpeedX * 100, 0); } }


		protected int bed_size_x = 100;
		public int BedSizeXMM
		{
			get { return bed_size_x; }
			set { if (bed_size_x != value) { bed_size_x = value; setting_modified(false, true);  } }
		}
		public bool BedSizeXMM_Modified { get { return bed_size_x != (int)Active.Machine.BedSizeXMM; } }

		protected int bed_size_y = 100;
		public int BedSizeYMM
		{
			get { return bed_size_y; }
			set { if (bed_size_y != value) { bed_size_y = value; setting_modified(false, true); } }
		}
		public bool BedSizeYMM_Modified { get { return bed_size_y != (int)Active.Machine.BedSizeYMM; } }

		protected int bed_size_z = 100;
		public int BedSizeZMM
		{
			get { return bed_size_z; }
			set { if (bed_size_z != value) { bed_size_z = value; setting_modified(false, true); } }
		}
		public bool BedSizeZMM_Modified { get { return bed_size_z != (int)Active.Machine.MaxHeightMM; } }


		public delegate void SettingsModifiedEvent(PrintSettings settings);
		public event SettingsModifiedEvent OnNewSettings;
		public event SettingsModifiedEvent OnSettingModified;


		void setting_modified(bool invalidate_slicing, bool invalidate_paths)
		{
			//if (invalidate_slicing)
			//	CC.InvalidateSlicing();
			//else if (invalidate_paths)
			//	CC.InvalidateToolPaths();
			bValueModified = true;
			OnSettingModified?.Invoke(this);
		}



		public void UpdateFromSettings(PlanarAdditiveSettings settings)
		{
			if (settings == null)
				return;
			if (settings is SingleMaterialFFFSettings == false)
				throw new Exception("PrintSettings.UpdateFromSettings: invalid settings type!");

			SingleMaterialFFFSettings ss = settings as SingleMaterialFFFSettings;
			Active = ss;

			LayerHeightMM = ss.LayerHeightMM;
			OuterShells = ss.Shells;
			RoofLayers = ss.RoofLayers;
			FloorLayers = ss.FloorLayers;
			InfillStepX = ss.SparseLinearInfillStepX;
			ClipSelfOverlaps = ss.ClipSelfOverlaps;
			InteriorSolidRegionShells = ss.InteriorSolidRegionShells;
			StartLayers = ss.StartLayers;
			StartLayerHeightMM = ss.StartLayerHeightMM;

			GenerateSupport = ss.GenerateSupport;
			OverhangAngleDeg = ss.SupportOverhangAngleDeg;
			SupportMinZTips = ss.SupportMinZTips;
			EnableSupportShell = ss.EnableSupportShell;
			EnableSupportReleaseOpt = ss.EnableSupportReleaseOpt;
			SupportStepX = ss.SupportSpacingStepX;
			SupportSolidSpace = ss.SupportSolidSpace;

			EnableBridging = ss.EnableBridging;
			MaxBridgeDistanceMM = ss.MaxBridgeWidthMM;

			LayerRangeMin = ss.LayerRangeFilter.a + 1;
			LayerRangeMax = ss.LayerRangeFilter.b + 1;

			NozzleDiameterMM = ss.Machine.NozzleDiamMM;
			FilamentDiameterMM = ss.Machine.FilamentDiamMM;
			ExtruderTempC = ss.ExtruderTempC;
			BedTempC = ss.HeatedBedTempC;
			PrintSpeedMMS = (int)Math.Round(ss.RapidExtrudeSpeed / 60, 0);
			TravelSpeedMMS = (int)Math.Round(ss.RapidTravelSpeed / 60, 0);
			FanSpeedX = (int)Math.Round(ss.FanSpeedX * 100, 0);
			BedSizeXMM = (int)ss.Machine.BedSizeXMM;
			BedSizeYMM = (int)ss.Machine.BedSizeYMM;
			BedSizeZMM = (int)ss.Machine.MaxHeightMM;

			bValueModified = false;
			OnNewSettings?.Invoke(this);
		}

		public void WriteToSettings(PlanarAdditiveSettings settings)
		{
			if (settings is SingleMaterialFFFSettings == false)
				throw new Exception("PrintSettings.UpdateToSettings: invalid settings type!");
			WriteToSettings(settings as SingleMaterialFFFSettings);
		}
		public void WriteToSettings(SingleMaterialFFFSettings settings)
		{
			settings.LayerHeightMM = LayerHeightMM;
			settings.Shells = OuterShells;
			settings.RoofLayers = RoofLayers;
			settings.FloorLayers = FloorLayers;
			settings.SparseLinearInfillStepX = InfillStepX;
			settings.ClipSelfOverlaps = ClipSelfOverlaps;
			settings.InteriorSolidRegionShells = InteriorSolidRegionShells;
			settings.StartLayers = StartLayers;
			settings.StartLayerHeightMM = StartLayerHeightMM;

			settings.GenerateSupport = GenerateSupport;
			settings.SupportOverhangAngleDeg = OverhangAngleDeg;
			settings.SupportMinZTips = SupportMinZTips;
			settings.EnableSupportShell = EnableSupportShell;
			settings.EnableSupportReleaseOpt = EnableSupportReleaseOpt;
			settings.SupportSpacingStepX = SupportStepX;
			settings.SupportSolidSpace = SupportSolidSpace;

			settings.EnableBridging = EnableBridging;
			settings.MaxBridgeWidthMM = MaxBridgeDistanceMM;

			int use_min = Math.Max(0, LayerRangeMin - 1);
			int use_max = (LayerRangeMax == 0) ? 999999999 : LayerRangeMax - 1;
			if (use_max < use_min)
				use_max = use_min;
			settings.LayerRangeFilter = new g3.Interval1i(use_min, use_max);

			settings.FanSpeedX = (double)FanSpeedX / 100.0;

			settings.Machine.NozzleDiamMM = NozzleDiameterMM;
			settings.Machine.FilamentDiamMM = FilamentDiameterMM;
			settings.ExtruderTempC = ExtruderTempC;
			settings.HeatedBedTempC = BedTempC;
			settings.RapidExtrudeSpeed = PrintSpeedMMS * 60;
			settings.RapidTravelSpeed = TravelSpeedMMS * 60;
			settings.Machine.BedSizeXMM = BedSizeXMM;
			settings.Machine.BedSizeYMM = BedSizeYMM;
			settings.Machine.MaxHeightMM = BedSizeZMM;
		}


		public SingleMaterialFFFSettings CloneCurrentSettings()
		{
			SingleMaterialFFFSettings clone = Active.CloneAs<SingleMaterialFFFSettings>();
			WriteToSettings(clone);
			return clone;
		}




	}

}
