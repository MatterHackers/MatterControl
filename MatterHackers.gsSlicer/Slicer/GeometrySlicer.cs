using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using g3;
using gs;
using gs.info;

namespace cotangent
{
	/// <summary>
	/// Auto-compute slicing of current CC.PrintMeshes
	///
	/// [TODO]
	///    - be smarter about polylines?
	///    - don't necessarily always need to discard SlicerMeshes, for example if only
	///      slicing params changed, we can avoid copy (maybe irrelevant though)
	/// </summary>
	public class GeometrySlicer
	{
		PrintMeshAssembly SlicerMeshes;
		PlanarSliceStack SliceSet;
		bool SliceStackValid;
		object data_lock = new object();

		bool show_slice_polylines = false;
		public bool ShowSlicePolylines
		{
			get { return show_slice_polylines; }
			set { set_slice_polylines_visible(value); }
		}

		public delegate void SlicingStateChangeHandler();
		public event SlicingStateChangeHandler SlicingInvalidatedEvent;
		public event SlicingStateChangeHandler SlicingUpdatedEvent;

		// public event SlicingProgressHandler SlicingProgressEvent;

		public bool PauseSlicing = false;

		public GeometrySlicer()
		{
			InvalidateSlicing();
		}

		public bool IsResultValid()
		{
			bool valid = false;
			lock (data_lock)
			{
				valid = SliceStackValid;
			}
			return valid;
		}

		public bool ExtractResultsIfValid(out PrintMeshAssembly printMeshes, out PlanarSliceStack slices)
		{
			bool valid = false;
			lock (data_lock)
			{
				printMeshes = SlicerMeshes;
				slices = SliceSet;
				valid = SliceStackValid;
			}
			return valid;
		}

		public void InvalidateSlicing()
		{
			//cancel_active_compute();

			lock (data_lock)
			{
				SlicerMeshes = null;
				SliceSet = null;
				SliceStackValid = false;
			}

			// discard_slice_polylines();

		}

		public void SliceMeshes(List<DMesh3> sourceMeshes, PrintSettings printSettings)
		{
			SliceStackValid = false;

			try
			{
				var sliceTask = new SliceTask();
				sliceTask.meshCopies = sourceMeshes;
				sliceTask.Compute(printSettings);

				SlicerMeshes = sliceTask.MeshAssembly;
				SliceSet = sliceTask.SliceStack;
				SliceStackValid = true;
			}
			catch (Exception ex)
			{
			}
		}

		float last_spawn_time = 0;

		SliceTask active_compute;
		object active_compute_lock = new object();

		class SliceTask
		{
			// input data
			public Frame3f[] meshToScene;
			public Vector3f[] localScale;
			public List<DMesh3> meshCopies { get; set; }
			public PrintMeshSettings[] meshSettings;

			// computed data
			public bool Finished;
			public bool Success;
			public PrintMeshAssembly MeshAssembly;
			public PlanarSliceStack SliceStack;

			// internal
			MeshPlanarSlicerPro slicer;

			public SliceTask()
			{
				Finished = false;
				Success = false;
			}

