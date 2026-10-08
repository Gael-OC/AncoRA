using AncorRA.AR;
using NUnit.Framework;

namespace AncorRA.Tests
{
    public class PaseoMapLoadTests
    {
        static PaseoMapLoad Maps()
        {
            var load = new PaseoMapLoad();
            load.Add(152192);
            load.Add(152199);
            return load;
        }

        [Test]
        public void ReportWithPointsIsLoadedAndWithoutPointsFailed()
        {
            var load = Maps();
            load.Report(152192, 16890);
            load.Report(152199, 0);
            Assert.IsTrue(load.IsLoaded(152192));
            Assert.IsTrue(load.IsFailed(152199));
            Assert.AreEqual(-1, load.FirstPending);
        }

        [Test]
        public void MapsThatNeverReportedFailOnceTheSdkIsReady()
        {
            // The SDK fires no event when Core.LoadMap fails, yet still becomes ready.
            var load = Maps();
            load.Report(152192, 16890);
            Assert.AreEqual(152199, load.FirstPending);
            CollectionAssert.AreEqual(new[] { 152199 }, load.FailPending());
            Assert.IsTrue(load.IsFailed(152199));
            Assert.IsFalse(load.IsLoaded(152199));
            Assert.AreEqual(-1, load.FirstPending, "nothing may stay 'loading' after the SDK is ready");
            CollectionAssert.IsEmpty(load.FailPending());
        }

        [Test]
        public void UnknownMapIsIgnored()
        {
            var load = Maps();
            load.Report(999, 10);
            Assert.IsFalse(load.IsLoaded(999));
            Assert.IsFalse(load.IsFailed(999));
        }
    }
}
