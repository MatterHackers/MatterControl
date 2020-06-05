
Diameter
========

The diameter of the filament you are printing with. This is normally either 1.75 mm or 2.85 mm.

Even though the filament is made to be a certain diameter, in reality it can vary by ±0.1 mm. Any variation in the diameter of the filament will cause slight overextrusion or underextrusion. This can have a major impact on the dimensional accuracy of the objects you are printing. Because of this, we recommend actually measuring the filament you are using with a set of calipers. 

The diameter of the filament can also vary along the length of a single spool, so it is best to measure the filament in several different spots and take an average.

**Recommended Baseline:** 1.75 mm or 2.85 mm  
**Units:** millimeters (mm)  
**G-Code Replacement Variable:** `filament_diameter`

Density
=======

The density of the material you are printing, in g/cm³. This setting has no effect on slicing. It is only used to estimate the mass and cost of the material used for a print. These estimates are shown in the layer view.

Densities for common materials are listed here. If you have a specialty material, you should be able to get the density from the manufacturer's technical specifications.

![](https://lh3.googleusercontent.com/TfSrsIuV876dy5AsgZNsGRzIodccewBY6pmvPh2JsKBKdUe1n5QNJBi-CpgcJDRAwsO1FAlfDouL1YFT21xcVbt7DQ=s0)
<!---
| Material | Density (g/cm³) |
| -------- | --------------- |
| PLA      | 1.24            |
| PETG     | 1.27            |
| ABS      | 1.04            |
| Nylon    | 1.14            |
| TPU      | 1.20            |
| HIPS     | 1.05            |
| PVA      | 1.23            |
--->

**Units:** g/cm³

Cost
====

The cost of one kilogram of filament. This setting has no effect on slicing. It is used to estimate the cost of a print, which is shown in the layer view. If this value is set to 0, then the estimate will not be shown.

**Units:** $/kg

Extruder Temperature
====================

The temperature of the hot end. This setting can also be found in the extruder controls. If the hot end is on, changing this setting will have an immediate effect.

Each type of filament extrudes at a different temperature. See our [Filament Comparison Guide](https://www.matterhackers.com/3d-printer-filament-compare) for recommended settings for different materials. It is important to remember that these recommendations are just a starting point, and you may have to experiment to find the best temperature for your particular filament and printer. See this article on [adjusting your settings to get the best flow](https://www.matterhackers.com/news/how-to-get-the-best-3d-printed-parts-by-understanding-extrusion-settings).

Higher temperatures will improve interlayer adhesion, making the print stronger. The plastic will also be able to flow better, but this will also increase stringing and oozing. If the temperature is too high, then the materials will start to burn and may jam your hot end.

Lower temperatures will make the plastic more viscous, which can reduce or eliminate stringing and oozing. It can also improve detail slightly. However it will also make the material harder to extrude. Too low and the plastic will not extrude at all. Lower temperatures will also reduce how well the layers bond to each other, making the print weaker and also causing splitting between the layers.

There are a number of other factors which will affect the proper printing temperature as well.

Each type of printer is different, and may be slightly better or worse at transferring heat to the filament. Different types of printers may also read different temperatures in the same situation due to different placement of the sensor.

Even among the same types of material, different spools may require different temperatures because they are different colors or are made by different brands. We have noticed that the lighter colors (especially white) use the most pigment, and so may need to be printed slightly hotter than others.

You may also need to increase the temperature in high flow situations. For instance, printing exceptionally fast or using a very large nozzle.

*Multi Extrusion Printers:* The temperatures for each hot end can be set separately using the extruder controls. 

**Units:** °C  
**G-Code Replacement Variable:** `temperature`

Bed Temperature
===============

*This option is only available if your printer has a heated bed*

The temperature of the heated bed. This setting can also be found in the bed controls. If the bed is on, changing this setting will have an immediate effect.

Heating, along with a proper bed surface, is essential for ensuring that your prints stick to the bed. All plastic undergoes thermal contraction, to varying degrees. This causes prints to shrink, warp, and peel up off of the bed as they cool. In order to prevent this, most printers are equipped with a heated bed in order to keep the print warm until it is finished.

The ideal bed temperature is different for each type of filament. See our [Filament Comparison Guide](https://www.matterhackers.com/3d-printer-filament-compare) for recommended settings for different materials. It is important to remember that these recommendations are just a starting point, and you may have to experiment to find the best temperature for your particular filament and printer. Generally, the bed temperature should be close to the material's glass transition temperature.

If this setting is set to 0 then bed heating will not be used.

**Units:** °C  
**G-Code Replacement Variable:** `bed_temperature`