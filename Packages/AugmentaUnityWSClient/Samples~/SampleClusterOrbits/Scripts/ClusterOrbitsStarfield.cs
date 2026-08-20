using UnityEngine;

/// <summary>
/// Builds a static backdrop of twinkling stars, as a single mesh of camera facing quads spread over
/// a rectangle. Brightness is carried by vertex colors, so all the stars share one material.
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ClusterOrbitsStarfield : MonoBehaviour
{
    [Header("Field")]
    public int starCount = 300;
    [Tooltip("Size of the rectangle the stars are spread over, in meters")]
    public Vector2 area = new(40f, 40f);
    [Tooltip("Random depth spread around the object plane, in meters")]
    public float depthSpread = 1f;
    public Vector2 sizeRange = new(.02f, .1f);

    [Header("Color")]
    [Tooltip("Stars are tinted by a random pick in this gradient")]
    public Gradient colorGradient = DefaultGradient();
    public Vector2 brightnessRange = new(.15f, 1f);

    [Header("Twinkle")]
    public bool twinkle = true;
    [Tooltip("How much a star dims when it twinkles")]
    [Range(0f, 1f)] public float twinkleDepth = .6f;
    public Vector2 twinkleSpeedRange = new(.2f, 1.2f);

    private Mesh mesh;
    private Color[] colors;
    private Color[] baseColors;
    private float[] twinkleSpeeds;
    private float[] twinklePhases;

    private void Start()
    {
        Build();
    }

    private void Update()
    {
        if (!twinkle || mesh == null)
        {
            return;
        }

        for (int i = 0; i < twinkleSpeeds.Length; ++i)
        {
            float wave = .5f + .5f * Mathf.Sin(Time.time * twinkleSpeeds[i] + twinklePhases[i]);
            float factor = Mathf.Lerp(1f - twinkleDepth, 1f, wave);

            for (int v = 0; v < 4; ++v)
            {
                Color color = baseColors[i * 4 + v];
                colors[i * 4 + v] = new Color(color.r, color.g, color.b, color.a * factor);
            }
        }

        mesh.SetColors(colors);
    }

    private void Build()
    {
        Vector3[] vertices = new Vector3[starCount * 4];
        Vector2[] uvs = new Vector2[starCount * 4];
        int[] triangles = new int[starCount * 6];

        colors = new Color[starCount * 4];
        baseColors = new Color[starCount * 4];
        twinkleSpeeds = new float[starCount];
        twinklePhases = new float[starCount];

        for (int i = 0; i < starCount; ++i)
        {
            Vector3 center = new(
                Random.Range(-area.x, area.x) * .5f,
                Random.Range(-depthSpread, depthSpread) * .5f,
                Random.Range(-area.y, area.y) * .5f);

            // Quads lie flat on the field plane, which reads as a starry ground from a top down camera
            float half = Random.Range(sizeRange.x, sizeRange.y) * .5f;
            int v0 = i * 4;
            vertices[v0 + 0] = center + new Vector3(-half, 0, -half);
            vertices[v0 + 1] = center + new Vector3(-half, 0, half);
            vertices[v0 + 2] = center + new Vector3(half, 0, half);
            vertices[v0 + 3] = center + new Vector3(half, 0, -half);

            uvs[v0 + 0] = new Vector2(0, 0);
            uvs[v0 + 1] = new Vector2(0, 1);
            uvs[v0 + 2] = new Vector2(1, 1);
            uvs[v0 + 3] = new Vector2(1, 0);

            Color color = colorGradient.Evaluate(Random.value);
            color.a = Random.Range(brightnessRange.x, brightnessRange.y);
            for (int v = 0; v < 4; ++v)
            {
                baseColors[v0 + v] = color;
                colors[v0 + v] = color;
            }

            int t0 = i * 6;
            triangles[t0 + 0] = v0 + 0;
            triangles[t0 + 1] = v0 + 1;
            triangles[t0 + 2] = v0 + 2;
            triangles[t0 + 3] = v0 + 0;
            triangles[t0 + 4] = v0 + 2;
            triangles[t0 + 5] = v0 + 3;

            twinkleSpeeds[i] = Random.Range(twinkleSpeedRange.x, twinkleSpeedRange.y);
            twinklePhases[i] = Random.Range(0f, Mathf.PI * 2f);
        }

        mesh = new Mesh { name = "Starfield" };
        mesh.indexFormat = starCount * 4 > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetColors(colors);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();

        GetComponent<MeshFilter>().mesh = mesh;
    }

    private void OnDestroy()
    {
        if (mesh != null)
        {
            Destroy(mesh);
        }
    }

    private static Gradient DefaultGradient()
    {
        Gradient gradient = new();
        gradient.SetKeys(
            new GradientColorKey[]
            {
                new(new Color(.60f, .74f, 1f), 0f),
                new(new Color(1f, 1f, 1f), .55f),
                new(new Color(1f, .82f, .62f), 1f),
            },
            new GradientAlphaKey[] { new(1f, 0f), new(1f, 1f) });
        return gradient;
    }
}
