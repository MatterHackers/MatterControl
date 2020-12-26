
*This section is only available if your printer has a layer cooling fan*

Adequate cooling is very important for achieving high quality prints. A layer cooling fan is used to cool off the plastic as soon as it comes out of the nozzle. This ensures that it hardens quickly and holds it's shape. If a layer above is being printed while the layer below is still soft, it will deform. If you have inadequate cooling it will be most apparent on overhangs and sharp corners. Overhangs will tend to curl up and become very messy underneath. Sharp corners will become rounded and blunt.

Fan settings are heavily dependent on the material you are printing. Some materials, like PLA, respond very well to cooling. For PLA you will want the fan running basically all the time. However, other materials do not react well to cooling. For instance, with ABS it can cause the layers to split apart from each other (delamination). In this case you only want to use the fan when absolutely necessary.

MatterControl controls the speed of your layer cooling fan based on how long each layer will take to print. Layers with a small area will take less time to print. Thus, there will be less time for them to cool off and harden before the layer above is printed. So more fan power will be necessary. MatterControl's layer view will show you how long a particular layer takes to print, and the fan speed used for that layer.

This graph shows an example fan speed profile.

![](https://lh3.googleusercontent.com/0YsAZ2INXEKL5uR6Tto9_U0zsj8krLjvsn2npMAKcqRII0wg_6MsP6vZ_JEiwJMiuiGU1oXXvqvvlYOC__yunxYJ=s0)


Turn On If Below
================

This is the maximum layer time where the layer cooling fan will be activated. The fan will run at the [minimum speed](#minimum-speed) if a layer takes this long to print. In the example above this setting is 180 seconds.

**Recommended Baseline for PLA:** 180 s  
**Units:** seconds (s)

Run Max If Below
================

This is the maximum layer time where the layer cooling fan will run at [maximum speed](#macimum-speed). If a layer takes longer than this to print, the fan will still run but it will be throttled down. In the example above this setting is 60 seconds.

**Recommended Baseline for PLA:** 60 s  
**Units:** seconds (s)

Minimum Speed
=============

This is the lowest speed the fan will go when it is turned on. If not enough power is applied to the fan, it will not turn on at all. For this reason, most printers are not capable of running their fan at very low speeds. This should be set to the minimum speed that you know your fan will work at. If you are unsure, you may try experimenting with different fan speeds using the [manual controls](../../printer-controls#fan) to see how high you have to go before the fan starts spinning.

In the example above the minimum fan speed is 30%.

**Recommended Baseline:** 30%  
**Units:** percent (%)  
**G-Code Replacement Variable:** `min_fan_speed`

Maximum Speed
=============

This is the fastest speed the fan will go when it is turned on. This should be set to 100% unless you have an extremely powerful fan.

**Recommended Baseline**: 100%  
**Units:** percent (%)  
**G-Code Replacement Variable:** `max_fan_speed`

Bridging Fan Speed
==================

This is the fan speed that will be used during [bridging](speed#bridges). This overrides the other fan speed settings. In most cases you will want to use as much cooling as possible during bridging in order to make sure that the lines harden as soon as possible. This draws them tight (due to thermal contraction) and prevents them from drooping.

**Recommended Baseline:** 100%  
**Units:** percent (%)  
**G-Code Replacement Variable:** `bridge_fan_speed`

Disable Fan For The First
=========================

The number of layers at the start of the print where the fan will not be turned on. Generally, for materials that require layer cooling, it’s a good idea to disable for the first layer in order to ensure good bed adhesion.

**Recommended Baseline:** 1  
**Units:** count