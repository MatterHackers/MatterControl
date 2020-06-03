
Infill is the area of a print inside the perimeters. The main infill settings are in the [General](general.md) section.

* [Fill Density](general#fill-density)
* [Infill Type](general#infill-type)

This section includes advanced infill settings.

Starting Angle
==============

The angle of the infill pattern relative to the X axis. This applies to sparse infill (the pattern on the inside of the print) and dense infill (used for the solid top/bottom layers). Every other solid layer will be offset by 90°. This angle is not used when bridging.

**Recommended Baseline:** 45°  
**Units:** degrees CCW from the X axis (°)

Infill Overlap
==============

The amount the infill lines will push into the perimeter. Helps ensure that there are no gaps between the fill and the perimeters. Too much of an overlap can cause blobs or other artifacts on the surface of your print.

**Recommended Baseline:** 50%  
**Units:** millimeters (mm) or percent of nozzle diameter (%)

Fill Thin Gaps
==============

When the space between perimeters is very small (less than one nozzle diameter wide), it is not possible to fill it with normal infill. The Fill Thin Gaps feature will attempt to fill these areas with an underextruded line.

These pictures show a thin rectangle printed with Fill Thin Gaps off and on.

![](https://lh3.googleusercontent.com/WDWwNQ2Alc4aAkDmO2m6KziFfX9Hw47rcSUKO_RMRdwWm0YtZXWqqn-hzrafoq0pzE9RLEpt7PNH8pOpueuYpuY3UA=w400)
![](https://lh3.googleusercontent.com/VBW3fJnwyfdhb7WXptamufdnHx90xxx4gS5v6JcoOctQUBEmEQEZ5Ibu0vZUHhbgfTTwjc5_bc1mlId-LRFsCJK1NQ=w400)

**Recommended Baseline:** On