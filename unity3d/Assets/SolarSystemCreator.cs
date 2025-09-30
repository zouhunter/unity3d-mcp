using UnityEngine;
using UnityEditor;

public class SolarSystemCreator : MonoBehaviour
{
    [MenuItem("Tools/Create Solar System")]
    public static void CreateSolarSystem()
    {
        // 创建太阳系父对象
        GameObject solarSystem = new GameObject("Solar System");

        // 创建太阳 (Sun)
        GameObject sun = CreatePlanet("Sun", Vector3.zero, 2.0f, Color.yellow, solarSystem.transform);

        // 添加发光效果给太阳
        Light sunLight = sun.AddComponent<Light>();
        sunLight.type = LightType.Point;
        sunLight.color = Color.yellow;
        sunLight.intensity = 2.0f;
        sunLight.range = 50.0f;

        // 创建行星轨道和行星
        CreatePlanetWithOrbit("Mercury", 4.0f, 0.3f, Color.gray, solarSystem.transform, 2.0f);
        CreatePlanetWithOrbit("Venus", 6.0f, 0.4f, new Color(1.0f, 0.8f, 0.4f), solarSystem.transform, 1.5f);

        // 地球系统
        GameObject earthOrbit = CreatePlanetWithOrbit("Earth", 8.0f, 0.5f, Color.blue, solarSystem.transform, 1.0f);
        GameObject earth = earthOrbit.transform.GetChild(0).gameObject;

        // 创建月球
        GameObject moonOrbit = new GameObject("Moon Orbit");
        moonOrbit.transform.SetParent(earth.transform);
        moonOrbit.transform.localPosition = Vector3.zero;

        GameObject moon = CreatePlanet("Moon", new Vector3(1.5f, 0, 0), 0.15f, Color.white, moonOrbit.transform);

        // 添加月球旋转脚本
        PlanetRotator moonRotator = moonOrbit.AddComponent<PlanetRotator>();
        moonRotator.rotationSpeed = 90.0f; // 月球绕地球

        CreatePlanetWithOrbit("Mars", 11.0f, 0.4f, Color.red, solarSystem.transform, 0.8f);
        CreatePlanetWithOrbit("Jupiter", 15.0f, 1.2f, new Color(0.8f, 0.6f, 0.4f), solarSystem.transform, 0.5f);
        CreatePlanetWithOrbit("Saturn", 20.0f, 1.0f, new Color(0.9f, 0.8f, 0.6f), solarSystem.transform, 0.3f);

        // 为土星添加土星环
        GameObject saturn = solarSystem.transform.Find("Saturn Orbit/Saturn").gameObject;
        CreateSaturnRings(saturn);

        CreatePlanetWithOrbit("Uranus", 25.0f, 0.8f, Color.cyan, solarSystem.transform, 0.2f);
        CreatePlanetWithOrbit("Neptune", 30.0f, 0.8f, Color.blue, solarSystem.transform, 0.1f);

        // 创建一些小行星带
        CreateAsteroidBelt(solarSystem.transform);

        // 选中太阳系对象
        Selection.activeGameObject = solarSystem;

        Debug.Log("太阳系已创建完成！包含太阳、8大行星、月球、土星环和小行星带。");
    }

