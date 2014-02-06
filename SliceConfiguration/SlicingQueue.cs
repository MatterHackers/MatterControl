using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Runtime.InteropServices;
using System.Diagnostics;

using MatterHackers.Agg;
using MatterHackers.MatterControl.DataStorage;
using MatterHackers.MatterControl.PrintQueue;
using MatterHackers.Agg.UI;
using MatterHackers.Localizations;

namespace MatterHackers.MatterControl
{
    public class SlicingQueue
    {
        static Thread slicePartThread = null;
        static List<PrintItemWrapper> listOfSlicingItems = new List<PrintItemWrapper>();
        static bool haltSlicingThread = false;

        SlicingQueue()
        {
            if (slicePartThread == null)
            {
                slicePartThread = new Thread(CreateSlicedPartsThread);
                slicePartThread.Name = "slicePartThread";
                slicePartThread.IsBackground = true;
                slicePartThread.Start();
            }
        }

        static SlicingQueue instance;
        static public SlicingQueue Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new SlicingQueue();
                }
                return instance;
            }
        }

        public void QueuePartForSlicing(PrintItemWrapper itemToQueue)
        {
            itemToQueue.DoneSlicing = false;
			string preparingToSliceModelTxt = new LocalizedString("Preparing to slice model").Translated;
			string peparingToSliceModelFull = string.Format ("{0}...", preparingToSliceModelTxt);
			itemToQueue.OnSlicingOutputMessage(new StringEventArgs(peparingToSliceModelFull));
            using (TimedLock.Lock(listOfSlicingItems, "QueuePartForSlicing"))
            {
                //Add to thumbnail generation queue
                listOfSlicingItems.Add(itemToQueue);
            }
        }

        public void ShutDownSlicingThread()
        {
            haltSlicingThread = true;
        }

		static string macQuotes(string textLine)
		{
			if (textLine.StartsWith ("\"") && textLine.EndsWith ("\"")) {
				return textLine;
			} else {
				return "\"" + textLine.Replace ("\"", "\\\"") + "\"";
			}
		}

        static string getSlicerFullPath()
        {
            switch (MatterHackers.Agg.UI.WindowsFormsAbstract.GetOSType())
            {
                case Agg.UI.WindowsFormsAbstract.OSType.Windows:
                    switch (PrinterCommunication.Instance.ActiveSliceEngine)
                    {
                        case PrinterCommunication.SlicingEngine.Slic3r:
                            {
                                string slic3rRelativePath = Path.Combine("..", "Slic3r", "slic3r.exe");
                                if (!File.Exists(slic3rRelativePath))
                                {
                                    slic3rRelativePath = System.IO.Path.Combine(".", "Slic3r", "slic3r.exe");
                                }
                                return System.IO.Path.GetFullPath(slic3rRelativePath);
                            }

                        case PrinterCommunication.SlicingEngine.CuraEngine:
                            {
                                string curaEngineRelativePath = Path.Combine("..", "CuraEngine.exe");
                                if (!File.Exists(curaEngineRelativePath))
                                {
                                    curaEngineRelativePath = System.IO.Path.Combine(".", "CuraEngine.exe");
                                }
                                return System.IO.Path.GetFullPath(curaEngineRelativePath);
                            }

                        case PrinterCommunication.SlicingEngine.MatterSlice:
                            {
                                string materSliceRelativePath = Path.Combine(".", "MatterSlice.exe");
                                return System.IO.Path.GetFullPath(materSliceRelativePath);
                            }

                        default:
                            throw new NotImplementedException();
                    }

			case Agg.UI.WindowsFormsAbstract.OSType.Mac:
				switch (PrinterCommunication.Instance.ActiveSliceEngine) {
				case PrinterCommunication.SlicingEngine.Slic3r:
					{
						//string parentLocation = Directory.GetParent (ApplicationDataStorage.Instance.ApplicationPath).ToString ();
						string applicationPath = System.IO.Path.Combine (ApplicationDataStorage.Instance.ApplicationPath, "Slic3r.app", "Contents", "MacOS", "slic3r");
						return applicationPath;
					}
				case PrinterCommunication.SlicingEngine.CuraEngine:
					{
						string applicationPath = System.IO.Path.Combine (ApplicationDataStorage.Instance.ApplicationPath, "CuraEngine");
						return applicationPath;
					}

				default:
					throw new NotImplementedException ();
				}

                default:
                    throw new NotImplementedException();
            }
        }

        static Process slicerProcess = null;
        static void CreateSlicedPartsThread()
        {
            while (!haltSlicingThread)
            {
                if (PrinterCommunication.Instance.ActivePrintItem != null && listOfSlicingItems.Count > 0)
                {
                    PrintItemWrapper itemToSlice = listOfSlicingItems[0];
                    itemToSlice.CurrentlySlicing = true;

                    string currentConfigurationFileAndPath = Path.Combine(ApplicationDataStorage.Instance.GCodeOutputPath, "config_" + ActiveSliceSettings.Instance.GetHashCode().ToString() + ".ini");
                    ActiveSliceSettings.Instance.GenerateConfigFile(currentConfigurationFileAndPath);

                    string gcodePathAndFileName = itemToSlice.GCodePathAndFileName;
                    bool gcodeFileIsComplete = itemToSlice.IsGCodeFileComplete(gcodePathAndFileName);

                    if (!File.Exists(gcodePathAndFileName) || !gcodeFileIsComplete)
                    {
                        slicerProcess = new Process();

                        switch (PrinterCommunication.Instance.ActiveSliceEngine)
                        {
                            case PrinterCommunication.SlicingEngine.Slic3r:
                                slicerProcess.StartInfo.Arguments = "--load \"" + currentConfigurationFileAndPath + "\" --output \"" + gcodePathAndFileName + "\" \"" + itemToSlice.PartToSlicePathAndFileName + "\"";
                                break;

                            case PrinterCommunication.SlicingEngine.CuraEngine:
                                slicerProcess.StartInfo.Arguments = "-v -o \"" + gcodePathAndFileName + "\" " + CuraEngineMappings.GetCuraCommandLineSettings() + " \"" + itemToSlice.PartToSlicePathAndFileName + "\"";
                                //Debug.Write(slicerProcess.StartInfo.Arguments);
                                break;

                            case PrinterCommunication.SlicingEngine.MatterSlice:
                                slicerProcess.StartInfo.Arguments = "--load \"" + currentConfigurationFileAndPath + "\" --output \"" + gcodePathAndFileName + "\" \"" + itemToSlice.PartToSlicePathAndFileName + "\"";
                                break;
                        }

					    string slicerFullPath = getSlicerFullPath();

                        slicerProcess.StartInfo.CreateNoWindow = true;
                        slicerProcess.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                        slicerProcess.StartInfo.RedirectStandardError = true;
                        slicerProcess.StartInfo.RedirectStandardOutput = true;

						slicerProcess.StartInfo.FileName = slicerFullPath;
                        slicerProcess.StartInfo.UseShellExecute = false;

                        slicerProcess.OutputDataReceived += (sender, args) =>
                        {
                            if (args.Data != null)
                            {
                                string message = args.Data;
                                message = message.Replace("=>", "").Trim();
                                if (message.Contains(".gcode"))
                                {
                                    message = "Saving intermediate file";
                                }
                                message += "...";
                                itemToSlice.OnSlicingOutputMessage(new StringEventArgs(message));
                            }
                        };

                        slicerProcess.Start();

                        slicerProcess.BeginOutputReadLine();
                        string stdError = slicerProcess.StandardError.ReadToEnd();

                        slicerProcess.WaitForExit();
                        using (TimedLock.Lock(slicerProcess, "SlicingProcess"))
                        {
                            slicerProcess = null;
                        }
                    }

                    itemToSlice.CurrentlySlicing = false;
                    itemToSlice.DoneSlicing = true;

                    using (TimedLock.Lock(listOfSlicingItems, "CreateSlicedPartsThread()"))
                    {
                        listOfSlicingItems.RemoveAt(0);
                    }
                }

                Thread.Sleep(100);
            }
        }

        internal void CancelCurrentSlicing()
        {
            if (slicerProcess != null)
            {
                using (TimedLock.Lock(slicerProcess, "SlicingProcess"))
                {
                    if (slicerProcess != null && !slicerProcess.HasExited)
                    {
                        slicerProcess.Kill();
                    }
                }
            }
        }
    }
}
