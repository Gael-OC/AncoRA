using AncorRA.AR;
using NUnit.Framework;

namespace AncorRA.Tests
{
    public class PaseoStableHashTests
    {
        [Test]
        public void KnownFnv1aValues()
        {
            Assert.AreEqual(0x811C9DC5u, PaseoStableHash.Of(""));
            Assert.AreEqual(0xE40C292Cu, PaseoStableHash.Of("a"));
            Assert.AreEqual(0xBF9CF968u, PaseoStableHash.Of("foobar"));
        }

        [Test]
        public void NullIsEmpty() => Assert.AreEqual(PaseoStableHash.Of(""), PaseoStableHash.Of(null));

        [Test]
        public void DifferentTextsDiffer() => Assert.AreNotEqual(PaseoStableHash.Of("X1"), PaseoStableHash.Of("EIC"));
    }
}
