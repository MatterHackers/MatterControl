/*
Copyright (c) 2023, Lars Brubaker
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

using Sprache;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MatterHackers.MatterControl.DesignTools
{
    public class ExpressionEvaluator
    {
        private Dictionary<string, object> constantsDictionary = new Dictionary<string, object>();
        private string expression;

        public ExpressionEvaluator(string expression)
        {
            this.expression = expression;
        }

        public string Calculate()
        {
            try
            {
                var input = expression;

                var identifier = Parse.Letter.AtLeastOnce().Text().Then(id => Parse.LetterOrDigit.Many().Text().Select(rest => id + rest)).Token();

                var number = Parse.Number.Select(n => double.Parse(n)).Token();
                var str = Parse.CharExcept('"').Many().Text().Contained(Parse.Char('"'), Parse.Char('"')).Token();

                // Updated Constant Parser
                var constantParser = identifier.Select(id =>
                {
                    if (constantsDictionary.TryGetValue(id, out var value))
                    {
                        return value is double ? (double)value : double.NaN;
                    }
                    return double.NaN;
                }).Where(c => !double.IsNaN(c));

                Parser<double> expr = null; // Declare expr as null initially

                // Forward declaration of concatFunc
                Parser<string> concatFunc = null;

                var factor = Parse.Ref(() => expr).Contained(Parse.Char('(').Token(), Parse.Char(')').Token())
                             .XOr(number)
                             .XOr(constantParser);

                var term = Parse.ChainOperator(Parse.Char('*').Or(Parse.Char('/')).Token(), factor, (op, a, b) => op == '*' ? a * b : a / b);
                expr = Parse.ChainOperator(Parse.Char('+').Or(Parse.Char('-')).Token(), term, (op, a, b) => op == '+' ? a + b : a - b);

                // Updated concatFunc definition to handle nested concatenations
                concatFunc = from concat in identifier
                             from _ in Parse.Char('(').Token()
                             from first in str.Or(Parse.Ref(() => concatFunc)).Or(expr.Select(e => e.ToString()))
                             from __ in Parse.Char(',').Token()
                             from second in str.Or(Parse.Ref(() => concatFunc)).Or(expr.Select(e => e.ToString()))
                             from ___ in Parse.Char(')').Token()
                             where concat == "concat"
                             select first + second;

                var whiteSpace = Parse.WhiteSpace.Many().Text();

                var fullParser = from leading in whiteSpace.Optional()
                                 from expression in concatFunc.XOr(expr.Select(e => e.ToString()))
                                 from trailing in whiteSpace.Optional()
                                 select expression;

                var result = fullParser.Parse(input).ToString();
                return result;
            }
            catch
            {
                return "NaN";
            }
        }

        public void DefineConstant(string key, object value)
        {
            constantsDictionary[key] = value;
        }
    }
}