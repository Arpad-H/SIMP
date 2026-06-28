using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Nature-themed Canvas renderer for <see cref="PeanutRadar"/>. It reads the radar's
/// <see cref="PeanutRadar.SweepAngle"/> and <see cref="PeanutRadar.Blips"/> every frame and draws
/// a round dial: a mossy radar face with range rings, a firefly-green sweep with a trailing
/// comet glow, and warm peanut-coloured blips that pop when the sweep hits them and fade out.
///
/// Everything is generated in code — the face, sweep and dot sprites are painted into textures at
/// startup, and the UI hierarchy is built under a Canvas — so there are no art assets to import
/// and nothing to wire up in the inspector. Just add this component (next to a
/// <see cref="PeanutRadar"/>, or anywhere — it will find one) and press Play. If it isn't already
/// under a Canvas it spins up its own screen-space overlay and parks the dial in a corner.
///
/// The look is fully recolourable from the Theme section; the radar's *behaviour* (spin time,
/// blip linger, range) still lives on <see cref="PeanutRadar"/>.
/// </summary>
public class PeanutRadarUI : MonoBehaviour
{
    public enum ScreenCorner { TopLeft, TopRight, BottomLeft, BottomRight, Center }

    [Header("Source")]
    [Tooltip("The radar logic to visualise. Found on this object, then anywhere in the scene, if empty.")]
    [SerializeField] private PeanutRadar radar;

    [Header("Layout")]
    [Tooltip("Diameter of the dial in canvas (reference) pixels.")]
    [SerializeField] private float size = 260f;

    [Tooltip("Where to park the dial when this component has to create its own overlay canvas " +
             "(i.e. it isn't already a child of a Canvas).")]
    [SerializeField] private ScreenCorner corner = ScreenCorner.TopRight;

    [Tooltip("Pixels of breathing room from the screen edges when using the auto-created canvas.")]
    [SerializeField] private Vector2 margin = new Vector2(24f, 24f);

    [Header("Theme — Face")]
    [Tooltip("Base colour of the radar face (the dark mossy disc).")]
    [SerializeField] private Color faceColor = new Color(0.06f, 0.16f, 0.09f, 1f);

    [Tooltip("Range rings + crosshair colour.")]
    [SerializeField] private Color ringColor = new Color(0.30f, 0.62f, 0.34f, 1f);

    [Tooltip("Outer rim / bezel colour (a bark-like brown reads nicely here).")]
    [SerializeField] private Color rimColor = new Color(0.24f, 0.16f, 0.09f, 1f);

    [Header("Theme — Sweep")]
    [Tooltip("Colour of the spinning sweep + its trailing glow. Alpha sets the overall sweep opacity.")]
    [SerializeField] private Color sweepColor = new Color(0.55f, 1f, 0.45f, 0.8f);

    [Tooltip("How many degrees the glowing tail stretches behind the leading edge.")]
    [Range(20f, 320f)]
    [SerializeField] private float sweepTrailDegrees = 130f;

    [Header("Theme — Blips")]
    [Tooltip("Colour of a blip that's close to the squirrel.")]
    [SerializeField] private Color blipNearColor = new Color(1f, 0.82f, 0.36f, 1f);

    [Tooltip("Colour of a blip out near the rim. Blips lerp from near→far by distance.")]
    [SerializeField] private Color blipFarColor = new Color(0.78f, 0.85f, 0.45f, 1f);

    [Tooltip("Marker colour for the squirrel at the centre of the dial.")]
    [SerializeField] private Color centerColor = new Color(0.95f, 0.85f, 0.7f, 1f);

    [Tooltip("Base blip diameter in pixels. Each blip also scales with its brightness.")]
    [SerializeField] private float blipSize = 20f;

    [SerializeField] private float blipMinScale = 0.55f;
    [SerializeField] private float blipMaxScale = 1.25f;

    // Fraction of the dial radius a max-range blip sits at — keeps it just inside the rim ring.
    private const float RimInset = 0.9f;

    private Sprite faceSprite;
    private Sprite sweepSprite;
    private Sprite dotSprite;

    private RectTransform container;
    private RectTransform sweepRect;
    private RectTransform blipParent;

    private readonly List<Image> blipPool = new();
    private float usableRadius;

    private void Awake()
    {
        if (radar == null) radar = GetComponent<PeanutRadar>();
        if (radar == null) radar = FindAnyObjectByType<PeanutRadar>();
        if (radar == null)
            Debug.LogWarning($"{nameof(PeanutRadarUI)} '{name}': no {nameof(PeanutRadar)} found — " +
                             "the dial will draw but stay empty. Add a PeanutRadar to the scene.", this);

        BuildSprites();
        BuildDial();
    }

