
Extruder Wipe Temperature
=========================

This setting is only for printers which have a nozzle wiping procedure that is done before printing. It is the temperature that the nozzle will be heated to before wiping. The wiping procedure must be scripted in the printer's [Start G-Code](../../printer-settings/gcode/gcode#start-gcode).

**Recommended Baseline:** 145 °C  
**Units:** °C  
**G-Code Replacement Variable:** `extruder_wipe_temperature`

Bed Remove Part Temperature
===========================

*This option is only available if your printer has a heated bed*

The temperature to which the bed will heat (or cool) in order to remove the print. This setting is only for printers which are set up to hold the bed at a certain temperature after a print. This must be scripted in the printer's [End G-Code](../../printer-settings/gcode/gcode#end-gcode).

**Recommended Baseline:** 50 °C  
**Units:** °C  
**G-Code Replacement Variable:** `bed_remove_part_temperature`

Extrusion Multiplier
====================

This setting can be used to purposely extruder more or less material than normal. If you find that your prints are overextruded or underextruded, you can use the extrusion multiplier to correct this. Beware that using this setting should only be considered a stopgap measure, not a final solution. Overextrusion and underextrusion is a sign that some other setting is wrong or there is some problem with the printer. Most likely you should try [calibrating your extruder](https://www.matterhackers.com/articles/how-to-calibrate-your-extruder).

The extrusion can also be adjusted on-the-fly using the tuning adjustments on the controls page. Beware that the tuning adjustment is a separate setting and will be compounded with this setting.

**Recommended Baseline:** 1.0  
**Units:** decimal  

First Layer
===========

Controls the width of the lines for the first layer of the print. Increasing this value will cause the lines to be wider and also spaced out farther, so there will be no overextrusion. Using this can help with bed adhesion, since fatter lines will have more surface area to stick to the bed.

**Recommended Baseline:** 100%  
**Units:** millimeters (mm) or percent of nozzle diameter  

Outside Perimeters
==================

Controls the line width of the outside perimeter loops. Can be useful to fine-adjust actual print size when objects print larger or smaller than specified in the digital model.

**Recommended Baseline:** 100%  
**Units:** millimeters (mm) or percent of nozzle diameter  