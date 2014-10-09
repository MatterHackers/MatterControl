using System;
using System.Collections.Generic;
using System.Text;

namespace CommonTypes
{
    /// <summary>
    /// A naive user defined type which we will use in the remoting example.
    /// Remember that all types used must be serializable.
    /// </summary>
    [Serializable]
    public class Rectangle
    {
        private int _width;
        private int _height;
        private int _area;

        public Rectangle(int height, int width)
        {
            _height = height;
            _width = width;
            _area = -1; //let the service calculate this
        }

        public int Area
        {
            get { return _area; }
            set { _area = value; }
        }

        public int Height
        {
            get { return _height; }
        }

        public int Width
        {
            get { return _width; }
        }
    }
}
