using System;
using System.Collections.Generic;
using System.Text;
using CommonTypes;

namespace RemotingLiteExampleServer
{
    public class ServiceImpl : IService
    {
        #region IService Members

        public int Sum(int a, int b)
        {
            return a + b;
        }

        public int Sum(params int[] values)
        {
            int sum = 0;
            foreach (int value in values)
                sum += value;
            return sum;
        }

        public string ToUpper(string str)
        {
            return str.ToUpper();
        }

        public void MakeStringUpperCase(ref string str)
        {
            str = str.ToUpper();
        }

        public void MakeStringLowerCase(string str, out string lowerCaseString)
        {
            lowerCaseString = str.ToLower();
        }

        public void CalculateArea(ref Rectangle rectangle)
        {
            rectangle.Area = rectangle.Width * rectangle.Height;
        }

        public void Square(long a, out long b)
        {
            b = a * a;
        }

        #endregion
    }
}
