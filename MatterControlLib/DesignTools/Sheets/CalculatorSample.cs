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
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using CustomFunction = System.Func<double[], double>;
using ParameterList = System.Collections.Generic.Dictionary<string, double>;

namespace MatterHackers.MatterControl.DesignTools
{
    public class CalculatorSample
    {
        public static void RunTest()
        {
            var calc = new XtensibleCalculator()
                .RegisterFunction("Multiply", (a, b, c) => a * b * c)
                .RegisterFunction("MultiplySquare", "Multiply(a, b, c) * Multiply(a, b, c)", "a", "b", "c");

            var func = calc.ParseExpression("Multiply(x, y, PI)", x => 2, y => 2 + 3).Compile();
            var product = calc.ParseExpression("MultiplySquare(a, b, c)",
                new Dictionary<string, double> { { "a", 1 }, { "b", 2 }, { "c", 3 } }).Compile();
            Console.WriteLine($"Multiply: {func()}");
            Console.WriteLine($"Product: {product()}");
            Console.ReadKey();
        }
    }

    /// <summary>
	/// Simple calculator grammar.
	/// Supports arithmetic operations and parentheses.
	/// </summary>
	public class SimpleCalculator
    {
        protected internal virtual Parser<string> DecimalWithoutLeadingDigits =>
            from dot in Parse.Char('.')
            from fraction in Parse.Number
            select dot + fraction;

        protected internal virtual Parser<string> DecimalWithLeadingDigits =>
            Parse.Number.Then(n => DecimalWithoutLeadingDigits.XOr(Parse.Return(string.Empty)).Select(f => n + f));

        protected internal virtual Parser<string> Decimal =>
            DecimalWithLeadingDigits.XOr(DecimalWithoutLeadingDigits);

        protected internal virtual Parser<Expression> Constant =>
            Decimal.Select(x => Expression.Constant(double.Parse(x, CultureInfo.InvariantCulture))).Named("Constant");

        protected internal Parser<ExpressionType> Operator(string op, ExpressionType opType) =>
            Parse.String(op).Token().Return(opType);

        protected internal virtual Parser<ExpressionType> Add =>
            Operator("+", ExpressionType.AddChecked);

        protected internal virtual Parser<ExpressionType> Subtract =>
            Operator("-", ExpressionType.SubtractChecked);

        protected internal virtual Parser<ExpressionType> Multiply =>
            Operator("*", ExpressionType.MultiplyChecked);

        protected internal virtual Parser<ExpressionType> Divide =>
            Operator("/", ExpressionType.Divide);

        protected internal virtual Parser<ExpressionType> Modulo =>
            Operator("%", ExpressionType.Modulo);

        protected internal virtual Parser<ExpressionType> Power =>
            Operator("^", ExpressionType.Power);

        protected virtual Parser<Expression> ExpressionInParentheses =>
            from lparen in Parse.Char('(')
            from expr in Expr
            from rparen in Parse.Char(')')
            select expr;

        protected internal virtual Parser<Expression> Factor =>
            ExpressionInParentheses.XOr(Constant);

        protected internal virtual Parser<Expression> NegativeFactor =>
            from sign in Parse.Char('-')
            from factor in Factor
            select Expression.NegateChecked(factor);

        protected internal virtual Parser<Expression> Operand =>
            (NegativeFactor.XOr(Factor)).Token();

        protected internal virtual Parser<Expression> InnerTerm =>
            Parse.ChainRightOperator(Power, Operand, Expression.MakeBinary);

        protected internal virtual Parser<Expression> Term =>
            Parse.ChainOperator(Multiply.Or(Divide).Or(Modulo), InnerTerm, Expression.MakeBinary);

        protected internal Parser<Expression> Expr =>
            Parse.ChainOperator(Add.Or(Subtract), Term, Expression.MakeBinary);

        protected internal virtual Parser<LambdaExpression> Lambda =>
            Expr.End().Select(body => Expression.Lambda<Func<double>>(body));

        public virtual Expression<Func<double>> ParseExpression(string text) =>
            Lambda.Parse(text) as Expression<Func<double>>;
    }

    /// <summary>
	/// Scientific calculator grammar.
	/// Supports binary and hexadecimal numbers, exponential notation and functions defined in System.Math.
	/// </summary>
	public class ScientificCalculator : SimpleCalculator
    {
        protected internal virtual Parser<string> Binary =>
            Parse.IgnoreCase("0b").Then(x =>
                Parse.Chars("01").AtLeastOnce().Text()).Token();

