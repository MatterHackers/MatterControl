/*  The MIT License(MIT)

//  Copyright(c) 2015 Stefan Gordon

//  Permission is hereby granted, free of charge, to any person obtaining a copy
//  of this software and associated documentation files (the "Software"), to deal
//  in the Software without restriction, including without limitation the rights
//  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//  copies of the Software, and to permit persons to whom the Software is
//  furnished to do so, subject to the following conditions:

//  The above copyright notice and this permission notice shall be included in all
//  copies or substantial portions of the Software.

//  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//  SOFTWARE.
*/

using System.Text;

namespace ObjParser.Types
{
	public class ObjMaterial : IObjArray
    {
        public string Name { get; set; }
        public ObjColor AmbientReflectivity { get; set; }
        public ObjColor DiffuseReflectivity { get; set; }
        public ObjColor SpecularReflectivity { get; set; }
        public ObjColor TransmissionFilter { get; set; }
        public ObjColor EmissiveCoefficient { get; set; }
        public float SpecularExponent { get; set; }
        public float OpticalDensity { get; set; }
        public float Dissolve { get; set; }
        public float IlluminationModel { get; set; }
		public string DiffuseTextureFileName { get; internal set; }

		public ObjMaterial()
        {
            this.Name = "DefaultMaterial";
            this.AmbientReflectivity = new ObjColor();
            this.DiffuseReflectivity = new ObjColor();
            this.SpecularReflectivity = new ObjColor();
            this.TransmissionFilter = new ObjColor();
            this.EmissiveCoefficient = new ObjColor();
            this.SpecularExponent = 0;
            this.OpticalDensity = 1.0f;
            this.Dissolve = 1.0f;
            this.IlluminationModel = 0;
        }

        public void LoadFromStringArray(string[] data)
        {
        }

        public override string ToString()
        {
            StringBuilder b = new StringBuilder();
            b.AppendLine("newmtl " + Name);

            b.AppendLine(string.Format("Ka {0}", AmbientReflectivity));
            b.AppendLine(string.Format("Kd {0}", DiffuseReflectivity));
            b.AppendLine(string.Format("Ks {0}", SpecularReflectivity));
            b.AppendLine(string.Format("Tf {0}", TransmissionFilter));
            b.AppendLine(string.Format("Ke {0}", EmissiveCoefficient));
            b.AppendLine(string.Format("Ns {0}", SpecularExponent));
            b.AppendLine(string.Format("Ni {0}", OpticalDensity));
            b.AppendLine(string.Format("d {0}", Dissolve));
            b.AppendLine(string.Format("illum {0}", IlluminationModel));

            return b.ToString();
        }
    }
}
