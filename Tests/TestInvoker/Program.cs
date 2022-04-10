using NUnit.Framework;
using NUnit.Framework.Api;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using NUnit.Framework.Internal.Commands;
using System;
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
		public ChildProcessTestAttribute()
		{
			if (ChildProcessTestCommand.InChildProcess)
				Properties.Add(PropertyNames.ApartmentState, ApartmentState.STA);
		}

		TestCommand ICommandWrapper.Wrap(TestCommand command)
		{
			return new ChildProcessTestCommand(command);
		}

		internal class ChildProcessTestCommand : DelegatingTestCommand
		{
			static public bool InChildProcess = false;

			public ChildProcessTestCommand(TestCommand innerCommand)
				: base(innerCommand)
			{
			}
			public override TestResult Execute(TestExecutionContext context)
			{
				if (InChildProcess)
				{
					context.CurrentResult = innerCommand.Execute(context);
				}
				else
				{
					string output;
					using (var pipeServer = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable))
					{
						var method = innerCommand.Test.Method!.MethodInfo;
						var type = method.DeclaringType;
						var psi = new ProcessStartInfo();
						psi.FileName = "TestInvoker";
						psi.ArgumentList.Add(pipeServer.GetClientHandleAsString());
						psi.ArgumentList.Add(type!.Assembly.Location);
						psi.ArgumentList.Add(type.FullName!);
						psi.ArgumentList.Add(method.Name);
						psi.UseShellExecute = false;
						psi.CreateNoWindow = true;
						var proc = Process.Start(psi);
						pipeServer.DisposeLocalCopyOfClientHandle();
						using (var pipeReader = new StreamReader(pipeServer))
							output = pipeReader.ReadToEnd();
						proc!.WaitForExit();
					}

					XmlSerializer xmlserializer = new XmlSerializer(typeof(TestInvoker.FakeTestResult));
					var fakeResult = (TestInvoker.FakeTestResult)xmlserializer.Deserialize(XmlReader.Create(new StringReader(output)))!;
					context.CurrentResult = fakeResult.ToReal(context.CurrentTest);
				}
				return context.CurrentResult;
			}
		}
	}
	/*
	[TestFixture, Apartment(ApartmentState.STA)]
	internal class TestProxy
	{
		static public string? assemblyPath;
		static public string? typeName;
		static public string? methodName;

		[Test]
		public void InvokeTest()
		{
			Assembly asm = Assembly.LoadFrom(assemblyPath!);
			NUnit.Framework.Assert.NotNull(asm);

			Type? type = asm.GetType(typeName!)!;
			NUnit.Framework.Assert.NotNull(type);

			MethodInfo? methodInfo = type.GetTypeInfo().GetDeclaredMethod(methodName!)!;
			NUnit.Framework.Assert.NotNull(methodInfo);

			object? instance = null;
			if (!methodInfo.IsStatic)
			{
				instance = Activator.CreateInstance(type);
				NUnit.Framework.Assert.NotNull(instance);
			}

			object result = methodInfo.Invoke(instance, null)!;

			if (result is Task task)
				task.GetAwaiter().GetResult();
		}
	}*/

	internal class Program
	{
		class SpecificTestFilter : TestFilter
		{
			public string? TypeName;
			public string? MethodName;

			public override TNode AddToXml(TNode parentNode, bool recursive)
			{
				throw new NotImplementedException();
			}

			public override bool Match(ITest test)
			{
				/*if (test is TestFixture fixture)
					if (fixture.TypeInfo.Type.FullName != TypeName)
						return false;

				if (test.HasChildren)
					return test.Tests.Any(Match);*/

				var method = test.Method?.MethodInfo;
				var type = method?.DeclaringType;
				return type != null && method != null && TypeName == type.FullName && MethodName == method.Name;
			}
		}

		//[STAThread]
		static int Main(string[] args)
		{
			using (var pipeClient = new AnonymousPipeClientStream(PipeDirection.Out, args[0]))
			{
				ChildProcessTestAttribute.ChildProcessTestCommand.InChildProcess = true;

				string assemblyPath = args[1];
				string typeName = args[2];
				string methodName = args[3];

				Assembly asm = Assembly.LoadFrom(assemblyPath!);
				NUnit.Framework.Assert.NotNull(asm);

				var runner = new NUnitTestAssemblyRunner(new DefaultTestAssemblyBuilder());
				runner.Load(asm, new Dictionary<string, object>());

				var result = runner.Run(TestListener.NULL, new SpecificTestFilter { TypeName = typeName, MethodName = methodName });

				while (result.HasChildren)
					result = result.Children.Single();

				XmlSerializer xmlserializer = new XmlSerializer(typeof(FakeTestResult));

				XmlWriterSettings settings = new XmlWriterSettings { OmitXmlDeclaration = true, Indent = true };
				using (XmlWriter writer = XmlWriter.Create(pipeClient, settings))
					xmlserializer.Serialize(writer, FakeTestResult.FromReal(result));
				return 0;
			}
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
			result.Duration = result.Duration;
			result.StartTime = result.StartTime;
			result.EndTime = result.EndTime;
			//result.Message = result.Message;
			//result.StackTrace = result.StackTrace;
			//result.TotalCount = result.TotalCount;
			//TODO: result.AssertCount = result.AssertCount;
			//result.FailCount = result.FailCount;
			//result.WarningCount = result.WarningCount;
			//result.PassCount = result.PassCount;
			//result.SkipCount = result.SkipCount;
			//result.InconclusiveCount = result.InconclusiveCount;
			result.OutWriter.Write(result.Output);
			result.OutWriter.Flush();
			foreach(var assertion in AssertionResults)
			{
				result.RecordAssertion(new AssertionResult(assertion.Status, assertion.Message, assertion.StackTrace));
			}

			return result;
		}
	}
}