    private void LateUpdate()
    {
        // LateUpdate runs after the radar's Update, so SweepAngle / Blips are this frame's values.
        if (radar == null || container == null) return;

        // UI Z-rotation is counter-clockwise-positive; the radar bearing is clockwise — hence the
        // minus, so the bright leading edge points exactly where the sweep heading does.
        sweepRect.localRotation = Quaternion.Euler(0f, 0f, -radar.SweepAngle);

        IReadOnlyList<PeanutRadar.Blip> blips = radar.Blips;
        int count = blips.Count;

        for (int i = 0; i < count; i++)
        {
            PeanutRadar.Blip b = blips[i];
            Image img = GetBlip(i);
            RectTransform rt = img.rectTransform;

            if (!img.gameObject.activeSelf) img.gameObject.SetActive(true);

            rt.anchoredPosition = b.position * usableRadius;

            float scale = Mathf.Lerp(blipMinScale, blipMaxScale, b.intensity);
            rt.localScale = new Vector3(scale, scale, 1f);

            Color col = Color.Lerp(blipNearColor, blipFarColor, Mathf.Clamp01(b.distance01));
            col.a = b.intensity;
            img.color = col;
        }

        // Park any unused pooled blips.
        for (int i = count; i < blipPool.Count; i++)
            if (blipPool[i].gameObject.activeSelf)
                blipPool[i].gameObject.SetActive(false);
    }

    // ---- UI assembly -------------------------------------------------------------------------

    private void BuildDial()
    {
        // Build under our own RectTransform if we're already inside a Canvas; otherwise spin up a
        // dedicated overlay canvas and park the dial in a corner of the screen.
        RectTransform selfRect = transform as RectTransform;
        bool underCanvas = selfRect != null && GetComponentInParent<Canvas>() != null;
        RectTransform parent = underCanvas ? selfRect : CreateOverlayCanvas();

        container = CreateChild("PeanutRadarDial", parent);
        container.sizeDelta = new Vector2(size, size);
        PlaceContainer(container, standalone: !underCanvas);

        // Face is the dial's own graphic AND the circular mask, so the sweep + blips are clipped
        // to the disc and can never spill into the corners.
        Image face = container.gameObject.AddComponent<Image>();
        face.sprite = faceSprite;
        face.raycastTarget = false;
        Mask mask = container.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = true;

        // Sweep sits under the blips so the dots glow on top of the beam.
        sweepRect = CreateChild("Sweep", container);
        sweepRect.sizeDelta = new Vector2(size, size);
        AddImage(sweepRect, sweepSprite, Color.white);

        blipParent = CreateChild("Blips", container);
        Stretch(blipParent);

        RectTransform center = CreateChild("Center", container);
        center.sizeDelta = new Vector2(blipSize * 0.8f, blipSize * 0.8f);
        AddImage(center, dotSprite, centerColor);

        usableRadius = size * 0.5f * RimInset;
    }

    private Image GetBlip(int index)
    {
        while (blipPool.Count <= index)
        {
            RectTransform rt = CreateChild($"Blip{blipPool.Count}", blipParent);
            rt.sizeDelta = new Vector2(blipSize, blipSize);
            Image img = AddImage(rt, dotSprite, blipNearColor);
            img.gameObject.SetActive(false);
            blipPool.Add(img);
        }
        return blipPool[index];
    }

    private RectTransform CreateOverlayCanvas()
    {
        var go = new GameObject("RadarCanvas (auto)");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        go.AddComponent<GraphicRaycaster>();
        return (RectTransform)go.transform;
    }

    private void PlaceContainer(RectTransform rt, bool standalone)
    {
        if (!standalone)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            return;
        }

