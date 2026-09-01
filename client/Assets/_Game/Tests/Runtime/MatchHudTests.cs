using Blastlands.Core;
using NUnit.Framework;
using UnityEngine;

namespace Blastlands.Runtime.Tests
{
    // Where a panel lands once the window has been divided up. The bug this guards is
    // silent: a panel over the wrong quadrant still renders, still updates, and still
    // shows real numbers, they are just somebody else's.
    public class MatchHudTests
    {
        private GameObject host;
        private MatchCamera cameras;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("Camera Under Test", typeof(Camera), typeof(MatchCamera));
            cameras = host.GetComponent<MatchCamera>();

            var state = new MatchState(new Arena(9, 9), MatchSettings.Default, 1u);
            for (int i = 0; i < 4; i++)
            {
                state.AddPlayer(new GridPos(1 + i, 1));
            }

            cameras.Bind(state, 4);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(host);
        }

        private static readonly Vector2[] Corners =
        {
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0f, 0f),
            new Vector2(1f, 0f)
        };

        [Test]
        public void EveryPanelOfAViewportLandsInsideThatViewport()
        {
            // The whole of the fix in one assertion. Read against the cameras rather
            // than against hardcoded quadrants, so it still means something if the split
            // layout is ever rearranged.
            cameras.Use(CameraMode.Split);

            for (int viewport = 0; viewport < cameras.ViewCount; viewport++)
            {
                Rect rect = cameras.ViewAt(viewport).rect;

                foreach (Vector2 corner in Corners)
                {
                    Vector2 anchor = MatchHud.AnchorIn(rect, corner);

                    Assert.That(anchor.x, Is.InRange(rect.xMin, rect.xMax), $"viewport {viewport}, corner {corner}");
                    Assert.That(anchor.y, Is.InRange(rect.yMin, rect.yMax), $"viewport {viewport}, corner {corner}");
                }
            }
        }

        [Test]
        public void TwoViewportsDoNotShareASingleAnchor()
        {
            // A transposed axis is the failure mode with teeth: it survives the range
            // check above on a symmetric layout while stacking two players' panels in
            // the same place.
            cameras.Use(CameraMode.Split);

            for (int a = 0; a < cameras.ViewCount; a++)
            {
                for (int b = a + 1; b < cameras.ViewCount; b++)
                {
                    foreach (Vector2 corner in Corners)
                    {
                        Assert.That(
                            MatchHud.AnchorIn(cameras.ViewAt(a).rect, corner),
                            Is.Not.EqualTo(MatchHud.AnchorIn(cameras.ViewAt(b).rect, corner)),
                            $"viewports {a} and {b} put {corner} in the same place");
                    }
                }
            }
        }

        [Test]
        public void OnASingleScreenTheCornersAreTheCornersOfTheWindow()
        {
            // Global and Follow have one view, and the fix must be invisible there: the
            // panels sit exactly where they sat before any of this.
            var whole = new Rect(0f, 0f, 1f, 1f);

            foreach (Vector2 corner in Corners)
            {
                Assert.That(MatchHud.AnchorIn(whole, corner), Is.EqualTo(corner));
            }
        }

        [Test]
        public void APanelStaysOnItsOwnSideOfAHorizontalSplit()
        {
            // Two seats split top and bottom rather than into quadrants, which is the
            // case where an unchanged y would look plausible and be wrong.
            var top = new Rect(0f, 0.5f, 1f, 0.5f);
            var bottom = new Rect(0f, 0f, 1f, 0.5f);

            Assert.That(MatchHud.AnchorIn(top, new Vector2(0f, 0f)).y, Is.EqualTo(0.5f));
            Assert.That(MatchHud.AnchorIn(top, new Vector2(0f, 1f)).y, Is.EqualTo(1f));
            Assert.That(MatchHud.AnchorIn(bottom, new Vector2(0f, 1f)).y, Is.EqualTo(0.5f));
            Assert.That(MatchHud.AnchorIn(bottom, new Vector2(0f, 0f)).y, Is.EqualTo(0f));
        }
    }
}
