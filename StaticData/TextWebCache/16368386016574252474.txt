
Support material is used to hold up parts of your print while it is being printed. The printer cannot print structures in midair, since the plastic will just fall down to the bed. So any parts of your print with nothing below will require support material to hold them up.

![](https://lh3.googleusercontent.com/wHd93HItCPyqb5fHGhOd4ic9wFXksziIvAMWhH-TIQmAayB_0Uu5fzFMZpMabCJPH81B0UE9YtJfYWmhzU_o_CpvTA=s0)

Support structures are designed to be removed once the print is completed.

Support is required on overhangs more than a certain angle and where [bridging](../speed/speed#bridging) is not possible.

Support structures can be generated automatically, or you can use the design tools to create support structures manually. This allows you to place the support only where you want it.

Adding Supports
===============

Supports can be added, then edited by clicking the supports button in the toolbar then the Generate within the drop down. Once supports have been added they can be deleted or moved at any time.

![Generating Supports](https://lh3.googleusercontent.com/szS02lAmwIwE46RkNo44UFzMUJGNX67XPIw0eMDtNaZZWcZFF4LWNGURL7Wcgm4UB4Dx3DvX_7BcLyCXTSxIczp-aw)

It is worth noting that you can also create custom geometry and turn int into support by clicking the support button rather than the drop down arrow.

Analyze Every Layer
===================

If you have used supports in an older version of MatterControl and want the behavior of them generating in every place that they might be needed you can enable this is the Slice Settings. Under Support turn on Analyze Every Layer and if there are no manual supports in the scene you will have the old behavior of fully automatic supports in the scene. The details below will give you more information about controlling supports while in this mode.

## Support Everywhere

When this option is off, support structures will only be created where the bottom of the support will be touching the bed. When this option is on, supports will be created everywhere, including places where the bottom of the support is on top of part of the print.

These pictures show Support Everywhere off and on.

![](https://lh3.googleusercontent.com/lsQezUyXcoKvXC9otva7en0n1m0RFYsjfbsX_ZvAycq8hRwwz9MnW_RMD3qQM_key3UD-92lCa9uwiuLByXcJk7sPw=w200) ![](https://lh3.googleusercontent.com/U81WpnoO4fFRM7bB3Fq9CxMSTFnQrtUAmmc8v3KkhUC7WNYDD61ljP5bQK5Y210BgaT8Hj8kQUllqvEStbWBmpAY=w200)

**Recommended Baseline:** Off

## Support Percent

This controls the amount of support material that will be generated. It is similar to the support angle setting in other slicers.

The support percent threshold controls how little overlap there can be between lines on one layer and the layer below. If there is not enough overlap, then support is created in that area.

![](https://lh3.googleusercontent.com/0oWaiPwhV4FB-QKEZ0G59UzUnw42C8sZHJXPhHpE8UiHO1RzBrBuH-Nw41KZYvUnv7ghz3uskMCqZ_26LbaPOhNw)

**0%** means that support will only be created in areas where the lines do not overlap the layer below. Note that even though the setting is 0, support will still be created.

**50%** means that support will be created in areas where the lines have 50% or less overlap with the layer below.

**100%** means that support will be created everywhere no matter how much overlap there is.

**Recommended Baseline:** 30%  
**Units:** percent of extrusion width