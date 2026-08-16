using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Blastlands.Runtime.Tests
{
    // Every theme has to fill every slot. A missing one is not a compile error and not
    // an exception: the view silently falls back to grey primitives for that one kind of
    // thing, so a theme with no walls looks like a theme with cubes for walls, and the
    // only way anyone finds out is by rolling the seed that picks it.
    public class ArenaThemeTests
    {
        private static ArenaTheme[] AllThemes()
        {
            string[] guids = AssetDatabase.FindAssets("t:" + nameof(ArenaTheme));
            var themes = new ArenaTheme[guids.Length];
            for (int i = 0; i < guids.Length; i++)
            {
                themes[i] = AssetDatabase.LoadAssetAtPath<ArenaTheme>(AssetDatabase.GUIDToAssetPath(guids[i]));
            }

            return themes;
        }

        [Test]
        public void TheProjectShipsThemes()
        {
            Assert.That(AllThemes(), Is.Not.Empty, "the driver would dress every match in primitives");
        }

        [Test]
        public void EveryThemeFillsEverySlot()
        {
            foreach (ArenaTheme theme in AllThemes())
            {
                Assert.That(theme.FloorTile(0), Is.Not.Null, $"{theme.name} has no ground");
                Assert.That(theme.HardBlock(0), Is.Not.Null, $"{theme.name} has no hard blocks");
                Assert.That(theme.SoftBlock(0), Is.Not.Null, $"{theme.name} has no walls");
                Assert.That(theme.Bush(0), Is.Not.Null, $"{theme.name} has no bushes");
                Assert.That(theme.HasScenery, Is.True, $"{theme.name} has no scenery");
                Assert.That(theme.HasGroundDetail, Is.True, $"{theme.name} has no ground detail");
                Assert.That(theme.Label, Is.Not.Empty, $"{theme.name} has no label");
            }
        }

        [Test]
        public void NoThemeHasAHoleInAVariantList()
        {
            // A null in the middle of an array is worse than an empty one: the fallback
            // only triggers on an empty set, so a hole spawns nothing at all and leaves
            // a walkable-looking tile that is not.
            foreach (ArenaTheme theme in AllThemes())
            {
                var serialized = new SerializedObject(theme);

                foreach (string slot in new[] { "floorTiles", "hardBlocks", "softBlocks", "bushes", "scenery", "groundDetail" })
                {
                    SerializedProperty list = serialized.FindProperty(slot);
                    for (int i = 0; i < list.arraySize; i++)
                    {
                        Assert.That(
                            list.GetArrayElementAtIndex(i).objectReferenceValue,
                            Is.Not.Null,
                            $"{theme.name}.{slot}[{i}]");
                    }
                }
            }
        }

        [Test]
        public void EveryThemeVariesWhatItRepeatsMost()
        {
            // Blocks cover the arena, so one mesh for either kind is the difference
            // between terrain and wallpaper. Scenery and ground detail are already
            // several deep; these two are the ones that get left at one entry.
            foreach (ArenaTheme theme in AllThemes())
            {
                Assert.That(theme.HardBlock(0), Is.Not.SameAs(theme.HardBlock(1)), $"{theme.name} repeats one hard block");
                Assert.That(theme.SoftBlock(0), Is.Not.SameAs(theme.SoftBlock(1)), $"{theme.name} repeats one wall");
                Assert.That(theme.Bush(0), Is.Not.SameAs(theme.Bush(1)), $"{theme.name} repeats one bush");
            }
        }
    }
}
