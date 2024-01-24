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
using Sprache;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace MatterHackers.MatterControl.DesignTools
{
    public class ExpressionParser
    {
        Expression expression;

        private static bool ranTests = false;

        public static void RunTests()
        {
            if (!ranTests)
            {
                ranTests = true;

                RunTest("1+1", "2");
                RunTest("concat(\"test\", \"this\")", "testthis");
                RunTest("concat(\"test \", 6/3)", "test 2");
                RunTest("5+3*(7-4)/A1+Radius", "12.5", new List<(string, string)>() { ("A1", "2"), ("Radius", "3") });
                RunTest("3+5*(5-3)", "13");
            }
        }

        public static void RunTest(string formula, string expectedOutput, IEnumerable<(string, string)> constants = null, bool expectSyntaxFail = false)
        {
            var oldWay = false;
            if (oldWay)
            {
                // the old way
                var expressionParser = new ExpressionParser(formula);
                if (constants != null)
                {
                    foreach (var constant in constants)
                    {
                        expressionParser.DefineConstant(constant.Item1, double.Parse(constant.Item2));
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
                    throw new Exception($"Expected syntax success but got failure for {formula}");
                }

                var result = expressionParser.Calculate();
                if (result != expectedOutput)
                {
                    throw new Exception($"Expected {expectedOutput} but got {result} for {formula}");
                }
            }
            else
            {
                // the new way
                if (ExpressionEvaluator.ParseExpression(formula, constants) != expectedOutput)
                {
                    var result = ExpressionEvaluator.ParseExpression(formula, constants);
                    throw new Exception($"Expected {expectedOutput} but got {result} for {formula}");
                }
            }
        }

        public ExpressionParser(string expressionString)
        {
#if DEBUG
            RunTests();
#endif

            expression = new Expression(expressionString);
        }

        public string Calculate()
        {
            return expression.calculate().ToString();
        }

        /// <summary>
        /// Check the syntax of the expression
        /// </summary>
        /// <returns>True if the syntax is valid</returns>
        public bool CheckSyntax()
        {
            return expression.checkSyntax();
        }

        public void DefineConstant(string key, double value)
        {
            expression.defineConstant(key, value);
        }

        public string GetErrorMessage()
        {
            return expression.getErrorMessage();
        }
    }


    public class ExpressionEvaluator
    {
        public static string ParseExpression(string input, IEnumerable<(string, string)> constants)
        {
            var constantsDictionary = new Dictionary<string, string>();
            if (constants != null)
            {
                constantsDictionary = constants.ToDictionary(c => c.Item1, c => c.Item2);
            }

            // Must find at least one letter then any number of letters or numbers
            var identifier = Parse.Letter.AtLeastOnce().Text().Then(id => Parse.LetterOrDigit.Many().Text().Select(rest => id + rest)).Token();


            var number = Parse.Number.Select(n => double.Parse(n)).Token();
            var str = Parse.CharExcept('"').Many().Text().Contained(Parse.Char('"'), Parse.Char('"')).Token();

            // Constant Parser
            var constantParser = identifier.Select(id =>
            {
                return constantsDictionary.TryGetValue(id, out var value) ? double.Parse(value) : double.NaN;
            }).Where(c => !double.IsNaN(c));

            Parser<double> expr = null; // Declare expr as null initially

            var factor = Parse.Ref(() => expr).Contained(Parse.Char('('), Parse.Char(')'))
                         .XOr(number)
                         .XOr(constantParser); // Integrate constant parser here

            var term = Parse.ChainOperator(Parse.Char('*').Or(Parse.Char('/')), factor, (op, a, b) => op == '*' ? a * b : a / b);
            expr = Parse.ChainOperator(Parse.Char('+').Or(Parse.Char('-')), term, (op, a, b) => op == '+' ? a + b : a - b);

            var concatFunc = from concat in identifier
                             from _ in Parse.Char('(')
                             from first in str.Or(expr.Select(e => e.ToString()))
                             from __ in Parse.Char(',')
                             from second in str.Or(expr.Select(e => e.ToString()))
                             from ___ in Parse.Char(')')
                             where concat == "concat"
                             select first + second;

            var fullParser = concatFunc.XOr(expr.Select(e => (object)e));

            var result = fullParser.Parse(input).ToString();
            return result;
        }
    }
}