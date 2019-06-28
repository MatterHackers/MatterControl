using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using g3;
using gs;

namespace cotangent
{
	public class ToolpathGenerator
	{
		private object active_compute_lock = new object();

		public GCodeFile CurrentGCode;
		public ToolpathSet Toolpaths;
		public LayersDetector LayerInfo;
		public SingleMaterialFFFSettings Settings;

		public PlanarSliceStack Slices;

		public bool ToolpathsValid;
		public bool ToolpathsFailed;

		public bool ShowActualGCodePaths = false;

		public bool PauseToolpathing = false;

		public ToolpathGenerator()
		{
		}

		public void CreateToolPaths(PrintMeshAssembly meshes, PlanarSliceStack slices, PrintSettings settings)
		{
			// have to wait for valid slice stack
			if (slices == null)
			{
				return;
			}

			//mark_spawn_time();
			this.BuildToolPaths(new GenerationTask
			{
				PrintSettings = settings.CloneCurrentSettings(),
				Meshes = meshes,
				SliceSet = slices,
				InterpretGCodePaths = ShowActualGCodePaths
			}, settings);
		}

		private void BuildToolPaths(GenerationTask generationTask, PrintSettings settings)
		{
			lock (active_compute_lock)
			{
				DebugUtil.Log("[ToolpathGenerator] Spawning Compute!!");
				generationTask.Compute(settings);

				if (generationTask.Success)
				{
					CurrentGCode = generationTask.gcode;
					Toolpaths = generationTask.paths;
					LayerInfo = generationTask.layerInfo;
					Settings = generationTask.PrintSettings;
					Slices = generationTask.SliceSet;
					ToolpathsValid = true;
					ToolpathsFailed = false;

					//CC.Objects.SetToolpaths(this);
				}
				else
				{
					CurrentGCode = null;
					Toolpaths = null;
					LayerInfo = null;
					Settings = null;
					Slices = null;
					ToolpathsValid = false;
					ToolpathsFailed = true;
				}
			}
		}

		class GenerationTask
		{
			// input data
			public SingleMaterialFFFSettings PrintSettings;
			public PrintMeshAssembly Meshes;
			public PlanarSliceStack SliceSet;
			public bool InterpretGCodePaths;

			// computed data
			public bool Finished;
			public bool Success;
			public bool RequestCancel;
			public GCodeFile gcode;
			public ToolpathSet paths;
			public LayersDetector layerInfo;

			// internal
			SingleMaterialFFFPrintGenerator printer;

			public GenerationTask()
			{
				Finished = false;
				Success = false;
				RequestCancel = false;
			}

			public void Compute(PrintSettings settings)
			{
				RequestCancel = false;

				printer =
					new SingleMaterialFFFPrintGenerator(Meshes, SliceSet, PrintSettings);

				if (PrintSettings.EnableSupportReleaseOpt)
				{
					printer.LayerPostProcessor = new SupportConnectionPostProcessor()
					{
						ZOffsetMM = PrintSettings.SupportReleaseGap
					};
				}

				// if we aren't interpreting GCode, we want generator to return its path set
				printer.AccumulatePathSet = (InterpretGCodePaths == false);

				// set clip region
				Box2d clip_box = new Box2d(Vector2d.Zero,
					new Vector2d(settings.BedSizeXMM / 2, settings.BedSizeYMM / 2));
				printer.PathClipRegions = new List<GeneralPolygon2d>() {
					new GeneralPolygon2d(new Polygon2d(clip_box.ComputeVertices()))
				};

				printer.ErrorF = (msg, trace) =>
				{
					if (RequestCancel == false)
						DebugUtil.Log(2, "Slicer Error! msg: {0} stack {1}", msg, trace);
				};

				DebugUtil.Log(2, "Generating gcode...");

				try
				{
					if (printer.Generate() == false)
						throw new Exception("generate failed");   // this will be caught below

					gcode = printer.Result;

					//DebugUtil.Log(2, "Interpreting gcode...");

					if (InterpretGCodePaths)
					{
						GCodeToToolpaths converter = new GCodeToToolpaths();
						MakerbotInterpreter interpreter = new MakerbotInterpreter();
						interpreter.AddListener(converter);
						InterpretArgs interpArgs = new InterpretArgs();
						interpreter.Interpret(gcode, interpArgs);
						paths = converter.PathSet;
					}
					else
						paths = printer.AccumulatedPaths;

					//DebugUtil.Log(2, "Detecting layers...");
					layerInfo = new LayersDetector(paths);

					Success = true;

				}
				catch (Exception e)
				{
					DebugUtil.Log("ToolpathGenerator.Compute: exception: " + e.Message);
					Success = false;
				}

				Finished = true;
			}
		}
	}
}
