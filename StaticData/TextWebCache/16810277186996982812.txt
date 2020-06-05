
Installation
============

MatterControl is available for Ubuntu/Debian or Arch based distros.

Ubuntu / Debian
---------------

MatterControl officially supports Ubuntu Linux and other Debian based distributions, such as Mint. The latest stable release is always available as a .deb package. It is available here or at [MatterControl.com](mattercontrol.com).

[Download MatterControl for Ubuntu](https://mattercontrol.appspot.com/downloads/mattercontrol-linux/release)

### Installing Mono

MatterControl requires the latest version of Mono in order to work. Although Mono is available in Ubuntu, the version provided is usually severely outdated. On Ubuntu 18.04, follow these steps to add the official Xamarin package repository and update to the latest version of Mono.

```
$ sudo apt install gnupg ca-certificates
$ sudo apt-key adv --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys 3FA7E0328081BFF6A14DA29AA6A19B38D3D831EF
$echo "deb https://download.mono-project.com/repo/ubuntu stable-bionic main" | sudo tee /etc/apt/sources.list.d/mono-official-stable.list
$ sudo apt update

```

There are also instructions for other distributions on the [Mono website](https://www.mono-project.com/download/stable/).

### Serial Port Permissions

In order for MatterControl to access the serial ports, you will need to give your user the appropriate permissions. On Debian or Fedora based distros, add yourself to the `dialout` group.

```
$ gpasswd -a $USER dialout
```

You will then need to logout and log back in for the changes to take effect.

Arch
----

An unofficial package is [available in the AUR](https://aur.archlinux.org/packages/mattercontrol/). Install it manually or using your favorite AUR helper.

```
$ yay -S mattercontrol
```

### Serial Port Permissions

In order for MatterControl to access the serial ports, you will need to give your user the appropriate permissions. On Arch you must add yourself to the `uucp` and `lock` groups.

```
$ gpasswd -a $USER uucp
$ gpasswd -a $USER lock
```

You will then need to logout and log back in for the changes to take effect.

Raspberry Pi
============

Because MatterControl is written in C#, it can run on any processor architecture. The regular Linux version of MatterControl will run the Raspberry Pi. However, you must have the [experimental OpenGL drivers](https://www.raspberrypi.org/blog/another-new-raspbian-release/) installed and enabled. To do this, run `raspi-config` and go to Advanced Options > GL Driver.

If you have the Raspberry Pi touchscreen, remember that you can switch MatterControl into Touchscreen Mode through the application settings.

Assigning Serial Ports
======================

On Linux, serial port assignments can change whenever a printer is connected or disconnected. MatterControl cannot tell which printer is connected to which serial port. You can setup a udev rule to permanently assign a unique port to your printer.

Do `ls /dev/tty*` before and after connecting your printer to find out which port it is assigned to. Printers will show up as either `/dev/ttyACM#` or `/dev/ttyUSB#`.

Use `udevadm` to get the serial number (UUID) of the USB device. This is a unique 20 digit hexadecimal value.

```
$ udevadm info --attribute-walk -n /dev/ttyACM0 | grep "serial"
```

Some printers will not report a serial number. In this case, you will have to use other attributes to identify it such as the vendor ID (idVendor) and the product ID (idProduct).

Create a file `/etc/udev/rules.d/97-3dprinters.rules`. Here is an example with rules for two printers.

```
SUBSYSTEM=="tty", ATTRS{serial}=="6403237383335190E0F1", GROUP="uucp", MODE="0660", SYMLINK+="tty-taz"
SUBSYSTEM=="tty", ATTRS{idVendor}=="16d0", ATTRS{idProduct}=="076b", GROUP="uucp", MODE="0660", SYMLINK+="tty-pulse"
```

Fill in either the serial number or vender and product IDs based on the information you obtained earlier. Make sure `GROUP` is set to the same group ownership as the rest of your serial ports. This is usually `dialout` on Debian or `uucp` on Arch. You can check by doing `ls -l /dev/ttyACM*`. Lastly, give your printer a unique name for the `SYMLINK`. This name must start with `tty` or it will not show up in the list in MatterControl.

The next time you connect the printer, a symlink will automatically be created that points to the correct serial device. You can now configure the printer in MatterControl to use the symlinked port.

Known Issues
============

Upgrading from 1.7 to 2.x
-------------------------

There is a problem with 1.7 that will cause the automatic update to fail. It will download the update package, but it will have the wrong file extension. This prevents the package from opening and being installed correctly.

To get around this, download and install the MatterControl 2.X package manually.


Does not start on systems with low end GPUs
-------------------------------------------

MatterControl uses anti-aliasing features which may not be available on extremely low end GPUs (for instance, virtual machines or embedded systems). In these situations, MatterControl will not be able to start. To launch MatterControl you will need to disable anti-aliasing. Edit the file `/usr/lib/mattercontrol/appsettings.json` and change `FSAASamples` from `8` to `0`.

```
"FSAASamples": 0
```
Even on systems that support antialiasing, disabling it can greatly improve performance.