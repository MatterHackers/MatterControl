using Superpower;
using Superpower.Parsers;
using Superpower.Tokenizers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace FormulaParser
{
    public static class ExpressionParser
    {
        public enum TokenType
        {
            Number,
            Plus,
            Minus,
            Times,
            Divide,
            LessThan,
            LessThanOrEqual,
            GreaterThan,
            GreaterThanOrEqual,
            LParen,
            RParen,
            Comma,
            Text,
            Identifier, // For function names
        }

        private static Tokenizer<TokenType> tokenizer = new TokenizerBuilder<TokenType>()
            .Match(Character.EqualTo('+'), TokenType.Plus)
            .Match(Character.EqualTo('-'), TokenType.Minus)
            .Match(Character.EqualTo('*'), TokenType.Times)
            .Match(Character.EqualTo('/'), TokenType.Divide)
            .Match(Numerics.Decimal, TokenType.Number)
            .Match(Character.EqualTo('<').IgnoreThen(Character.EqualTo('=')), TokenType.LessThanOrEqual)
            .Match(Character.EqualTo('<'), TokenType.LessThan)
            .Match(Character.EqualTo('>').IgnoreThen(Character.EqualTo('=')), TokenType.GreaterThanOrEqual)
            .Match(Character.EqualTo('>'), TokenType.GreaterThan)
            .Match(Character.EqualTo('('), TokenType.LParen)
            .Match(Character.EqualTo(')'), TokenType.RParen)
            .Match(Character.EqualTo(','), TokenType.Comma)
            .Match(QuotedString.CStyle, TokenType.Text)
            .Match(Identifier.CStyle, TokenType.Identifier) // For function names
            .Ignore(Span.WhiteSpace)
            .Build();

        private static readonly TokenListParser<TokenType, string> Text =
            Token.EqualTo(TokenType.Text).Select(tok => tok.ToStringValue().Trim('"'));

        private static readonly TokenListParser<TokenType, string> Expression;

        private static readonly TokenListParser<TokenType, string> Comparison;

        private static readonly TokenListParser<TokenType, string> GeneralFunction =
            from identifier in Token.EqualTo(TokenType.Identifier)
            from _ in Token.EqualTo(TokenType.LParen)
            from args in Parse.Ref(() => Expression).ManyDelimitedBy(Token.EqualTo(TokenType.Comma))
            from __ in Token.EqualTo(TokenType.RParen)
            select ApplyFunction(identifier.ToStringValue(), args.ToArray());

        static ExpressionParser()
        {
            var Number = Token.EqualTo(TokenType.Number).Apply(Numerics.DecimalDecimal);
            var Unary =
                Parse.Chain(
                    Token.EqualTo(TokenType.Minus),
                    Number,
                    (op, left, right) => -right
                );

            var Factor = Parse.Ref(() => Expression)
                .Between(Token.EqualTo(TokenType.LParen), Token.EqualTo(TokenType.RParen))
                //.Or(Unary)
                .Or(Number.Select(n => n.ToString(CultureInfo.InvariantCulture)))
                .Or(Text);

            var Term = Parse.Chain(Token.EqualTo(TokenType.Times).Or(Token.EqualTo(TokenType.Divide)), Factor, (op, left, right) =>
                op.Kind == TokenType.Times ? (decimal.Parse(left) * decimal.Parse(right)).ToString() : (decimal.Parse(left) / decimal.Parse(right)).ToString());

            var AddAndSub = Parse.Chain(Token.EqualTo(TokenType.Plus).Or(Token.EqualTo(TokenType.Minus)), Term, (op, left, right) =>
                op.Kind == TokenType.Plus ? (decimal.Parse(left) + decimal.Parse(right)).ToString() : (decimal.Parse(left) - decimal.Parse(right)).ToString());

            Comparison =
                Parse.Chain(
                    Token.EqualTo(TokenType.LessThan).Or(Token.EqualTo(TokenType.LessThanOrEqual))
                    .Or(Token.EqualTo(TokenType.GreaterThan)).Or(Token.EqualTo(TokenType.GreaterThanOrEqual)),
                    AddAndSub, // Change this to use AddAndSub instead of Term
                    (op, left, right) =>
                    {
                        switch (op.Kind)
                        {
                            case TokenType.LessThan:
                                return Convert.ToDouble(left) < Convert.ToDouble(right) ? "true" : "false";
                            case TokenType.LessThanOrEqual:
                                return Convert.ToDouble(left) <= Convert.ToDouble(right) ? "true" : "false";
                            case TokenType.GreaterThan:
                                return Convert.ToDouble(left) > Convert.ToDouble(right) ? "true" : "false";
                            case TokenType.GreaterThanOrEqual:
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
            var tokens = tokenizer.Tokenize(input);
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
