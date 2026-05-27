using UnityEngine;

namespace VirtualFlux.Input
{
    public readonly struct IronSample
    {
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public readonly float TipTempSetpointC;
        public readonly bool Energized;
        public readonly uint Sequence;

        public IronSample(Vector3 position, Quaternion rotation, float tipTempSetpointC, bool energized, uint sequence)
        {
            Position = position;
            Rotation = rotation;
            TipTempSetpointC = tipTempSetpointC;
            Energized = energized;
            Sequence = sequence;
        }
    }

    public interface IIronInputSource
    {
        bool IsConnected { get; }

        IronSample Latest { get; }

        void Tick(float deltaTime);
    }
}
