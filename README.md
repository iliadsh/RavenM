# RavenM

--------------------------------------------------------------------------------

A Ravenfield multiplayer mod.

[![Discord](https://img.shields.io/discord/458403487982682113.svg?label=Discord&logo=Discord&colorB=7289da&style=for-the-badge)](https://discord.gg/63zE4gY)
[![Downloads](https://img.shields.io/github/downloads/ABigPickle/RavenM/total.svg?label=Downloads&logo=GitHub&style=for-the-badge)](https://github.com/ABigPickle/RavenM/releases/latest)

## This mod is very <b>W.I.P.</b> There are a lot of bugs and opportunities to crash, so please report anything you find!

# Building

Visual Studio 2019+ is recommended. .NET 4.6 is required.

## Steps to build:

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

# Installing

<b>Important Note:</b> RavenM does not support BepInEx version 6. Please ensure to install the latest version of BepInEx 5.x.x to complete the installation.

This mod depends on [BepInEx](https://github.com/BepInEx/BepInEx), a cross-platform Unity modding framework. First, install BepInEx into Ravenfield following the installation instructions [here](https://docs.bepinex.dev/articles/user_guide/installation/index.html). As per the instructions, make sure to run the game at least once with BepInEx installed before adding the mod to generate config files.

Next, place `RavenM.dll` into `Ravenfield/BepInEx/plugins/`. Optionally, you may also place `RavenM.pdb` to generate better debug information in the logs.

Run the game and RavenM should now be installed.

# Playing

<b>Tl;dr</b>: The connection menu is opened with `M` while in the `Instant Action` menu.

<b>Please be aware pirated/non-official copies of Ravenfield may encounter issues when using RavenM.</b> The mod relies entirely on Steam to transfer game data and mods securely between players.

To play together, one player must be the host. This player will control the behaviour of all the bots, the game parameters, and the current game state. All other players will connect to the host during the match. Despite this, no port-forwarding is required! All data is routed through the Steam relay servers, which means fast, easy and encrypted connections with DDoS protection and Steam authentication.

## Hosting
Go to the `Instant Action` menu and press `M`. Press `Host` and choose whether the lobby is friends only or not. After pressing `Start`, you will be put into a lobby. At this point, you cannot exit the `Instant Action` page without leaving the lobby. Other players can connect with the `Lobby ID`.

Starting the game will put everyone in the lobby in a match and terminate the lobby.

## Joining
Go to the `Instant Action` menu and press `M`. Press `Join` and paste the `Lobby ID` of an existing lobby. At this point, you cannot edit any of the options in the `Instant Action` page except for your team. You also cannot start the match. The settings chosen by the host will reflect on your own options.

Have fun!

![Credit: Sofa#8366](https://steamuserimages-a.akamaihd.net/ugc/1917988387306327667/C90622D8C9B8B654E187AA5038A84759DFF050D9/)

Credit for the Discord Rich Presence Images: `Wolffe#6986`
