# Neanka's V Rising server tools

Mod add some useful features to dedicated server:
  - Makes a bridge between in-game chat and your discord chat
  - Shows in the chats who connected/disconnected 
  - Adds auto-messages system
  - Adds MOTD different from server description
  - Adds discord command /status to show server status in discord
  - Adds VIPlist for users hwo can connect to full server without admin permissions

## Reqirements

  - [BepInExPack V Rising](https://v-rising.thunderstore.io/package/BepInEx/BepInExPack_V_Rising/)
  - [Wetstone](https://v-rising.thunderstore.io/package/molenzwiebel/Wetstone/)

## Installation

First of all if you want to use discord features you need to create your [discord bot](https://discord.com/developers/applications) and invite it on your server (pick `bot` and `applications.commands` scopes). 
Install all dependencies on your server. Unpack `servertools.dll` and `servertools.VIPlist.txt` to the `VRising_Server\BepInEx\plugins` folder.
Start the server, the mod will create an empty configuration file.
Turn off the server and edit the file `\VRising_Server\BepInEx\config\neanka.servertools.cfg`
Start the server again. That's it.

## TODO
  - ...
