
Initial Layer Speed
===================

The speed at which the nozzle will move when printing the first layer. The first layer typically requires slower than normal print speeds for best bed adhesion. This affects the first layer of the print as well as the first layer of the raft, if there is one.

For SLA printers this speed can be applied to more than just the first layer. See [Initial Layers](sla-speed#initial-layers).

**Recommended Baseline:** 20 mm/s  
**Units:** millimeters per second (mm/s) or percent of [infill speed](#infill-speed) (%)  
**G-Code Replacement Variable:** `first_layer_speed`

Infill
======

The speed at which [infill](../general/infill.md) will print. Infill can be printed faster than any other part of the print. Most other speeds can be specified as a percentage of the infill speed. Generally when people refer to a print being done at a certain speed, they are referring to the infill speed.

**Recommended Baseline:** 60 mm/s  
**Units:** millimeters per second (mm/s)  
**G-Code Replacement Variable:** `infill_speed`

Top Solid Infill
================

This is the speed used for the infill on the last of the [top solid layers](../general/general#top-solid-layers). This can be done slower than the regular infill in order to improve the quality of the surface finish.

**Recommended Baseline:** 75%  
**Units:** millimeters per second (mm/s) or percent of [infill speed](#infill-speed) (%)

Raft
====

The speed used for printing a [raft](../adhesion/raft.md), if you are using one. The first layer of the raft will still be printed at the [initial layer speed](#initial-layer-speed). This setting only applies to the other layers.

**Recommended Baseline:** 30 mm/s  
**Units:** millimeters per second (mm/s) or percent of [infill speed](#infill-speed) (%)

Inside Perimeters
=================

The speed of the inner [perimeter loops](../general/general#perimeters), if you have more than one perimeter. These are not visible when the print is finished. They can generally be printed faster than the outer perimeter, but should probably be slightly slower than infill.

**Recommended Baseline:** 50 mm/s  
**Units:** millimeters per second (mm/s)  
**G-Code Replacement Variable:** `perimeter_speed`

Outside Perimeter
=================

Perhaps the most important speed setting. This is the speed for the outermost perimeter loop, which is what you see and feel when a print is complete. We recommend printing slow in order to ensure the best quality. It doesn’t add much print time to print slow outside perimeters, but can significantly improve print quality and surface finish.

**Recommended Baseline:** 75%  
**Units:** millimeters per second (mm/s) or percent of [inside perimeter speed](#inside-perimeters) (%)  
**G-Code Replacement Variable:** `external_perimeter_speed`

Support Material
================

The speed at which [support material](../support/support.md) structures will print. You may wish to print this slower than the infill speed in order to ensure that the support structures are sturdy and do not break or fall over during printing.

**Recommended Baseline:** 40 mm/s  
**Units:** millimeters per second (mm/s)  
**G-Code Replacement Variable:** `support_material_speed`

Bridges
=======

Bridges are sections of a print which have nothing underneath them, but are supported on either side.

![](https://lh3.googleusercontent.com/adxGgEkZ1fLOS34hl6q7a6z9jDUrPpACuKKeT-iICsDXhR2fNRwQr4bTMf2dG9kVCyea9yKFigFT6mEIwz7syClSLQ)

These parts can be printed without the use of support material because the plastic can be drawn from one side to the other like a spider web. Longer bridges are obviously more difficult than shorter bridges, but it is not uncommon to bridge up to 50 mm with good results.

Bridging is tricky. In order for it to work the bridge needs to be printed at the right speed, and there must be adequate cooling. In addition, your material, temperature, nozzle diameter, and layer thickness all have an effect. Some materials print better with slow bridge speeds and some print better quickly.

**Recommended Baseline for PLA:** 25 mm/s  
**Units:** millimeters per second (mm/s)  
**G-Code Replacement Variable:** `bridge_speed`  
**See Also:** [Bridging Fan Speed](../filament/fan#bridging-fan-speed), [Bridge Over Infill](#bridge-over-infill)

Travel
======

The speed the nozzle will go when moving from one part of the print to another, without extruding material. The travel speed is one of the most important settings for reducing stringing and oozing. This should be set as fast as your printer can go. 80 mm/s is safe for most printers, however many printers (such as deltas or others with bowden extruders) can go as high as 200 mm/s.

Since travel moves are the fastest moves the printer makes, they are the most likely to cause layer shifting. If you are experiencing layer shifting you may consider reducing the travel speed.

**Recommended Baseline:** 80 mm/s  
**Units:** millimeters per second (mm/s)  
**G-Code Replacement Variable:** `travel_speed`

Bridge Over Infill
==================

When this setting is on, [bridging speed](#bridges) and [fan](../filament/fan#bridging-fan-speed) settings will be used when printing the first solid top layer over infill. This setting will reduce the number of [top solid layers](../general/general#top-solid-layers) you need in order to have a smooth top surface with no gaps. This is especially true if you have a low [fill density](../general/general#fill-density), so there are large spaces in the infill pattern.

**Recommended Baseline:** On