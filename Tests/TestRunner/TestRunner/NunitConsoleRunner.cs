using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace TestRunner
{
	public class NunitConsoleRunner
	{
		// Currently just running from the bin folder
		private const string nunitPath = "NUnit-2.6.4";

		private Uri siteBaseUri = new Uri("http://matterdata.azurewebsites.net/");

		public void RunTests(string projectFilePath, string resultsFilePath)
		{
			if (File.Exists(resultsFilePath))
			{
				File.Delete(resultsFilePath);
			}

			string binPath = !Directory.Exists(nunitPath) ? null : Path.Combine(nunitPath, "bin\\nunit-console-x86.exe");

			// Run the unit tests
			if (!string.IsNullOrWhiteSpace(binPath) && File.Exists(binPath))
			{
				Process process = new Process();
				process.StartInfo.Arguments = string.Format(
					"{0} /xml:{1} /noshadow:true /config:Release", // /include:Agg.UI" // /include:MatterControl.UI2;Leveling"
					projectFilePath,
					resultsFilePath);
				process.StartInfo.FileName = binPath;
				process.StartInfo.UseShellExecute = true;
				process.Start();

				process.WaitForExit();
			}
		}

		public Uri RunAndReport(string projectFilePath, string resultsFilePath)
		{
			RunTests(projectFilePath, resultsFilePath);

			Debugger.Launch();

			// Post the results
			if (!File.Exists(resultsFilePath))
			{
				return null;
			}
			else
			{
				var uri = new Uri(siteBaseUri, "testresults/create/");

				// Post the file to the server
				var client = new WebClient();
				var bytes = client.UploadFile(uri, resultsFilePath);

				string reportID = UTF8Encoding.UTF8.GetString(bytes);

				// Launch the results
				return new Uri(siteBaseUri, "testresults/details/" + reportID);
			}
		}
	}
}
