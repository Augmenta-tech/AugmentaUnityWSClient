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

    [Header("Appearance")]
    [Tooltip("Time taken by a system to spiral out to its full size when its cluster enters, in seconds")]
    public float appearDuration = .8f;
    [Tooltip("Time taken by a system to spiral back into its core and vanish when its cluster leaves, in seconds")]
    public float disappearDuration = 1f;

    private struct Sphere
    {
        public Transform transform;
        public Transform planet;
        public TrailRenderer trail;
        public Vector3 planetScale;
        public float trailBaseWidth;
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

    // 0 = fully gone, 1 = fully grown. Everything visual is scaled by it, so nothing ever pops.
    private float presence;
    private float presenceTarget;

    // Reused for every per-instance color, to avoid leaking a material clone per renderer
    private MaterialPropertyBlock propertyBlock;

    /// <summary>
    /// True once the system has finished disappearing, and can be destroyed.
    /// </summary>
    public bool hasDisappeared => presenceTarget <= 0f && presence <= 0f;

    /// <summary>
    /// Position an orbiting system takes to follow the given cluster: the bottom of its bounding box.
    /// </summary>
    public static Vector3 GetFollowPosition(AugmentaCluster augmentaCluster)
    {
        // The cluster position is the center of its box, so go down half its height to orbit at floor level
        return augmentaCluster.transform.position - augmentaCluster.transform.up * (augmentaCluster.boxSize.y * .5f);
    }

    public void Initialize(AugmentaCluster augmentaCluster, Color clusterColor)
    {
        this.augmentaCluster = augmentaCluster;
        this.clusterColor = clusterColor;
        augmentaCluster.onUpdate.AddListener(OnObjectUpdate);

        propertyBlock = new MaterialPropertyBlock();
        starPulsePhase = Random.Range(0f, Mathf.PI * 2f);

        presence = 0f;
        presenceTarget = 1f;

        ReadClusterTargets();

        // Start on the cluster
        transform.position = targetPosition;
        baseRadius = targetRadius;

        SetupStar();
        SetupLight();
        SpawnSpheres();

        // Nothing must be visible on the first frame, the system grows from there
        ApplyPresence(0f);
    }

    public void Shutdown()
    {
        if (!augmentaCluster)
        {
            return;
        }

        augmentaCluster.onUpdate.RemoveListener(OnObjectUpdate);
        augmentaCluster = null;
    }

    /// <summary>
    /// Starts the disappearance. The system unbinds from its cluster but keeps running, frozen on
    /// its last known position, until it has fully spiraled back into its core.
    /// </summary>
    public void BeginDisappear()
    {
        Shutdown();
        presenceTarget = 0f;
    }

    /// <summary>
    /// Re-binds a disappearing system to a new cluster and grows it back from its current size,
    /// keeping its colors and planets so a flickering tracking does not restart the system.
    /// </summary>
    public void Revive(AugmentaCluster augmentaCluster)
    {
        this.augmentaCluster = augmentaCluster;
        augmentaCluster.onUpdate.AddListener(OnObjectUpdate);

        presenceTarget = 1f;

        // Glide to the new cluster instead of snapping on it
        ReadClusterTargets();
    }

    private void Update()
    {
        if (spheres == null)
        {
            return;
        }

        // MoveTowards, not SmoothDamp: the fade must reach exactly 0 in a known time, the owner
        // destroys the system on that
        float presenceDuration = presenceTarget > presence ? appearDuration : disappearDuration;
        presence = presenceDuration > 0f
            ? Mathf.MoveTowards(presence, presenceTarget, Time.deltaTime / presenceDuration)
            : presenceTarget;
        float eased = Mathf.SmoothStep(0f, 1f, presence);

        // Ease towards clusters instead of snapping
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref followVelocity, followSmoothTime);
        baseRadius = Mathf.SmoothDamp(baseRadius, targetRadius, ref radiusVelocity, followSmoothTime);

        for (int i = 0; i < spheres.Length; ++i)
        {
            spheres[i].angle += spheres[i].speed * Time.deltaTime;

            // Planets spiral out of the star as the system appears, and back into it as it leaves
            float radius = baseRadius * spheres[i].radiusFactor * eased;
            float rad = spheres[i].angle * Mathf.Deg2Rad;
            Vector3 onRing = new Vector3(Mathf.Cos(rad), 0, Mathf.Sin(rad)) * radius;
            spheres[i].transform.localPosition = spheres[i].orbitRotation * onRing;
        }

        ApplyPresence(eased);
    }

    private void OnObjectUpdate(AugmentaObject obj)
    {
        Assert.AreEqual(obj, augmentaCluster);
        ReadClusterTargets();
    }

    private void ReadClusterTargets()
    {
        targetPosition = GetFollowPosition(augmentaCluster);
        targetRadius = Mathf.Max(augmentaCluster.boxSize.x, augmentaCluster.boxSize.z) * .5f * radiusScale;
    }

    /// <summary>
    /// Scales everything that has a size or a brightness by the eased presence, so the system grows
    /// out of nothing and shrinks back into it.
    /// </summary>
    private void ApplyPresence(float eased)
    {
        if (spheres != null)
        {
            for (int i = 0; i < spheres.Length; ++i)
            {
                if (spheres[i].planet)
                {
                    spheres[i].planet.localScale = spheres[i].planetScale * eased;
                }

                if (spheres[i].trail)
                {
                    spheres[i].trail.widthMultiplier = spheres[i].trailBaseWidth * eased;
                }
            }
        }

        UpdateStar(eased);
        UpdateLight(eased);
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

    private void UpdateStar(float presence)
    {
        if (!starRenderer)
        {
            return;
        }

        float pulse = 1f + Mathf.Sin(Time.time * starPulseSpeed + starPulsePhase) * starPulseAmplitude;

        // The minimum size is a minimum of the living star, presence shrinks it past that to nothing
        float size = Mathf.Max(minStarSize, baseRadius * starScale) * pulse * presence;
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

    private void UpdateLight(float presence)
    {
        if (!clusterLight)
        {
            return;
        }

        // Bigger clusters light a wider area around them
        clusterLight.range = baseRadius * lightRangeScale;
        clusterLight.intensity = lightIntensity * presence;
    }

    private void SpawnSpheres()
    {
        Assert.IsNotNull(orbitingSpherePrefab);

        spheres = new Sphere[sphereCount];

        for (int i = 0; i < sphereCount; ++i)
        {
            GameObject sphere = Instantiate(orbitingSpherePrefab, transform);

            spheres[i] = new Sphere
            {
                transform = sphere.transform,
                planetScale = Vector3.one * sphereScale * Random.Range(1f - sphereScaleJitter, 1f + sphereScaleJitter),
                angle = Random.Range(0f, 360f),
                speed = Random.Range(speedRange.x, speedRange.y) * (Random.value < .5f ? -1f : 1f),
                radiusFactor = Random.Range(1f - radiusJitter, 1f + radiusJitter),
                orbitRotation = Quaternion.Euler(Random.Range(-maxInclination, maxInclination), Random.Range(0f, 360f), 0f),
            };

            SetupPlanet(ref spheres[i], sphere, GetPlanetColor());
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
    /// Colors the planet mesh of a freshly spawned sphere, tints its trail to match, and caches the
    /// transform and trail that the presence scales every frame.
    /// Everything else about the planet and its trail comes from the sphere prefab.
    /// </summary>
    private void SetupPlanet(ref Sphere sphere, GameObject sphereObject, Color color)
    {
        // The prefab root stays unscaled, so the planet scale does not affect the trail width
        MeshRenderer planetRenderer = sphereObject.GetComponentInChildren<MeshRenderer>();
        if (planetRenderer)
        {
            sphere.planet = planetRenderer.transform;

            planetRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_BaseColor", color);
            propertyBlock.SetColor("_EmissionColor", color * planetEmission);
            planetRenderer.SetPropertyBlock(propertyBlock);
        }

        TrailRenderer trail = sphereObject.GetComponentInChildren<TrailRenderer>();
        if (trail)
        {
            sphere.trail = trail;
            sphere.trailBaseWidth = trail.widthMultiplier;

            trail.startColor = color;
            trail.endColor = new Color(color.r, color.g, color.b, 0f);
        }
    }
}
