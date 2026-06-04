using UnityEngine;
using VirtualFlux.Sim;

namespace VirtualFlux.App
{
    /// <summary>
    /// Read-only presentation of a <see cref="Pad"/>'s simulation state: tints the pad surface by
    /// temperature/phase and grows a solder bump from the SolderFlow volume. It only reads Pad's
    /// public state each frame and never mutates the physics.
    /// </summary>
    [RequireComponent(typeof(Pad))]
    public sealed class PadVisualizer : MonoBehaviour
    {
        [SerializeField] private float coolTempC = 25f;
        [SerializeField] private float hotTempC = 350f;
        [SerializeField] private float maxBlobVolume = 1.5f;
        [SerializeField] private float blobDiameterFracOfPad = 0.6f;
        [SerializeField] private float blobHeightFracOfDiameter = 0.5f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private Pad _pad;
        private Renderer _renderer;
        private MaterialPropertyBlock _mpb;

        private Transform _blob;
        private Renderer _blobRenderer;
        private Material _blobMaterial;
        private MaterialPropertyBlock _blobMpb;

        private void Awake()
        {
            _pad = GetComponent<Pad>();
            _renderer = GetComponent<Renderer>();
            _mpb = new MaterialPropertyBlock();
            _blobMpb = new MaterialPropertyBlock();
            CreateBlob();
        }

        private void OnDestroy()
        {
            if (_blobMaterial != null) Destroy(_blobMaterial);
            if (_blob != null) Destroy(_blob.gameObject);
        }

        private void Update()
        {
            UpdatePadTint();
            UpdateBlob();
        }

        private void UpdatePadTint()
        {
            if (_renderer == null) return;
            var c = PadHeatPalette.HeatColor(_pad.TempC, _pad.Phase, coolTempC, hotTempC);
            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, c);
            _mpb.SetColor(ColorId, c);
            _renderer.SetPropertyBlock(_mpb);
        }

        private void UpdateBlob()
        {
            var flow = _pad.Flow;
            if (flow == null || _blob == null) return;

            // Total solder + volume-weighted centroid in pad-local XZ (-0.5..0.5).
            float total = 0f, wx = 0f, wz = 0f;
            for (int x = 0; x < flow.Width; x++)
            {
                for (int y = 0; y < flow.Height; y++)
                {
                    float s = flow.GetSolder(x, y);
                    if (s <= 0f) continue;
                    total += s;
                    wx += s * ((x + 0.5f) / flow.Width - 0.5f);
                    wz += s * ((y + 0.5f) / flow.Height - 0.5f);
                }
            }

            if (total <= 1e-4f)
            {
                if (_blobRenderer.enabled) _blobRenderer.enabled = false;
                return;
            }
            if (!_blobRenderer.enabled) _blobRenderer.enabled = true;

            float fill = PadHeatPalette.BlobFill01(total, maxBlobVolume);
            float padSize = transform.lossyScale.x; // pad mesh is a unit square in local XZ
            float diameter = padSize * blobDiameterFracOfPad * Mathf.Max(0.15f, fill);
            float height = diameter * blobHeightFracOfDiameter;

            var localCentroid = new Vector3(wx / total, 0f, wz / total);
            var worldCenter = transform.TransformPoint(localCentroid) + transform.up * (height * 0.5f);

            // Driven in world space to avoid inheriting the pad's non-uniform scale.
            _blob.position = worldCenter;
            _blob.rotation = transform.rotation;
            _blob.localScale = new Vector3(diameter, height, diameter);

            Color solder = _pad.Phase switch
            {
                SolderPhase.Liquid => new Color(0.95f, 0.95f, 1.00f), // molten: brighter
                SolderPhase.Burnt => new Color(0.20f, 0.18f, 0.16f),  // oxidized
                _ => new Color(0.75f, 0.76f, 0.80f),                  // cooled solder
            };
            _blobRenderer.GetPropertyBlock(_blobMpb);
            _blobMpb.SetColor(BaseColorId, solder);
            _blobMpb.SetColor(ColorId, solder);
            _blobRenderer.SetPropertyBlock(_blobMpb);
        }

        private void CreateBlob()
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = name + "_SolderBlob";
            var col = sphere.GetComponent<Collider>();
            if (col != null) Destroy(col);

            _blob = sphere.transform;
            _blob.SetParent(null, worldPositionStays: true);

            _blobRenderer = sphere.GetComponent<Renderer>();
            // Clone the pad's material so the blob inherits a pipeline-valid shader; only fall
            // back to Shader.Find when the pad has no renderer (e.g. Gerber-spawned pads).
            if (_renderer != null && _renderer.sharedMaterial != null)
            {
                _blobMaterial = new Material(_renderer.sharedMaterial);
            }
            else
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                _blobMaterial = new Material(shader);
            }
            _blobMaterial.name = "SolderBlobMaterial";
            _blobRenderer.sharedMaterial = _blobMaterial;
            _blobRenderer.enabled = false;
        }
    }
}
