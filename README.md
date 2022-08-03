# RavenM

--------------------------------------------------------------------------------
# This fork adds additional Ravenscript features that modders can use in scripts/mutators.



A Ravenfield multiplayer mod.

Discord: https://discord.gg/63zE4gY

## This mod is very <b>W.I.P.</b> There are a lot of bugs and opportunities to crash, so please report anything you find!

# Building
There are several dependencies on game assemblies located in `Ravenfield/ravenfield_Data/Managed/`. To resolve them, create a `libs/` folder in the root folder and place the following assemblies there:
- Assembly-CSharp.dll
- Assembly-CSharp-firstpass.dll
- netstandard.dll
- UnityEngine.dll
- UnityEngine.AnimationModule.dll
- UnityEngine.AssetBundleModule.dll
- UnityEngine.AudioModule.dll
- UnityEngine.CoreModule.dll
- UnityEngine.ImageConversionModule.dll
- UnityEngine.IMGUIModule.dll
- UnityEngine.InputLegacyModule.dll
- UnityEngine.PhysicsModule.dll
- UnityEngine.UI.dll
- UnityEngine.UIModule.dll

Afterwards, you should be able to build the project as normal. Visual Studio 2019+ is recommended. .NET 4.6 is required.

# Installing

This mod depends on [BepInEx](https://github.com/BepInEx/BepInEx), a cross-platform Unity modding framework. First, install BepInEx into Ravenfield following the installation instructions [here](https://docs.bepinex.dev/articles/user_guide/installation/index.html). As per the instructions, make sure to run the game at least once with BepInEx installed before adding the mod to generate config files.

Next, place `RavenM.dll` into `Ravenfield/BepInEx/plugins/`. Optionally, you may also place `RavenM.pdb` to generate better debug information in the logs.

Run the game and RavenM should now be installed.

# Playing
<b>Tl;dr</b>: The connection menu is opened with `M` while in the `Instant Action` menu.

To play together, one player must be the host. This player will control the behaviour of all the bots, the game parameters, and the current game state. All other players will connect to the host during the match. Despite this, no port-forwarding is required! All data is routed through the Steam relay servers, which means fast, easy and encrypted connections with DDoS protection and Steam authentication.

## Hosting
Go to the `Instant Action` menu and press `M`. Press `Host` and choose whether the lobby is friends only or not. After pressing `Start`, you will be put into a lobby. At this point, you cannot exit the `Instant Action` page without leaving the lobby. Other players can connect with the `Lobby ID`.

Starting the game will put everyone in the lobby in a match and terminate the lobby.

## Joining
Go to the `Instant Action` menu and press `M`. Press `Join` and paste the `Lobby ID` of an existing lobby. At this point, you cannot edit any of the options in the `Instant Action` page except for your team. You also cannot start the match. The settings chosen by the host will reflect on your own options.

Have fun!

![Vehicles are more fun with friends](https://i.imgur.com/UnaFuwD.png)
