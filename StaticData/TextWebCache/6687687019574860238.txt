
A skirt is a couple of loops drawn around the outside of the first layer of a print. They are sometimes also referred to as priming loops.

![](https://lh3.googleusercontent.com/GGH5d8Po_EtGHVhGLxrT2aaZWrm2ootlP28L99H1712gHVhWJmd5znjlBeyODiNZ6V00Rk6iyldNfujViwn_ThwrqLI=s0)

This is the very first thing that is printed. It is used to prime the nozzle and also allows you to check your leveling and nozzle height before the actual print begins. When the skirt is printing, it is a good time to adjust your nozzle height using [babystepping](../../printer-controls#movement).

Distance or Loops
=================

The number of skirt loops to draw. Alternatively, you can also specify the thickness of the skirt in millimeters.

**Recommended Baseline:** 3  
**Units:** count or millimeters (mm)

Distance From Object
====================

The gap between the skirt and the print.

**Recommended Baseline:** 3 mm  
**Units:** millimeters (mm)

Minimum Extrusion Length
========================

The minimum length of filament to use to draw the skirt. Enough loops will be drawn to use this amount of filament. This takes precedence over the number of loops. This is the measurement of the filament going into the extruder, not the length of the loops being extruded. We recommend setting this to the length of your hot end's hot zone.

We recommend using a high minimum extrusion length and few loops. This ensures that you do not waste too much time printing skirt loops on large prints, but also get enough priming on small prints.

**Recommended Baseline:** 5 mm  
**Units:** millimeters (mm)