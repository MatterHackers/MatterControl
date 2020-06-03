
A raft is a removable layer printed underneath your object.

![](https://lh3.googleusercontent.com/zFUGCRoafVe-xGgdjYVp06gb1Wjyh3zaNnb3QPYYTpCpDRqnui6HCgjZXIDLlxWdcQeWoiYZNsdLhTHsCTxwBN6g=s0)

A raft can serve two purposes.

1. It provides better bed adhesion for difficult materials. The raft uses very thick lines on the bottom, which allow help it stick to the bed. The object you are printing will stick to the raft well because they are made of the same material.

2. It can help if you are having difficulty with bed leveling. Because the first layer of the raft is much thicker than normal, it allows for more variation in the height of the bed. 

In both of these cases the raft should be considered a last resort. Rafts take a long time to print, waste a lot of material, and result in a rougher finish on the bottom of your print. In addition, it is tricky to get the raft settings right so that it can be easily removed when the print is finished.

The best solution for bed adhesion is to [find a bed surface that works better](https://www.matterhackers.com/news/choosing-the-right-3d-print-bed-surface) with the material you are printing. For leveling problems, you should try using MatterControl's software print leveling first.

Expand Distance
===============

This is how far the raft will extend beyond the edge of your print. Making a larger raft will provide more adhesion and help prevent warping.

**Recommended Baseline:** 3 mm  
**Units:** millimeters (mm)

Air Gap
=======

The distance between the top of the raft and the first layer of your print. The ideal raft provides good bed adhesion while printing and then easily peels off once finished. The Air Gap determines how easy it is to peel the raft off your part. Too much of a gap and your part may not stick well to the raft. Too little, and the raft will be very difficult to remove. Material, nozzle diameter, and layer height all affect the results. A good starting point is ½ your nozzle diameter. So, if you have a .4mm nozzle, start with a 0.2mm Air Gap. In general, materials with excellent interlayer adhesion – like nylon and Ninjaflex – require a larger air gap.

**Recommended Baseline:** 0.2 mm  
**Units:** millimeters (mm)  
**See Also:** [Support Air Gap](../support/advanced#air-gap)

Raft Extruder
=============

*This option is only available if your printer has multiple extruders*

The extruder to use for printing your raft. This allows you to choose which material your raft will be made of. Generally it is best to make the raft with dissolvable support material, if you have it. Extruders are numbered starting with `1`. If you set this option to `0` then it will automatically use your [support material extruder](../support/advanced#support-material-extruder).

**Recommended Baseline:** 0  
**Units:** extruder index  
**See Also:** [Support Material Extruder](../support/advanced#support-material-extruder)