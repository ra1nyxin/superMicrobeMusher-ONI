# superMicrobeMusher

superMicrobeMusher is an Oxygen Not Included mod that expands the vanilla Microbe Musher recipe list.

## Features

- Adds many safe pickupable game items as Microbe Musher recipes.
- Uses a tiny water input for added recipes.
- Produces practical output amounts:
  - Mass-based products: `1000 kg`
  - Unit-based products, foods, seeds, and medicine: `1` item
- Skips unsafe, internal, hidden, deprecated, building, creature, minion, rocket, and invalid prefabs.
- Works from loaded game prefabs so it can adapt to the active game content.

## Compatibility

The mod patches `ComplexRecipeManager.PostProcess` and registers additional recipes for the vanilla `MicrobeMusher`.

It does not replace the Microbe Musher building, does not remove vanilla recipes, and does not force unsafe prefabs into recipes.

The mod skips and logs recipes whose final game-generated IDs conflict with recipes already registered or pending when its post-processing prefix runs. Mods that add conflicting recipes later can still conflict.

## Build

Run:

```powershell
.\build.ps1
```

The build script compiles and installs the dev mod to:

```text
%USERPROFILE%\Documents\Klei\OxygenNotIncluded\mods\Dev\superMicrobeMusher
```

The project targets .NET Framework 4.8 to match the current game assemblies. Update the managed assembly path in the project and build script if the game is installed elsewhere.

## Workshop Package

Upload only the mod package files:

- `superMicrobeMusher.dll`
- `mod.yaml`
- `mod_info.yaml`
- optional preview image
- workshop description from `workshop_description.txt`

Do not upload game DLLs from `OxygenNotIncluded_Data\Managed`.
