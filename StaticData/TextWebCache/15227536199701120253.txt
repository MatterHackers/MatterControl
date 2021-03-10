# MatterControl 2.21.2 (February, 5, 2021)
[Windows Download](https://mattercontrol.appspot.com/downloads/development/ag9zfm1hdHRlcmNvbnRyb2xyOwsSB1Byb2plY3QY6gcMCxINUHVibGljUmVsZWFzZRiAgLCHpunuCwwLEgZVcGxvYWQYgICwr_uutAgM)

[Mac Download](https://mattercontrol.appspot.com/downloads/development/ag9zfm1hdHRlcmNvbnRyb2xyOwsSB1Byb2plY3QY7AcMCxINUHVibGljUmVsZWFzZRiAgLDbz-iWCQwLEgZVcGxvYWQYgICw74qr3QoM)

## Changes

- Features
  - Made it possible to change material colors
- UI improvements
  - Added ctrl-a in library views
  - New color control  
  ![Color Control](https://lh3.googleusercontent.com/ej0HAxqEwc9HYJ_B7xgLQUhlaf9BTV9r-5_2WelGKwPQOiqp5DbyPyL4VCn_UKpWNyony5ToPHtH2EBMz6RiZ7LslWMMNx2oKVrjmHU)
- Bugs
  - Added try re-connectting to connection error message
  - Changing settings details (simple, intermediate, advanced) does not close settings when floating
  - Fixed rendering error on recent items


# MatterControl 2.20.12 (December, 21, 2020)
[Windows Download](https://mattercontrol.appspot.com/downloads/development/ag9zfm1hdHRlcmNvbnRyb2xyOwsSB1Byb2plY3QY6gcMCxINUHVibGljUmVsZWFzZRiAgLCBn6bUCgwLEgZVcGxvYWQYgICwq6WDlgkM)

[Mac Download](https://mattercontrol.appspot.com/downloads/development/ag9zfm1hdHRlcmNvbnRyb2xyOwsSB1Byb2plY3QY7AcMCxINUHVibGljUmVsZWFzZRiAgNDOtcqiCAwLEgZVcGxvYWQYgICwq6bs7AoM)

## Changes

- Features
  - Mac build working again
  - Custom printer setup wizard
  - Set baud rate in manual printer connect window
- UI improvements
  - Improved print completion dialog
  - Implemented GLFW backend
  - Improved base editing
- Bugs
  - Fixed export bug with G92 E0
  - Circular bed texture
  - Thin walls fix for single perimeter


# MatterControl 2.20.10 (October, 5, 2020)
[Windows Download](https://mattercontrol.appspot.com/downloads/development/ag9zfm1hdHRlcmNvbnRyb2xyOwsSB1Byb2plY3QY6gcMCxINUHVibGljUmVsZWFzZRiAgLDkoJKuCAwLEgZVcGxvYWQYgICw7KuIqgsM)

Mac Download - Coming Soon

## Changes

- Features
  - Improved design icons
  - Accelerated Slicing
  - Icons for Printer Parts libraries
  - Added a simple measure tool
- UI improvements
  - Better support of high res devices
  - Added all supported operations to part right click menu
  - Add warning for connected to emulator
  - Add warning for bad leveling data
- Bugs
  - Fixed slicing issue with fill thin gaps
  - Fixed settings update bug with probe offset


# MatterControl 2.20.9 (September, 4, 2020)
[Windows Download](https://mattercontrol.appspot.com/downloads/development/ag9zfm1hdHRlcmNvbnRyb2xyOwsSB1Byb2plY3QY6gcMCxINUHVibGljUmVsZWFzZRiAgLDAyMfcCAwLEgZVcGxvYWQYgICw4IPd2QkM)

## Changes

- Features
  - Added export as AMF
  - Added baby stepping for extruder 2 (Dual Extrusion)
  - Printer settings are scanned for for updates to defaults
- UI improvements
  - Improve discovery of entering share codes
  - Default options for history data notes
  - Right click settings menu has more options
- Bugs
  - Settings override display always shows correct colors
  - Load / Unload ignore extrusion multiplier
  - fixed icon color problems
  - restore small icon rendering


# MatterControl 2.20.8 (August, 4, 2020)
[Windows Download](https://mattercontrol.appspot.com/downloads/development/ag9zfm1hdHRlcmNvbnRyb2xyOwsSB1Byb2plY3QY6gcMCxINUHVibGljUmVsZWFzZRiAgNC94uviCwwLEgZVcGxvYWQYgIDQx_-uqggM)

## Changes

- Z Offset can be adjusted from progress display  
 ![Z Offset](https://lh3.googleusercontent.com/KaUGC0WSnR7P0rH4xNaqbvL2D_hskJA3OI6AQ0nJPvRqc5RsZMhz6jRxKwCixKBbN59DL_-HrAE7TMMQpRcm6Ms6yA=w320)


- Improved Arrange All  
 ![Arrange All](https://lh3.googleusercontent.com/D-q-Ulc2rWT6ymOx_5I-PlXGtoY2VaZDC71_62hq0kR0-tbEuabQPK56swRomIEzYZANQlbA3UDI3_xoLDbJw2pBTt8=w420)

- Added Gyroid infill  
![Gyroid Layers](https://lh3.googleusercontent.com/eqD330J6VC_Uegiq3Ic8KXvI6Syex7X66CbynkcSQKvy2ijhA91yQEFICOzXi-ZpWB00KNUxCuQQJXS804jVX7uSFQ=w320) ![Gyroid Settings](https://lh3.googleusercontent.com/qa1OE9KbLGQl04M7OhWBeCUbDOEiy2OU_jOjMHbYkNS68GxmxRcd34YMsy2blAfR8vY_X5FA3jL5_QtIdw90NpvIw9U=w220)


- Print History shows printer used and can collect quality data  
![History](https://lh3.googleusercontent.com/ijW8Et-CsdlrsAnStclAbBy1U-BRGAECD_skC8z_xqOTtFk_5LVeILgl-oi69RfOiLWB5zFLcqy_J67pdaNLK3f00Q)

  - Pro feature added to export to .csv file


- UI improvements
  - Easier to find and create Share Codes
  - Easier to enter Share Codes
  - Cut / Copy / Past available on all text controls
  - Tabs can shrink when too big for tool bar
- Bugs
  - First perimeter on 2nd extruder when only material 2 used
  - Export X3G does not fail on warning
  - Settings name not saved on close

# MatterControl 2.20.6 (June, 10, 2020)
[Windows Download](https://mattercontrol.appspot.com/downloads/development/ag9zfm1hdHRlcmNvbnRyb2xyOwsSB1Byb2plY3QY6gcMCxINUHVibGljUmVsZWFzZRiAgNC13P-aCgwLEgZVcGxvYWQYgIDQ7ZiYiQoM)

## Changes

- Added [MatterControl Pro Edition](https://www.matterhackers.com/store/l/mattercontrol-pro-edition) upgrade option
  - Get MatterHackers Professional Support
  - Help support the MatterControl community
  - Access to pro only tools
  - Unlimited cloud storage
  - Added Threads feature
- Added  Chinese and Japanese translations
- Better handling of self intersecting and bad winding in parts
- Improved bridging detection and handling
- UI improvements
  - Toolbar icon contrast and design
  -	Design tab sizing
  - Layout of properties panel
  - TreeView keyboard navigation
  - Image Converter weighted centering
- Bugs
  - Don’t move to origin at start of print
  -	Disable Print button while printing
  -	Heating T1 when only printing support

# MatterControl 2.20.4 (April, 4, 2020)
[Windows Download](https://mattercontrol.appspot.com/downloads/development/ag9zfm1hdHRlcmNvbnRyb2xyOwsSB1Byb2plY3QY6gcMCxINUHVibGljUmVsZWFzZRiAgNDitZ7GCgwLEgZVcGxvYWQYgIDQromy-AoM)

[Mac Download](https://mattercontrol.appspot.com/downloads/development/ag9zfm1hdHRlcmNvbnRyb2xyOwsSB1Byb2plY3QY7AcMCxINUHVibGljUmVsZWFzZRiAgNDI4MDrCwwLEgZVcGxvYWQYgIDQzuy_-QsM)

## Changes

* Added Hollow Feature  
 ![Hollow Example](https://lh3.googleusercontent.com/-ImcYYK1I3P7tvxJXLRYDitBkc2xfXD0mElN3tiX8mZk1-Qe0Gxm5TtXXzC-Er756XajqOPpu7HFEuflNCnbZZqEzg=w220) ![Hollow Menu](https://lh3.googleusercontent.com/JiCUdiJx0eboPJk2cQH3dMOvlrFsFcz7OK-v9nG3G8ztDDHovXw--xaDsN8-HbFhFfAz5jSFKHUNQwnee5WXRNApH2M=w120)
* Added Polygon Reduce  
![Reduce Options](https://lh3.googleusercontent.com/h6opzhbdA352u9JFtIcqPnrnJC4JjcoVehdFstGZHe1gu7qiupQ8KAYrngTORjSyUerGlxhX48sGHLlwF2AoPjG0ifw=w220) ![Reduce Menu](https://lh3.googleusercontent.com/Pw2RYm45dFljKfmAq65378bpwULWxH857_Gz_SB95JLsmQYF3YmhOJ-XFEtWqWcFcK4weNLmz2hnVggk_85jWFDE=w120) 
* Added Mesh Repair  
 ![Repair Options](https://lh3.googleusercontent.com/C-fT1jQ-z1oOU1uBzWNLCN2IsAGOGAmJdhmUKqQLhC3p9_WdeKFDNKSoTGb4U8RRDdYk2ZRbWJ2FbjfNKzo6ii6v=w220) ![Repair Menu](https://lh3.googleusercontent.com/uQ8uaWvzremfTd7jkSu7OhKURHfvyEAFtbT1_KaTL1wgSrSUOjjQ0tm1a6uROpe6JZwC50HvdB4bJcGq8XqGAUMwmg=w120) 
* Put in fully automatic support (legacy support) as an option in addition to new manual support option
* Added Support for gsSlicer (Experimental new slicing engine)
* Fixed bugs

# MatterControl 2.19.10 (September, 27, 2019)
[Windows Download](https://mattercontrol.appspot.com/downloads/development/ag9zfm1hdHRlcmNvbnRyb2xyOwsSB1Byb2plY3QY6gcMCxINUHVibGljUmVsZWFzZRiAgJDX6u_ICAwLEgZVcGxvYWQYgIDQsJO5jggM)

[Mac Download](https://mattercontrol.appspot.com/downloads/development/ag9zfm1hdHRlcmNvbnRyb2xyOwsSB1Byb2plY3QY7AcMCxINUHVibGljUmVsZWFzZRiAgJDnoZqsCwwLEgZVcGxvYWQYgIDQiLCwtAgM)

## Changes

* Dual Extrusion Improvements
    * Made the default wipe tower round
    * Made un-retract after tool change able to be negative
    * Made custom wipe towers follow the geometry defined by the user
* Improved ungrouping of mesh (splitting into multiple meshes)
    * Discard degenerate faces
    * Discard microscopic discrete features
* Fixed Bugs
    * Export STL when no printer has been created
    * Export STL on Mac
    * Arrange all when no printer has been created and the bed is empty

# MatterControl 2.19.7 (July, 15, 2019)
[Windows Download](https://mattercontrol.appspot.com/downloads/development/ag9zfm1hdHRlcmNvbnRyb2xyOwsSB1Byb2plY3QY6gcMCxINUHVibGljUmVsZWFzZRiAgJDb5NzbCAwLEgZVcGxvYWQYgICQp9-hnAgM)

[Mac Download](https://mattercontrol.appspot.com/downloads/development/ag9zfm1hdHRlcmNvbnRyb2xyOwsSB1Byb2plY3QY7AcMCxINUHVibGljUmVsZWFzZRiAgJCbheGPCgwLEgZVcGxvYWQYgICQp7mfqgsM)

## Changes

* Added search bar for application
    * ![Search](https://lh3.googleusercontent.com/pAN6dqaGJJZs0cVZZDtkY40IlLXeoHNFmoovzivkGdhzCwN65wuqQdYvguoVo7SewCNl33mbLMd__OVw6BJhhV1n)
* Improved design tool bar
    * Added grouping to some items
    * Added dual align button
    * Added Arrange All button
* Nudge items on the bed with arrow keys
* Downloads folder is sorted by date
* Sped up dual extrusion calibration prints

# MatterControl 2.19.6 (June, 10, 2019)
[Windows Download](https://mattercontrol.appspot.com/downloads/development/ag9zfm1hdHRlcmNvbnRyb2xyOwsSB1Byb2plY3QY6gcMCxINUHVibGljUmVsZWFzZRiAgICj88eNCQwLEgZVcGxvYWQYgICQ6-PE6woM)

[Mac Download](https://mattercontrol.appspot.com/downloads/development/ag9zfm1hdHRlcmNvbnRyb2xyOwsSB1Byb2plY3QY7AcMCxINUHVibGljUmVsZWFzZRiAgIDYrveUCgwLEgZVcGxvYWQYgICQq76l2AkM)

## Changes

* Printer setup dramatically improved with new unified experience
    * Show outstanding tasks and progress
    * Leveling visualization
* Dual extrusion improvements 
    * New Nozzle Calibration Wizard (for calibrating dual extrusion printers)
    * Support for custom wipe tower shapes
    * Improved support material detection
* UI improvements
    * Faster updates in Cloud Library folders
    * Restore UI on re-open
    * Better Keyboard navigation support
* New error detection and warning system
    * More hardware errors handled
* Design tools improvements and optimizations
    * New Twist tools 
    * Improved Curve tool
    * Improved Align

# MatterControl 2.19.2 (February, 6, 2019)
[Windows Download](https://mattercontrol.appspot.com/downloads/development/ag9zfm1hdHRlcmNvbnRyb2xyOwsSB1Byb2plY3QY6gcMCxINUHVibGljUmVsZWFzZRiAgICt8ISVCgwLEgZVcGxvYWQYgICA_ZPAggoM)

## Changes

* Fixed bugs with exporting G-Code
* Improved flatten
* Improved Undo support
* Improved design history

# MatterControl 2.19.1 (January, 2, 2019)
[Windows Download](https://mattercontrol.appspot.com/downloads/development/ag9zfm1hdHRlcmNvbnRyb2xyOwsSB1Byb2plY3QY6gcMCxINUHVibGljUmVsZWFzZRiAgIC17KWBCgwLEgZVcGxvYWQYgICAzeeZlQoM)

[Mac Download](https://mattercontrol.appspot.com/downloads/development/ag9zfm1hdHRlcmNvbnRyb2xyOwsSB1Byb2plY3QY7AcMCxINUHVibGljUmVsZWFzZRiAgICY9dKCCgwLEgZVcGxvYWQYgICA2Oq1kAoM)

## Changes
* Versioning: Moving to a (version).(year).(month) version number. Easier to read and more informative.
* Multi-printer control
* A single instance of MatterControl can now run multiple printers simultaneously
* New State-of-the-art Subtract, Combine and Intersection (Window only)
* We now start up with a 'Feature Tour' to help new users find their way

# MatterControl 2.0.0 (November, 19, 2018)
[Windows Download](https://mattercontrol.appspot.com/downloads/development/ag9zfm1hdHRlcmNvbnRyb2xyOwsSB1Byb2plY3QY6gcMCxINUHVibGljUmVsZWFzZRiAgIC1rPiMCgwLEgZVcGxvYWQYgICAtezqjgoM)

[Mac Download](https://mattercontrol.appspot.com/downloads/development/ag9zfm1hdHRlcmNvbnRyb2xyOwsSB1Byb2plY3QY7AcMCxINUHVibGljUmVsZWFzZRiAgICY3u-ICgwLEgZVcGxvYWQYgICAmPmLnAoM)

## Changes
* Design Tools - The ability to 3D model with a complete set of modeling primitives
* Use a primitive to create your own customized supports
* Design Apps - Design Apps: sophisticated customizable designs
* 64-bit Processing

# MatterControl 1.7.5 (August, 14, 2017)
[Windows Download](https://mattercontrol.appspot.com/downloads/development/ag9zfm1hdHRlcmNvbnRyb2xyOwsSB1Byb2plY3QY6gcMCxINUHVibGljUmVsZWFzZRiAgICGgYiLCgwLEgZVcGxvYWQYgICAps6mhwoM)

If you are looking for the older interface to MatterControl, this is the last stable version without design tools.
