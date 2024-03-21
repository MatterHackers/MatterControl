﻿/*
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
using Superpower.Model;
using Superpower.Parsers;
using Superpower.Tokenizers;

namespace Matter_CAD_Lib.DesignTools.Sheets
{
    public static class ExpressionTokenizer
    {
        private static Tokenizer<ExpressionTokenType> tokenizer = new TokenizerBuilder<ExpressionTokenType>()
            .Match(Character.EqualTo('+'), ExpressionTokenType.Plus)
            .Match(Character.EqualTo('-'), ExpressionTokenType.Minus)
            .Match(Character.EqualTo('*'), ExpressionTokenType.Times)
            .Match(Character.EqualTo('/'), ExpressionTokenType.Divide)
            .Match(Numerics.Decimal, ExpressionTokenType.Number)
            .Match(Character.EqualTo('<').IgnoreThen(Character.EqualTo('=')), ExpressionTokenType.LessThanOrEqual)
            .Match(Character.EqualTo('<'), ExpressionTokenType.LessThan)
            .Match(Character.EqualTo('>').IgnoreThen(Character.EqualTo('=')), ExpressionTokenType.GreaterThanOrEqual)
            .Match(Character.EqualTo('>'), ExpressionTokenType.GreaterThan)
            .Match(Character.EqualTo('('), ExpressionTokenType.LParen)
            .Match(Character.EqualTo(')'), ExpressionTokenType.RParen)
            .Match(Character.EqualTo(','), ExpressionTokenType.Comma)
            .Match(Character.EqualTo('=').IgnoreThen(Character.EqualTo('=')), ExpressionTokenType.Equal)
            .Match(Character.EqualTo('!').IgnoreThen(Character.EqualTo('=')), ExpressionTokenType.NotEqual)
            .Match(QuotedString.CStyle, ExpressionTokenType.Text)
            .Match(Identifier.CStyle, ExpressionTokenType.Identifier) // For function names
            .Ignore(Span.WhiteSpace)
            .Build();

        public static TokenList<ExpressionTokenType> Tokenize(string input)
        {
            return tokenizer.Tokenize(input);
        }
    }
}