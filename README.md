# Terraria Content Conversion Suite (TCCS)

A simple tool designed to take the original 'Content' folders of the XBLA version of Terraria, and make the required changes in order for it to be suitable to use with [TerrariaOGC](https://github.com/PPrism/TerrariaOGC).

This tool is made to perform a specific set of actions to the input directory for the optimal experience in using TerrariaOGC, based on user direction. 

Features include:
* Command-line interface so the setup process for TerrariaOGC's assets is virtually trivial.
* Adjusting or inserting assets for use with TerrariaOGC
* \*Decompressing v1.01 assets made for the Xbox 360
* \*Patching specific assets with xDelta3 automatically


*\* assuming the proper auxillary applications are available alongside TCCS*

## Supported Versions
Currently, this program only supports the first major release of TerrariaOGC, which only has support for the initial versions of the game, along with version 1.01. 

When TerrariaOGC gets updated to support the 1.2 versions (1.03 & 1.09), so will TCCS.

## How is it used?
Once you have a build of the program (made by yourself or downloaded from the 'Releases' tab), you can execute the program while it is in the same directory as both the 'Content' folder and the provided 'Prerequisites' folder.
You will also need to ensure any required files are also present in that directory.

## How do I build it?
If you don't wish to use the publish build provided for some reason, you can just use the solution in Visual Studio and build it with that. It has a build process that is trivial compared to TerrariaOGC.

## Auxillary Files:
In order for TerrariaOGC to be safe in a legal sense, I cannot provide a substantial part of the Terraria's original content, outside of a few resources, meaning TerrariaOGC depends on you having the original game made for the 'Old-Gen' console versions.

Unfortunately, it is not as simple as having a copy of the game and just extracting the game files to get what you need, due to compression, encoding, and/or the framework TerrariaOGC was built on.

To account for this, TCCS provides the option for the user to let the program handle the decompression or conversion of some assets if they have not been actioned yet, but the handling is dependent on some auxillary files, most of which I cannot provide for legal reasons, so I'll be leaving their acquisition up to the user.

**Make sure you acquire any of the below files from a safe source.**

The supported applications which can be used by the TCCS and their function are listed below:
* `xDelta3.exe`: *This is a command-line program used for processes involving delta encoding, which in this case, is used to apply the patch to the created .ZIP file for the game's sound files so that they can work with TerrariaOGC.*
  * This is only needed if you are building the initial versions of the game. TerrariaOGC will be built with `VERSION_INITIAL`, `IS_PATCHED`, or `USE_ORIGINAL_CODE` if this is the case.
  * You need version 3.0.11 just to be safe, which in this case, I can actually provide a link to. You can find it [here](https://github.com/jmacd/xdelta-gpl/releases/tag/v3.0.11).

* `xbdecompress.exe`: *All versions of the game come with compressed resources in order to save space, and in versions 1.01 and above, every file has non-XNB compression applied to it. TCCS is made primarily for the Xbox 360 version of the game and as such, accounts for the type of compression it comes with.
  This tool was developed by Microsoft and is included in the Xbox 360 Development Kit (XDK), and as the name would suggest, it is used to decompress these files.*
  * This is needed if you have not already decompressed the files and are building versions 1.01, 1.03, or the final version of the game. TerrariaOGC will be built with `VERSION_101`, `VERSION_103`, or `VERSION_FINAL` if this is the case.

* `unbundler.exe`: *Like the above, the files are compressed and are also packaged, which means in order to use the resources, they must be unpackaged after decompression. This tool is also made by Microsoft, found in the XDK, and is used to handle .XPR files, which are packaged Xbox 360 textures.*
  * This is needed if you have not already decompressed the files and are building versions 1.01, 1.03, or the final version of the game. TerrariaOGC will be built with `VERSION_101`, `VERSION_103`, or `VERSION_FINAL` if this is the case.
  * If you want to do this manually, you can use Noesis to unpackage .XPR files.

* `xma2encode.exe`: *This is another tool made by Microsoft, found in the XDK, and it is used to handle XMA2 sound files, a format used for Xbox sound files.*
  * This is needed if you have not already decompressed the files and are building versions 1.01, 1.03, or the final version of the game. TerrariaOGC will be built with `VERSION_101`, `VERSION_103`, or `VERSION_FINAL` if this is the case.
  * If you want to do this manually, you can try to use [unxwb](https://github.com/mariodon/unxwb) to unpackage XMA2 sound files.

* `xwmaencode.exe`: *This is another tool made by Microsoft, found in the XDK, and it is used to handle xWMA sound files, a format used by Microsoft for sound files.*
  * This is needed if you have not already decompressed the files and are building versions 1.01, 1.03, or the final version of the game. TerrariaOGC will be built with `VERSION_101`, `VERSION_103`, or `VERSION_FINAL` if this is the case.
  * If you want to do this manually, you can try to use [unxwb](https://github.com/mariodon/unxwb) to unpackage xWMA sound files.

* You will also need any dependency files needed by these programs, which to my knowledge, are: `msvcp71.dll`, `msvcr71.dll`, and `xbdm.dll`.
