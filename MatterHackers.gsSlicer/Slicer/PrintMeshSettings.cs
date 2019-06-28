using System;

namespace cotangent
{
	public static class DebugUtil
	{
		internal static void Log(string v)
		{
			Console.WriteLine(v);
			//throw new NotImplementedException();
		}

		internal static void Log(int v1, string v2)
		{
			Console.WriteLine("{0} - {1}", v1, v2);
		}

		internal static void Log(int v1, string v2, string msg, string trace)
		{
			Console.WriteLine("{0} - {1} {2}", v1, v2, msg);
		}
	}

	public class PrintMeshSettings
    {
        // WARNING: this class is serialized! Do not change enum constants!
        public int Version = 1;

        public enum ObjectTypes
        {
            Solid = 0, Support = 1, Cavity = 2, CropRegion = 3, Ignored = 4
        }

        public enum OpenMeshModes
        {
            Default = 0, Clipped = 1, Embedded = 2, Ignored = 3,
        }

        public ObjectTypes ObjectType = ObjectTypes.Solid;
        public bool NoVoids = false;
        public bool OuterShellOnly = false;
        public OpenMeshModes OpenMeshMode = OpenMeshModes.Default;

        public double Clearance = 0;
        public double OffsetXY = 0;


        public PrintMeshSettings Clone() {
            return new PrintMeshSettings() {
                ObjectType = this.ObjectType,
                NoVoids = this.NoVoids,
                OuterShellOnly = this.OuterShellOnly,
                OpenMeshMode = this.OpenMeshMode,
                Clearance = this.Clearance,
                OffsetXY = this.OffsetXY
            };
        }


        public static gs.PrintMeshOptions.OpenPathsModes Convert(OpenMeshModes mode)
        {
            switch (mode) {
                case OpenMeshModes.Clipped: return gs.PrintMeshOptions.OpenPathsModes.Clipped;
                case OpenMeshModes.Embedded: return gs.PrintMeshOptions.OpenPathsModes.Embedded;
                case OpenMeshModes.Ignored: return gs.PrintMeshOptions.OpenPathsModes.Ignored;
                default:
                case OpenMeshModes.Default: return gs.PrintMeshOptions.OpenPathsModes.Default;
            }
        }


    }

}
