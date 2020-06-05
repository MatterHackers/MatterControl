
*The following settings are only available if you are using a laser powered SLA (stereolithography) printer.*

With laser SLA printers, the printing speed and the laser power are both used to control how much light the resin is exposed to. Different types of resin require different amounts of light in order to cure.

Thinner layers can be printed faster, since they have less material to cure. Thicker layers must be printed slower.

MatterControl chooses the correct speed based on your layer height. The settings let you specify the speed that works best for your resin at two common layer heights (25 microns and 100 micros). If your layer height is different from these two, the speed is interpolated between them.

Speed at 0.025 Height
=====================

This is the speed the laser point will travel when the layer thickness is 0.025 mm (25 microns).

The actual speed will be calculated based on your layer thickness, by interpolating between this and the speed at 0.1 mm.

**Units:** mm/s

Speed at 0.1 Height
===================

This is the speed the laser point will travel when the layer thickness is 0.100 mm (100 microns).

The actual speed will be calculated based on your layer thickness, by interpolating between this and the speed at 0.025 mm.

**Units:** mm/s

Initial Layer Speed
===================
See [Initial Layer Speed](speed#initial-layer-speed).

Travel
======
See [Travel](speed#travel).

Initial Layers
==============

The number of layers at the beginning of the print which will be printed slower than the others.

**Units:** count or millimeters (mm)