/*
Copyright (c) 2014, Lars Brubaker
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MatterHackers.VectorMath;

namespace MatterHackers.MatterControl.SlicerConfiguration
{
    public class Slic3rEngineMappings : SliceEngineMaping
    {
        static List<string> hideItems = null;

        // private so that this class is a sigleton
        Slic3rEngineMappings()
        {
        }

        static Slic3rEngineMappings instance = null;
        public static Slic3rEngineMappings Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new Slic3rEngineMappings();
                    hideItems = new List<string>();
                    hideItems.Add("cool_extruder_lift");
                    hideItems.Add("support_material_create_internal_support");
                    hideItems.Add("min_extrusion_before_retract");
                    hideItems.Add("support_material_xy_distance");
                    hideItems.Add("support_material_z_distance");
                    hideItems.Add("center_part_on_bed");
                    hideItems.Add("infill_overlap_perimeter");
                    hideItems.Add("support_type");
                    hideItems.Add("infill_type");
                    hideItems.Add("create_raft");
                    hideItems.Add("z_gap");
                    hideItems.Add("bottom_clip_amount");
                    hideItems.Add("gcode_output_type");
                }
                return instance;
            }
        }

        public override bool MapContains(string defaultKey)
        {
            if (hideItems.Contains(defaultKey))
            {
                return false;
            }
            return true;
        }
    }
}
