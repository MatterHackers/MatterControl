using NUnit.Framework;
using NUnit.Framework.Api;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using NUnit.Framework.Internal.Commands;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Pipes;
using System.Reflection;
using System.Xml;
using System.Xml.Serialization;

namespace TestInvoker // Note: actual namespace depends on the project name.
{
	[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
	public class ChildProcessTestAttribute : PropertyAttribute, IWrapSetUpTearDown
	{
		private readonly int _numParallelTests = 1;

		/// <param name="numParallelTests">The number of times to repeat this test in parallel. Set to more than 1 to debug potential timing issues or race conditions. If any of the tests fail, one failing result will be returned.</param>
		public ChildProcessTestAttribute(int numParallelTests = 1)
		{
			// Run the test on an STA thread.
			if (Program.InChildProcess)
				Properties.Add(PropertyNames.ApartmentState, ApartmentState.STA);
			_numParallelTests = numParallelTests;
		}

		TestCommand ICommandWrapper.Wrap(TestCommand command)
		{
			return new ChildProcessTestCommand(command, _numParallelTests);
		}

		internal class ChildProcessTestCommand : DelegatingTestCommand
		{
			private readonly int _numParallelTests;

			public sealed class TemporarySynchronizationContext : IDisposable
			{
				SynchronizationContext? originalContext = SynchronizationContext.Current;

				public TemporarySynchronizationContext(SynchronizationContext synchronizationContext)
				{
					SynchronizationContext.SetSynchronizationContext(synchronizationContext);
				}

				void IDisposable.Dispose()
				{
					if (Interlocked.Exchange(ref originalContext, null) is var context)
						SynchronizationContext.SetSynchronizationContext(context);
				}
			}

			public ChildProcessTestCommand(TestCommand innerCommand, int numParallelTests)
				: base(innerCommand)
			{
				_numParallelTests = numParallelTests;
			}
			public override TestResult Execute(TestExecutionContext context)
			{
				if (Program.InChildProcess)
				{
					// Back in .NET Framework, NUnit testing would create a new AppDomain and enter the test with a NULL sync context.
					// Application.Run would then replace it.
					// But now, there is no new AppDomain. NUnit sets up its default sync context and Application.Run keeps it, leading to deadlock.
					// So setup the correct sync context.
					using (var tempCtx = new TemporarySynchronizationContext(new WindowsFormsSynchronizationContext()))
						context.CurrentResult = innerCommand.Execute(context);
				}
				else
				{
					TestResult doTest()
					{
						string output;
						using (var pipeServer = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable))
						using (var pipeSense = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.Inheritable))
						{
							var method = innerCommand.Test.Method!.MethodInfo;
							var type = method.DeclaringType;
							var psi = new ProcessStartInfo();
							psi.FileName = "TestInvoker";
							psi.ArgumentList.Add(pipeServer.GetClientHandleAsString());
							psi.ArgumentList.Add(pipeSense.GetClientHandleAsString());
							psi.ArgumentList.Add(type!.Assembly.Location);
							psi.ArgumentList.Add(type.FullName!);
							psi.ArgumentList.Add(method.Name);
							psi.UseShellExecute = false;
							psi.CreateNoWindow = true;
							using var proc = Process.Start(psi);
							pipeServer.DisposeLocalCopyOfClientHandle();
							pipeSense.DisposeLocalCopyOfClientHandle();
							using (var pipeReader = new StreamReader(pipeServer))
								output = pipeReader.ReadToEnd();
							proc!.WaitForExit();
						}

						if (output.Length <= 0)
							throw new Exception("Test child process did not return a result.");

						try
						{
							XmlSerializer xmlserializer = new XmlSerializer(typeof(TestInvoker.FakeTestResult));
							var fakeResult = (TestInvoker.FakeTestResult)xmlserializer.Deserialize(XmlReader.Create(new StringReader(output)))!;
							return fakeResult.ToReal(context.CurrentTest);
						}
						catch (Exception ex)
						{
							throw new Exception("Failed to parse the test result's XML.", ex);
						}
					}

					if (_numParallelTests > 1)
					{
						ConcurrentBag <TestResult> results = new();
						Parallel.For(0, _numParallelTests, (i) =>
						{
							results.Add(doTest());
						});

						foreach (var result in results)
						{
							context.CurrentResult = result;
							if (result.FailCount > 0)
								break;
						}
					}
					else
					{
						context.CurrentResult = doTest();
					}
				}
				return context.CurrentResult;
			}
		}
	}
	
	public class Program
	{
		public static bool InChildProcess
		{
			get;
			private set;
		} = false;

		class SpecificTestFilter : TestFilter
		{
			public MethodInfo? TheMethod;

			public override TNode AddToXml(TNode parentNode, bool recursive)
			{
				throw new NotImplementedException();
			}

			public override bool Match(ITest test)
			{
				return test is TestMethod testMethod && testMethod.Method?.MethodInfo == TheMethod;
			}
		}

		static int Main(string[] args)
		{
			InChildProcess = true;

			// Terminate this process if the parent exits.
			Task.Run(() =>
			{
				try
				{
					using (var pipeSense = new AnonymousPipeClientStream(PipeDirection.In, args[1]))
					{
						while (pipeSense.ReadByte() >= 0)
							;
					}
				}
				finally
				{
					System.Environment.Exit(5);
				}
			});

			using var pipeClient = new AnonymousPipeClientStream(PipeDirection.Out, args[0]);

			string assemblyPath = args[2];
			string typeName = args[3];
			string methodName = args[4];

			Assembly asm = Assembly.LoadFrom(assemblyPath!);
			NUnit.Framework.Assert.NotNull(asm);

			MethodInfo? methodInfo = asm.GetType(typeName)?.GetMethod(methodName);
			NUnit.Framework.Assert.NotNull(asm);

			var runner = new NUnitTestAssemblyRunner(new DefaultTestAssemblyBuilder());
			runner.Load(asm, new Dictionary<string, object>());

			var result = runner.Run(TestListener.NULL, new SpecificTestFilter { TheMethod = methodInfo });

			while (result.HasChildren)
				result = result.Children.Single();

			// If nothing was tested, don't output the empty success result.
			if (result == null || result.Test?.Method?.MethodInfo != methodInfo)
				return 1;

			XmlSerializer xmlserializer = new(typeof(FakeTestResult));

			XmlWriterSettings settings = new();
			using (XmlWriter writer = XmlWriter.Create(pipeClient, settings))
				xmlserializer.Serialize(writer, FakeTestResult.FromReal(result));
			return 0;
		}
	}

	public struct FakeTestResultStatus
	{
		public TestStatus Status;
		public string Label;
		public FailureSite Site;
	}

	public struct FakeTestResultAssertionResult
	{
		public AssertionStatus Status;
		public string? Message;
		public string? StackTrace;
	}

	[Serializable]
	public struct FakeTestResult
	{
		public FakeTestResultStatus ResultState;
		public string Name;
		public string FullName;
		public double Duration;
		public DateTime StartTime;
		public DateTime EndTime;
		public string? Message;
		public string? StackTrace;
		public int TotalCount;
		public int AssertCount;
		public int FailCount;
		public int WarningCount;
		public int PassCount;
		public int SkipCount;
		public int InconclusiveCount;
		public string Output;
		public List<FakeTestResultAssertionResult> AssertionResults;

		public static FakeTestResult FromReal(ITestResult result)
		{
			FakeTestResult fakeTestResult = default;
			fakeTestResult.ResultState.Status = result.ResultState.Status;
			fakeTestResult.ResultState.Label = result.ResultState.Label;
			fakeTestResult.ResultState.Site = result.ResultState.Site;
			fakeTestResult.Name = result.Name;
			fakeTestResult.FullName = result.FullName;
			fakeTestResult.Duration = result.Duration;
			fakeTestResult.StartTime = result.StartTime;
			fakeTestResult.EndTime = result.EndTime;
			fakeTestResult.Message = result.Message;
			fakeTestResult.StackTrace = result.StackTrace;
			fakeTestResult.TotalCount = result.TotalCount;
			fakeTestResult.AssertCount = result.AssertCount;
			fakeTestResult.FailCount = result.FailCount;
			fakeTestResult.WarningCount = result.WarningCount;
			fakeTestResult.PassCount = result.PassCount;
			fakeTestResult.SkipCount = result.SkipCount;
			fakeTestResult.InconclusiveCount = result.InconclusiveCount;
			fakeTestResult.Output = result.Output;
			fakeTestResult.AssertionResults = result.AssertionResults.Select(x => new FakeTestResultAssertionResult
			{
				Status = x.Status,
				Message = x.Message,
				StackTrace = x.StackTrace,
			}).ToList();
			return fakeTestResult;
		}

		public TestResult ToReal(Test test)
		{

			//var result = new TestCaseResult(testMethod);
			var result = test.MakeTestResult();
			result.SetResult(new ResultState(ResultState.Status, ResultState.Label, ResultState.Site),
				Message, StackTrace);
			//result.Name = result.Name;
			//result.FullName = result.FullName;
			result.Duration = Duration;
			result.StartTime = StartTime;
			result.EndTime = EndTime;
			//result.Message = result.Message;
			//result.StackTrace = result.StackTrace;
			//result.TotalCount = result.TotalCount;
			//TODO: result.AssertCount = result.AssertCount;
			//result.FailCount = result.FailCount;
			//result.WarningCount = result.WarningCount;
			//result.PassCount = result.PassCount;
			//result.SkipCount = result.SkipCount;
			//result.InconclusiveCount = result.InconclusiveCount;
			result.OutWriter.Write(Output);
			result.OutWriter.Flush();
			foreach (var assertion in AssertionResults)
			{
				result.RecordAssertion(new AssertionResult(assertion.Status, assertion.Message, assertion.StackTrace));
			}

			return result;
		}
	}
}