        protected internal virtual Parser<string> Hexadecimal =>
            Parse.IgnoreCase("0x").Then(x =>
                Parse.Chars("0123456789ABCDEFabcdef").AtLeastOnce().Text()).Token();

        protected internal virtual ulong ConvertBinary(string bin)
        {
            return bin.Aggregate(0ul, (result, c) =>
            {
                if (c < '0' || c > '1')
                {
                    throw new ParseException(bin + " cannot be parsed as binary number");
                }

                return result * 2 + c - '0';
            });
        }

        protected internal virtual ulong ConvertHexadecimal(string hex)
        {
            var result = 0ul;
            if (ulong.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out result))
            {
                return result;
            }

            throw new ParseException(hex + " cannot be parsed as hexadecimal number");
        }

        protected internal virtual Parser<string> Exponent =>
            Parse.Chars("Ee").Then(e => Parse.Number.Select(n => "e+" + n).XOr(
                Parse.Chars("+-").Then(s => Parse.Number.Select(n => "e" + s + n))));

        protected internal override Parser<string> Decimal =>
            from d in base.Decimal
            from e in Exponent.Optional()
            select d + e.GetOrElse(string.Empty);

        protected internal override Parser<Expression> Constant =>
            Hexadecimal.Select(x => Expression.Constant((double)ConvertHexadecimal(x)))
                .Or(Binary.Select(b => Expression.Constant((double)ConvertBinary(b))))
                .Or(base.Constant);

        protected internal virtual Parser<string> Identifier =>
            Parse.Letter.AtLeastOnce().Text().Then(h =>
                Parse.LetterOrDigit.Many().Text().Select(t => h + t)).Token();

        protected internal virtual Parser<Expression> FunctionCall =>
            from name in Identifier
            from lparen in Parse.Char('(')
            from expr in Expr.DelimitedBy(Parse.Char(',').Token())
            from rparen in Parse.Char(')')
            select CallFunction(name, expr.ToArray());

        protected internal virtual Expression CallFunction(string name, params Expression[] parameters)
        {
            var methodInfo = typeof(Math).GetMethod(name, parameters.Select(e => e.Type).ToArray());
            if (methodInfo == null)
            {
                throw new ParseException(string.Format("Function '{0}({1})' does not exist.",
                    name, string.Join(",", parameters.Select(e => e.Type.Name))));
            }

            return Expression.Call(methodInfo, parameters);
        }

