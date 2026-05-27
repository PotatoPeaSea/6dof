using NUnit.Framework;
using VirtualFlux.Sim;

namespace VirtualFlux.Tests
{
    public sealed class SolderFlowTests
    {
        [Test]
        public void SolidSolderDoesNotMove()
        {
            var flow = new SolderFlow(4, 1, meltingPointC: 183f);
            flow.AddSolder(0, 0, 1f);
            for (int x = 0; x < 4; x++) flow.SetTemp(x, 0, 25f);
            flow.Step(0.1f);
            Assert.That(flow.GetSolder(0, 0), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(flow.GetSolder(1, 0), Is.EqualTo(0f).Within(1e-5f));
        }

        [Test]
        public void MoltenSolderWetsAdjacentFluxedCells()
        {
            var flow = new SolderFlow(3, 1, meltingPointC: 183f);
            for (int x = 0; x < 3; x++)
            {
                flow.SetTemp(x, 0, 250f);
                flow.ApplyFlux(x, 0, 1f);
            }
            // Heat-step the flux into Active state.
            flow.Step(0.1f);
            flow.AddSolder(0, 0, 1f);

            for (int i = 0; i < 50; i++) flow.Step(0.05f);

            Assert.Greater(flow.GetSolder(1, 0), 0.05f);
            Assert.Greater(flow.GetSolder(2, 0), 0f);
        }

        [Test]
        public void SolderDoesNotCrossColdGap()
        {
            var flow = new SolderFlow(3, 1, meltingPointC: 183f);
            flow.SetTemp(0, 0, 250f);
            flow.SetTemp(1, 0, 25f);   // cold gap
            flow.SetTemp(2, 0, 250f);
            flow.ApplyFlux(0, 0, 1f);
            flow.ApplyFlux(2, 0, 1f);
            flow.Step(0.1f);
            flow.AddSolder(0, 0, 1f);

            for (int i = 0; i < 50; i++) flow.Step(0.05f);

            Assert.That(flow.GetSolder(2, 0), Is.EqualTo(0f).Within(1e-5f));
        }
    }
}
