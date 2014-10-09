using System;
using System.Collections.Generic;
using System.Text;

namespace CommonTypes
{
    public interface IService
    {
        /// <summary>
        /// An example with two integer arguments
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        int Sum(int a, int b);

        /// <summary>
        /// An example with a variable number of arguments
        /// </summary>
        /// <param name="values"></param>
        /// <returns></returns>
        int Sum(params int[] values);

        /// <summary>
        /// An example with a return value
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        string ToUpper(string str);

        /// <summary>
        /// An example with a ref argument
        /// </summary>
        /// <param name="str"></param>
        void MakeStringUpperCase(ref string str);

        /// <summary>
        /// An example with an out argument
        /// </summary>
        /// <param name="str"></param>
        /// <param name="lowerCaseString"></param>
        void MakeStringLowerCase(string str, out string lowerCaseString);

        /// <summary>
        /// An example which alters a user defined type. In the current version you
        /// have to pass the value by reference in order to alter the object.
        /// </summary>
        /// <param name="rectangle"></param>
        void CalculateArea(ref Rectangle rectangle);

        void Square(long a, out long b);
    }
}
