using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.GuiAutomation;
using MatterHackers.MatterControl;
using MatterHackers.MatterControl.SlicerConfiguration;
using MatterHackers.MatterControl.Tests.Automation;
using NUnit.Framework;
using static MatterControl.Tests.MatterControl.SliceSettingsFieldTests;

namespace MatterControl.Tests.MatterControl
{
	public static class RunnerX
	{
		public static Task RunTest(this SystemWindow systemWindow, AutomationTest automationTest, int timeout)
		{
			return AutomationRunner.ShowWindowAndExecuteTests(systemWindow, automationTest, timeout);
		}

		public static void Add(this List<ValueMap> valueMap, string input, string expected)
		{
			valueMap.Add(new ValueMap(input, expected));
		}
	}

	[TestFixture, Category("SliceSettingsTests")]
	public class SliceSettingsFieldTests
	{
		[Test]
		public void TestExistsForEachUIFieldType()
		{
			AggContext.StaticData = new FileSystemStaticData(TestContext.CurrentContext.ResolveProjectPath(4, "StaticData"));
			MatterControlUtilities.OverrideAppDataLocation(TestContext.CurrentContext.ResolveProjectPath(4));

			var testClass = this.GetType();
			var thisClassMethods = testClass.GetMethods(BindingFlags.Public | BindingFlags.Instance);

			foreach (var uiFieldType in PluginFinder.FindTypes<UIField>())
			{
				// Skip abstract class
				if (uiFieldType.Name == "UIField")
				{
					continue;
				}

				string expectedTestName = $"{uiFieldType.Name}Test";
				Assert.AreEqual(
					1,
					thisClassMethods.Where(m => m.Name == expectedTestName).Count(),
					"Test for UIField missing - not yet created or typo'd - Expected: " + expectedTestName);
			}
		}

		[Test, Apartment(System.Threading.ApartmentState.STA)]
		public async Task DoubleFieldTest()
		{
			await ValidateAgainstValueMap<DoubleField>(
				(field) => (field.Content as MHNumberEdit).ActuallNumberEdit.Text,
				new List<ValueMap>()
				{
					{"0.12345", "0.12345"},
					{"1.2345", "1.2345"},
					{"12.345", "12.345"},
					{"+0.12345", "0.12345"},
					{"+1.2345", "1.2345"},
					{"+12.345", "12.345"},
					{"-0.12345", "-0.12345"},
					{"-1.2345", "-1.2345"},
					// Invalid values revert to expected
					{"abc", "0"},
					{"+abc", "0"},
					{"-abc", "0"},
				});
		}

		[Test, Apartment(System.Threading.ApartmentState.STA)]
		public async Task PositiveDoubleFieldTest()
		{
			await ValidateAgainstValueMap<PositiveDoubleField>(
				(field) => (field.Content as MHNumberEdit).ActuallNumberEdit.Text,
				new List<ValueMap>()
				{
					{"0.12345", "0.12345"},
					{"1.2345", "1.2345"},
					{"12.345", "12.345"},
					{"+0.12345", "0.12345"},
					{"+1.2345", "1.2345"},
					{"+12.345", "12.345"},
					{"-0.12345", "0"}, // TODO: Classic behavior but... shouldn't we just drop the negative sign rather than force to 0?
					{"-1.2345", "0"},
					// Invalid values revert to expected
					{"abc", "0"},
					{"+abc", "0"},
					{"-abc", "0"},
				});
		}


		[Test, Ignore("Not Implemented")]
		public void IntFieldTest()
		{
			Assert.Fail();
		}

		[Test, Ignore("Not Implemented")]
		public void DoubleOrPercentFieldTest()
		{
			Assert.Fail();
		}

		[Test, Ignore("Not Implemented")]
		public void ValueOrUnitsFieldTest()
		{
			Assert.Fail();
		}

		[Test, Ignore("Not Implemented")]
		public void CheckboxFieldTest()
		{
			Assert.Fail();
		}

		[Test, Ignore("Not Implemented")]
		public void ToggleboxFieldTest()
		{
			Assert.Fail();
		}

		[Test, Ignore("Not Implemented")]
		public void MultilineStringFieldTest()
		{
			Assert.Fail();
		}

		[Test, Ignore("Not Implemented")]
		public void ComPortFieldTest()
		{
			Assert.Fail();
		}

		[Test, Ignore("Not Implemented")]
		public void Test()
		{
			Assert.Fail();
		}

		[Test, Ignore("Not Implemented")]
		public void ListFieldTest()
		{
			Assert.Fail();
		}

		[Test, Ignore("Not Implemented")]
		public void ExtruderOffsetFieldTest()
		{
			Assert.Fail();
		}

		[Test, Ignore("Not Implemented")]
		public void NumberFieldTest()
		{
			Assert.Fail();
		}

		[Test, Ignore("Not Implemented")]
		public void TextFieldTest()
		{
			Assert.Fail();
		}

		[Test, Ignore("Not Implemented")]
		public void Vector2FieldTest()
		{
			Assert.Fail();
		}

		[Test, Ignore("Not Implemented")]
		public void BoundDoubleFieldTest()
		{
			//var field = new BoundDoubleField();
			Assert.Fail();
		}

		public class ValueMap
		{
			public ValueMap(string input, string expected)
			{
				this.InputValue = input;
				this.ExpectedValue = expected;
			}

			public string InputValue { get; }
			public string ExpectedValue { get; }
		}

		/// <summary>
		/// Take a Type, a delegate to resolve the UI widget value and a map of input->expected values and validates the results for a given field
		/// </summary>
		/// <typeparam name="T">The UIField to validate</typeparam>
		/// <param name="collectValueFromWidget">A delegate to resolve the currently displayed widget value</param>
		/// <param name="valuesMap">A map of input to expected values</param>
		/// <returns></returns>
		public static Task ValidateAgainstValueMap<T>(Func<UIField, string> collectValueFromWidget, IEnumerable<ValueMap> valuesMap) where T : UIField, new()
		{
			var field = new T();

			var testsWindow = new UIFieldTestWindow(400, 200, field);

			return testsWindow.RunTest((testRunner) =>
			{
				var primaryFieldWidget = field.Content as MHNumberEdit;

				foreach (var item in valuesMap)
				{
					testsWindow.SetAndValidateValues(item.ExpectedValue, item.ExpectedValue, collectValueFromWidget);
				}

				return Task.CompletedTask;
			}, 30);

		}

	}


}
