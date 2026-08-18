using AugmentaWebsocketClient;
using UnityEngine;
using UnityEngine.Assertions;

/// <summary>
/// Follows an Augmenta cluster and turns it into a small star system: a glowing core, a light of
/// the cluster color, and planets orbiting on tilted planes in shades of that same color.
/// </summary>
public class OrbitingCluster : MonoBehaviour
{
    [HideInInspector] public AugmentaCluster augmentaCluster;

    [Header("Planets")]
    public GameObject orbitingSpherePrefab;
    [Tooltip("Number of planets orbiting around each cluster")]
    public int sphereCount = 6;
    [Tooltip("Scale applied to each spawned planet")]
    public float sphereScale = .1f;
    [Tooltip("Random variation of the planet scale, per planet")]
    [Range(0f, 1f)] public float sphereScaleJitter = .4f;

    [Header("Planet colors")]
    [Tooltip("Random hue offset applied to the cluster color, per planet")]
    [Range(0f, .2f)] public float hueVariation = .02f;
    [Tooltip("Random saturation multiplier applied to the cluster color, per planet")]
    public Vector2 saturationRange = new(.6f, 1.1f);
    [Tooltip("Random brightness multiplier applied to the cluster color, per planet")]
    public Vector2 brightnessRange = new(.45f, 1f);
    [Tooltip("How much the planets glow with their own color, so their night side is not pitch black")]
    public float planetEmission = .3f;

    [Header("Orbit")]
    [Tooltip("Multiplier applied to the orbit radius deduced from the cluster size")]
    public float radiusScale = 1f;
    [Tooltip("Random variation of the orbit radius, per planet")]
    [Range(0f, 1f)] public float radiusJitter = .25f;
    [Tooltip("Orbiting speed range, in degrees per second. Direction is randomized per planet.")]
    public Vector2 speedRange = new(30f, 120f);
    [Tooltip("Maximum tilt of an orbit plane, in degrees. 0 makes all planets orbit in the same flat ring.")]
    [Range(0f, 90f)] public float maxInclination = 25f;

    [Header("Star")]
    [Tooltip("Renderer of the glowing core at the center of the system")]
    public Renderer starRenderer;
    [Tooltip("Diameter of the star, as a fraction of the orbit radius")]
    public float starScale = .5f;
    [Tooltip("Minimum diameter of the star, in meters")]
    public float minStarSize = .08f;
    [Tooltip("Emission multiplier of the star. Lower it if the cores blow out to white.")]
    public float starEmission = 4f;
    [Tooltip("Relative size variation of the star pulse")]
    [Range(0f, 1f)] public float starPulseAmplitude = .08f;
    public float starPulseSpeed = 1.5f;

    [Header("Light")]
    public Light clusterLight;
    public float lightIntensity = 3f;
    [Tooltip("Light range, as a multiple of the orbit radius")]
    public float lightRangeScale = 8f;

    [Header("Follow")]
    [Tooltip("Approximate time taken to catch up with the cluster position and size. Higher is smoother, but lags more.")]
    public float followSmoothTime = .2f;

    private struct Sphere
    {
        public Transform transform;
        public float angle;
        public float speed;
        public float radiusFactor;
        public Quaternion orbitRotation;
    }

    private Sphere[] spheres;

    private Color clusterColor = Color.white;

    // Random phase so several clusters don't pulse in sync
    private float starPulsePhase;

    // Latest values received from the cluster, smoothly caught up with in Update
    private Vector3 targetPosition;
    private float targetRadius;

    // Orbit radius currently used, smoothed towards targetRadius
    private float baseRadius;

    private Vector3 followVelocity;
    private float radiusVelocity;

    // Reused for every per-instance color, to avoid leaking a material clone per renderer
    private MaterialPropertyBlock propertyBlock;

    public void Initialize(AugmentaCluster augmentaCluster, Color clusterColor)
    {
        this.augmentaCluster = augmentaCluster;
        this.clusterColor = clusterColor;
        augmentaCluster.onUpdate.AddListener(OnObjectUpdate);

        propertyBlock = new MaterialPropertyBlock();
        starPulsePhase = Random.Range(0f, Mathf.PI * 2f);

        ReadClusterTargets();

        // Start on the cluster
        transform.position = targetPosition;
        baseRadius = targetRadius;

        SetupStar();
        SetupLight();
        SpawnSpheres();
    }

    public void Shutdown()
    {
        augmentaCluster.onUpdate.RemoveListener(OnObjectUpdate);
        augmentaCluster = null;
    }

    private void Update()
    {
        if (spheres == null)
        {
            return;
        }

        // Ease towards clusters instead of snapping
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref followVelocity, followSmoothTime);
        baseRadius = Mathf.SmoothDamp(baseRadius, targetRadius, ref radiusVelocity, followSmoothTime);