        protected internal override Parser<Expression> Factor =>
            base.Factor.XOr(FunctionCall);
    }

    /// <summary>
	/// Extensible calculator.
	/// Supports named parameters and custom functions.
	/// </summary>
	public class XtensibleCalculator : ScientificCalculator
    {
        protected internal virtual Parser<Expression> Parameter =>
            // identifier not followed by a '(' is a parameter reference
            from id in Identifier
            from n in Parse.Not(Parse.Char('('))
            select GetParameterExpression(id);

        protected internal override Parser<Expression> Factor =>
            Parameter.Or(base.Factor);

        protected internal virtual Expression GetParameterExpression(string name)
        {
            // try to find a constant in System.Math
            var systemMathConstants = typeof(System.Math).GetFields(BindingFlags.Public | BindingFlags.Static);
            var constant = systemMathConstants.FirstOrDefault(c => c.Name == name);
            if (constant != null)
            {
                // return System.Math constant value
                return Expression.Constant(constant.GetValue(null));
            }

            // return parameter value: Parameters[name]
            var getItemMethod = typeof(ParameterList).GetMethod("get_Item");
            return Expression.Call(ParameterExpression, getItemMethod, Expression.Constant(name));
        }

        protected internal virtual ParameterExpression ParameterExpression { get; } =
            Expression.Parameter(typeof(ParameterList), "Parameters");

        protected internal override Parser<LambdaExpression> Lambda =>
            Expr.End().Select(body => Expression.Lambda<Func<ParameterList, double>>(body, ParameterExpression));

        public virtual Expression<Func<ParameterList, double>> ParseFunction(string text) =>
            Lambda.Parse(text) as Expression<Func<ParameterList, double>>;

        public virtual Expression<Func<double>> ParseExpression(string text, ParameterList parameters)
        {
            // VariableList => double is converted to () => double
            var sourceExpression = ParseFunction(text);
            var newBody = Expression.Invoke(sourceExpression, Expression.Constant(parameters));
            return Expression.Lambda<Func<double>>(newBody);
        }

        public override Expression<Func<double>> ParseExpression(string text) =>
            ParseExpression(text, new ParameterList());

        public virtual Expression<Func<double>> ParseExpression(string text, params Expression<Func<double, double>>[] parameters)
        {
            // syntactic sugar: ParseExpression("a*b-b*c", a => 1, b => 2, c => 3)
            var paramList = new ParameterList();
            foreach (var p in parameters)
            {
                var paramName = p.Parameters.Single().Name;
                var paramValue = p.Compile()(0);
                paramList[paramName] = paramValue;
            }

            return ParseExpression(text, paramList);
        }

        public virtual Expression<Func<double>> ParseExpression(string text, object anonymous)
        {
            // syntactic sugar: ParseExpression("a + b / c", new { a = 1, b = 2, c = 3 })
            var paramList = new ParameterList();
            foreach (var p in anonymous.GetType().GetProperties())
            {
                var paramName = p.Name;
                var paramValue = Convert.ToDouble(p.GetValue(anonymous, new object[0]));
                paramList[paramName] = paramValue;
            }

            return ParseExpression(text, paramList);
        }

        protected internal virtual Dictionary<string, CustomFunction> CustomFuctions { get; } =
            new Dictionary<string, CustomFunction>();

        protected internal virtual string MangleName(string name, int paramCount) =>
            name + ":" + paramCount;

        protected internal override Expression CallFunction(string name, params Expression[] parameters)
        {
            // look up a custom function first
            var mangledName = MangleName(name, parameters.Length);
            if (CustomFuctions.ContainsKey(mangledName))
            {
                // convert parameters
                var callCustomFunction = new Func<string, double[], double>(CallCustomFunction).GetMethodInfo();
                var newParameters = new List<Expression>();
                newParameters.Add(Expression.Constant(mangledName));
                newParameters.Add(Expression.NewArrayInit(typeof(double), parameters));

                // call this.CallCustomFunction(mangledName, double[]);
                return Expression.Call(Expression.Constant(this), callCustomFunction, newParameters.ToArray());
            }

            // fall back to System.Math functions
            return base.CallFunction(name, parameters);
        }

        protected virtual double CallCustomFunction(string mangledName, double[] parameters) =>
            CustomFuctions[mangledName](parameters);

        public XtensibleCalculator RegisterFunction(string name, Func<double> function)
        {
            CustomFuctions[MangleName(name, 0)] = x => function();
            return this;
        }

        public XtensibleCalculator RegisterFunction(string name, Func<double, double> function)
        {
            CustomFuctions[MangleName(name, 1)] = x => function(x[0]);
            return this;
        }

        public XtensibleCalculator RegisterFunction(string name, Func<double, double, double> function)
        {
            CustomFuctions[MangleName(name, 2)] = x => function(x[0], x[1]);
            return this;
        }

        public XtensibleCalculator RegisterFunction(string name, Func<double, double, double, double> function)
        {
            CustomFuctions[MangleName(name, 3)] = x => function(x[0], x[1], x[2]);
            return this;
        }

        public XtensibleCalculator RegisterFunction(string name, Func<double, double, double, double, double> function)
        {
            CustomFuctions[MangleName(name, 4)] = x => function(x[0], x[1], x[2], x[3]);
            return this;
        }

        public XtensibleCalculator RegisterFunction(string name, Func<double, double, double, double, double, double> function)
        {
            CustomFuctions[MangleName(name, 5)] = x => function(x[0], x[1], x[2], x[3], x[4]);
            return this;
        }

        public XtensibleCalculator RegisterFunction(string name, string functionExpression, params string[] parameters)
        {
            // Func<Dictionary, double>
            var compiledFunction = ParseFunction(functionExpression).Compile();

            //syntactic sugar: ParseExpression("a + b / c", "a","b","c")
            CustomFuctions[MangleName(name, parameters.Length)] = x =>
            {
                // convert double[] to Dictionary
                var parametersDictionary = new Dictionary<string, double>();
                for (int paramSeq = 0; paramSeq < parameters.Length; paramSeq++)
                {
                    parametersDictionary.Add(parameters[paramSeq], x[paramSeq]);
                }

                return compiledFunction(parametersDictionary);
            };

            return this;
        }
    }
}