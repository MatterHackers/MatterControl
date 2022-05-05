// For communication with the main instance. Use ServiceWire or just pipes.
#define USE_SERVICEWIRE

using MatterHackers.MatterControl;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Security.AccessControl;
using System.Security.Principal;

#if USE_SERVICEWIRE
using ServiceWire;
#else
using System.IO.Pipes;
using System.Xml.Serialization;
#endif

namespace MatterHackers.MatterControl
{
	public interface IMainService
	{
		void ShellOpenFile(string[] files);
	}

	[Serializable]
	public class LocalService : IMainService
	{
		private const string ServicePipeName = "MatterControlMainInstance";

#if USE_SERVICEWIRE
		private const string MainInstanceMutexName = "MatterControlMainInstanceMutex";
#pragma warning disable IDE0052 // Remove unread private members
		// Don't let the GC clean this up.
		private static Mutex MainInstanceMutex = null;
#pragma warning restore IDE0052 // Remove unread private members
#else
		static string readPipeMessage(PipeStream pipe)
		{
			MemoryStream ms = new MemoryStream();
			using var cancellation = new CancellationTokenSource();
			byte[] buffer = new byte[1024];
			do
			{
				var task = pipe.ReadAsync(buffer, 0, buffer.Length, cancellation.Token);
				cancellation.CancelAfter(1000);
				task.Wait();
				ms.Write(buffer, 0, task.Result);
				if (task.Result <= 0)
					break;
			} while (!pipe.IsMessageComplete);

			return Encoding.Unicode.GetString(ms.ToArray());
		}
#endif

		private static readonly object locker = new();

		public static bool TryStartServer()
		{
#if USE_SERVICEWIRE
			// ServiceWire will allow lots of pipes to exist under the same name, so a mutex is needed.
			// Locking isn't needed. Windows should clean up when the main instance closes.
			Mutex mutex = new(false, MainInstanceMutexName, out bool createdNew);
			try
			{
				if (createdNew)
				{
					try
					{
						var host = new ServiceWire.NamedPipes.NpHost(ServicePipeName, new ServiceWireLogger());
						host.AddService<IMainService>(new LocalService());
						host.Open();

						// Keep the mutex alive.
						MainInstanceMutex = mutex;
						mutex = null;

						return true;
					}
					catch (Exception)
					{
					}
				}
			}
			finally
			{
				// Not the main instance. Release the handle.
				mutex?.Dispose();
			}

			return false;
#else
			NamedPipeServerStream pipeServer = null;
			try
			{
				pipeServer = new NamedPipeServerStream(ServicePipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Message, PipeOptions.CurrentUserOnly);
			}
			catch (IOException)
			{
				return false;
			}

			new Task(() =>
			{
				try
				{
					var localService = new LocalService();

					for (; ; )
					{
						pipeServer.WaitForConnection();

						try
						{
							string str = readPipeMessage(pipeServer);

							var serializer = new XmlSerializer(typeof(InstancePipeMessage));
							var message = (InstancePipeMessage)serializer.Deserialize(new StringReader(str));
							localService.ShellOpenFile(message.Paths);

							using var cancellation = new CancellationTokenSource();
							var task = pipeServer.WriteAsync(Encoding.Unicode.GetBytes("ok"), cancellation.Token).AsTask();
							cancellation.CancelAfter(1000);
							task.Wait();
						}
						catch (Exception)
						{
						}

						// NamedPipeServerStream can only handle one client ever. Need a new server pipe. ServiceWire does the same thing.
						// So here, there is a time where there is no server pipe. Another instance could become the main instance.
						// NamedPipeClientStream.Connect should retry the connection.
						pipeServer.Dispose();
						pipeServer = new NamedPipeServerStream(ServicePipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Message, PipeOptions.CurrentUserOnly);
					}
				}
				catch (Exception ex) // TimeoutException or IOException
				{
					//System.Windows.Forms.MessageBox.Show(ex.ToString());
					System.Diagnostics.Trace.WriteLine("Main instance pipe server died: " + ex.ToString());
				}

				pipeServer.Dispose();
				pipeServer = null;
			}).Start();

			return true;
#endif
		}

