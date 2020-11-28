
# Filesystem & File loading
Probably the most important things to know about the filesystem are:
 1) **The majority of the game's code is stored in files in the filesytem**, and
 2) **Files are all loaded dynamically**.
 
When I say "loaded dynamically", what I mean is that they're copied into RAM only when needed, and are not always copied into the same spot. So this means that the location of all files, most game variables, and critically a LOT of the games code can vary between modes and courses. More details below, but I wanted to start with that, since it's critical to know if you plan on doing any sort of analysis or debugging of the game.

## Filesystem structure
I've explained the filesystem structure in detail [here](https://github.com/RolandMunsil/ParadigmFileExtractor/blob/master/Filesystem%20details.md). But the short version is that it's pretty simple. After the game's code, there's a block of memory with tables (one for each file type) of file locations. After that are just all of the files one after another (with a little header to indicate how long each file is). Additionally, nearly all of the files are divided into sections which each have a magic word (4 ASCII characters) identifying them.

Two important gotchas, though - the magic word for a file is stored both in the file table and in the file itself. There are a couple file types that use *different magic words* in the file table vs the file itself. And second, the UVRW table is special, some of the files it references are just raw data without the normal file entry structure.

## How the game loads files
The main method that loads a file is at 0x800019b8 in RAM. At a high level - It loads a file based on a file type and an index into the table for that file type - so in Ghidra you might see `GetFile(0x55565458, 213)`. `0x55565458` is `UVTX` in ASCII (the file type for texture files), so this will load the texture file at index 213 in the texture file table. (Note that if the file has already been loaded, it just returns a reference to the already loaded file). It loads it by first copying the file into RDRAM, then calling the appropriate loader code that transforms the file into an object. It then returns a reference to that object.

### The file table in memory
The file tables as stored in the ROM are transformed into an entirely different format in RDRAM, and it's really useful to know where the file tables are in memory if you want to be able to find a specfic loaded file. There should always be a reference to the file tables at 0x8002D9B4. Those file tables look like this:

```
55564D4F 00857821 80036010 55565454    UVMO..x!..`.UVTT
00060000 80036860 55564453 0007F809    ......h`UVDS..・
800368C0 55565458 05B80000 80036930    ..hÀUVTX.¸....i0
...                                    
```
Each entry is 12 bytes:

| 4 bytes | 2 bytes | 2 bytes | 4 bytes |
|-|-|-|-|
| file type magic word | # of files in list | unknown | pointer to file list 

One of those file lists will look like:

```
00025FD0 803C1580 00000001 00000000
0002EB98 00000000 00000000 00000000
0002FBE8 00000000 00000000 00000000
```

Each entry is 16 bytes:

| 4 bytes | 4 bytes | 4 bytes | 4 bytes |
|-|-|-|-|
| file location in ROM | pointer to loaded file | reference count | unknown, always 0? 

### The "loading files stack" in memory
The game has a stack for files that are *currently* being loaded. It's a stack because some files load other files in the process of being loaded themselves. Here's what the first three entries look like in the process of loading some file:

```
00828F6A 55565058 000000B0 000000A0    ...jUVPX...°....
001D7998 55565458 00000264 00000264    ..y.UVTX...d...d
00000000 55565458 000006FC 000006FC    ....UVTX...ü...ü
```
Each entry is 16 bytes:

| 4 bytes | 4 bytes | 4 bytes | 4 bytes |
|-|-|-|-|
| file location in ROM | file type magic word | total file size in ROM | amount of file loaded so far

So we can see that the UVPX file was being loaded, but after reading up to A0 it then loaded a UVTX file. (Once that UVTX has been loaded the UVPX file will continue being loaded.)

### Loader code
The game decides how to transform the file into an object depending on its file type. If it's UVMO (for modules, which are files containing code), it calls a special loader, but otherwise, there's a module for every file type and the method has some mapping from magic word to module. FWIW, all of the loader modules seem have a name in the format "\<file type>ld_rom". 

## Modules
Modules (found in the MODU file table, and having the magic word UVMO in their header) are a very important type of file! They contain code, and are where the vast majority of the game's logic is located.

Unfortunately, as mentioned at the start, they are loaded dynamically, which means the location of a method or variable can change depending on multiple factors (e.g. the course and mode selected). And because they're loaded dynamically, the code itself is stored a special format which can't really be statically analyzed nicely. Specifically, all references to methods in other modules or variables stored on the heap are replaced with special values, and when the game loads a module it somehow resolves all of those values to actual addresses (I think it has something to do with the RELA section, fwiw).

Before I explain how you should work with modules in Ghidra, I'll just mention one good thing about modules, which is that they have names stored in their MDBG section! This can be extremely useful for figuring out what code does or trying to find code for a specific thing.

### How modules are stored once they're processed
The data structure that's returned from GetFile when a module is loaded is just an array of pointers to some of its' methods (and sometimes variables). It's not *all* the methods or variables, though - think of it as the module's public interface, much like a class in programming has public methods and variables. It's worth noting that the module code itself seems to always follow immediately after the array of pointers (though I don't know if this is always true).

