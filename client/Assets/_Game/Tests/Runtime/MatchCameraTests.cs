using System.Collections.Generic;
using Blastlands.Core;
using NUnit.Framework;
using UnityEngine;

namespace Blastlands.Runtime.Tests
{
    // Whose eyes each viewport renders through. It is a small function carrying the one
    // decision the fog cannot make for itself, and getting it wrong shows somebody
    // their opponent's vision instead of their own, quietly and without an error.
    public class MatchCameraTests
    {
        private GameObject host;
        private MatchCamera cameras;
        private readonly List<int> viewers = new List<int>();

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("Camera Under Test", typeof(Camera), typeof(MatchCamera));
            cameras = host.GetComponent<MatchCamera>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(host);
        }

        private void Bind(int seats)
        {
            var state = new MatchState(new Arena(9, 9), MatchSettings.Default, 1u);
            for (int i = 0; i < 4; i++)
            {
                state.AddPlayer(new GridPos(1 + i, 1));
            }

            cameras.Bind(state, seats);
        }

        [Test]
        public void SplitGivesEachViewportItsOwnSeat()
        {
            Bind(4);
            cameras.Use(CameraMode.Split);

            for (int i = 0; i < 4; i++)
            {
                cameras.ViewersOf(i, viewers);
                Assert.That(viewers, Is.EqualTo(new[] { i }), $"viewport {i}");
            }
        }

        [Test]
        public void FollowRendersForTheSeatItFollows()
        {
            Bind(2);
            cameras.Use(CameraMode.Follow);

            cameras.ViewersOf(0, viewers);

            Assert.That(viewers, Is.EqualTo(new[] { 0 }));
        }

        [Test]
        public void GlobalRendersForEverySeatOnTheCouch()
        {
            // The mode has no single viewpoint, so it takes the union. Anything narrower
            // would hide a local player from a screen they are sitting in front of.
            Bind(3);
            cameras.Use(CameraMode.Global);

            cameras.ViewersOf(0, viewers);

            Assert.That(viewers, Is.EqualTo(new[] { 0, 1, 2 }));
        }

        [Test]
        public void GlobalWithOneSeatIsThatSeatAlone()
        {
            Bind(1);
            cameras.Use(CameraMode.Global);

            cameras.ViewersOf(0, viewers);

            Assert.That(viewers, Is.EqualTo(new[] { 0 }), "three bots do not get to see for you");
        }
    }
}