			public void Compute(PrintSettings printSettings)
			{
				int N = meshCopies.Count();

				slicer = new MeshPlanarSlicerPro()
				{
					LayerHeightMM = printSettings.LayerHeightMM,
					// [RMS] 1.5 here is a hack. If we don't leave a bit of space then often the filament gets squeezed right at
					//   inside/outside transitions, which is bad. Need a better way to handle.
					OpenPathDefaultWidthMM = printSettings.NozzleDiameterMM * 1.5,
					SetMinZValue = 0,
					SliceFactoryF = PlanarSlicePro.FactoryF
				};

				if (printSettings.OpenMode == PrintSettings.OpenMeshMode.Clipped)
					slicer.DefaultOpenPathMode = PrintMeshOptions.OpenPathsModes.Clipped;
				else if (printSettings.OpenMode == PrintSettings.OpenMeshMode.Embedded)
					slicer.DefaultOpenPathMode = PrintMeshOptions.OpenPathsModes.Embedded;
				else if (printSettings.OpenMode == PrintSettings.OpenMeshMode.Ignored)
					slicer.DefaultOpenPathMode = PrintMeshOptions.OpenPathsModes.Ignored;

				if (printSettings.StartLayers > 0)
				{
					int start_layers = printSettings.StartLayers;
					double std_layer_height = printSettings.LayerHeightMM;
					double start_layer_height = printSettings.StartLayerHeightMM;
					slicer.LayerHeightF = (layer_i) =>
					{
						return (layer_i < start_layers) ? start_layer_height : std_layer_height;
					};
				}

				try
				{
					MeshAssembly = new PrintMeshAssembly();

					for (int k = 0; k < N; ++k)
					{
						DMesh3 mesh = meshCopies[k];
						//Frame3f mapF = meshToScene[k];
						PrintMeshSettings settings = new PrintMeshSettings();

						var options = new PrintMeshOptions
						{
							IsSupport = (settings.ObjectType == PrintMeshSettings.ObjectTypes.Support),
							IsCavity = (settings.ObjectType == PrintMeshSettings.ObjectTypes.Cavity),
							IsCropRegion = (settings.ObjectType == PrintMeshSettings.ObjectTypes.CropRegion),
							IsOpen = settings.OuterShellOnly,
							OpenPathMode = PrintMeshSettings.Convert(settings.OpenMeshMode),
							Extended = new ExtendedPrintMeshOptions()
							{
								ClearanceXY = settings.Clearance,
								OffsetXY = settings.OffsetXY
							}
						};

						//Vector3f scale = localScale[k];
						//MeshTransforms.Scale(mesh, scale.x, scale.y, scale.z);
						//MeshTransforms.FromFrame(mesh, mapF);
						//MeshTransforms.FlipLeftRightCoordSystems(mesh);
						//MeshTransforms.ConvertYUpToZUp(mesh);

						var decomposer = new MeshAssembly(mesh)
						{
							HasNoVoids = settings.NoVoids
						};
						decomposer.Decompose();

						MeshAssembly.AddMeshes(decomposer.ClosedSolids, options);

						PrintMeshOptions openOptions = options.Clone();
						MeshAssembly.AddMeshes(decomposer.OpenMeshes, openOptions);
					}

					if (slicer.Add(MeshAssembly) == false)
						throw new Exception("error adding PrintMeshAssembly to Slicer!!");

					// set clip box
					Box2d clip_box = new Box2d(
						Vector2d.Zero,
						new Vector2d(printSettings.BedSizeXMM / 2, printSettings.BedSizeYMM / 2));

					slicer.ValidRegions = new List<GeneralPolygon2d>() {
						new GeneralPolygon2d(new Polygon2d(clip_box.ComputeVertices()))
					};

					SliceStack = slicer.Compute();

					Success = true;
				}
				catch (Exception e)
				{
					//DebugUtil.Log("GeometrySlicer.Compute: exception: " + e.Message);
					System.Diagnostics.Debugger.Break();
					Success = false;
				}

				Finished = true;
			}
		}

		void set_slice_polylines_visible(bool bSet)
		{
			if (bSet == show_slice_polylines)
				return;

			compute_slice_polylines();
			show_slice_polylines = true;
		}

		void compute_slice_polylines()
		{
			// fMaterial mat1 = MaterialUtil.CreateFlatMaterialF(Colorf.Black);
			// fMaterial mat2 = MaterialUtil.CreateFlatMaterialF(Colorf.BlueMetal);

			// [TODO] do we need to hold data_lock here? seems like no since main thread is blocked,
			//  then it would never be the case that we are setting SliceSet = null

			// create geometry
			int slice_i = 0;
			//SlicePolylines = new List<fPolylineGameObject>();

			foreach (PlanarSlice slice in SliceSet.Slices)
			{
				//DebugUtil.Log(2, "Slice has {0} solids", slice.Solids.Count);
				Colorf slice_color = (slice_i % 2 == 0) ? Colorf.Black : Colorf.BlueMetal;
				// fMaterial slice_mat = (slice_i % 2 == 0) ? mat1 : mat2;
				slice_i++;
				foreach (GeneralPolygon2d poly in slice.Solids)
				{
					List<Vector3f> polyLine = new List<Vector3f>();
					for (int pi = 0; pi <= poly.Outer.VertexCount; ++pi)
					{
						int i = pi % poly.Outer.VertexCount;
						Vector2d v2 = poly.Outer[i];
						Vector2d n2 = poly.Outer.GetTangent(i).Perp;

						Vector3d v3 = new Vector3d(v2.x, v2.y, slice.Z);
						v3 = MeshTransforms.ConvertZUpToYUp(v3);
						v3 = MeshTransforms.FlipLeftRightCoordSystems(v3);
						Vector3d n3 = MeshTransforms.ConvertZUpToYUp(new Vector3d(n2.x, n2.y, 0));
						n3 = MeshTransforms.FlipLeftRightCoordSystems(n3);
						n3.Normalize();
						v3 += 0.1f * n3;

						polyLine.Add((Vector3f)v3);
					}

					// Do something with polyline....
					Console.WriteLine(polyLine);

					////DebugUtil.Log(2, "Polyline has {0} vertiecs", polyLine.Count);
					//fPolylineGameObject go = GameObjectFactory.CreatePolylineGO(
					//    "slice_outer", polyLine, slice_color, 0.1f, LineWidthType.World);
					//go.SetMaterial(slice_mat, true);
					//CC.ActiveScene.RootGameObject.AddChild(go, false);
					//SlicePolylines.Add(go);
				}
			}
		}
	}
}
