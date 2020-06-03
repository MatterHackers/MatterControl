
Avoid Crossing Perimeters
=========================

Prevents the nozzle from crossing outside of the perimeters when it is traveling from one place to another. This can significantly improve the surface finish of your part by reducing stringing and other extrusion artifacts.

This shows a ring sliced with Avoid Crossing Perimeters off and on. The green lines are travel moves which would likely form strings in the final print.

![](https://lh3.googleusercontent.com/VTz46RplN90OCFVf1umAhsSE4aMaKxq4IRlcg0dvj3D9nVuUUaLslXINEaD7u_SMO8QC93kwBPjB6zm40hRi2pwkcw=w200) ![](https://lh3.googleusercontent.com/HtQiIndTEF1gBvHX0ooDVapGUP4GvOIm9pZDU0tbpZKVtCZ8gTuiCzcUSGCy6eGjPObA4OvozKV9t5eS_5g1jYUt=w200)

For especially complicated shapes it may be difficult for the slicer to find an efficient path. In those cases you might consider turning this option off.

**Recommended Baseline:** On


External Perimeters First
=========================

By default, the inner perimeters are printed first and the outermost perimeter loop is printed last. This ensures that the nozzle is fully primed when printing the outer loop, reducing surface artifacts.

This option will instead print the outer loop first, then the inner loops. In some cases this can improve dimensional accuracy, however it increases the risk of artifacts on the outer surface.

**Recommended Baseline:** Off

Start End Overlap
=================

This is the amount of overlap between the start and end of the perimeter loops. It is expressed as a percentage of the nozzle diameter. These pictures show 0%, 50%, and 100% overlap.

![](https://lh3.googleusercontent.com/SFn3xY3CKIH4fS0b7eJ6kIQ6QjLcKQAjmYzcbsO7omzmkcRynjLE-0gShWQOGgm-LTL6WVKUHHGhIG7RoUXTMhmbQQ=w180) ![](https://lh3.googleusercontent.com/I9-TE0IbDwTlUxeGr_9_0KIKj0N3bkd87CQnNdySa7FoBMXCfkwcd71ChOOaGLc3YE9kMwDmZ7z1HlFZEtWDbPU8Xg=w180) ![](https://lh3.googleusercontent.com/tcjZtf-aGiyKMB29gpHsaM3yYMD4ot_oBlGrB1jsoo1hjq4l8tFjcP1777b6cqt5HFeYpKCrzg2VK-Uv1sql0BNb8Q=w180)

You can tune this setting in order to minimize the visibility of the perimeter seam. No overlap means there will be a slight gap between the start and end of the loop. Too much overlap and there will be a small bump instead of a gap.

**Recommended Baseline:** 75%  
**Units:** percent of nozzle diameter (%)

Merge Overlapping Lines
=======================

In very thin sections of a model it is possible for the perimeter loops to cross over each other. This results in a line of plastic being layed down twice in the same spot. The Merge Overlapping Lines feature detects when perimeter loops cross over each other, and replaces them with a single fatter line. These pictures show a thin section of a print with Merge Overlapping Lines turned off and turned on.

![](https://lh3.googleusercontent.com/HpvzkkRpdE11ZAne2xP9iJQrWsr99chgWzXH4p9dBlVhcHBSvxZH_oY57YvTIKXjeY7GmeDFmM-4pQgQc3d-PWDOtQ=s0) ![](https://lh3.googleusercontent.com/ghbigi4iA9nvTlFXfBjnZu9ZkYk1JYT0cow_BYzlHU0S0kH3o5kAuoV7kC4Q4_1YQm9kev-Emggv0rLN6Mciqo2F=s0)

In order to preserve the quality of the outer surface, Merge Overlapping Lines only applies to inner perimeter loops, not the outer loops.

Expand Thin Walls
=================

3D printers cannot print anything thinner than the nozzle diameter (0.4 mm on most printers). This means that some models with intricate details may not print correctly since they have features smaller than the nozzle. Expand Thin Walls identifies these parts of the model and expands them in order to make them printable.

This problem is often encountered when trying to print text. Here is how some text in a cursive font will print with Expand Thin Walls turned off and on.

![](https://lh3.googleusercontent.com/kqWPJb88yHyQ1Fk4ZQa3Z-6-Q5GqdxPrRm_y-c4Po6bZFQJ1Voq-oaJO-sqIZgg3B-6w-3W7lTz_0D-RsseUMsmjJsc=w200) ![](https://lh3.googleusercontent.com/bVOcsyyVyGwqncQWmw-CEtlHEFrXRgCVJWcWUdH2VO7HiMUMFK33iLigTtXXkq59njYFDFJ1CBg2s64VkbKzkqshfA=w200)

**Recommended Baseline:** On