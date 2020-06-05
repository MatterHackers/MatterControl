
This guide will walk you through printing multi-color or multi-material models on dual extrusion printer.

Add the models
==============

Multi-color models are typically distributed as a set of separate STL files. Each STL represents the part of a model that is a certain color. Start by [adding each of the models to your scene](../designing/add-existing-objects.md). In this guide we will be using the dual extrusion version of [Phil A. Ment](https://www.matterhackers.com/store/l/matterhackers-mascot-phil-a-ment/sk/M6DV4FS2) as an example. You can download the files for him at MatterHackers.com. Phil's body will be one color, but his face, shoes, logo, etc. will be another color.

![](https://lh3.googleusercontent.com/GA8TPvMe4_l3KAkV-3pCLibjGFrW2q80HUHTb2uPq2zPgT8JzTfIJB5u9xrp-U-YcEkWVR8haU1f6zmrCsWAutaQuQ)

Choose Materials & Colors
=========================

Select one of the models and a pane will appear on the right side where you can choose the material and color.

The material selection lets you choose which extruder will be used for printing that model. The default is extruder 1.

![](https://lh3.googleusercontent.com/EsizqnatADRbwCmlzQmZUk7d3gb5N0X4QKWtEADqMjHwwloDz-Jaz_MThO4Zcw0PYSbGCeOdxl2_rYu2E_Y0ngGwqg)

The color selection is purely for aesthetics. It only changes how the model appears in MatterControl and has no bearing on how the model will be printed. Nevertheless, you can use it to get a better sense of what your final print will look like.

![](https://lh3.googleusercontent.com/onY2gZniO3I_My5oUt6YeEald9FMnadNWq9ydI4V2fys4ZOixSQ9-sVlj4hpSyGqMAqyk5Iv8YP5z3eVUvZ_wx0pqxU)

The View Style menu will let you switch between the materials view and the colors view.

![](https://lh3.googleusercontent.com/M_R60B62hNhiMJj5YbI283AK1VXvRw__0rAIqAdp2zbRjEpb1iPIxwJOSnbYzkhgG73eP6lUR_gcI14Tgy_3oMalRA)

Align the Models
================

Now you must merge the models together into one object. To do this, we will use the Align tool.

![](https://lh3.googleusercontent.com/8RWZjN0DctgyRzYiiiFiJKMnpxVVHU8OCwP3DMfQoGCSel-GrTbKzKpGLO8WXOBB8cHhSk4h9fWyGPUFmWmLnCIW_Q)

Select both models and then click Align button in the toolbar. Properly designed multi-extrusion models will share the same origin. In the tool panel, choose origin for all three axes.

![](https://lh3.googleusercontent.com/SNbayPrQBplRcy-hCGGSzLGyagxcwIPi2-QElxCIcmob90uAEuXwqyPrPSGKK0OMjbcszPwhOUZJoch1oP7LdFvV)

Tips & Tricks
=============

Print Area
----------

You may have noticed that when a multi-extrusion model is selected, part of the print bed becomes grayed out. This is because, on many printers, the full build area is not available for use by both extruders. When an object is selected, MatterControl shows you which extruders are needed to print that object and grays out the parts of the bed that are not reachable by all of them.

![](https://lh3.googleusercontent.com/38bI_iKQXaU7ZYwWVl0mzxV6p2lY8LMGwcS8WFjc9HpoUSgnnYJ_Mt5tO4NwxzEHDd6MArDJU-DvGk4m3RxR7ddwJhk)

Making Changes
--------------

After alignment, the models are combined. Changing the color or material will affect the object as a whole. However, it is still possible to make changes to the individual components if you wish.

To do this, click the arrow next to the newly created "Align" object in the design tree. This will show it's components (the original models). From here you can choose a model and give it a different color or material.

![](https://lh3.googleusercontent.com/ogbQNE5Cn6rt2ez16fa2MSHA2P9lt2UE0HcHXZv4kQrxuYaVOZ9Nx5ESUy5T2w3WXVdQpQkl5WkEsi_wgh8MCJqt)