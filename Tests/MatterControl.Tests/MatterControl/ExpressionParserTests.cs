
using System;
using Xunit;

namespace FormulaParser
{
    public class ExpressionParserTests
    {
        [Fact]
        public void Tests()
        {
            // positive numbers
            TestFormula("1", "1");
            TestFormula("1+1", "2");
            TestFormula(" 1 + 1 ", "2");
            TestFormula("1+2", "3");
            TestFormula("1 + 2", "3");
            // subtraction
            TestFormula("-1", "-1");
            TestFormula("4-2", "2");
            TestFormula("-(1+2)", "-3");
            // multiplication
            TestFormula("4*5", "20");
            // division
            TestFormula("6/2", "3");
            // order of operations
            TestFormula("(1+2)*3", "9");
            // decimal numbers
            TestFormula("1.1 + 2.3", "3.4");
            TestFormula("1.1 + 2 * 3", "7.1");
            // string concat
            TestFormula("concat(\"foo\", \"bar\")", "foobar");
            TestFormula("ConCat(\"foo\", \"bar\")", "foobar");
            TestFormula("concat(3 + 5, 2 + 3)", "85");
            TestFormula("concat(\"foo\", 2 + 3)", "foo5");
            TestFormula("concat(\"test\", \"this\")", "testthis");
            TestFormula("concat(\"test \", 6/3)", "test 2");
            // nested concat
            TestFormula("concat(\"test\", concat(\" \", 6/3))", "test 2");
            // lets test some Math functions
            TestFormula("pow(2, 3)", "8");
            TestFormula("Pow(2, 3)", "8");
            // sqrt
            TestFormula("sqrt(4)", "2");
            // abs
            TestFormula("abs(-4)", "4");
            // max
            TestFormula("max(4, 5)", "5");
            // min
            TestFormula("min(4, 5)", "4");
            // round
            var test = Math.Round(4.500001);
            TestFormula("round(4.50001)", "5");
            // floor
            TestFormula("floor(4.5)", "4");
            // ceiling
            TestFormula("ceiling(4.5)", "5");
            // trig functions
            TestFormula("sin(0)", "0");
            TestFormula("cos(0)", "1");
            TestFormula("tan(0)", "0");
            // atan
            TestFormula("atan(0)", "0");
            // nested math functions
            TestFormula("pow(2, max(2, 3))", "8");
            TestFormula("strlen(\"test\")", "4");
            TestFormula("substring(\"test\", 1, 2)", "es");
            TestFormula("1<2", "1");
            TestFormula("2<1", "0");
            TestFormula("2<=2", "1");
            TestFormula("2<=1", "0");
            TestFormula("2>1", "1");
            TestFormula("1>2", "0");
            TestFormula("2>=2", "1");
            TestFormula("1>=2", "0");
            TestFormula("1==1", "1");
            TestFormula("1==2", "0");
            TestFormula("1!=2", "1");
            // string comparison
            TestFormula("\"test\" == \"test\"", "1");
            TestFormula("\"test\" == \"test2\"", "0");
        }

        private void TestFormula(string input, string expected)
        {
            var result = ExpressionParser.ParseAndEvaluate(input);
            Assert.Equal(expected, result);
            if (expected != result)
            {
                System.Console.WriteLine($"Failed: {input} = {result} (expected '{expected}')");
            }
            else
            {
                Console.WriteLine($"Passed: {input} = {result} (expected '{expected}')");
            }
        }
    }
}
