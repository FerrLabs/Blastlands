# Licensed assets

The art under `client/Assets/Synty` is Synty Studios content. Its licence allows it in a shipped
game but not in a repository anyone can read, so it lives in a separate private repository,
[FerrLabs/Blastlands-Assets](https://github.com/FerrLabs/Blastlands-Assets), mounted here as a
submodule.

Only the files the game references are kept, each with its `.meta`, so every GUID resolves exactly
as it did when the packs were imported whole. A clone without the submodule has the code and the
scenes but pink materials and missing prefabs.

## Getting it

```bash
git clone --recurse-submodules git@github.com:FerrLabs/Blastlands.git
```

In an existing clone:

```bash
git submodule update --init
```

Both repositories keep their binaries in Git LFS on `lfsx.ferrlabs.com`, which authenticates with a
GitHub token rather than a password (username `git`). One way to store it, from a shell where `gh`
is logged in:

```bash
git config --global credential.https://lfsx.ferrlabs.com.username git
printf 'protocol=https\nhost=lfsx.ferrlabs.com\nusername=git\npassword=%s\n' "$(gh auth token)" | git credential approve
```

CI reads the submodule and its LFS objects with the `BLASTLANDS_ASSETS_TOKEN` secret, a token with
read access to `FerrLabs/Blastlands-Assets`. The run's own token cannot see another private
repository.

## Adding an asset

1. Import the pack in Unity as usual. The `INTERFACE_*` packs land under `client/Assets/Synty`,
   inside the submodule. The `SIMPLE` packs land at `client/Assets/<PackName>`, outside it: move
   the folder and its `.meta` to `client/Assets/Synty/<PackName>` before committing anything, and
   check that `git status` at the Blastlands root reports no new `client/Assets/` entry. Licensed
   art committed outside the submodule is exactly what this split exists to prevent, and nothing
   in the repository catches it.
2. Use the asset from the game (a prefab reference, an `ArenaTheme` slot, `MatchArt`, ...).
3. In the submodule, commit only what the game now references: the asset, its `.meta`, the `.meta`
   of every new folder above it, and whatever it depends on. Unity's
   `AssetDatabase.GetDependencies` on the scenes and the `_Game` assets gives the exact list.
   Never commit a pack's `Samples/` folder beyond what that list names: the Animation packs'
   dummy mesh, the avatar their clips are imported against, takes its material from there.
4. Push the submodule, then commit the new submodule pointer in Blastlands in the same PR as the
   code that uses the asset.

Replacing an asset works the same way in reverse: once nothing references the old one, the same
`GetDependencies` list no longer names it, and it is deleted from the submodule with its `.meta`
(and the `.meta` of any folder left empty) in the same PR.

Anything left untracked in the submodule stays on your machine only, which is how a pack can be
imported whole to browse it without growing the repository.

## Public domain sound

The sound effects are the exception to the split. They are built from Kenney's packs, which are
released under CC0, so nothing stops them living in this repository, and they do: under
`client/Assets/_Game/Audio`, next to a `LICENSE.txt` that names the source pack and the source file
of every clip. Add a sound there only if its licence is CC0 or equivalent, and record where it came
from in that file in the same commit. Anything under a licence that forbids redistributing the raw
file, however free to use in a game, goes in the submodule like the Synty art.

## Rendered images

The download page at `blastlands.ferrlabs.com` shows the four characters. Those pictures live in
this repository, under `server/crates/lobby/src/site/`, because they are rendered stills of the
game used to present it, which the licence allows, rather than the models, textures or animations
themselves. Nothing that can be imported back into an engine belongs there: a render is a flat
WebP, never a mesh, a texture atlas or a source file.

The logo, `logo.png`, is a render too: the grenade model from POLYGON Battle Royale
(`SM_Wep_Grenade_01`) with the pack's first texture, lit the same way and saved as a 192 pixel
PNG so it can also serve as the browser tab icon. It is not the pack's own grenade icon, which is
a sprite and would import straight back into Unity.

The portraits were rendered headless from the characters' prefabs in their idle pose, lit warm
from the front with an orange rim, on a transparent background, then cropped and saved as WebP at
880 pixels high. A new character or a changed outfit needs a new render in the same framing.

Each character also has a `<name>-idle.webp`: the same setup rendered as 54 frames over one cycle
of the idle animation, with the body swaying 22 degrees either side, assembled into a looping
animated WebP 520 pixels high. The page only plays the one in the middle of the picker.
