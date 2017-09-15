using System;
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
using MatterHackers.VectorMath;
using NUnit.Framework;

namespace MatterControl.Tests.MatterControl
{

	public static class RunnerX
	{
		public static Task RunTest(this SystemWindow systemWindow, AutomationTest automationTest, int timeout)
		{
			return AutomationRunner.ShowWindowAndExecuteTests(systemWindow, automationTest, timeout);
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

		public class UIFieldTestWindow : SystemWindow
		{
			public MHTextEditWidget ExpectedText { get; }
			public MHTextEditWidget InputText { get; }

			private UIField field;

			public UIFieldTestWindow(int width, int height, UIField field)
				: base (width, height)
			{
				this.BackgroundColor = new RGBA_Bytes(56, 56, 56);
				
				GuiWidget column, row;
				double pixelWidth = 70;

				// Store
				this.field = field;

				// Initialize the field and store the generated content reference
				field.Initialize(0);

				GuiWidget widgetUnderTest = field.Content;
				
				row = new FlowLayoutWidget
				{
					VAnchor = VAnchor.Center | VAnchor.Fit,
					HAnchor = HAnchor.Center | HAnchor.Fit
				};
				this.AddChild(row);

				column = new FlowLayoutWidget(FlowDirection.TopToBottom)
				{
					Margin = new BorderDouble(0, 10),
				};
				row.AddChild(column);

				column.AddChild(new TextWidget("Input:", textColor: RGBA_Bytes.White)
				{
					Margin = new BorderDouble(right: 10, bottom: 2),
				});

				this.InputText = new MHTextEditWidget("", pixelWidth: pixelWidth)
				{
					Margin = new BorderDouble(right: 8)
				};
				column.AddChild(InputText);

				column = new FlowLayoutWidget(FlowDirection.TopToBottom)
				{
					Margin = new BorderDouble(0, 10),
				};
				row.AddChild(column);

				column.AddChild(new TextWidget("Expected:", textColor: RGBA_Bytes.White)
				{
					Margin = new BorderDouble(right: 10, bottom: 2)
				});

				this.ExpectedText = new MHTextEditWidget("", pixelWidth: pixelWidth)
				{
					Margin = new BorderDouble(right: 8)
				};
				column.AddChild(ExpectedText);

				column = new FlowLayoutWidget(FlowDirection.TopToBottom)
				{
					Margin = new BorderDouble(0, 10),
				};
				row.AddChild(column);

				column.AddChild(new TextWidget("Actual:", textColor: RGBA_Bytes.White)
				{
					Margin = new BorderDouble(right: 10, bottom: 2)
				});
				column.AddChild(widgetUnderTest);
			}

			public void SetAndValidateValues(string expectedValue, string inputValue, Func<string> collectValueFromWidget, int delay = 500)
			{
				// Set expected and source
				this.ExpectedText.Text = expectedValue;
				this.InputText.Text = inputValue;

				// Update field
				field.SetValue(inputValue, false);

				// Assert expected field value
				Assert.AreEqual(expectedValue, field.Value);

				// Assert expected widget value
				Assert.AreEqual(expectedValue, collectValueFromWidget());

				// Sleep
				System.Threading.Thread.Sleep(delay);
			}
		}

		[Test, Apartment(System.Threading.ApartmentState.STA)]
		public async Task DoubleFieldTest()
		{
			var valuesMap = new(string inputValue, string expected)[]
			{
				("0.12345", "0.12345"),
				("1.2345", "1.2345"),
				("12.345", "12.345"),
				("+0.12345", "0.12345"),
				("+1.2345", "1.2345"),
				("+12.345", "12.345"),
				("-0.12345", "-0.12345"),
				("-1.2345", "-1.2345"),
				// Invalid values revert to expected
				("abc", "0"),
				("+abc", "0"),
				("-abc", "0"),
			};

			var field = new DoubleField();

			var testsWindow = new UIFieldTestWindow(400, 200, field);

			await testsWindow.RunTest((testRunner) =>
			{
				var primaryFieldWidget = field.Content as MHNumberEdit;

				foreach (var item in valuesMap)
				{
					testsWindow.SetAndValidateValues(
						item.expected, 
						item.inputValue, 
						() => primaryFieldWidget.ActuallNumberEdit.Text);
				}

				return Task.CompletedTask;
			}, 30);
		}

		[Test]
		public void IntFieldTest()
		{
			Assert.Fail();
		}

		[Test]
		public void DoubleOrPercentFieldTest()
		{
			Assert.Fail();
		}

		[Test]
		public void ValueOrUnitsFieldTest()
		{
			Assert.Fail();
		}

		[Test]
		public void CheckboxFieldTest()
		{
			Assert.Fail();
		}

		[Test]
		public void ToggleboxFieldTest()
		{
			Assert.Fail();
		}

		[Test]
		public void MultilineStringFieldTest()
		{
			Assert.Fail();
		}

		[Test]
		public void ComPortFieldTest()
		{
			Assert.Fail();
		}

		[Test]
		public void Test()
		{
			Assert.Fail();
		}

		[Test]
		public void ListFieldTest()
		{
			Assert.Fail();
		}

		[Test]
		public void ExtruderOffsetFieldTest()
		{
			Assert.Fail();
		}

		[Test]
		public void NumberFieldTest()
		{
			Assert.Fail();
		}

		[Test]
		public void TextFieldTest()
		{
			Assert.Fail();
		}

		[Test]
		public void Vector2FieldTest()
		{
			Assert.Fail();
		}

		[Test]
		public void BoundDoubleFieldTest()
		{
			Assert.Fail();
		}
	}


}
