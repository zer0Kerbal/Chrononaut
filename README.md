# Chrononaut Unofficial

Reload single models run-time. Unofficial fork by Lisias.


## In a Hurry

* [Latest Release](https://github.com/net-lisias-kspu/Chrononaut/releases)
    + [Binaries](https://github.com/net-lisias-kspu/Chrononaut/tree/Archive)
* [Source](https://github.com/net-lisias-kspu/Chrononaut)
* [Change Log](./CHANGE_LOG.md)


## Description

Chrononaut is a tool for part modders to reduce the time spent on reloading the database when trying out changes to their parts.

In flight mode, press F8 to reload the models of the parts in your current vessel. 

### Features (implemented and planned)

[x] Enabled in flight mode
[x] Enabled in the VAB editor
[x] Reload meshes from file
[x] Update transforms 
[x] Update UVs
[x] Supports configs using the mesh attribute
[x] Supports configs using the MODEL module
[x] Configurable action key
[x] Update model structure 
[x] Update textures

[  ] Attach nodes
[  ] Reload IVA:s

### Known issues

* will not load textures that were added (in contrast to updated) after the game was started
* breaks the functionality of engines and other complex objects in flight


## Installation

To install, place the GameData folder inside your Kerbal Space Program folder.

**REMOVE ANY OLD VERSIONS OF THE PRODUCT BEFORE INSTALLING**.

### Dependencies
<!--
* Hard Dependencies
	* [KSP API Extensions/L](https://github.com/net-lisias-ksp/KSPAPIExtensions) 2.0 or newer
-->
None at the moment. :)

### Licensing
This work is licensed under the [CC BY-NC-SA 4.0](https://creativecommons.org/licenses/by-nc-sa/4.0/legalcode). See [here](./LICENSE)

+ You are free to:
	- Share : copy and redistribute the material in any medium or format
	- Adapt : remix, transform, and build upon the material for any purpose, even commercially.
+ Under the following terms:
	- Attribution : You must give appropriate credit, provide a link to the license, and indicate if changes were made. You may do so in a ny reasonable manner, but not in any way that suggests the licensor endorses you or your use.
	- ShareAlike : If you remix, transform, or build upon the material, you must distribute your contributions under the same license as the original.


## UPSTREAM

* [Katten](https://forum.kerbalspaceprogram.com/index.php?/profile/180392-katten/):
	+ [Forum](https://forum.kerbalspaceprogram.com/index.php?/topic/173015-142-chrononaut-v041-part-mod-tool/)
	+ [GitHub](https://github.com/KSPKatten/Chrononaut)
