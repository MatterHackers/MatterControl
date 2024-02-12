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

using System.ComponentModel;
using Matter_CAD_Lib.DesignTools.Interfaces;
using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.DesignTools
{
    [TypeConverter(typeof(Vector3OrExpression))]
    public class Vector3OrExpression : DirectOrExpression
    {
        public Vector3 Value(IObject3D owner)
        {
            var value = Expressions.EvaluateExpression<Vector3>(owner, Expression);
            if (owner.RebuildLocked)
            {
                ExpressionValueAtLastRebuild = value.ToString();
            }

            return value;
        }

        public static Vector3 ParseVector(IObject3D owner, string inputExpression)
        {
            var resultVector = Vector3.Zero;

            if (inputExpression.Length > 6
                && inputExpression.StartsWith("[")
                && inputExpression.EndsWith("]"))
            {
                var withoutBrackets = inputExpression.Substring(1, inputExpression.Length - 2);

                var result = withoutBrackets.Split(',');
                if (result.Length == 3)
                {
                    resultVector.X = Expressions.EvaluateExpression<double>(owner, result[0].Trim());
                    resultVector.Y = Expressions.EvaluateExpression<double>(owner, result[1].Trim());
                    resultVector.Z = Expressions.EvaluateExpression<double>(owner, result[2].Trim());
                }
            }

            return resultVector;
        }

        public Vector3OrExpression(Vector3 value)
        {
            Expression = value.ToString();
        }

        public Vector3OrExpression(string expression)
        {
            Expression = expression;
        }

        public static implicit operator Vector3OrExpression(Vector3 value)
        {
            return new Vector3OrExpression(value);
        }

        public static implicit operator Vector3OrExpression(string expression)
        {
            return new Vector3OrExpression(expression);
        }
    }
}