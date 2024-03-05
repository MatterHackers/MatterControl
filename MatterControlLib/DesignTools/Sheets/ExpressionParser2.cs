/*
Copyright (c) 2024, Lars Brubaker
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

using Superpower;
using Superpower.Parsers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace FormulaParser
{
    public static class ExpressionParser
    {
        private static readonly TokenListParser<ExpressionTokenType, string> Text =
            Token.EqualTo(ExpressionTokenType.Text).Select(tok => tok.ToStringValue().Trim('"'));

        private static readonly TokenListParser<ExpressionTokenType, string> Expression;

        private static readonly TokenListParser<ExpressionTokenType, string> Comparison;

        private static readonly TokenListParser<ExpressionTokenType, string> GeneralFunction =
            from identifier in Token.EqualTo(ExpressionTokenType.Identifier)
            from _ in Token.EqualTo(ExpressionTokenType.LParen)
            from args in Parse.Ref(() => Expression).ManyDelimitedBy(Token.EqualTo(ExpressionTokenType.Comma))
            from __ in Token.EqualTo(ExpressionTokenType.RParen)
            select ApplyFunction(identifier.ToStringValue(), args.ToArray());

        static ExpressionParser()
        {
            var Number = Token.EqualTo(ExpressionTokenType.Number).Apply(Numerics.DecimalDecimal);
            var Unary =
                Parse.Chain(
                    Token.EqualTo(ExpressionTokenType.Minus),
                    Number,
                    (op, left, right) => -right
                );

            var Factor = Parse.Ref(() => Expression)
                .Between(Token.EqualTo(ExpressionTokenType.LParen), Token.EqualTo(ExpressionTokenType.RParen))
                //.Or(Unary)
                .Or(Number.Select(n => n.ToString(CultureInfo.InvariantCulture)))
                .Or(Text);

            var Term = Parse.Chain(Token.EqualTo(ExpressionTokenType.Times).Or(Token.EqualTo(ExpressionTokenType.Divide)), Factor, (op, left, right) =>
                op.Kind == ExpressionTokenType.Times ? (decimal.Parse(left) * decimal.Parse(right)).ToString() : (decimal.Parse(left) / decimal.Parse(right)).ToString());

            var AddAndSub = Parse.Chain(Token.EqualTo(ExpressionTokenType.Plus).Or(Token.EqualTo(ExpressionTokenType.Minus)), Term, (op, left, right) =>
                op.Kind == ExpressionTokenType.Plus ? (decimal.Parse(left) + decimal.Parse(right)).ToString() : (decimal.Parse(left) - decimal.Parse(right)).ToString());

            Comparison =
                Parse.Chain(
                    Token.EqualTo(ExpressionTokenType.LessThan).Or(Token.EqualTo(ExpressionTokenType.LessThanOrEqual))
                    .Or(Token.EqualTo(ExpressionTokenType.GreaterThan)).Or(Token.EqualTo(ExpressionTokenType.GreaterThanOrEqual)),
                    AddAndSub, // Change this to use AddAndSub instead of Term
                    (op, left, right) =>
                    {
                        switch (op.Kind)
                        {
                            case ExpressionTokenType.LessThan:
                                return Convert.ToDouble(left) < Convert.ToDouble(right) ? "true" : "false";
                            case ExpressionTokenType.LessThanOrEqual:
                                return Convert.ToDouble(left) <= Convert.ToDouble(right) ? "true" : "false";
                            case ExpressionTokenType.GreaterThan:
                                return Convert.ToDouble(left) > Convert.ToDouble(right) ? "true" : "false";
                            case ExpressionTokenType.GreaterThanOrEqual:
                                return Convert.ToDouble(left) >= Convert.ToDouble(right) ? "true" : "false";
                            default:
                                throw new InvalidOperationException("Unexpected comparison operator.");
                        }
                    }
                    );

            Expression = Comparison
                .Or(GeneralFunction)
                .Or(Text)
                .Or(AddAndSub) // Include AddAndSub in the Expression composition
                .Or(Factor); // Ensure that standalone factors can be evaluated.
        }


        public static string ParseAndEvaluate(string input)
        {
            var tokens = ExpressionTokenizer.Tokenize(input);
            return Expression.Parse(tokens);
        }

        private static string ApplyFunction(string name, IList<dynamic> args)
        {
            // Convert the first letter to uppercase to match the Math method naming convention
            var methodName = char.ToUpper(name[0]) + name.Substring(1).ToLower();

            // Check if the method exists in the Math class and has the correct number of arguments
            var method = typeof(Math).GetMethod(methodName, args.Select(a => typeof(double)).ToArray());

            if (method != null)
            {
                try
                {
                    // Convert all arguments to double and invoke the Math method
                    var methodArgs = args.Select(arg => Convert.ToDouble(arg)).ToArray();
                    var result = method.Invoke(null, methodArgs);
                    return Convert.ToString(result, CultureInfo.InvariantCulture);
                }
                catch (Exception ex)
                {
                    throw new ArgumentException($"Error invoking Math function {name}: {ex.Message}");
                }
            }

            // Existing switch statement for other functions
            switch (name.ToLower())
            {
                case "concat":
                    return string.Concat(args.Select(arg => Convert.ToString(arg)));
                case "sum":
                    return args.Sum(arg => Convert.ToDouble(arg)).ToString(CultureInfo.InvariantCulture);
                case "strlen":
                    return args.First().Length.ToString(CultureInfo.InvariantCulture);
                case "substring":
                    return args.First().Substring(Convert.ToInt32(args[1]), Convert.ToInt32(args[2]));
                default:
                    throw new ArgumentException($"Unknown function: {name}");
            }
        }

    }
}