        for (int i = 0; i < spheres.Length; ++i)
        {
            spheres[i].angle += spheres[i].speed * Time.deltaTime;

            float radius = baseRadius * spheres[i].radiusFactor;
            float rad = spheres[i].angle * Mathf.Deg2Rad;
            Vector3 onRing = new Vector3(Mathf.Cos(rad), 0, Mathf.Sin(rad)) * radius;
            spheres[i].transform.localPosition = spheres[i].orbitRotation * onRing;
        }

        UpdateStar();
        UpdateLight();
    }

    private void OnObjectUpdate(AugmentaObject obj)
    {
        Assert.AreEqual(obj, augmentaCluster);
        ReadClusterTargets();
    }

    private void ReadClusterTargets()
    {
        Vector3 boxSize = augmentaCluster.boxSize;

        // The cluster position is the center of its box, so go down half its height to orbit at floor level
        targetPosition = augmentaCluster.transform.position - augmentaCluster.transform.up * (boxSize.y * .5f);

        targetRadius = Mathf.Max(boxSize.x, boxSize.z) * .5f * radiusScale;
    }

    private void SetupStar()
    {
        if (!starRenderer)
        {
            return;
        }

        starRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor("_BaseColor", Color.black);
        propertyBlock.SetColor("_EmissionColor", clusterColor * starEmission);
        starRenderer.SetPropertyBlock(propertyBlock);
    }

    private void UpdateStar()
    {
        if (!starRenderer)
        {
            return;
        }

        float pulse = 1f + Mathf.Sin(Time.time * starPulseSpeed + starPulsePhase) * starPulseAmplitude;
        float size = Mathf.Max(minStarSize, baseRadius * starScale) * pulse;
        starRenderer.transform.localScale = Vector3.one * size;
    }

    private void SetupLight()
    {
        if (!clusterLight)
        {
            return;
        }

        clusterLight.color = clusterColor;
        clusterLight.intensity = lightIntensity;
    }

    private void UpdateLight()
    {
        if (!clusterLight)
        {
            return;
        }

        // Bigger clusters light a wider area around them
        clusterLight.range = baseRadius * lightRangeScale;
    }

    private void SpawnSpheres()
    {
        Assert.IsNotNull(orbitingSpherePrefab);

        spheres = new Sphere[sphereCount];

        for (int i = 0; i < sphereCount; ++i)
        {
            GameObject sphere = Instantiate(orbitingSpherePrefab, transform);
            Vector3 planetScale = Vector3.one * sphereScale * Random.Range(1f - sphereScaleJitter, 1f + sphereScaleJitter);
            SetupPlanet(sphere, planetScale, GetPlanetColor());

            spheres[i] = new Sphere
            {
                transform = sphere.transform,
                angle = Random.Range(0f, 360f),
                speed = Random.Range(speedRange.x, speedRange.y) * (Random.value < .5f ? -1f : 1f),
                radiusFactor = Random.Range(1f - radiusJitter, 1f + radiusJitter),
                orbitRotation = Quaternion.Euler(Random.Range(-maxInclination, maxInclination), Random.Range(0f, 360f), 0f),
            };
        }
    }

    /// <summary>
    /// Returns a variation of the cluster color, so all the planets of a system look related while
    /// staying distinct from the planets of the neighbouring systems.
    /// </summary>
    private Color GetPlanetColor()
    {
        Color.RGBToHSV(clusterColor, out float h, out float s, out float v);

        h = Mathf.Repeat(h + Random.Range(-hueVariation, hueVariation), 1f);
        s = Mathf.Clamp01(s * Random.Range(saturationRange.x, saturationRange.y));
        v = Mathf.Clamp01(v * Random.Range(brightnessRange.x, brightnessRange.y));

        return Color.HSVToRGB(h, s, v);
    }

    /// <summary>
    /// Scales and colors the planet mesh of a freshly spawned sphere, and tints its trail to match.
    /// Everything else about the planet and its trail comes from the sphere prefab.
    /// </summary>
    private void SetupPlanet(GameObject sphere, Vector3 scale, Color color)
    {
        // The prefab root stays unscaled, so the planet scale does not affect the trail width
        MeshRenderer planetRenderer = sphere.GetComponentInChildren<MeshRenderer>();
        if (planetRenderer)
        {
            planetRenderer.transform.localScale = scale;

            planetRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_BaseColor", color);
            propertyBlock.SetColor("_EmissionColor", color * planetEmission);
            planetRenderer.SetPropertyBlock(propertyBlock);
        }

        TrailRenderer trail = sphere.GetComponentInChildren<TrailRenderer>();
        if (trail)
        {
            trail.startColor = color;
            trail.endColor = new Color(color.r, color.g, color.b, 0f);
        }
    }
}
