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

namespace MatterHackers.MatterControl.DesignTools
{
    public class ExpressionParser
    {
        Expression expression;

        private static bool ranTests = false;

        public static void RunTests()
        {
            RunTest("1+1", "2");
            RunTest("5+3*(7-4)/A1+Radius", "12.5", new List<(string, string)>() { ("A1", "2"), ("Radius", "3") });
        }

        public static void RunTest(string formula, string expectedOutput, IEnumerable<(string, string)> constants = null, bool expectSyntaxFail = false)
        {
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

        public ExpressionParser(string expressionString)
        {
#if DEBUG
            if (!ranTests)
            {
                ranTests = true;
                RunTests();
            }
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
    }
}