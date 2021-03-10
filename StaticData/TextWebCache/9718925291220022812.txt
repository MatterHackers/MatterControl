
MatterControl has a rich set of controls that you can use to manually control your printer's function and make adjustments during printing

You can find the manual controls on the far right after selecting and opening a printer.

![](https://www.matterhackers.com/r/vXKvzY)

Movement
========

The Movement section of the Controls pane allows the user to manually move the printer nozzle(s), and extrude and retract filament when no print is active. This section also serves as fine movement adjustment during an active print.

![](https://lh3.googleusercontent.com/2Yx1l2KUr7bzGr2FSES6be652ei6bVWOIx6dhjt7LC6Ia_PIB0fcv2Vltd0yFyR-EOWqQmzDrKoTUfioDHa2S3_qFQ=s0)

The homing controls allow you to home one or all axes (move it to the starting position). The **Release** button disables power to the motors, allowing you to move the printer by hand. You can select how far you want the printer to move. The printers current coordinates are shown on the bottom. You can adjust the speeds that the printer will move at by clicking the pencil icon ![Pencil-edit.png](http://wiki.mattercontrol.com/images/b/b0/Pencil-edit.png
"Pencil-edit.png").

Live Adjustment While Printing
------------------------------

While your printing is running you can use the Z+ and Z- buttons to adjust the height of the nozzle. This allows you to tune the height for a good first layer without having to restart the print. The current Z Offset is shown at the top of the Movement section. This is remembered for future prints. After printing, you can clear the Z offset by clicking the X.

![](https://lh3.googleusercontent.com/dpAFnIaaEEF3s9WHq_c94opjlwHfuQh3bPt9rWy6_V3nzhWctcDLXzDovMz4uK67EQcEm1qCW3Rmn1ygy-leX8Q1ng=w512)

Keyboard Controls
-----------------

Clicking the keyboard icon ![Keyboard\_icon.png](http://wiki.mattercontrol.com/images/d/d7/Keyboard_icon.png
"Keyboard_icon.png") allows you to move the printer with your keyboard.

![](https://lh3.googleusercontent.com/reuHxkm3XOjiX5aO0Yr0GeZqh_ZuCbv37420r7boQ_ADRl-mxcJcl0lTBzWFJa2_17sxamPu8JTtnlYC_b64oyK-Yw=s0)

<!---
| Function | Key       |
| -------- | --------- |
| Home All | Home      |
| Home X   | X         |
| Home Y   | Y         |
| Home Z   | Z         |
| X+       | ←         |
| X-       | →         |
| Y+       | ↑         |
| Y-       | ↓         |
| Z+       | Page Up   |
| Z-       | Page Down |
| E+       | E         |
| E-       | R         |
--->


Calibration
===========

The calibration section of the controls allows you to manage MatterControl's software print leveling feature. Software print leveling is only available on printers which do not use their own form of automatic print leveling.

![](https://lh3.googleusercontent.com/eCe0uWImo0urmud8MHkUkVShyfpKmUqEeSQlzyloYNb_BOFbDKBLaoNn4eOKJizpGjMCOM9wTMkQIOBc94xVe77PoETlphV75kMh4-c)

Click the pencil icon ![Pencil-edit.png](http://wiki.mattercontrol.com/images/b/b0/Pencil-edit.png
"Pencil-edit.png") to view or edit the print leveling data.

Bed Leveling
------------

Click the gear icon to open the Print Leveling Wizard. It will guide you through taking measurements of the height of the bed at various points.

Use the toggle switch to enable or disable software print leveling. Some printers are not capable of manual leveling. In this case, software print leveling cannot be disabled.

Calibrate Probe Offset
----------------------

Click the gear icon to measure the offset between your printer's probe and the nozzle. This feature is not available if your printer does not have a leveling probe.


[Macros](macros.md)
===================

Macros are snippets of saved G-Code which can be called with the click of a button instead of having to be typed repeatedly.

![](https://lh3.googleusercontent.com/0t9m7MoB4MJ8ezB5jWAmJ1cn6nHSs1egRjLKX3LZY3GKxLXFQOIErVv_LQ2PZEFnBneWG-ktf4-JJpJ1snTTSvmrCdc=s0)

See the article on [Macros](macros.md) for more information.


Fan
===

The fan control lets you turn the printer's layer cooling fan on and off, and also set the speed.

![](https://lh3.googleusercontent.com/J_vCFI0KdgZtBfcu84pG5XggUrs4zBS4-Etd8Z3aGRJsarC8Zg8mtyFXsPguoINUd6rXKKyQFxZ6GfkpkmeVBRxaVFs=s0)

You can adjust the fan speed during printing, however your adjustment will only last until the next fan speed change encountered in the G-Code.

If your printer does not have a layer cooling fan then this control is not shown.


Power Control
=============

If the printer's controller board supports it then this section allows for direct control of the printer's PSU.

![](https://lh3.googleusercontent.com/6ZfR-AEd1xkBi140AgAuPdBE6V_ceH_fcXTt3D9gKiB4jKhEF7dLWIm6iGnE2gGLaqgCAXUqeOnzxc03BiO4KjOL=s0)


Tuning Adjustment
=================

Allows for on-the-fly adjustment of speed and extrusion during a print.

![](https://lh3.googleusercontent.com/JeJSmRR2bVuTg7AQGpeBUjWFielnyhuC4R9MwxGIkwg-ZuaM-FM2jGgVsMyxTwPYi-s_Ys-u4PqXOwPankLv8um-EA=s0)

These settings are reset to 1.0 whenever MatterControl is restarted.

Speed Multiplier
----------------

The speed multiplier can be used to speed up or slow down a print. The speed multiplier applies to all types of moves. The lowest possible setting is 0.25 and the highest possible is 3.0.

Extrusion Multiplier
--------------------

During an active print, the extrusion multiplier modifies the extrusion flow rate, allowing you to increase or decrease the amount of plastic laid down.

This variable is different from that of the Extrusion Multiplier slice setting and will work in conjunction with it. For example, if the slicer setting is set at 1.06 and then the slider in Controls is used during the print and set to 1.08, the total result will be a multiplier of 1.1448 (1.06 * 1.08).


Firmware Updates
================

If your printer has an Arduino Mega 2560 based microcontroller, you can use the firmware updater to upload new firmware to it.

![](https://lh3.googleusercontent.com/3C166BaJZFQUNHtfeMaQprsOCN7RGvSs4xacVWpz-N8E0JnA26kBEc--egSQf1OOYudlYcGAKLrg3-BxTKUyQXEAPw=s0)

If your printer is officially supported by MatterHackers (for instance the Pulse), the firmware updater will automatically check your firmware version and alert you if an update is available. Click the **Update** button to do an automatic update.

You can also update the firmware on other printers but you will need to acquire a firmware image from the printer's manufacturer. The firmware image must be a compiled `.hex` file. Click the **Change** button to select a firmware image file.

The firmware updater will automatically make a backup of your printer's old firmware before uploading the new firmware. If the new firmware has problems, you can click the **Revert** button to flash the backed up firmware.

The firmware updater does *not* require MatterControl to successfully connect to your printer in order to function. This allows you to upload new firmware to a printer which currently has corrupted firmware or no firmware at all.
