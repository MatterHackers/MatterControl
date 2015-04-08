using System;
using System.IO;

namespace MatterHackers.MatterControl.Testing
{
	public class TestingDispatch
	{
		private bool hadErrors = false;

		public bool HadErrors { get { return hadErrors; } }

		private string errorLogFileName = null;

		public TestingDispatch()
		{
			string exePath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
			errorLogFileName = Path.Combine(exePath, "ErrorLog.txt");
			string firstLine = string.Format("MatterControl Errors: {0:yyyy-MM-dd hh:mm:ss tt}", DateTime.Now);
			using (StreamWriter file = new StreamWriter(errorLogFileName))
			{
				file.WriteLine(firstLine);
			}
		}

		public void RunTests()
		{
			try { ReleaseTests.AssertDebugNotDefined(); }
			catch (Exception e) { DumpException(e); }

			try { MatterHackers.GCodeVisualizer.GCodeFile.AssertDebugNotDefined(); }
			catch (Exception e) { DumpException(e); }

			try { MatterHackers.Agg.Graphics2D.AssertDebugNotDefined(); }
			catch (Exception e) { DumpException(e); }

			try { MatterHackers.Agg.UI.SystemWindow.AssertDebugNotDefined(); }
			catch (Exception e) { DumpException(e); }

			try { ClipperLib.Clipper.AssertDebugNotDefined(); }
			catch (Exception e) { DumpException(e); }

			try { MatterHackers.Csg.CSGTests.AssertDebugNotDefined(); }
			catch (Exception e) { DumpException(e); }

			try { MatterHackers.Agg.ImageProcessing.InvertLightness.AssertDebugNotDefined(); }
			catch (Exception e) { DumpException(e); }

			try { MatterHackers.Localizations.TranslationMap.AssertDebugNotDefined(); }
			catch (Exception e) { DumpException(e); }

			try { MatterHackers.MarchingSquares.MarchingSquaresByte.AssertDebugNotDefined(); }
			catch (Exception e) { DumpException(e); }

			try { MatterHackers.MatterControl.PluginSystem.MatterControlPlugin.AssertDebugNotDefined(); }
			catch (Exception e) { DumpException(e); }

			try { MatterHackers.MatterSlice.MatterSlice.AssertDebugNotDefined(); }
			catch (Exception e) { DumpException(e); }

			try { MatterHackers.MeshVisualizer.MeshViewerWidget.AssertDebugNotDefined(); }
			catch (Exception e) { DumpException(e); }

			try { MatterHackers.RenderOpenGl.GLMeshTrianglePlugin.AssertDebugNotDefined(); }
			catch (Exception e) { DumpException(e); }

			if (!HadErrors)
			{
				File.Delete(errorLogFileName);
			}
		}

		private void DumpException(Exception e)
		{
			hadErrors = true;
			using (StreamWriter w = File.AppendText(errorLogFileName))
			{
				w.WriteLine(e.Message);
				w.Write(e.StackTrace);
				w.WriteLine();
			}
		}
	}
}