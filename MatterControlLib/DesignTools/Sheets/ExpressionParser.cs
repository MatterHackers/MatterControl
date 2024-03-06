/*
Copyright (c) 2023, Lars Brubaker, John Lewin
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using org.mariuszgromada.math.mxparser;
using System;
using System.Collections.Generic;

namespace Matter_CAD_Lib.DesignTools.Sheets
{
    public class ExpressionParser
    {
        private string expressionString;
        Expression mxParser;
        ExpressionParserNew expressionEvaluator;

        private static bool ranTests = false;

        public static void RunTests()
        {
            if (!ranTests)
            {
                ranTests = true;

                RunTest("Radius", "3", new List<(string, string)>() { ("Radius", "3") });
                RunTest("1+1", "2");
                RunTest(" 1 + 1 ", "2");
                RunTest("1.1 + 2.3", "3.4");
                RunTest("concat(\"test\", \"this\")", "testthis");
                RunTest("concat(\"test \", 6/3)", "test 2");
                RunTest("3+5*(5-3)", "13");
                // positive numbers
                RunTest("1", "1");
                RunTest("1+1", "2");
                RunTest(" 1 + 1 ", "2");
                RunTest("1+2", "3");
                RunTest("1 + 2", "3");
                // subtraction
                RunTest("-1", "-1");
                RunTest("4-2", "2");
                RunTest("-(1+2)", "-3");
                // multiplication
                RunTest("4*5", "20");
                // division
                RunTest("6/2", "3");
                // order of operations
                RunTest("(1+2)*3", "9");
                // decimal numbers
                RunTest("1.1 + 2.3", "3.4");
                RunTest("1.1 + 2 * 3", "7.1");
                // string concat
                RunTest("concat(\"foo\", \"bar\")", "foobar");
                RunTest("ConCat(\"foo\", \"bar\")", "foobar");
                RunTest("concat(3 + 5, 2 + 3)", "85");
                RunTest("concat(\"foo\", 2 + 3)", "foo5");
                RunTest("concat(\"test\", \"this\")", "testthis");
                RunTest("concat(\"test \", 6/3)", "test 2");
                // nested concat
                RunTest("concat(\"test\", concat(\" \", 6/3))", "test 2");
                // lets test some Math functions
                RunTest("pow(2, 3)", "8");
                RunTest("Pow(2, 3)", "8");
                // sqrt
                RunTest("sqrt(4)", "2");
                // abs
                RunTest("abs(-4)", "4");
                // max
                RunTest("max(4, 5)", "5");
                // min
                RunTest("min(4, 5)", "4");
                // round
                var test = Math.Round(4.500001);
                RunTest("round(4.50001)", "5");
                // floor
                RunTest("floor(4.5)", "4");
                // ceiling
                RunTest("ceiling(4.5)", "5");
                // trig functions
                RunTest("sin(0)", "0");
                RunTest("cos(0)", "1");
                RunTest("tan(0)", "0");
                // atan
                RunTest("atan(0)", "0");
                // nested math functions
                RunTest("pow(2, max(2, 3))", "8");
                RunTest("strlen(\"test\")", "4");
                RunTest("substring(\"test\", 1, 2)", "es");
                RunTest("1<2", "1");
                RunTest("2<1", "0");
                RunTest("2<=2", "1");
                RunTest("2<=1", "0");
                RunTest("2>1", "1");
                RunTest("1>2", "0");
                RunTest("2>=2", "1");
                RunTest("1>=2", "0");
                RunTest("1==1", "1");
                RunTest("1==2", "0");
                RunTest("1!=2", "1");
                // string comparison
                RunTest("\"test\" == \"test\"", "1");
                RunTest("\"test\" == \"test2\"", "0");
                // test with constants
                //RunTest("A1+Radius",
                //    "5",
                //    new List<(string, string)>()
                //    {
                //        ("A1", "2"),
                //        ("Radius", "3")
                //    });

                //RunTest("5+3*(7-4)/A1+Radius",
                //    "12.5",
                //    new List<(string, string)>()
                //    {
                //        ("A1", "2"),
                //        ("Radius", "3")
                //    });

                //RunTest("concat(ipbase, concat(sku, concat(13, concat(of, count))))",
                //    "basevalWG-XP-LM13 of 75",
                //    new List<(string, string)>()
                //    {
                //        ("ipbase", "baseval"),
                //        ("sku", "WG-XP-LM"),
                //        ("index", "13"),
                //        ("count", "75")
                //    });
            }
        }

        public static void RunTest(string formula, string expectedOutput, IEnumerable<(string, string)> constants = null, bool expectSyntaxFail = false)
        {
            var expressionParser = new ExpressionParser(formula);

            if (constants != null)
            {
                foreach (var constant in constants)
                {
                    expressionParser.DefineConstant(constant.Item1, constant.Item2);
                }
            }

            if (expectSyntaxFail)
            {
                if (expressionParser.CheckSyntax())
                {
                    throw new Exception($"Expected syntax failure but got success for {formula}");
                }
            }

            if (!expressionParser.CheckSyntax())
            {
                //throw new Exception($"Expected syntax success but got failure for {formula}");
            }

            var result = expressionParser.Calculate();
            if (result != expectedOutput)
            {
                throw new Exception($"Expected {expectedOutput} but got {result} for {formula}");
            }
        }

        public ExpressionParser(string expressionString)
        {
            this.expressionString = expressionString;
            mxParser = new Expression(expressionString);
            expressionEvaluator = new ExpressionParserNew();
        }

        public string Calculate()
        {
            var newResult = expressionEvaluator.ParseAndEvaluate(expressionString);

            var result = mxParser.calculate();
            if (double.IsNaN(result))
            {
                return newResult;
            }

            var resultString = result.ToString();
            // check if they are the same
            if (resultString != newResult)
            {
                throw new Exception($"Expected {resultString} but got {newResult}");
            }

            return newResult;
        }

        /// <summary>
        /// Check the syntax of the expression
        /// </summary>
        /// <returns>True if the syntax is valid</returns>
        public bool CheckSyntax()
        {
            return mxParser.checkSyntax();
        }

        public void DefineConstant(string key, string value)
        {
            if (double.TryParse(value, out double doubleValue))
            {
                mxParser.defineConstant(key, doubleValue);
            }
            else
            {
                mxParser.defineConstant(key, 0);
            }
            expressionEvaluator.SetVariable(key, value);
        }

        public string GetErrorMessage()
        {
            return mxParser.getErrorMessage();
        }
    }
}