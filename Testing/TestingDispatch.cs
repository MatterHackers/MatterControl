using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace MatterHackers.MatterControl.Testing
{
    public class TestingDispatch
    {
        bool hadErrors = false;
        public bool HadErrors { get { return hadErrors; } }

        string errorLogFileName = null;
        public TestingDispatch()
        {
            string exePath = Path.GetDirectoryName( System.Reflection.Assembly.GetExecutingAssembly().Location );
            errorLogFileName = Path.Combine(exePath, "ErrorLog.txt");
            string firstLine = string.Format("MatterControl Errors: {0:yyyy-MM-dd hh:mm:ss tt}", DateTime.Now);
            using (StreamWriter file = new StreamWriter(errorLogFileName))
            {
                file.WriteLine(firstLine);
            }
        }

        public void RunTests(string[] testCommands)
        {
            try { ReleaseTests.AssertDebugNotDefined(); }
            catch (Exception e) { DumpException(e); }

            try { MatterHackers.GCodeVisualizer.GCodeFile.AssertDebugNotDefined(); }
            catch (Exception e) { DumpException(e); }
        }

        void DumpException(Exception e)
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
