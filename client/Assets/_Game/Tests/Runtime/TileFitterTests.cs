using NUnit.Framework;
using UnityEngine;

namespace Blastlands.Runtime.Tests
{
    public class TileFitterTests
    {
        private GameObject subject;

        [TearDown]
        public void Clear()
        {
            if (subject != null)
            {
                Object.DestroyImmediate(subject);
            }
        }

        [Test]
        public void AParticleSystemInsideAPrefabDoesNotCountTowardsItsSize()
        {
            subject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var spark = new GameObject("Spark", typeof(ParticleSystem));
            spark.transform.SetParent(subject.transform, false);
            spark.transform.localPosition = new Vector3(5f, -3f, 0f);
            spark.GetComponent<ParticleSystem>().Simulate(0.5f);

            Assert.That(TileFitter.TryMeasure(subject, out Bounds bounds), Is.True);
            Assert.That(bounds.center, Is.EqualTo(Vector3.zero), "the spark pulled the centre off the mesh");
            Assert.That(bounds.size, Is.EqualTo(Vector3.one), "the spark widened the box");
        }

        [Test]
        public void NothingButParticlesHasNothingToMeasure()
        {
            subject = new GameObject("Fire", typeof(ParticleSystem));

            Assert.That(TileFitter.TryMeasure(subject, out Bounds _), Is.False);
        }
    }
}
