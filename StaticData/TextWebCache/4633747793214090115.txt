
Create Perimeter
================

Generates a shell around the supports. This gives the support more structure and makes it stronger. Depending on your print, this can make the supports easier or harder to remove.

**Recommended Baseline:** On

Interface Layers
================

Interface layers go at the top of the supports. They are solid layers that provide a flat surface for the object to be printed on top of. Using interface layers will give you a smoother bottom surface on your print, but may make the supports more difficult to remove, especially in tight areas.

This setting controls how many interface layers are used (if any). You can specify it in a number of layers or as a thickness in millimeters.

If using interface layers, it is especially important to have a well tuned [air gap](#air-gap), otherwise you will not be able to remove the interface layers from your print.

If you have a multi extrusion printer, you can use the [support interface extruder](#support-interface-extruder) setting to choose which material the interface layers are printed with.

**Recommended Baseline:** 2  
**Units:** count or millimeters (mm)  
**See Also:** [Support Interface Extruder](support-interface-extruder)

X and Y Distance
================

The space between the side of the support structures and the side of the print. This should be large enough that the sides of the supports are not touching and bonding with the object. This ensures that only the top of the support structures are touching the print, ensuring that they are easy to remove.

**Recommended Baseline:** 2 mm  
**Units:** millimeters (mm)

Air Gap
=======

The air gap is the space between the top of the support material and the bottom of the print. Air gap is the most critical support material setting, since it controls how well the support structures will stick to your print.

The larger the air gap, the easier it will be to remove the support material. However, it will also make the bottom surface of your print messier. A smaller air gap will give a smoother, higher quality bottom surface, but also make the support material harder to remove.

Choosing the right air gap requires a lot of experimentation, and it is heavily dependent on the material you are printing. Materials that typically haver stronger interlayer adhesion, like PLA, will require a larger air gap.

Unlike other slicers, which simply skip layers, MatterControl takes a unique approach to the air gap. For the first layer of the print above the support material, the nozzle is lifted by the air gap distance. Since the nozzle is higher than it normally would be for that layer, the plastic falls a short distance onto the support material below, and cools a little while it is falling. By adjusting the air gap, you can precisely control how much the plastic cools while it falls and thus how well it sticks to the support material below.

For subsequent layers, the nozzle drops back down to the height it would be at otherwise. The air gap only affects the part of the first layer that is directly above the support material. Other parts of the layer will be printed at the normal height.

This animation shows the air gap in action. Remember that even though the first layer of the print appears to be in the middle of a higher layer, in reality the plastic will just fall down onto the support below.

![](https://lh3.googleusercontent.com/Ray4qnvUbPSbeJBPTqENVsVT4ecJSUN4EaK42bzGSySa6N87Cpxf9rbcfOQdIrZTYS5A4surELn1lnMW6_tK6GBvSA)

If you are using dissolvable support material, then the air gap should be 0. This ensures that the supports will be bonded properly and you will get the smoothest surface finish after they are dissolved.

**Recommended Baseline for PLA:** 0.6 mm  
**Recommended Baseline for ABS:** 0.4 mm  
**Units:** millimeters (mm)

Support Type
============

The geometric pattern to use for the support structures. Lines is the most common pattern and makes the support easier to remove, however Grid provides more structure.

**Recommended Baseline:** Lines  
**Options:**
* Lines
* Grid

Pattern Spacing
===============

The space between the lines of the support pattern. A smaller spacing will make the support pattern more dense. Generally you want this to be as large as possible so that it is easier to break apart the supports. Making the support structures less dense will also reduce the amount of material wasted and make the print take less time. However, it will also cause more sagging on the bottom of the print. This should be close to your printer's maximum [bridging](../speed/speed#bridges) distance.

**Recommended Baseline:** 8 mm  
**Units:** millimeters (mm)

Infill Angle
============

The angle at which the support material lines will be drawn, relative to the X axis.

**Recommended Baseline:** 45°  
**Units:** degrees CCW from the X axis (°)

Support Material Extruder
=========================

*This option is only available if your printer has multiple extruders*

Allows you to choose which material is used for printing the support structures. This allows you to use dissolvable support material in multi-extrusion printers.

Dissolvable support materials (like PVA and HIPS) are preferable because they are easier to remove and leave behind a completely smooth surface. They also allow you print more complex and intricate shapes, because the material can be dissolved out of areas that you would not be able to break it out of by hand.

Extruders are numbered starting with 1.

**Units:** extruder index

Support Interface Extruder
==========================

*This option is only available if your printer has multiple extruders*

Allows you to choose which material is used for printing the [interface layers](#interface-layers). This allows you to conserve dissolvable support material by using it only for the interface layers instead of for the entire support structure.

Extruders are numbered starting with 1.

**Units:** extruder index