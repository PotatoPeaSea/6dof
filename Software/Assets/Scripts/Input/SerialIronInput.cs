using System.Globalization;
using UnityEngine;

namespace VirtualFlux.Input
{
    /// <summary>
    /// Stub for the future USB-serial input source. Wire format is defined in
    /// docs/serial-protocol.md. V1 disables this component; <see cref="KeyboardIronInput"/>
    /// drives the iron until firmware ships.
    /// </summary>
    public sealed class SerialIronInput : MonoBehaviour, IIronInputSource
    {
        [SerializeField] private string portName = "/dev/ttyACM0";
        [SerializeField] private int baudRate = 115200;

        public bool IsConnected => false;
        public IronSample Latest { get; private set; }

        public void Tick(float deltaTime)
        {
            // Intentionally empty in V1. When firmware exists, parse pose lines per
            // docs/serial-protocol.md and assign Latest from the most recent sample.
        }

        public static bool TryParsePoseLine(string line, out IronSample sample)
        {
            sample = default;
            if (string.IsNullOrEmpty(line) || line[0] != 'P')
            {
                return false;
            }

            var parts = line.Split(',');
            if (parts.Length != 9)
            {
                return false;
            }

            var ci = CultureInfo.InvariantCulture;
            if (!float.TryParse(parts[1], NumberStyles.Float, ci, out var x)) return false;
            if (!float.TryParse(parts[2], NumberStyles.Float, ci, out var y)) return false;
            if (!float.TryParse(parts[3], NumberStyles.Float, ci, out var z)) return false;
            if (!float.TryParse(parts[4], NumberStyles.Float, ci, out var pitch)) return false;
            if (!float.TryParse(parts[5], NumberStyles.Float, ci, out var yaw)) return false;
            if (!float.TryParse(parts[6], NumberStyles.Float, ci, out var roll)) return false;
            if (!float.TryParse(parts[7], NumberStyles.Float, ci, out var tipC)) return false;
            if (!uint.TryParse(parts[8], NumberStyles.Integer, ci, out var seq)) return false;

            sample = new IronSample(
                new Vector3(x, y, z) * 0.001f,
                Quaternion.Euler(pitch, yaw, roll),
                tipC,
                energized: true,
                sequence: seq);
            return true;
        }
    }
}
