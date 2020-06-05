
Cooling is very important for print quality. If a layer does not have enough time to cool and harden before the next one starts printing, then it will deform. In order to prevent this, MatterControl can slow down the printing speed of layers which would normally take very little time to print. This gives the layer more time to harden. It increases the time it takes to print small objects but it can also substantially improve print quality.

If you have inadequate cooling it will be most apparent on overhangs and sharp corners. Overhangs will tend to curl up and become very messy underneath. Sharp corners will become rounded and blunt.

These settings control when layers are slowed down and by how much. In addition to the cooling slowdown, you should also use a [layer cooling fan](../filament/fan) if your printer is equipped with one.

Note: The slicer will not change the speeds by more than 10% from one layer to another. This is to prevent a velocity painting effect. Abrupt changes in speed can make layers look different from each other. This is why you may notice it takes a couple of layers for the speed to ramp up or down.

Slow Down If Layer Print Time Is Below
======================================

The minimum amount of time a layer must take to print. If a layer takes less time than this, then all printing speeds will be slowed down until the layer takes at least this much time.

This setting is heavily material dependant. Some materials need lots of time to cool while others do not need any.

**Recommended Baseline for PLA:** 30 s  
**Units:** seconds (s)

Minimum Print Speed
===================

The minimum speed that the printer can go when the layer is slowed down for cooling. The slicer will try to slow the printer so that each layer takes the minimum layer time (above), but it will not slow any print speeds lower than this speed.

This prevents the printer from going so slow that the plastic is not extruded consistently.

**Recommended Baseline:** 15 mm/s  
**Units:** millimeters per second (mm/s)  
**G-Code Replacement Variable:** `min_print_speed`