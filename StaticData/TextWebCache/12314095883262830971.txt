
When you have a printer open you can find the 'Slice Settings' near the top far right of MatterControl. 

Clicking this will open up the settings panel where you can edit all of the settings about your printer.

![](https://lh3.googleusercontent.com/4QYHXu6CNmSgJ9oOsegjn5igMyKgkxi3gtomSUDfRtqgG6WIHom34CQpmwr1pVuLZ-hqnnAf59nbkYoAUDPPrvvjZw=s0)


Quality and Material Presets
============================

MatterControl lets you choose from preset settings for print quality and material.

![](https://lh3.googleusercontent.com/QL7qE2bTBi6pXBqeLK1mQ5ZvmyetcrPg9vaUL-J3BdJRDI0A-7PfBsHxXfzhzHMziaf3KRx7VK0OM9huWloPmBX2qQU=s0)

The materials presets include a range of filaments which have been tested to work with your particular machine. Your printer profile comes with a few quality and material options to choose from, but you can also edit them and make your own. Click the pencil icon to change the particular settings affected by a preset.

When a preset is enabled, the affected settings are highlighted. Quality settings are highlighted in yellow and material settings are highlighted in orange. Learn more about [how the presets are used](#settings-layers) below.


Search
======

MatterControl includes a search feature so you can easily find the settings you are looking for.

![](https://lh3.googleusercontent.com/L4d841reikEdUG9lJlwuC69Wf4KVDdiGs117AXy0yMpjzbotwAC8ZWr56gjZmE8ojf8HOKpIRFTTZoufQxYGJU0N=s0)


Cloud Synchronization
=====================

If you have signed in to your MatterControl Cloud account, then your slice settings are automatically synchronized with the Cloud. If you change a slice setting in MatterControl, then that change will immediately be sent to any other computers you have running MatterControl, which are logged in to the same account.


History
=======

MatterControl saves backups of all of your slice settings on a regular basis, and uploads these backups to the MatterControl Cloud. This is useful if you are experimenting with slice settings and decide that things have gone wrong and you need to change them back to how they were before. The **Restore Settings** option in the overflow menu (![](https://lh3.googleusercontent.com/B0iPKfPTIEs8X9qR5xZYj5aarp5PcLy3-cLjr3DYIRxZnyWLFe3-UMBYmfafoU8CjfD1dDUMmjMpcqZsJuAUsg8k-A)) will allow you to choose settings from a previous date to restore to.

![](https://lh3.googleusercontent.com/lvKJ0Zs95KYiROFa2erpw4eVN55rf5yfuKizWRFcPBh-tkjStZx8tO7quFxe7SBRyARotGj0C40aUuZQUUSVU32T)

In addition, you can reset all settings to the factory defaults by choosing **Reset to Defaults**.

Settings Layers
===============

MatterControl's slice settings work on a system of layers. Each layer can override the settings in the layers below it. The settings view shows you which layer each setting is coming from based on the color it is highlighted.

At the bottom is the base layer. These are the default settings provided by your printer's manufacturer. All settings are included in the base layer. In the settings view, if a particular setting is not highlighted with any color (orange, yellow, blue, etc.) then that means the baseline setting is being used. The baseline settings are never changed, which means that you can always revert back to your manufacturer's defaults if something goes wrong.

Quality Presets
---------------

The next layer is the Quality layer. If a setting from this layer is being used, it is highlighted in yellow in the settings view.

![](https://lh3.googleusercontent.com/_tUvOzZPwKxfZSCXox3RVkkmSLX3vL3WWAXIun2EOPQFES9iKYdCS529__0yCyTaXWtTX5wEkmzXqxCB19Vg1IF9tQ=s0)

Your printer profile comes with a few Quality presets to choose from, but you can also edit them and make your own. This typically contains settings related to layer thickness and speed. However, you are free to use your discretion about what belongs here. Any setting can be added. For instance, if I wanted to make a quality setting for high strength prints, I could name it "Strong" and then add settings like the infill density and number of perimeters.

Material Presets
----------------

Next is the Material layer. These settings are highlighted orange. 

![](https://lh3.googleusercontent.com/06EDlnXpQTegBTN270jbPaRUMl7vi0QJ2qcH-YlHxB0wvB4GpgwRLv6gYYxWgdpyHRRnjKarxfmm3Uw-M5fgxMYBYw=s0)

Typically the materials presets contain your temperature settings, however you are free to include any other setting in this layer as well. For instance, if I am printing a flexible material I might want to slow down the speed and disable retraction. These kinds of settings would not normally be included in a preset for PLA or ABS.

Other Changes
-------------

The last layer contains any other changes that you have made in the settings view. They override everything else. They are highlighted in blue.

![](https://lh3.googleusercontent.com/1B34J9zubBl7liHY-0Pz3MD1PaV7LghN-59x4JkQ47d6izSdFmnDqmrfBKHGzF3t4FNj5zaT8TM5JcpyUtQuCxf_=s0)

To clear a change and revert to the baseline setting, click the X to the right of that setting. This is where you should make changes that are not related to either quality or material. For instance, if you want to use hexagonal infill instead of triangles, that has nothing to do with wither the print quality or the material being used.

This system is very powerful, but it is up to you to use it in whatever way you see fit. The labels "Quality" and "Material" are really just guidelines. You can actually use them for whatever you like.