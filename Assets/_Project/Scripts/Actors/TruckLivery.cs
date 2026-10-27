using System.Collections.Generic;
using UnityEngine;
using GameCore.Inventory;
using GameCore.Services;

/// <summary>
/// Paint and logo for every truck, applied once when the truck starts.
///
///  - Cab paint: ONLY the cab's white-paint palette cells are recoloured. The tractor shares the project's palette atlas with
///    everything else, so a per-colour COPY of that texture is made (cached, four of them in all) with just the cab-paint
///    texels swapped, and given to the tractor's body material alone. Glass, bumper, lights, wheels, the driver and the whole
///    trailer keep their own materials and are never touched. Inbound trucks are always white; outbound trucks are light grey,
///    light blue or light green, chosen from the customer so a customer's truck is the same colour every visit.
///  - Logo: the two trailer decals that carry the Savage Dev logo show the VENDOR's icon on an inbound truck and the CUSTOMER's
///    icon on an outbound one.
/// </summary>
public static class TruckLivery
{
    // The cab body is this grey in the palette (sRGB byte value, found by tinting each bright palette cell and looking at the truck).
    // The bumper skirt (229) and the small lights (255) are different cells and stay as they are.
    private const int CabPaintGrey = 202;
    private const int PaintTolerance = 2;

    private static readonly Color32 Inbound  = new Color32(246, 247, 249, 255);   // white
    // Muted pastels: a mid-light grey base with a touch of colour. They are deliberately DARKER than "light" looks on paper: the Ultra grade
    // adds about +1 EV, which pushes anything above ~175 grey straight to white on screen (the first set read as plain white in game).
    private static readonly Color32[] OutboundPaints =
    {
        new Color32(132, 136, 144, 255),   // grey
        new Color32(96, 140, 200, 255),   // dusty blue
        new Color32(104, 168, 128, 255),   // sage green
    };

    private const int LogoSize = 256;

    private static readonly Dictionary<string, Texture2D> _paletteVariants = new();
    private static readonly Dictionary<Texture, Dictionary<int, Material>> _bodyMaterials = new();
    private static readonly Dictionary<(Sprite, bool), Material> _logoMaterials = new();
    private static readonly Dictionary<Sprite, Texture2D> _logoTextures = new();
    private static bool _warned;