### How you should work with modules in Ghidra
Because of the whole dynamic loading issue, the way *I* recommend to do static analysis on the modules is by running the game in an emulator and just dumping all of RDRAM when the game is in a state where you think all the relevant code you want will be present. Then do your analysis on that.

However, doing this does come with it's own annoyances. For one thing, if you want to analyze two modules that don't get loaded together, you may have to work with two different dumps. I recommend making a shared archive in Ghidra for all of your types so you can at least share that, but for method names, variable names, etc. I don't really have a good solution. (Although, I just realized, maybe you could use a debugger to force the game to load whatever modules you want. Let me know if you try this!)

The other annoyance is that it's hard to find and identify modules in memory. In the ROM, it's really easy because each module has a debug string in the file, as well as a magic word used to identify it (separate from "UVMO"). These don't get retained in the loaded file, though. What I recommend doing is having both the dumped memory and the ROM open (in a hex editor or a separate Ghidra window, your choice). When you want to identify a module or method in the dumped memory, find maybe 12-16 bytes of code that don't seem to have any references to heap memory or other modules, copy them, and then search for them in the ROM. Once you find a match, you can then jump to the next nearest MDBG section in the ROM to figure out what module you're in.

### General module structure
I don't know if this is 100% consistent, but for every module file I've examined, the first two methods have always been the method to initialize the module and create the pointer list, followed by the method to clean up when the module is set to be unloaded. That second method is sometimes completely empty, if there's nothing to be unloaded, though (literally just two instructions, `jr ra` followed by `nop`).

### Also
You may notice that the module files themselves have some non-code at the end of the code section, and that this is also present when the modules are loaded (although the values may have changed). These are just variables - I am not 100% confident but I believe these are how static variables and constants are compiled. I think this is something that most compilers do? Rather than allocating the variables dynamically at start they just include them alongside the code.

# Tools and resources
## Tools
If you want to start reverse engineering BAR yourself, you really only need two things:
 * Ghidra: This is basically a necessity for any serious reverse engineering, especially code analysis. It's a free 
   tool for decompilation, and while it can be a little finicky it's extremely useful. You almost don't need to know
   anything about MIPS (or even assembly) to use it, although I'd still recommend at least getting a basic understanding.
   You can use this loader if you want https://github.com/zeroKilo/N64LoaderWV although you don't need it - it just does
   some basic setup.
 * Project64(?): As far as I'm aware, Project64 has by far the nicest debugging tools. Unfortunately I believe it's also the least
   accurate N64 emulator, but depending on what you're doing that might be fine. Feel free to try other emulators ofc. If you do use it,
   it's really useful to get familiar with its' scripting system http://shygoo.net/pj64d/apidoc.php
 * Depending on what you're doing, it's also nice to have a good hex editor (I use HxD) and some sort of "programmer" calculator that
   can work with binary and hex values (I just use the default Windows calculator in Programmer mode).
   
I also wrote a tool to extract and convert files from Paradigm games - even if you don't particularly need the files I set it up to use the names of MODU files when it extracts them so it's a nice way to browse all the modules: https://github.com/RolandMunsil/ParadigmFileExtractor (maybe I should just make a script to list the modules lol). You also might find to be a useful reference for how some of the file types work.
   
## Resources
 * Official N64 Programming Manual: http://ultra64.ca/files/documentation/nintendo/Nintendo_64_Programming_Manual_NU6-06-0030-001G_HQ.pdf 
   Kinda vague sometimes, but still really useful.
 * VR4300 (N64 CPU) reference: http://datasheets.chipdb.org/NEC/Vr-Series/Vr43xx/U10504EJ7V0UMJ1.pdf (mostly you just 
   need this to look up instructions or the behavior of registers)
 * https://r12a.github.io/app-conversion/ just for identifying magic words (e.g. 0x55565454 = UVTT)
 * https://www.h-schmidt.net/FloatConverter/IEEE754.html Useful if you find a 32-bit value that you think might be floating point.
 
And if you're doing rendering stuff:
 * https://wiki.cloudmodding.com/oot/F3DZEX2 - So far this has been pretty accurate, but it is just a community wiki page (and is maybe for a 
   slight variation on the microcode BAR uses) so if something looks wrong don't be hesitant to double-check.
 * https://github.com/gonetz/GLideN64/tree/master/src source code for GLideN64, a highly accurate N64 rendering plugin for emulators. 
   You can use this to check behavior when the N64 programming manual is vague.
 
# Random Other Tips
 * In MIPS, the first instruction after a branch is always executed regardless of the branch condition. This confused me a fair bit at first!
 
# Questions?
Don't hesitate to message me if anything is unclear or you want help with something specific! There's a lot of knowledge in my head after working with BAR a bunch, and I wrote most of this in one go so I may have missed some critical bit when I wrote it. 