        Vector2 anchor = corner switch
        {
            ScreenCorner.TopLeft     => new Vector2(0f, 1f),
            ScreenCorner.TopRight    => new Vector2(1f, 1f),
            ScreenCorner.BottomLeft  => new Vector2(0f, 0f),
            ScreenCorner.BottomRight => new Vector2(1f, 0f),
            _                        => new Vector2(0.5f, 0.5f),
        };

        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        float ox = anchor.x == 0f ? margin.x : anchor.x == 1f ? -margin.x : 0f;
        float oy = anchor.y == 0f ? margin.y : anchor.y == 1f ? -margin.y : 0f;
        rt.anchoredPosition = new Vector2(ox, oy);
    }

    private static RectTransform CreateChild(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, worldPositionStays: false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.localScale = Vector3.one;
        return rt;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
    }

    private static Image AddImage(RectTransform rt, Sprite sprite, Color color)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    // ---- Procedural sprites ------------------------------------------------------------------

    private void BuildSprites()
    {
        faceSprite = MakeFaceSprite(256);
        sweepSprite = MakeSweepSprite(256);
        dotSprite = MakeDotSprite(64);
    }

    // The dial face: a disc that darkens toward the edge, with concentric range rings, a faint
    // crosshair and a bark-brown rim. Transparent outside the circle so it doubles as the mask.
    private Sprite MakeFaceSprite(int s)
    {
        var tex = NewTexture(s);
        var px = new Color[s * s];
        float c = (s - 1) * 0.5f;
        float r = s * 0.5f;

        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            float dx = (x - c) / (r - 1f);
            float dy = (y - c) / (r - 1f);
            float d = Mathf.Sqrt(dx * dx + dy * dy);

            Color col;
            if (d >= 1f)
            {
                col = new Color(0f, 0f, 0f, 0f);
            }
            else
            {
                col = Color.Lerp(Lighten(faceColor, 0.12f), Darken(faceColor, 0.28f), d);

                // Range rings.
                if (Mathf.Abs(d - 0.33f) < 0.012f || Mathf.Abs(d - 0.66f) < 0.012f)
                    col = Color.Lerp(col, ringColor, 0.8f);

                // Crosshair.
                if (Mathf.Abs(dx) < 0.006f || Mathf.Abs(dy) < 0.006f)
                    col = Color.Lerp(col, ringColor, 0.45f);

                // Rim / bezel.
                if (d > 0.9f)
                    col = Color.Lerp(col, rimColor, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.9f, 1f, d)) * 0.95f);

                col.a = Mathf.Clamp01(1f - Mathf.InverseLerp(0.985f, 1f, d)); // soft anti-aliased edge
            }

            px[y * s + x] = col;
        }

        return Finish(tex, px, s);
    }

    // The sweep: a comet of light whose crisp leading edge points "up" (rotated to the sweep
    // heading at runtime) with a glow that trails off behind it over sweepTrailDegrees.
    private Sprite MakeSweepSprite(int s)
    {
        var tex = NewTexture(s);
        var px = new Color[s * s];
        float c = (s - 1) * 0.5f;
        float r = s * 0.5f;

        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            float dx = (x - c) / (r - 1f);
            float dy = (y - c) / (r - 1f);
            float d = Mathf.Sqrt(dx * dx + dy * dy);

            float alpha = 0f;
            if (d < 1f)
            {
                // Bearing of this pixel, 0 = up, clockwise. The trail lives just behind the
                // leading edge (the counter-clockwise side), so it lags as the beam turns.
                float a = Mod(Mathf.Atan2(dx, dy) * Mathf.Rad2Deg, 360f);
                float behind = Mod(360f - a, 360f);

                float trail = behind <= sweepTrailDegrees
                    ? Mathf.Pow(1f - behind / sweepTrailDegrees, 1.6f)
                    : 0f;

                float fadeCenter = Mathf.InverseLerp(0.0f, 0.06f, d);       // hide a hot dot at the hub
                float fadeEdge = 1f - Mathf.InverseLerp(0.985f, 1f, d);     // match the face edge
                alpha = trail * sweepColor.a * fadeCenter * Mathf.Clamp01(fadeEdge);
            }

            px[y * s + x] = new Color(sweepColor.r, sweepColor.g, sweepColor.b, Mathf.Clamp01(alpha));
        }

        return Finish(tex, px, s);
    }

    // A soft round glow with a solid core, painted white so each blip can be tinted via Image.color.
    private Sprite MakeDotSprite(int s)
    {
        var tex = NewTexture(s);
        var px = new Color[s * s];
        float c = (s - 1) * 0.5f;
        float r = s * 0.5f;

        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            float dx = (x - c) / (r - 1f);
            float dy = (y - c) / (r - 1f);
            float d = Mathf.Sqrt(dx * dx + dy * dy);

            float glow = Mathf.Pow(Mathf.Clamp01(1f - d), 2.5f);
            float core = 1f - Mathf.InverseLerp(0.0f, 0.45f, d);
            float a = Mathf.Clamp01(Mathf.Max(glow, core * 0.9f));

            px[y * s + x] = new Color(1f, 1f, 1f, a);
        }

        return Finish(tex, px, s);
    }

    private static Texture2D NewTexture(int s) =>
        new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };

    private static Sprite Finish(Texture2D tex, Color[] px, int s)
    {
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
    }

    private static Color Lighten(Color c, float k) => Color.Lerp(c, Color.white, k);
    private static Color Darken(Color c, float k) => Color.Lerp(c, Color.black, k);
    private static float Mod(float a, float m) => a - m * Mathf.Floor(a / m);
}