    /// <summary>Domain reload is off in this project, so statics outlive Play sessions.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _paletteVariants.Clear();
        _bodyMaterials.Clear();
        _logoMaterials.Clear();
        _logoTextures.Clear();
        _warned = false;
    }

    public static void Apply(TruckController truck)
    {
        if (truck == null) return;
        try
        {
            string party = PartyIdFor(truck, out Sprite icon);
            ApplyPaint(truck, party);
            ApplyLogo(truck, icon);
        }
        catch (System.Exception e)
        {
            if (!_warned) { _warned = true; Debug.LogWarning("[TruckLivery] Could not dress " + truck.name + ": " + e.Message); }
        }
    }

    // ── who owns this truck ───────────────────────────────────────────────────

    private static string PartyIdFor(TruckController truck, out Sprite icon)
    {
        icon = null;
        if (truck.IsOutbound)
        {
            string customerId = null;
            if (ServiceLocator.TryGet(out DockScheduleService sched) && sched != null && !string.IsNullOrEmpty(truck.OutboundAppointmentId))
                customerId = sched.FindById(truck.OutboundAppointmentId)?.CustomerId;

            var customers = CustomerRegistry.Load();
            if (customers != null)
            {
                var c = customers.GetById(customerId);
                if (c == null && customers.customers.Count > 0)   // no customer on record: a stable stand-in so the truck still carries a logo
                    c = customers.customers[Mathf.Abs(StableHash(truck.name)) % customers.customers.Count];
                if (c != null) { icon = c.Icon; if (customerId == null) customerId = c.CustomerId; }
            }
            return customerId ?? truck.name;
        }

        string vendorId = truck.AssignedShipment != null ? truck.AssignedShipment.SupplierId : null;
        var vendors = VendorRegistry.Load();
        if (vendors != null)
        {
            var v = vendors.GetById(vendorId);
            if (v == null && vendors.vendors.Count > 0)
                v = vendors.vendors[Mathf.Abs(StableHash(vendorId ?? truck.name)) % vendors.vendors.Count];
            if (v != null) icon = v.Icon;
        }
        return vendorId ?? truck.name;
    }

    private static int StableHash(string s)
    {
        unchecked { int h = 17; foreach (char c in s ?? "") h = h * 31 + c; return h; }
    }

    // ── cab paint ─────────────────────────────────────────────────────────────

    private static void ApplyPaint(TruckController truck, string party)
    {
        var tractor = FindRenderer(truck.transform, "Tractor");
        if (tractor == null) return;

        var mats = tractor.sharedMaterials;
        if (mats == null || mats.Length == 0 || mats[0] == null) return;
        var palette = mats[0].mainTexture as Texture2D;
        if (palette == null || !palette.isReadable) return;   // cannot recolour a texture we cannot read; leave the truck as it is

        int variant = truck.IsOutbound ? 1 + Mathf.Abs(StableHash(party)) % OutboundPaints.Length : 0;
        if (!_bodyMaterials.TryGetValue(palette, out var byVariant)) _bodyMaterials[palette] = byVariant = new Dictionary<int, Material>();
        if (!byVariant.TryGetValue(variant, out var body) || body == null)
        {
            var tex = PaletteVariant(palette, variant);
            body = new Material(mats[0]) { name = mats[0].name + "_Cab" + variant };
            body.mainTexture = tex;
            if (body.HasProperty("_BaseMap")) body.SetTexture("_BaseMap", tex);
            byVariant[variant] = body;
        }

        mats[0] = body;
        tractor.sharedMaterials = mats;
    }

    private static Texture2D PaletteVariant(Texture2D source, int variant)
    {
        string key = source.name + "|" + source.width + "|" + variant;
        if (_paletteVariants.TryGetValue(key, out var cached) && cached != null) return cached;

        Color32 paint = variant == 0 ? Inbound : OutboundPaints[variant - 1];
        var px = source.GetPixels32();
        for (int i = 0; i < px.Length; i++)
        {
            var c = px[i];
            if (Mathf.Abs(c.r - CabPaintGrey) <= PaintTolerance && Mathf.Abs(c.g - c.r) <= PaintTolerance && Mathf.Abs(c.b - c.r) <= PaintTolerance)
                px[i] = new Color32(paint.r, paint.g, paint.b, c.a);
        }

        var tex = new Texture2D(source.width, source.height, TextureFormat.RGBA32, source.mipmapCount > 1)
        {
            name = source.name + "_Cab" + variant,
            filterMode = source.filterMode,
            wrapMode = source.wrapMode,
            anisoLevel = source.anisoLevel,
        };
        tex.SetPixels32(px);
        tex.Apply(true, false);
        _paletteVariants[key] = tex;
        return tex;
    }

    // ── logo ──────────────────────────────────────────────────────────────────

    private static void ApplyLogo(TruckController truck, Sprite icon)
    {
        if (icon == null || icon.texture == null) return;

        var decals = new List<MeshRenderer>();
        foreach (var r in truck.GetComponentsInChildren<MeshRenderer>(true))
            if (r.name.StartsWith("SavageDecal")) decals.Add(r);
        if (decals.Count == 0) return;

        var trailer = FindRenderer(truck.transform, "TrailerMesh");
        foreach (var d in decals)
        {
            // The decal quads were authored 2cm INSIDE the trailer wall, so no logo ever showed from outside. Move each just outside
            // its wall. Verified by rendering both sides: with the UVs as authored the text reads correctly from outside (an
            // earlier attempt that mirrored the texture put every logo backwards).
            PlaceOutsideWall(truck.transform, trailer, d);
            d.sharedMaterial = LogoMaterial(icon, d.sharedMaterial);
        }
    }

    private static Material LogoMaterial(Sprite icon, Material template)
    {
        var key = (icon, false);
        if (_logoMaterials.TryGetValue(key, out var mat) && mat != null) return mat;

        var tex = _logoTextures.TryGetValue(icon, out var t) && t != null ? t : (_logoTextures[icon] = BuildLogoTexture(icon));
        mat = new Material(template) { name = "TruckLogo_" + icon.name };
        mat.mainTexture = tex;
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);   // both faces: harmless, and the quad visible from either side
        var scale = Vector2.one; var offset = Vector2.zero;
        mat.mainTextureScale = scale; mat.mainTextureOffset = offset;
        if (mat.HasProperty("_BaseMap")) { mat.SetTextureScale("_BaseMap", scale); mat.SetTextureOffset("_BaseMap", offset); }
        _logoMaterials[key] = mat;
        return mat;
    }

    /// <summary>Moves the decal to just outside the trailer side it belongs to.</summary>
    private static bool PlaceOutsideWall(Transform root, MeshRenderer trailer, MeshRenderer decal)
    {
        var mf = decal.GetComponent<MeshFilter>();
        if (trailer == null || mf == null || mf.sharedMesh == null) return false;

        // trailer extent across the truck (root-local X)
        var tmesh = trailer.GetComponent<MeshFilter>().sharedMesh;
        var tb = tmesh.bounds; float minX = float.MaxValue, maxX = float.MinValue;
        for (int i = 0; i < 8; i++)
        {
            var c = tb.center + Vector3.Scale(tb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            float x = root.InverseTransformPoint(trailer.transform.TransformPoint(c)).x;
            minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
        }

        float dx = root.InverseTransformPoint(decal.bounds.center).x;
        bool right = dx > (minX + maxX) * 0.5f;
        float target = right ? maxX + 0.03f : minX - 0.03f;
        decal.transform.position += root.right * (target - dx);

        return false;
    }

    /// <summary>The icon on a white plate. The decal material is opaque, so the sprite's transparent parts have to be flattened
    /// onto something. Works for any sprite, readable or not, by drawing it into a render texture first.</summary>
    private static Texture2D BuildLogoTexture(Sprite sprite)
    {
        var src = sprite.texture;
        var r = sprite.textureRect;
        var scale = new Vector2(r.width / src.width, r.height / src.height);
        var offset = new Vector2(r.x / src.width, r.y / src.height);

        var rt = RenderTexture.GetTemporary(LogoSize, LogoSize, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var prev = RenderTexture.active;
        Graphics.Blit(src, rt, scale, offset);
        RenderTexture.active = rt;
        var tex = new Texture2D(LogoSize, LogoSize, TextureFormat.RGBA32, true) { name = "TruckLogo_" + sprite.name, wrapMode = TextureWrapMode.Clamp };
        tex.ReadPixels(new Rect(0, 0, LogoSize, LogoSize), 0, 0);
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);

        var px = tex.GetPixels32();
        for (int i = 0; i < px.Length; i++)
        {
            var c = px[i];
            float a = c.a / 255f;
            px[i] = new Color32((byte)(c.r * a + 255 * (1f - a)), (byte)(c.g * a + 255 * (1f - a)), (byte)(c.b * a + 255 * (1f - a)), 255);
        }
        tex.SetPixels32(px);
        tex.Apply(true, false);
        return tex;
    }

    private static MeshRenderer FindRenderer(Transform root, string name)
    {
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            if (r.name == name) return r;
        return null;
    }
}
