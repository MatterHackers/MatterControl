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

using System.ComponentModel;
using MatterHackers.Agg;
using MatterHackers.DataConverters3D;

namespace MatterHackers.MatterControl.DesignTools
{
    [TypeConverter(typeof(IntOrExpression))]
    public class IntOrExpression : DirectOrExpression
    {
        public int Value(IObject3D owner)
        {
            var rebuilding = owner.RebuildLocked;
            var value = Expressions.EvaluateExpression<int>(owner, Expression);
            if (rebuilding)
            {
                ExpressionValueAtLastRebuild = value.ToString();
            }

            return value;
        }

        public IntOrExpression(int value)
        {
            Expression = value.ToString();
        }

        public IntOrExpression(double value)
        {
            Expression = ((int)value).ToString();
        }

        public IntOrExpression(string expression)
        {
            Expression = expression;
        }

        public static implicit operator IntOrExpression(int value)
        {
            return new IntOrExpression(value);
        }

        public static implicit operator IntOrExpression(double value)
        {
            return new IntOrExpression(value);
        }

        public static implicit operator IntOrExpression(string expression)
        {
            return new IntOrExpression(expression);
        }

        /// <summary>
        /// Evaluate the expression clap the result and return the clamped value.
        /// If the expression as not an equation, modify it to be the clamped value.
        /// </summary>
        /// <param name="item">The Object to find the table relative to</param>
        /// <param name="min">The min value to clamp to</param>
        /// <param name="max">The max value to clamp to</param>
        /// <param name="valuesChanged">Did the value actual get changed (clamped).</param>
        /// <returns></returns>
        public int ClampIfNotCalculated(IObject3D item, int min, int max, ref bool valuesChanged)
        {
            var value = Util.Clamp(this.Value(item), min, max, ref valuesChanged);
            if (!this.IsEquation)
            {
                // clamp the actual expression as it is not an equation
                Expression = value.ToString();
            }

            return value;
        }
    }
}