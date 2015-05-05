using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Net;

namespace TestRunner
{
	class Program
	{
		private const string outputFileName = "result.xml";

		// Currently just running from the bin folder
		private const string nunitPath = "NUnit-2.6.4";
		//private const string nunitPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "NUnit 2.6.3");

		static void Main(string[] args)
		{
			Uri siteBaseUri = new Uri(args[0]);

			//SimulateZipPost();
			//return;

			if (File.Exists(outputFileName))
			{
				File.Delete(outputFileName);
			}

			string binPath = null;

			if (Directory.Exists(nunitPath))
			{
				binPath = Path.Combine(nunitPath, "bin\\nunit-console-x86.exe");
			}

			// Run the unit tests
			if (!string.IsNullOrWhiteSpace(binPath) && File.Exists(binPath))
			{
				Process process = new Process();
				process.StartInfo.Arguments = string.Format(
					"{0} /xml:{1} /noshadow:true",
					Path.GetFullPath("../../../MatterControl.nunit"),
					Path.GetFullPath(outputFileName));
				process.StartInfo.FileName = binPath;
				process.StartInfo.UseShellExecute = true;
				process.Start();

				process.WaitForExit();
			}

			// Post the results
			if (File.Exists(outputFileName))
			{
				var uri = new Uri(siteBaseUri, "testresults/create/");

				// Post the file to the server
				var client = new WebClient();
				var bytes = client.UploadFile(uri, outputFileName);

				string reportID = UTF8Encoding.UTF8.GetString(bytes);

				// Launch the results
				Process.Start(new Uri(siteBaseUri, "testresults/details/" + reportID).AbsoluteUri);
			}
		}

		private static void SimulateZipPost(Uri baseUri)
		{
			var uri = new Uri(baseUri, "testresults/createfromzip/");

			// Post the file to the server
			var client = new WebClient();
			var bytes = client.UploadFile(uri, @"E:\sources\websites\matterdata\TestResult4.zip");

			string reportID = System.Text.UTF8Encoding.UTF8.GetString(bytes);
		}
	}
}
