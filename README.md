# SilentWalker

Allows you to simulate packing your backpack like a seasoned survivor. Think of it as everything packed thoughtfully and deliberately. Bottles completely full and upright, metal separated and wrapped, firewood tied. The mod makes your movement quieter for you, the wildlife, or both.

## What it does

In ***Linked*** mode, one slider makes your footsteps quieter, for you and for the wildlife alike.

In ***Separate*** mode, you can tune how much is just what you hear versus what the wildlife hears.

Further sliders let you pack each kind of gear more quietly and fine-tune the ground and clothing sounds. Every setting is described under [Settings](#settings) below.

## Requirements

- The Long Dark 2.55
- MelonLoader 0.7.2 or later
- ModSettings

## Settings

Open the game's Mod Settings menu and choose SilentWalker. The settings are listed in the order they appear there. On every slider, 100% is vanilla.

| Setting                      | Description                                                                                                                                                                                                                                                                |
| ---------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Enabled**                  | Off: the mod does nothing at all, and footsteps, wildlife hearing and everything else are vanilla until you turn it back on. The other settings are hidden while it is off. (Default: on)                                                                                  |
| ***Movement noise***         |                                                                                                                                                                                                                                                                            |
| Mode                         | Linked: one slider sets both how far wildlife hears you and how loud you hear your own footsteps. Separate: set each on its own slider, for players who want the sound without the gameplay change, or the reverse. (Default: Linked)                                      |
| Movement noise               | Linked mode. Changes gameplay and sound: wildlife hears your footsteps at this percentage of its normal distance, and you hear them at this percentage of their volume. Crouching and walking still reduce hearing further. (Default: 100%)                                |
| Wildlife hearing (gameplay)  | Separate mode. How far animals can hear your footsteps, as a percentage of the normal distance. Does not change what you hear. Crouching and walking still reduce it further. (Default: 100%)                                                                              |
| Footstep volume (audio only) | Separate mode. How loud you hear your own footsteps, relative to the game's sound effects volume. Never changes what animals hear. (Default: 100%)                                                                                                                         |
| ***Pack noise***             | One slider per kind of gear the game tracks. Each 10% lower is 3 dB quieter, and 0% is silent. In Linked mode, wildlife hearing drops by as much as that gear contributes to your noise, so packing gear you carry little of changes little. In Separate mode, sound only. |
| Small supplies               | Matches, medicine, bandages, sewing kit, paper. Lower values model packing them carefully. (Default: 100%)                                                                                                                                                                 |
| Wood                         | Firewood and sticks. Lower values model tying them down. (Default: 100%)                                                                                                                                                                                                   |
| Metal                        | Tools, scrap, cans and canned drinks. Lower values model wrapping and separating it. (Default: 100%)                                                                                                                                                                       |
| Water                        | Bottled liquids: water, lamp fuel, antiseptic. Lower values model wedging them upright. (Default: 100%)                                                                                                                                                                    |
| ***Detail***                 |                                                                                                                                                                                                                                                                            |
| Show detailed settings       | Shows the four sliders below. While they are hidden, they have no effect and the sounds stay vanilla. (Default: off)                                                                                                                                                       |
| Ground impact                | Volume of your boots on the ground. In Linked mode, wildlife hearing drops by as much as the ground contributes to your noise; in Separate mode, sound only. (Default: 100%)                                                                                               |
| Clothing rustle              | Volume of your clothing as you move. In Linked mode, wildlife hearing drops by as much as your clothing contributes to your noise; in Separate mode, sound only. (Default: 100%)                                                                                           |
| Pack treble (experimental)   | The high frequencies of everything in your pack. Lower values muffle the pack, as if it were wrapped in fabric. Changes tone only, never wildlife hearing. (Default: 100%)                                                                                                 |
| Metal ring (experimental)    | The ring of metal in your pack. Lower values take the ring out, as if each piece were wrapped. Changes tone only, never wildlife hearing. (Default: 100%)                                                                                                                  |

## Upgrading from 1.0.0

Your 1.0.0 settings are translated on the first launch. If footsteps were silenced, Movement noise is set to 0%. If you had lowered any of the backpack sliders, the mod switches to Separate mode (which, like 1.0.0, changes only the sound) and sets the matching Pack noise sliders to the same loudness. The defaults of 2.0.0 are otherwise vanilla: open the mod's page in Mod Settings to set it up.

## Game updates

After a game update that changes the footstep sounds, the Pack noise and detailed sliders may stop working until SilentWalker is updated. Movement noise and Separate mode keep working, and your footsteps always play.

## Known limits

- How much each kind of gear adds to what wildlife hears is a first estimate. Feedback is welcome.
- Mods that replace or block the game's footsteps may not work with SilentWalker.