		public static bool TrySendToServer(string[] shellFiles)
		{
#if USE_SERVICEWIRE
			try
			{
				using (var client = new ServiceWire.NamedPipes.NpClient<IMainService>(new ServiceWire.NamedPipes.NpEndPoint(ServicePipeName)))
				{
					if (client.IsConnected)
					{
						// notify the running instance of the event
						client.Proxy.ShellOpenFile(shellFiles);

						System.Threading.Thread.Sleep(1000);

						// Finally, close the process spawned by Explorer.exe
						return true;
					}
				}
			}
			catch (Exception)
			{
			}
#else
			try
			{
				using var pipeClient = new NamedPipeClientStream(".", ServicePipeName, PipeDirection.InOut, PipeOptions.CurrentUserOnly);
				pipeClient.Connect(1000);
				pipeClient.ReadMode = PipeTransmissionMode.Message;

				StringBuilder sb = new();
				using (var writer = new StringWriter(sb))
					new XmlSerializer(typeof(InstancePipeMessage)).Serialize(writer, new InstancePipeMessage { Paths = shellFiles.ToArray() });

				using var cancellation = new CancellationTokenSource();
				var task = pipeClient.WriteAsync(Encoding.Unicode.GetBytes(sb.ToString()), cancellation.Token).AsTask();
				cancellation.CancelAfter(1000);
				task.Wait();
				if (task.IsCompletedSuccessfully && readPipeMessage(pipeClient).Trim() == "ok")
					return true;
			}
			catch (Exception ex) // TimeoutException or IOException
			{
				//System.Windows.Forms.MessageBox.Show(ex.ToString());
				System.Diagnostics.Trace.WriteLine("Instance pipe client died: " + ex.ToString());
			}
#endif

			return false;
		}

		public void ShellOpenFile(string[] files)
		{
			// If at least one argument is an acceptable shell file extension
			var itemsToAdd = files.Where(f => File.Exists(f)
				&& ApplicationController.ShellFileExtensions.Contains(Path.GetExtension(f).ToLower()));

			if (itemsToAdd.Any())
			{
				lock (locker)
				{
					// Add each file
					foreach (string file in itemsToAdd)
					{
						ApplicationController.Instance.ShellOpenFile(file);
					}
				}
			}
		}



#if USE_SERVICEWIRE
		private class ServiceWireLogger : ServiceWire.ILog
		{
			static private void Log(ServiceWire.LogLevel level, string formattedMessage, params object[] args)
			{
				// Handled as in https://github.com/tylerjensen/ServiceWire/blob/master/src/ServiceWire/Logger.cs

				if (null == formattedMessage)
					return;

				if (level <= LogLevel.Warn)
				{
					string msg = (null != args && args.Length > 0)
						? string.Format(formattedMessage, args)
						: formattedMessage;

					System.Diagnostics.Trace.WriteLine(msg);
				}
			}

			void ILog.Debug(string formattedMessage, params object[] args)
			{
				Log(LogLevel.Debug, formattedMessage, args);
			}

			void ILog.Error(string formattedMessage, params object[] args)
			{
				Log(LogLevel.Error, formattedMessage, args);
			}

			void ILog.Fatal(string formattedMessage, params object[] args)
			{
				Log(LogLevel.Fatal, formattedMessage, args);
			}

			void ILog.Info(string formattedMessage, params object[] args)
			{
				Log(LogLevel.Info, formattedMessage, args);
			}

			void ILog.Warn(string formattedMessage, params object[] args)
			{
				Log(LogLevel.Warn, formattedMessage, args);
			}
		}
#else
		[Serializable]
		public struct InstancePipeMessage
		{
			public string[] Paths;
		}
#endif
	}
}
