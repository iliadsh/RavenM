# RavenM

A Ravenfield multiplayer mod.

[![Discord](https://img.shields.io/discord/458403487982682113.svg?label=Discord&logo=Discord&colorB=7289da&style=for-the-badge)](https://discord.gg/63zE4gY)
[![Downloads](https://img.shields.io/github/downloads/ABigPickle/RavenM/total.svg?label=Downloads&logo=GitHub&style=for-the-badge)](https://github.com/ABigPickle/RavenM/releases/latest)

## This mod is very **W.I.P.** There are a lot of bugs and opportunities to crash, so please report anything you find!

# Installing

<b>Important Note:</b> RavenM does not support BepInEx version 6. Please ensure to install the latest version of BepInEx 5.x.x to complete the installation.

This mod depends on [BepInEx](https://github.com/BepInEx/BepInEx), a cross-platform Unity modding framework. 

First, install BepInEx into Ravenfield following the installation instructions [here](https://docs.bepinex.dev/articles/user_guide/installation/index.html). As per the instructions, make sure to run the game at least once with BepInEx installed before adding the mod to generate config files.

Next, Download RavenM [here](https://github.com/iliadsh/RavenM/releases/latest) and unzip the file, place `RavenM.dll` into `Ravenfield/BepInEx/plugins/`. Optionally, you may also place `RavenM.pdb` to generate better debug information in the logs.

Run the game and RavenM should now be installed.

Or join the [Discord server](https://discord.gg/63zE4gY), you can get the windows installer on `#mod-installation` channel.

# Playing

When starting game, the RavenM Updater may install the new update from server first.

**Please be aware pirated/non-official copies of Ravenfield may encounter issues when using RavenM.</b> The mod relies entirely on Steam to transfer game data and mods securely between players.**

To play together, one player must be the host. This player will control the behaviour of all the bots, the game parameters, and the current game state. All other players will connect to the host during the match. Despite this, no port-forwarding is required! All data is routed through the Steam relay servers, which means fast, easy and encrypted connections with DDoS protection and Steam authentication.

Now, press `M` button to open connection menu.

## Hosting
Press `Host` and choose whether the lobby is friends only or not. After pressing `Start`, you will be put into a lobby. At this point, you cannot exit the `Instant Action` page without leaving the lobby. Other players can connect with the `Lobby ID`.

Starting the game will put everyone in the lobby in a match and terminate the lobby.

## Joining
Press `Join` and paste the `Lobby ID` of an existing lobby. At this point, you cannot edit any of the options in the `Instant Action` page except for your team. You also cannot start the match. The settings chosen by the host will reflect on your own options.

## On Gaming

Press `P` to open/hide the chat field, press `Y` to type a global message (press `Enter` to send, `Esc` to close the textbox), press `U` to type a message to your team (Eagle\Raven. the keybind is same) 

Pressing `Enter` to open the Loadout UI also open the Lobby menu

Pressing `CapsLock` can use voice chat (Discord required?)

Pressing `F7` can open a tset ui (only for test).

Pressing `~` can open the player coordinate display

Squad leader controls (`G`, `B` keys) are available to direct other players in your squad

Have fun!

![Credit: Sofa#8366](https://steamuserimages-a.akamaihd.net/ugc/1917988387306327667/C90622D8C9B8B654E187AA5038A84759DFF050D9/)

Credit for the Discord Rich Presence Images: `Wolffe#6986`

# Building from source

Visual Studio 2019+ is recommended. .NET 4.6 is required.

Steps to build:

1. Clone the repository to your local machine
   
   ```bash
    $ git clone https://github.com/iliadsh/RavenM.git
    $ git checkout master
    ```

2. Build project

    ```bash
    $ dotnet build RavenM
    ```

    Dependencies should be restored when building. If not, run the following command:

    ```bash
    $ dotnet restore
    ```
