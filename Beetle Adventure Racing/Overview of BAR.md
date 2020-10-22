
# Filesystem & File loading
## Filesystem structure
## Modules
### How you should load modules into Ghidra
## Loading process & mem values
## How to find stuff in memory
# Other stuff
## Useful memory values

# Tools and resources
## Tools
If you want to start reverse engineering BAR yourself, you really only need two things:
 * Ghidra: This is basically a necessity for anything beyond just searching for values in memory. It's a free 
   tool for decompilation, and while it can be a little finicky it's extremely useful. You almost don't need to know
   anything about MIPS (or even assembly) to use it, although I'd still recommend at least getting a basic understanding.
   You can use this loader if you want https://github.com/zeroKilo/N64LoaderWV although you don't need it - it just does
   some basic setup.
 * Project64(?): As far as I'm aware, Project64 has by far the nicest debugging tools. Unfortunately I believe it's also the least
   accurate N64 emulator, but depending on what you're doing that might be fine. Feel free to try other emulators ofc. If you do use it,
   it's really useful to get familiar with its' scripting system http://shygoo.net/pj64d/apidoc.php
 * Depending on what you're doing, it's also nice to have a good hex editor (I use HxD) and some sort of "programmer" calculator that
   can work with binary and hex values (I just use the default Windows calculator in Programmer mode).
   
## Resources
 * Official N64 Programming Manual: http://ultra64.ca/files/documentation/nintendo/Nintendo_64_Programming_Manual_NU6-06-0030-001G_HQ.pdf 
   Kinda vague sometimes, but still really useful.
 * VR4300 (N64 CPU) reference: http://datasheets.chipdb.org/NEC/Vr-Series/Vr43xx/U10504EJ7V0UMJ1.pdf (mostly you just 
   need this to look up instructions or the behavior of registers)
 * https://r12a.github.io/app-conversion/ just for identifying magic words (e.g. 0x55565454 = UVTT)
 * https://www.h-schmidt.net/FloatConverter/IEEE754.html Useful if you find a 32-bit value that you think might be floating point
 
And if you're doing rendering stuff:
 * https://wiki.cloudmodding.com/oot/F3DZEX2 - So far this has been pretty accurate, but it is just a community wiki page (and is maybe for a 
   slight variation on the microcode BAR uses) so if something looks wrong don't be hesitant to double-check
 * https://github.com/gonetz/GLideN64/tree/master/src source code for GLideN64, a highly accurate N64 rendering plugin for emulators. 
   You can use this to check behavior when the N64 programming manual is vague.
 
# Tips
 * In MIPS, the first instruction after a branch is always executed regardless of the branch asdhajsdasjdhasdjhasd this confused me at first