    private static GameObject CreatePlanet(string name, Vector3 position, float scale, Color color, Transform parent)
    {
        GameObject planet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        planet.name = name;
        planet.transform.SetParent(parent);
        planet.transform.localPosition = position;
        planet.transform.localScale = Vector3.one * scale;

        // 设置材质
        Renderer renderer = planet.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material material = new Material(Shader.Find("Standard"));
            material.color = color;
            if (name == "Sun")
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 0.5f);
            }
            renderer.material = material;
        }

        // 添加自转
        PlanetRotator rotator = planet.AddComponent<PlanetRotator>();
        rotator.rotationSpeed = Random.Range(10f, 50f);
        rotator.isOrbiting = false;

        return planet;
    }

    private static GameObject CreatePlanetWithOrbit(string name, float orbitRadius, float planetScale, Color color, Transform parent, float orbitSpeed)
    {
        // 创建轨道对象
        GameObject orbit = new GameObject(name + " Orbit");
        orbit.transform.SetParent(parent);
        orbit.transform.localPosition = Vector3.zero;

        // 创建行星
        GameObject planet = CreatePlanet(name, new Vector3(orbitRadius, 0, 0), planetScale, color, orbit.transform);

        // 添加轨道旋转
        PlanetRotator orbitRotator = orbit.AddComponent<PlanetRotator>();
        orbitRotator.rotationSpeed = orbitSpeed;
        orbitRotator.isOrbiting = true;

        return orbit;
    }

    private static void CreateSaturnRings(GameObject saturn)
    {
        for (int i = 0; i < 3; i++)
        {
            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Saturn Ring " + (i + 1);
            ring.transform.SetParent(saturn.transform);
            ring.transform.localPosition = Vector3.zero;
            ring.transform.localRotation = Quaternion.Euler(90, 0, 0);

            float ringScale = 2.5f + i * 0.3f;
            ring.transform.localScale = new Vector3(ringScale, 0.01f, ringScale);

            // 设置环的材质
            Renderer renderer = ring.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material material = new Material(Shader.Find("Standard"));
                material.color = new Color(0.8f, 0.7f, 0.5f, 0.7f);
                material.SetFloat("_Mode", 3); // 设置为透明模式
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.DisableKeyword("_ALPHATEST_ON");
                material.EnableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = 3000;
                renderer.material = material;
            }

            // 移除碰撞体
            DestroyImmediate(ring.GetComponent<Collider>());
        }
    }

    private static void CreateAsteroidBelt(Transform parent)
    {
        GameObject asteroidBelt = new GameObject("Asteroid Belt");
        asteroidBelt.transform.SetParent(parent);
        asteroidBelt.transform.localPosition = Vector3.zero;

        // 在火星和木星之间创建小行星带
        for (int i = 0; i < 50; i++)
        {
            float angle = i * (360f / 50f);
            float radius = Random.Range(12f, 14f);
            float height = Random.Range(-0.5f, 0.5f);

            Vector3 position = new Vector3(
                Mathf.Cos(angle * Mathf.Deg2Rad) * radius,
                height,
                Mathf.Sin(angle * Mathf.Deg2Rad) * radius
            );

            GameObject asteroid = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            asteroid.name = "Asteroid " + (i + 1);
            asteroid.transform.SetParent(asteroidBelt.transform);
            asteroid.transform.localPosition = position;
            asteroid.transform.localScale = Vector3.one * Random.Range(0.05f, 0.15f);

            // 设置小行星材质
            Renderer renderer = asteroid.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material material = new Material(Shader.Find("Standard"));
                material.color = new Color(0.5f, 0.4f, 0.3f);
                renderer.material = material;
            }

            // 添加随机旋转
            PlanetRotator rotator = asteroid.AddComponent<PlanetRotator>();
            rotator.rotationSpeed = Random.Range(50f, 200f);
            rotator.isOrbiting = false;
        }

        // 让整个小行星带缓慢旋转
        PlanetRotator beltRotator = asteroidBelt.AddComponent<PlanetRotator>();
        beltRotator.rotationSpeed = 0.1f;
        beltRotator.isOrbiting = true;
    }
}

// 行星旋转脚本
public class PlanetRotator : MonoBehaviour
{
    public float rotationSpeed = 10f;
    public bool isOrbiting = false; // 是否是轨道旋转

    void Update()
    {
        if (isOrbiting)
        {
            // 轨道旋转
            transform.Rotate(0, rotationSpeed * Time.deltaTime, 0);
        }
        else
        {
            // 自转
            transform.Rotate(rotationSpeed * Time.deltaTime, 0, 0);
        }
    }
}
