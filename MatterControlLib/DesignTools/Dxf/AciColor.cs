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

namespace Matter_CAD_Lib.DesignTools.Dxf
{
    /// <summary>
    /// AciColor class, containing all the AciColor codes and ARGB converters
    /// </summary>
    public class AciColor
    {
        private byte index;

        //Hex values (ARGB) for all the 256 ACI Color indexes
        private static uint[] argb = new uint[256] {
            0xFF000000, 0xFFFF0000, 0xFFFFFF00, 0xFF00FF00, 0xFF00FFFF, 0xFF0000FF, 0xFFFF00FF, 0xFFFFFFFF, //007
            0xFF414141, 0xFF808080, 0xFFFF0000, 0xFFFFAAAA, 0xFFBD0000, 0xFFBD7E7E, 0xFF810000, 0xFF815656, //015
            0xFF680000, 0xFF684545, 0xFF4F0000, 0xFF4F3535, 0xFFFF3F00, 0xFFFFBFAA, 0xFFBD2E00, 0xFFBD8D7E, //023
            0xFF811F00, 0xFF816056, 0xFF681900, 0xFF684E45, 0xFF4F1300, 0xFF4F3B35, 0xFFFF7F00, 0xFFFFD4AA, //031
            0xFFBD5E00, 0xFFBD9D7E, 0xFF814000, 0xFF816B56, 0xFF683400, 0xFF685645, 0xFF4F2700, 0xFF4F4235, //039
            0xFFFFBF00, 0xFFFFEAAA, 0xFFBD8D00, 0xFFBDAD7E, 0xFF816000, 0xFF817656, 0xFF684E00, 0xFF685F45, //047
            0xFF4F3B00, 0xFF4F4935, 0xFFFFFF00, 0xFFFFFFAA, 0xFFBDBD00, 0xFFBDBD7E, 0xFF818100, 0xFF818156, //055
            0xFF686800, 0xFF686845, 0xFF4F4F00, 0xFF4F4F35, 0xFFBFFF00, 0xFFEAFFAA, 0xFF8DBD00, 0xFFADBD7E, //063
            0xFF608100, 0xFF768156, 0xFF4E6800, 0xFF5F6845, 0xFF3B4F00, 0xFF494F35, 0xFF7FFF00, 0xFFD4FFAA, //071
            0xFF5EBD00, 0xFF9DBD7E, 0xFF408100, 0xFF6B8156, 0xFF346800, 0xFF566845, 0xFF274F00, 0xFF424F35, //079
            0xFF3FFF00, 0xFFBFFFAA, 0xFF2EBD00, 0xFF8DBD7E, 0xFF1F8100, 0xFF608156, 0xFF196800, 0xFF4E6845, //087
            0xFF134F00, 0xFF3B4F35, 0xFF00FF00, 0xFFAAFFAA, 0xFF00BD00, 0xFF7EBD7E, 0xFF008100, 0xFF568156, //095
            0xFF006800, 0xFF456845, 0xFF004F00, 0xFF354F35, 0xFF00FF3F, 0xFFAAFFBF, 0xFF00BD2E, 0xFF7EBD8D, //103
            0xFF00811F, 0xFF568160, 0xFF006819, 0xFF45684E, 0xFF004F13, 0xFF354F3B, 0xFF00FF7F, 0xFFAAFFD4, //111
            0xFF00BD5E, 0xFF7EBD9D, 0xFF008140, 0xFF56816B, 0xFF006834, 0xFF456856, 0xFF004F27, 0xFF354F42, //119
            0xFF00FFBF, 0xFFAAFFEA, 0xFF00BD8D, 0xFF7EBDAD, 0xFF008160, 0xFF568176, 0xFF00684E, 0xFF45685F, //127
            0xFF004F3B, 0xFF354F49, 0xFF00FFFF, 0xFFAAFFFF, 0xFF00BDBD, 0xFF7EBDBD, 0xFF008181, 0xFF568181, //135
            0xFF006868, 0xFF456868, 0xFF004F4F, 0xFF354F4F, 0xFF00BFFF, 0xFFAAEAFF, 0xFF008DBD, 0xFF7EADBD, //143
            0xFF006081, 0xFF567681, 0xFF004E68, 0xFF455F68, 0xFF003B4F, 0xFF35494F, 0xFF007FFF, 0xFFAAD4FF, //151
            0xFF005EBD, 0xFF7E9DBD, 0xFF004081, 0xFF566B81, 0xFF003468, 0xFF455668, 0xFF00274F, 0xFF35424F, //159
            0xFF003FFF, 0xFFAABFFF, 0xFF002EBD, 0xFF7E8DBD, 0xFF001F81, 0xFF566081, 0xFF001968, 0xFF454E68, //167
            0xFF00134F, 0xFF353B4F, 0xFF0000FF, 0xFFAAAAFF, 0xFF0000BD, 0xFF7E7EBD, 0xFF000081, 0xFF565681, //175
            0xFF000068, 0xFF454568, 0xFF00004F, 0xFF35354F, 0xFF3F00FF, 0xFFBFAAFF, 0xFF2E00BD, 0xFF8D7EBD, //183
            0xFF1F0081, 0xFF605681, 0xFF190068, 0xFF4E4568, 0xFF13004F, 0xFF3B354F, 0xFF7F00FF, 0xFFD4AAFF, //191
            0xFF5E00BD, 0xFF9D7EBD, 0xFF400081, 0xFF6B5681, 0xFF340068, 0xFF564568, 0xFF27004F, 0xFF42354F, //199
            0xFFBF00FF, 0xFFEAAAFF, 0xFF8D00BD, 0xFFAD7EBD, 0xFF600081, 0xFF765681, 0xFF4E0068, 0xFF5F4568, //207
            0xFF3B004F, 0xFF49354F, 0xFFFF00FF, 0xFFFFAAFF, 0xFFBD00BD, 0xFFBD7EBD, 0xFF810081, 0xFF815681, //215
            0xFF680068, 0xFF684568, 0xFF4F004F, 0xFF4F354F, 0xFFFF00BF, 0xFFFFAAEA, 0xFFBD008D, 0xFFBD7EAD, //223
            0xFF810060, 0xFF815676, 0xFF68004E, 0xFF68455F, 0xFF4F003B, 0xFF4F3549, 0xFFFF007F, 0xFFFFAAD4, //231
            0xFFBD005E, 0xFFBD7E9D, 0xFF810040, 0xFF81566B, 0xFF680034, 0xFF684556, 0xFF4F0027, 0xFF4F3542, //239
            0xFFFF003F, 0xFFFFAABF, 0xFFBD002E, 0xFFBD7E8D, 0xFF81001F, 0xFF815660, 0xFF680019, 0xFF68454E, //247
            0xFF4F0013, 0xFF4F353B, 0xFF333333, 0xFF505050, 0xFF696969, 0xFF828282, 0xFFBEBEBE, 0xFFFFFFFF  //255
        };

        /// <summary>
        /// Initialize a new instance of the AciColor object
        /// </summary>
        /// <param name="index">The AciColor index</param>
        /// <returns>An AciColor object</returns>
        public AciColor(byte Index)
        {
            index = Index;

        }

        /// <summary>
        /// Convert current AciColor to ARGB
        /// </summary>
        /// <returns>32bit unsigned integer with the color in ARGB format</returns>
        public uint ToARGB()
        {
            if (index > 255 || index < 0)
                return 0;
            else
                return argb[index];
        }

        /// <summary>
        /// Convert any AciColor value to ARGB
        /// </summary>
        /// <param name="ACIColor">The AciColor index, from 0 to 2055</param>
        /// <returns>32bit unsigned integer with the color in ARGB format</returns>
        public static uint ACItoARGB(byte AciColor)
        {
            if (AciColor > 255 || AciColor < 0)
                return 0;
            else
                return argb[AciColor];
        }
    }
}