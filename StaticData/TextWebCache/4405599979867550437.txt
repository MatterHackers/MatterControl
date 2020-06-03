
Retraction is a technique used to prevent plastic from leaking out of the nozzle when the printer is making a [travel move](../speed/speed#travel). By pulling the filament back slightly, the chamber pressure in the melt zone of the hot end is relieved. This prevents oozing and stringing, however it cannot eliminate it completely. See this article on [how to tune your retraction settings](https://www.matterhackers.com/articles/retraction-just-say-no-to-oozing).

Retractions are visible in the layer view. Red circles indicate a retraction and blue circles indicate an unretraction. The size of the circle is proportional to the length of the retraction.

![](https://lh3.googleusercontent.com/IyuZct3UGe2KMd7PmSorEFRzld7DffeV4SkrCWLd3gr-aMx2MkchH1GN98L-VC-cHoZnSqLVvngdFQzvCBbU6RqKMQ)

Retract Length
==============

The distance the filament will be pulled back. The ideal distance varies from one hot end to another. The longer the retraction, the more the oozing will be reduced, but only up to a point. Beyond that you will see diminishing returns and increased risk of jamming. This is because retracting too far will pull molten plastic into the cold zone, where it can solidify.

Generally, bowden extruder will need more retraction than direct feed extruders due to the backlash in the tube.

**Recommended Baseline:** 3 mm  
**Units:** millimeters (mm)  
**G-Code Replacement Variable:** `retract_length`

Extra Length On Restart
=======================

After the travel move, the filament will be unretracted (pushed back to the same place it was before). However, during the travel it is still likely that a small amount of plastic will have leaked from the nozzle. This means that the nozzle may not be fully primed when it starts extruding again. This can cause some gaps or unevenness in your print. The Extra Length On Restart option compensates for this by extruding a small amount of extra material in addition to the normal unretract, thus making up for whatever material may have leaked.

This setting will depend on the material you are printing, since some materials are runnier than others (less viscous) and will leak more.

**Recommended Baseline:**  
**Units:** millimeters (mm)  

Time For Extra Length
=====================

During a travel move, the plastic does not leak from the nozzle all at once. It happens over time. There will be more leakage during a long travel move than a short one. Therefor, more extra extrusion will need to be done after a long move in order to reprime the nozzle.

This setting allows you to control how much extra extrusion is done on restart based on how long the travel move took. This setting is the amount of time it take for the nozzle to fully drain. Any moves that take less than this time will get proportionally less Extra Length On Restart. If zero, the full length will always be used.

**Recommended Baseline:**  
**Units:** seconds (s)

Speed
=====

The speed the extruder will move to retract the filament.

This should be the maximum speed your extruder is capable of. This should be fast so that the pressure is relieved as fast as possible and so that the nozzle does not linger in one spot too long while the retraction is happening. This can leave a blob.

However, going to fast will cause the motor to skip steps.

**Recommended Baseline:** 35 mm/s  
**Units:** millimeters per seconds (mm/s)  
**G-Code Replacement Variable:** `retract_speed`

Z Lift
======

This function lifts the nozzle during retraction, which ensures that the nozzle will not be dragged across the top of the print during travel moves.

Generally Z Lift should not be used, however sometimes it may be beneficial to set it to 1 or 2 layer thicknesses (0.2 - 0.4 mm). This reduces the likelihood of the nozzle colliding with any buildups of plastic while traveling, which can cause layer shifting. It will also stop the clunk-clunk-clunk noise as the nozzle crosses over the infill.

**Recommended Baseline:** 0 mm  
**Units:** millimeters (mm)  

Minimum Travel Requiring Retraction
===================================

This is one of several settings that control when retractions will be performed. This setting prevents retractions from being done on short travel moves. It sets the minimum travel distance where a retraction will definitely be performed. However, retractions may still be done for shorter travel moves depending on the conditions. [Retract When Changing Islands](#retract-when-changing-islands) takes precedence over this setting.

Generally you should have [Retract When Changing Islands](#retract-when-changing-islands) and [Avoid Crossing Perimeters](../general/layers-surface#avoid-crossing-perimeters) turned on. In this case, this setting will mainly only have an effect when moving from one part of the infill to the other. Stringing will not be visible, but retractions are still beneficial here since they will ensure that the nozzle is primed when it starts printing the next infill line.

This should normally be set to a large distance, since otherwise it may lead to too frequent retraction which can cause jamming.

**Recommended Baseline:** 20 mm  
**Units:** millimeters (mm)  

Retract When Changing Islands
=============================

Islands are two areas of a layer which are not connected. If this option is on, then a retraction will be performed when the nozzle is traveling from one island to another. This setting takes precedence over [Minimum Travel Requiring Retraction](#minimum-travel-requiring-retraction) but not [Minimum Extrusion Requiring Retraction](#minimum-extrusion-requiring-retraction).

Stringing primarily occurs when traveling between islands, so it is recommended to always have this option turned on.

This picture shows a layer with two islands. The red and blue circles indicate retraction and unretraction happening as the nozzle travels between them.

![](https://lh3.googleusercontent.com/E65EfrFDhhe71Tgb9jUNGiaZ--ywuCAiUgFxyEgjoeismEZvsU_JJcdQTmEZm1N1T6dTEwOdD7Sq1HbrVbL1PjEwh_A)

**Recommended Baseline:** On  

Minimum Extrusion Requiring Retraction
======================================

This setting is used to control how frequently retractions are allowed to occur. Performing retractions too frequently can cause underextrusion (due to the nozzle not being fully primed) or jamming. This setting ensures that a certain amount of filament gets extruded before the next retraction is allowed to occur. This setting takes precedence over both [Minimum Travel Requiring Retraction](#minimum-travel-requiring-retraction) and [Retract When Changing Islands](#retract-when-changing-islands).

This setting should be set to the smallest length possible. If you have confidence in your hot end and extruder, then you can set it to 0. However if you notice your printer has trouble keeping when retractions happen frequently, then you should try increasing this setting in increments of 0.01 mm.

**Recommended Baseline:** 0.05 mm  
**Units:** millimeters (mm)

Length on Tool Change
=====================

*This option is only available if your printer has multiple extruders*

This is the distance that the filament will be retracted when switching from one extruder to another. You may want this to be longer than your normal retraction distance so that the filament is completely removed from the melt zone.

**Recommended Baseline:** 6.0 mm  
**Units:** millimeters (mm)

Extra Length After Tool Change
==============================

*This option is only available if your printer has multiple extruders*

Similar to [Extra Length on Restart](#extra-length-on-restart), this option allows you to extrude extra material after a tool change has occurred. This allows you to compensate for any leakage that may have occurred while the nozzle was inactive. It is recommended to use this option in combination with a [wipe tower or wipe shield](../general/extruder-change) to prevent it from depositing a blob on your print.

**Recommended Baseline:** 2.0 mm  
**Units:** millimeters (mm)