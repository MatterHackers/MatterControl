
*This section is only available if your printer has multiple extruders*

Wipe Shield Distance
====================

A wipe shield (or ooze shield) is a wall around your print. It protects the print from plastic leaking from the inactive nozzle while the other nozzle is printing. When the nozzle passes over the shield, any plastic drooling from it is wiped off.

![](https://lh3.googleusercontent.com/umKVkO1WJHPaxSYm3OVkIR-C5luUQuUYC_H2YCxiq-zFJ2HPjqUR7CAmwT62rLl1apXR4_izSykRaAy61e-QVJe5=s0)

A wipe shield is mainly necessary for printers with fixed dual extruders. If the printer has an independent extruder system then the wipe shield is not necessary.

This setting controls how far the wipe shield will be from the print. If set to `0`, the wipe shield will not be printed at all. The wipe shield will also not be printed if only one extruder is being used.

**Recommended Baseline:** 3 mm  
**Units:** millimeters (mm)

Wipe Tower Distance
===================

A wipe tower (or prime tower) is a structure used to prime the nozzle after switching extruders.

![](https://lh3.googleusercontent.com/Ua0cCUc6ziOlt77t2_VNT00-eyWw_Ng8nzHFVl-iCjUhl1tBiL2lbsiBkYB2ddBQ78zDeBg8-c-LJrZu8miFZAMGvlA=s0)

When switching from one extruder to another, the nozzle will go over to the tower and fill it in. This ensures that the nozzle is fully primed and leaking plastic is cleaned off before it does the actual print.

This setting specifies how wide the tower will be. If set to `0`, the tower will not be printed at all. The tower will also not be printed if only one extruder is being used.

**Recommended Baseline:** 10 mm  
**Units:** millimeters (mm)