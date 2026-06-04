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
        [SerializeField] private float blobDiameterFracOfPad = 0.85f;
        [SerializeField] private float blobHeightFracOfDiameter = 0.2f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        private Pad _pad;
        private Renderer _renderer;
        private MaterialPropertyBlock _mpb;

        private Transform _blob;
        private Renderer _blobRenderer;
        private Material _blobMaterial;
        private MaterialPropertyBlock _blobMpb;

        private Texture2D _texture;
        private Color32[] _pixels;
        private int _gridWidth;
        private int _gridHeight;

        private SolderablePin[] _allPins;

        private void Awake()
        {
            _pad = GetComponent<Pad>();
            _renderer = GetComponent<Renderer>();
            _mpb = new MaterialPropertyBlock();
            _blobMpb = new MaterialPropertyBlock();
            CreateBlob();
        }

        private void Start()
        {
            _allPins = FindObjectsByType<SolderablePin>(FindObjectsSortMode.None);
        }

        private void OnDestroy()
        {
            if (_blobMaterial != null) Destroy(_blobMaterial);
            if (_blob != null) Destroy(_blob.gameObject);
            if (_texture != null) Destroy(_texture);
        }

        private void Update()
        {
            UpdatePadTint();
            UpdateBlob();
        }

        private void UpdatePadTint()
        {
            if (_renderer == null || _pad == null || _pad.Flow == null) return;

            var flow = _pad.Flow;

            // Initialize texture on-demand to avoid initialization execution order issues
            if (_texture == null)
            {
                _gridWidth = flow.Width;
                _gridHeight = flow.Height;
                _texture = new Texture2D(_gridWidth, _gridHeight, TextureFormat.RGBA32, false);
                _texture.filterMode = FilterMode.Bilinear;
                _texture.wrapMode = TextureWrapMode.Clamp;
                _pixels = new Color32[_gridWidth * _gridHeight];

                // Ensure texture keywords and default texture reference exist on the material
                if (_renderer.sharedMaterial != null)
                {
                    if (_renderer.sharedMaterial.HasProperty("_BaseMap"))
                    {
                        if (_renderer.sharedMaterial.GetTexture(BaseMapId) == null)
                        {
                            _renderer.sharedMaterial.SetTexture(BaseMapId, Texture2D.whiteTexture);
                        }
                        _renderer.sharedMaterial.EnableKeyword("_BASE_MAP");
                    }
                    if (_renderer.sharedMaterial.HasProperty("_MainTex"))
                    {
                        if (_renderer.sharedMaterial.GetTexture(MainTexId) == null)
                        {
                            _renderer.sharedMaterial.SetTexture(MainTexId, Texture2D.whiteTexture);
                        }
                        _renderer.sharedMaterial.EnableKeyword("_MAIN_TEX");
                    }
                }
            }

            var baseHeatColor = PadHeatPalette.HeatColor(_pad.TempC, _pad.Phase, coolTempC, hotTempC);

            // Greyish/silver colors for flux to make it highly visible on copper
            Color coldFluxColor = new Color(0.6f, 0.62f, 0.65f, 1f);       // greyish silver
            Color activeFluxColor = new Color(0.85f, 0.87f, 0.90f, 1f);    // bright glowing silver/white
            Color burntFluxColor = new Color(0.12f, 0.08f, 0.05f, 1f);     // charred dark brown/black
            Color oxidationColor = new Color(0.25f, 0.35f, 0.25f, 1f);     // dull greenish/dark gray

            bool dirty = false;

            for (int y = 0; y < _gridHeight; y++)
            {
                for (int x = 0; x < _gridWidth; x++)
                {
                    var flux = flow.GetFlux(x, y);
                    if (flux == null) continue;

                    Color cellColor = baseHeatColor;

                    // 1. Blend oxidation onto copper base
                    if (flux.Oxidation > 0f)
                    {
                        cellColor = Color.Lerp(cellColor, oxidationColor, flux.Oxidation);
                    }

                    // 2. Blend flux states on top
                    if (flux.State == FluxState.Burnt)
                    {
                        cellColor = Color.Lerp(cellColor, burntFluxColor, 0.85f);
                    }
                    else if (flux.State == FluxState.Active)
                    {
                        cellColor = Color.Lerp(cellColor, activeFluxColor, Mathf.Clamp01(flux.Amount * 0.8f));
                    }
                    else if (flux.State == FluxState.Cold)
                    {
                        cellColor = Color.Lerp(cellColor, coldFluxColor, Mathf.Clamp01(flux.Amount * 0.6f));
                    }

                    int idx = y * _gridWidth + x;
                    Color32 c32 = cellColor;
                    if (!ColorsEqual(_pixels[idx], c32))
                    {
                        _pixels[idx] = c32;
                        dirty = true;
                    }
                }
            }

            if (dirty)
            {
                _texture.SetPixels32(_pixels);
                _texture.Apply();
            }

            _renderer.GetPropertyBlock(_mpb);
            // Set base color to white so we don't multiply/double-tint the texture colors
            _mpb.SetColor(BaseColorId, Color.white);
            _mpb.SetColor(ColorId, Color.white);
            _mpb.SetTexture(BaseMapId, _texture);
            _mpb.SetTexture(MainTexId, _texture);
            _renderer.SetPropertyBlock(_mpb);
        }

        private static bool ColorsEqual(Color32 a, Color32 b)
        {
            return a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
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

            // Adhere to metal component pin if nearby:
            SolderablePin overlappingPin = null;
            if (_allPins != null)
            {
                foreach (var pin in _allPins)
                {
                    if (pin != null && pin.CurrentOverlappingPad == _pad)
                    {
                        overlappingPin = pin;
                        break;
                    }
                }
            }

            if (overlappingPin != null)
            {
                // Pull local centroid in XZ towards the pin's local position
                var pinLocal = transform.InverseTransformPoint(overlappingPin.transform.position);
                localCentroid = Vector3.Lerp(localCentroid, new Vector3(pinLocal.x, 0f, pinLocal.z), 0.6f);
                worldCenter = transform.TransformPoint(localCentroid) + transform.up * (height * 0.5f);

                // Shift worldCenter Y position upward to bridge/clasp the pin
                var pinWorld = overlappingPin.transform.position;
                worldCenter.y = Mathf.Lerp(worldCenter.y, pinWorld.y, 0.45f);

                // Make the solder blob taller (wetting fillet shape) and slightly narrower
                height = height * 2.2f;
                diameter = diameter * 0.85f;
            }

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

            // Set metallic and smoothness to look like highly reflective shiny metal solder
            if (_blobMaterial.HasProperty("_Metallic")) _blobMaterial.SetFloat("_Metallic", 1f);
            if (_blobMaterial.HasProperty("_Smoothness")) _blobMaterial.SetFloat("_Smoothness", 0.9f);
            if (_blobMaterial.HasProperty("_Glossiness")) _blobMaterial.SetFloat("_Glossiness", 0.9f);

            _blobRenderer.sharedMaterial = _blobMaterial;
            _blobRenderer.enabled = false;
        }
    }
}
