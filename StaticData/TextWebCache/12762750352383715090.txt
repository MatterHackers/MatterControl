 
# [Complete Release Notes](release-notes.md)
 
You can find the complete release notes [here](release-notes.md)
 
# MatterControl 2.22.04 (April, 4, 2022)

[Windows Download](https://mattercontrol.appspot.com/downloads/development/ag9zfm1hdHRlcmNvbnRyb2xyOwsSB1Byb2plY3QY6gcMCxINUHVibGljUmVsZWFzZRiAgPDuk763CAwLEgZVcGxvYWQYgIDwwa6QgAsM)

[Mac Download](https://mattercontrol.appspot.com/downloads/development/ag9zfm1hdHRlcmNvbnRyb2xyOwsSB1Byb2plY3QY7AcMCxINUHVibGljUmVsZWFzZRiAgPCerp-XCAwLEgZVcGxvYWQYgIDwwc7mogkM)

**Orthographic Projection**

Orthographic Projection is an important part of many CAD workflows and we are very excited to announce that MatterControl now supports this critical mode. Thanks to fortsnek9348 who earned the $1,736 bounty for contributing this work.  There are [more bounties](https://github.com/MatterHackers/MatterControl/labels/bounty) offered and they do not all require programming.  

![](https://lh3.googleusercontent.com/o9UhPRAxIlV-9m-gfOlGC75UZnKL_ojp2Y8Qkf-UnOZSwi2HwDn00r5SOC7u72CBxu6Z-qQE5zXSbKX3ZovW5bgYsSFk2v0-2zp4oro=w220)

**Materials Library**

With the inclusion of a new materials library you can always find the right material and its settings. Dozens of materials have been tested and added and there are more on the way.

![](https://lh3.googleusercontent.com/7nE7dr-SumfT-5GmGTRTa868eVghJC8jbhImZli0mUzslVfj7DjOuW-QZnk-02__J6YzU-9W4-0TJSw_sJPqBZAMV5i82xkaDIwT-YaQ=w540)

**Color Picker**

Now it is easier than ever to set the colors of your design to be just the way you want. We have added a new eye dropper tool to the Color Picker. You can now set the color of one part by selecting another part to copy from.  

![](https://lh3.googleusercontent.com/pHjCF4ONK-GkgUM2cKx-rSb_fhUwv1HiYYKyldXvkVnTLD-qrUBpbqugjfnHEwir6b4US4G1ukbwQqXvG0af2LKxn9O45D9R5DuaNg=w540)

**Additional Improvements**
  - Added equations and cell references to component objects. This allows for creating component objects that can then be a part of a larger component.
  - Accelerated the processing of Monotonic infill (as much as 10x faster)
  - Improved Z-Calibration Wizard to have better instructions based on user testing
  - Show progress bars on export to gcode
  - Added 'Max Printing Speed' setting. Limits all printing speeds
  - Improved error and warning messages

**Bug Fixes**
  - Improved Monotonic infill pathing
  - Fixed extra segments appearing in air gap bottom layer
  - Make sure initial printing move is at the correct height
  - Validate that all parts are within the printing bounds considering raft, skirt and brim before starting a print
  - Fixed warning and error icons