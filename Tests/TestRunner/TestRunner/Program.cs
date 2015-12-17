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

		static void Main(string[] args)
		{
			Uri siteBaseUri = new Uri("http://matterdata.azurewebsites.net/");

			var consoleRunner = new NunitConsoleRunner();
			var resultsUrl = consoleRunner.RunAndReport(
				Path.GetFullPath("../../../MatterControl.nunit"),
				Path.GetFullPath("results.xml"));

			if(resultsUrl != null)
			{
				// Launch the results
				Process.Start(resultsUrl.AbsoluteUri);
			}
		}
	}
}
