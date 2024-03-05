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
        private static readonly TokenListParser<ExpressionTokenType, string> TextTlp =
            Token.EqualTo(ExpressionTokenType.Text).Select(tok => tok.ToStringValue().Trim('"'));

        private static readonly TokenListParser<ExpressionTokenType, string> ExpressionTlp;

        private static readonly TokenListParser<ExpressionTokenType, string> ComparisonTlp;

        private static readonly TokenListParser<ExpressionTokenType, string> GeneralFunctionTlp =
            from identifier in Token.EqualTo(ExpressionTokenType.Identifier)
            from _ in Token.EqualTo(ExpressionTokenType.LParen)
            from args in Parse.Ref(() => ExpressionTlp).ManyDelimitedBy(Token.EqualTo(ExpressionTokenType.Comma))
            from __ in Token.EqualTo(ExpressionTokenType.RParen)
            select ApplyFunction(identifier.ToStringValue(), args.ToArray());

        static ExpressionParser()
        {
            var NumberTlp = Token.EqualTo(ExpressionTokenType.Number).Apply(Numerics.DecimalDecimal);

            TokenListParser<ExpressionTokenType, string> FactorTlp = null;

            var UnaryTlp =
                from minus in Token.EqualTo(ExpressionTokenType.Minus)
                from expr in Parse.Ref(() => FactorTlp)
                select "-" + expr;

            FactorTlp = UnaryTlp
                .Or(Parse.Ref(() => ExpressionTlp).Between(Token.EqualTo(ExpressionTokenType.LParen), Token.EqualTo(ExpressionTokenType.RParen)))
                .Or(NumberTlp.Select(n => n.ToString(CultureInfo.InvariantCulture)))
                .Or(TextTlp);

            var TermTlp = Parse.Chain(Token.EqualTo(ExpressionTokenType.Times).Or(Token.EqualTo(ExpressionTokenType.Divide)), FactorTlp, (op, left, right) =>
                op.Kind == ExpressionTokenType.Times ? (decimal.Parse(left) * decimal.Parse(right)).ToString() : (decimal.Parse(left) / decimal.Parse(right)).ToString());

            var AddAndSubTlp = Parse.Chain(
                Token.EqualTo(ExpressionTokenType.Plus).Or(Token.EqualTo(ExpressionTokenType.Minus)),
                TermTlp, // Make sure this is using terms which can include factors or unary operations
                    (op, left, right) =>
                    {
                        if (op.Kind == ExpressionTokenType.Plus)
                        {
                            return (decimal.Parse(left, CultureInfo.InvariantCulture) + decimal.Parse(right, CultureInfo.InvariantCulture)).ToString(CultureInfo.InvariantCulture);
                        }
                        else
                        { // This is a true subtraction operation
                            return (decimal.Parse(left, CultureInfo.InvariantCulture) - decimal.Parse(right, CultureInfo.InvariantCulture)).ToString(CultureInfo.InvariantCulture);
                        }
                    });

            ComparisonTlp =
                Parse.Chain(
                    Token.EqualTo(ExpressionTokenType.LessThan)
                    .Or(Token.EqualTo(ExpressionTokenType.LessThanOrEqual))
                    .Or(Token.EqualTo(ExpressionTokenType.GreaterThan))
                    .Or(Token.EqualTo(ExpressionTokenType.GreaterThanOrEqual))
                    .Or(Token.EqualTo(ExpressionTokenType.Equal))
                    .Or(Token.EqualTo(ExpressionTokenType.NotEqual)),
                    AddAndSubTlp, // Change this to use AddAndSub instead of Term
                    (op, left, right) =>
                    {
                        switch (op.Kind)
                        {
                            case ExpressionTokenType.LessThan:
                                return Convert.ToDouble(left) < Convert.ToDouble(right) ? "1" : "0";
                            case ExpressionTokenType.LessThanOrEqual:
                                return Convert.ToDouble(left) <= Convert.ToDouble(right) ? "1" : "0";
                            case ExpressionTokenType.GreaterThan:
                                return Convert.ToDouble(left) > Convert.ToDouble(right) ? "1" : "0";
                            case ExpressionTokenType.GreaterThanOrEqual:
                                return Convert.ToDouble(left) >= Convert.ToDouble(right) ? "1" : "0";
                            case ExpressionTokenType.Equal:
                                {
                                    bool isNumericLeft = double.TryParse(left, out double leftNum);
                                    bool isNumericRight = double.TryParse(right, out double rightNum);
                                    if (isNumericLeft && isNumericRight)
                                    {
                                        return leftNum == rightNum ? "1" : "0";
                                    }
                                    else
                                    {
                                        // Assuming non-numeric values are strings; adjust as necessary for your context.
                                        return left == right ? "1" : "0";
                                    }
                                }
                            case ExpressionTokenType.NotEqual:
                                {
                                    bool isNumericLeft = double.TryParse(left, out double leftNum);
                                    bool isNumericRight = double.TryParse(right, out double rightNum);
                                    if (isNumericLeft && isNumericRight)
                                    {
                                        return leftNum != rightNum ? "1" : "0";
                                    }
                                    else
                                    {
                                        // Assuming non-numeric values are strings; adjust as necessary for your context.
                                        return left != right ? "1" : "0";
                                    }
                                }
                            default:
                                throw new InvalidOperationException("Unexpected comparison operator.");
                        }
                    }
                    );

            ExpressionTlp = ComparisonTlp
                .Or(GeneralFunctionTlp)
                .Or(TextTlp)
                .Or(AddAndSubTlp) // Include AddAndSub in the Expression composition
                .Or(FactorTlp); // Ensure that standalone factors can be evaluated.
        }


        public static string ParseAndEvaluate(string input)
        {
            var tokens = ExpressionTokenizer.Tokenize(input);
            return ExpressionTlp.Parse(tokens);
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
