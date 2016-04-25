MatterControl
=============

|        | Master |
| ------ | ------ |
| Linux | [![Travis CI-Master](https://travis-ci.org/MatterHackers/MatterControl.svg?branch=master)](https://travis-ci.org/MatterHackers/MatterControl) |
| Windows | [![AppVeyor-Master](https://ci.appveyor.com/api/projects/status/c85oe36mdgp446uw?svg=true)](https://ci.appveyor.com/project/johnlewin/mattercontrol) |

[MatterControl](http://www.mattercontrol.com/) is an open-source program designed to control and enhance the desktop 3D printing experience. It's designed to help you get the most out of your 3D printer - making it easy to track, preview, and print your 3D parts. Development of MatterControl is sponsored by [MatterHackers](http://www.matterhackers.com/) and it's partners.

![Screenshot](http://www.mattercontrol.com/static/mattercontrol/screenshot_slice.png)

Features
--------
* Integrated slicing engine [MatterSlice](https://github.com/MatterHackers/MatterSlice), as well as the ability to use Slic3r or Cura.
* Designed to be driven and extended by the 3D printing community using a powerful [plugin architecture](http://wiki.mattercontrol.com/Developing_Plugins).
* [Library](http://wiki.mattercontrol.com/Library) for managing your STL files, with a plugin for cloud synchronization.
* Built in profiles for [a plethora of different printers](http://www.mattercontrol.com/#jumpSupportedModels).
* Built in [editing tools](http://wiki.mattercontrol.com/3D_View/Edit) along with [plugins for creating](http://wiki.mattercontrol.com/Category:Design_Tools) text, images, and braille.
* [Queue](http://wiki.mattercontrol.com/Queue) of items you are going to print, and [history](http://wiki.mattercontrol.com/History) of items you have printed.
* [2D/3D preview](http://wiki.mattercontrol.com/Layer_View) of the sliced object.
* Advanced [printer controls](http://wiki.mattercontrol.com/Controls), including the ability to make adjustments while printing.
* Software based [print leveling](http://wiki.mattercontrol.com/Options/Software_Print_Leveling).
* [Remote monitoring of your printer](http://sync.mattercontrol.com/), along with [SMS/email notifications](http://wiki.mattercontrol.com/Options/Notifications) when your print is completed.

Download
------------------------
* [Windows](https://mattercontrol.appspot.com/downloads/mattercontrol-windows/release)
* [Mac](https://mattercontrol.appspot.com/downloads/mattercontrol-mac-os-x/release)
* [Linux](http://wiki.mattercontrol.com/Running_on_Linux)

[Release Notes](http://wiki.mattercontrol.com/Release_Notes)

Building from Source
----------------------
MatterControl is written in C#. It uses the [agg-sharp](https://github.com/MatterHackers/agg-sharp) GUI abstraction layer. See this wiki article if you want to [contribute code](http://wiki.mattercontrol.com/Contributing_Code).

1. Checkout the latest source code and submodules:

        git clone --recursive https://github.com/MatterHackers/MatterControl.git
        cd MatterControl

2. Install MonoDevelop and Nuget.

        sudo apt-get install monodevelop nuget

3. Add Mono SSL Support - Copy in Mozilla Root certificates to enable NuGet and MatterControl SSL requests

        mozroots --import --sync

4. Restore NuGet packages - On MonoDevelop 4.0 or older you can install [NuGet Addin](https://github.com/mrward/monodevelop-nuget-addin). If you are on Mint, also install libmono-cairo2.0-cil. Alternatively you can run the command line NuGet application to restore the project packages:

        nuget restore MatterControl.sln

5. Optionally switch to a target branch

        git checkout master
        git submodule update --init --recursive

    As a single command line statement:

        targetBranch=master && git checkout $targetBranch && git submodule update --init --recursive

6. Build MatterControl

        mdtool build -c:Release MatterControl.sln

    **or**

        xbuild /p:Configuration=Release MatterControl.sln

7. Link the StaticData from your source directory to the build directory

        ln -s ../../StaticData bin/Release/StaticData

8. After MatterControl has been built in MonoDevelop it is recommended that you run the application via command line or via a shell script to invoke mono.

        mono bin/Release/MatterControl.exe

    If you'd like to log errors for troubleshooting

        mono bin/Release/MatterControl.exe > log.txt

    If you want detailed error logging and tracing

        MONO_LOG_LEVEL=debug mono bin/Release/MatterControl.exe > log.txt

9. In order for MatterControl to access the serial ports, you will need to give your user the appropriate permissions. On Debian based distros, add yourself to the dialout group. On Arch, add yourself the the uucp and lock groups instead.

        gpasswd -a $USER dialout


### Serial Helper

1. Change to the SerialHelper directory

        cd Submodules/agg-sharp/SerialPortCommunication/SerialHelper

2. Run the build script

        ./build.sh

3. If your receive errors you may need to install libc6-dev-i386 for x86 compilation

        sudo apt-get install libc6-dev-i386


Help, Bugs, Feedback
--------------------
For information on using MatterControl, check the [MatterControl Wiki](http://wiki.mattercontrol.com/Main_Page). If you have questions or feedback, feel free to post on the [MatterHackers Forums](http://forums.matterhackers.com/) or send an email to support@matterhackers.com. To report a bug, file an [issue on GitHub](https://github.com/MatterHackers/MatterControl/issues).
