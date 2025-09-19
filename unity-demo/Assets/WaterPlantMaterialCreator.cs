using UnityEngine;
using UnityEditor;
using System.IO;

public class WaterPlantMaterialCreator : MonoBehaviour
{
    [MenuItem("Tools/Create Water Plant Materials")]
    public static void CreateWaterPlantMaterials()
    {
        string textureFolder = "Assets/Textures/WaterPlant";
        string materialFolder = "Assets/Materials/WaterPlant";

        // 创建材质文件夹
        if (!Directory.Exists(materialFolder))
        {
            Directory.CreateDirectory(materialFolder);
            AssetDatabase.Refresh();
        }

        // 创建各种材质
        CreateMaterialsFromTextures(textureFolder, materialFolder);

        Debug.Log("自来水厂材质创建完成！");

        // 刷新资源数据库
        AssetDatabase.Refresh();
    }

    [MenuItem("Tools/Apply Materials to Water Plant")]
    public static void ApplyMaterialsToWaterPlant()
    {
        // 首先创建自来水厂
        WaterTreatmentPlantCreator.CreateWaterTreatmentPlant();

        // 然后应用材质
        GameObject waterPlant = GameObject.Find("Water Treatment Plant");
        if (waterPlant == null)
        {
            Debug.LogError("找不到自来水厂对象！请先创建自来水厂。");
            return;
        }

        ApplyMaterialsToBuildings(waterPlant);

        Debug.Log("材质已应用到自来水厂建筑！");
    }

    private static void CreateMaterialsFromTextures(string textureFolder, string materialFolder)
    {
        // 材质配置数据
        var materialConfigs = new[]
        {
            new { textureName = "concrete.jpg", materialName = "ConcreteMaterial", color = Color.white, metallic = 0.1f, smoothness = 0.3f },
            new { textureName = "brick.jpg", materialName = "BrickMaterial", color = new Color(0.9f, 0.7f, 0.6f), metallic = 0.0f, smoothness = 0.4f },
            new { textureName = "metal.jpg", materialName = "MetalMaterial", color = new Color(0.8f, 0.8f, 0.9f), metallic = 0.9f, smoothness = 0.8f },
            new { textureName = "metal_pipe.jpg", materialName = "MetalPipeMaterial", color = new Color(0.6f, 0.6f, 0.7f), metallic = 0.8f, smoothness = 0.7f },
            new { textureName = "water.jpg", materialName = "WaterMaterial", color = new Color(0.3f, 0.6f, 0.9f, 0.8f), metallic = 0.0f, smoothness = 0.9f },
            new { textureName = "water_surface.jpg", materialName = "WaterSurfaceMaterial", color = new Color(0.4f, 0.7f, 1.0f, 0.7f), metallic = 0.0f, smoothness = 0.95f },
            new { textureName = "roof.jpg", materialName = "RoofMaterial", color = new Color(0.7f, 0.5f, 0.4f), metallic = 0.1f, smoothness = 0.5f },
            new { textureName = "grass_texture.jpg", materialName = "GrassMaterial", color = new Color(0.3f, 0.8f, 0.2f), metallic = 0.0f, smoothness = 0.2f },
            new { textureName = "industrial_floor.jpg", materialName = "IndustrialFloorMaterial", color = new Color(0.6f, 0.6f, 0.6f), metallic = 0.2f, smoothness = 0.6f }
        };

        foreach (var config in materialConfigs)
        {
            CreateMaterial(textureFolder, materialFolder, config.textureName, config.materialName,
                         config.color, config.metallic, config.smoothness);
        }
    }

    private static void CreateMaterial(string textureFolder, string materialFolder, string textureName,
                                     string materialName, Color color, float metallic, float smoothness)
    {
        // 加载纹理
        string texturePath = Path.Combine(textureFolder, textureName).Replace("\\", "/");
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);

        // 创建材质
        Material material = new Material(Shader.Find("Standard"));
        material.name = materialName;
        material.color = color;
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Glossiness", smoothness);

        // 如果纹理存在，应用纹理
        if (texture != null)
        {
            material.mainTexture = texture;
            Debug.Log($"成功为材质 {materialName} 应用纹理 {textureName}");
        }
        else
        {
            Debug.LogWarning($"找不到纹理文件: {texturePath}");
        }

        // 设置透明材质
        if (color.a < 1.0f)
        {
            SetupTransparentMaterial(material);
        }

        // 保存材质
        string materialPath = Path.Combine(materialFolder, materialName + ".mat").Replace("\\", "/");
        AssetDatabase.CreateAsset(material, materialPath);

        Debug.Log($"创建材质: {materialName} -> {materialPath}");
    }

    private static void SetupTransparentMaterial(Material material)
    {
        material.SetFloat("_Mode", 3); // Transparent mode
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.renderQueue = 3000;
    }

    private static void ApplyMaterialsToBuildings(GameObject waterPlant)
    {
        string materialFolder = "Assets/Materials/WaterPlant";

        // 加载材质
        Material concreteMaterial = LoadMaterial(materialFolder, "ConcreteMaterial");
        Material brickMaterial = LoadMaterial(materialFolder, "BrickMaterial");
        Material metalMaterial = LoadMaterial(materialFolder, "MetalMaterial");
        Material metalPipeMaterial = LoadMaterial(materialFolder, "MetalPipeMaterial");
        Material waterMaterial = LoadMaterial(materialFolder, "WaterMaterial");
        Material waterSurfaceMaterial = LoadMaterial(materialFolder, "WaterSurfaceMaterial");
        Material roofMaterial = LoadMaterial(materialFolder, "RoofMaterial");
        Material grassMaterial = LoadMaterial(materialFolder, "GrassMaterial");
        Material industrialFloorMaterial = LoadMaterial(materialFolder, "IndustrialFloorMaterial");

        // 应用材质到不同的建筑组件
        ApplyMaterialToObjects(waterPlant, "Foundation", concreteMaterial);
        ApplyMaterialToObjects(waterPlant, "Main Structure", brickMaterial);
        ApplyMaterialToObjects(waterPlant, "Roof", roofMaterial);
        ApplyMaterialToObjects(waterPlant, "Window", null, true); // 保持透明窗户

        // 沉淀池和过滤池
        ApplyMaterialToObjects(waterPlant, "Sedimentation Tank", concreteMaterial);
        ApplyMaterialToObjects(waterPlant, "Filtration Tank", concreteMaterial);
        ApplyMaterialToObjects(waterPlant, "Clear Water Tank", concreteMaterial);

        // 水材质
        ApplyMaterialToObjects(waterPlant, "Dirty Water", waterMaterial);
        ApplyMaterialToObjects(waterPlant, "Clean Water", waterSurfaceMaterial);
        ApplyMaterialToObjects(waterPlant, "Stored Clean Water", waterSurfaceMaterial);

        // 泵站和设备
        ApplyMaterialToObjects(waterPlant, "Pump House", industrialFloorMaterial);
        ApplyMaterialToObjects(waterPlant, "Pump", metalMaterial);

        // 管道系统
        ApplyMaterialToObjects(waterPlant, "Pipeline", metalPipeMaterial);
        ApplyMaterialToObjects(waterPlant, "Main Pipeline", metalPipeMaterial);
        ApplyMaterialToObjects(waterPlant, "Storage Pipeline", metalPipeMaterial);
        ApplyMaterialToObjects(waterPlant, "Output Pipeline", metalPipeMaterial);

        // 化学品储罐
        ApplyMaterialToObjects(waterPlant, "Chemical Tank", metalMaterial);
        ApplyMaterialToObjects(waterPlant, "Chemical Storage Building", industrialFloorMaterial);

        // 办公楼
        ApplyMaterialToObjects(waterPlant, "Office", brickMaterial);
        ApplyMaterialToObjects(waterPlant, "Entrance", null, true); // 保持门的材质

        // 围墙和基础设施
        ApplyMaterialToObjects(waterPlant, "Wall", concreteMaterial);
        ApplyMaterialToObjects(waterPlant, "Gate", metalMaterial);

        // 景观
        ApplyMaterialToObjects(waterPlant, "Grass", grassMaterial);
        ApplyMaterialToObjects(waterPlant, "Tree Trunk", null, true); // 保持树干材质
        ApplyMaterialToObjects(waterPlant, "Tree Leaves", null, true); // 保持树叶材质

        // 道路
        ApplyMaterialToObjects(waterPlant, "Road", industrialFloorMaterial);
    }

    private static Material LoadMaterial(string materialFolder, string materialName)
    {
        string materialPath = Path.Combine(materialFolder, materialName + ".mat").Replace("\\", "/");
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);

        if (material == null)
        {
            Debug.LogWarning($"找不到材质文件: {materialPath}");
        }

        return material;
    }

    private static void ApplyMaterialToObjects(GameObject parent, string namePattern, Material material, bool skipIfNull = false)
    {
        if (material == null && !skipIfNull)
            return;

        Transform[] allTransforms = parent.GetComponentsInChildren<Transform>(true);

        foreach (Transform t in allTransforms)
        {
            if (t.gameObject.name.Contains(namePattern))
            {
                Renderer renderer = t.GetComponent<Renderer>();
                if (renderer != null && material != null)
                {
                    renderer.material = material;
                    Debug.Log($"应用材质 {material.name} 到 {t.gameObject.name}");
                }
            }
        }
    }
}

// 增强版的自来水厂创建器，集成材质应用
public class EnhancedWaterTreatmentPlantCreator : WaterTreatmentPlantCreator
{
    [MenuItem("Tools/Create Complete Water Treatment Plant")]
    public static void CreateCompleteWaterTreatmentPlant()
    {
        // 创建材质
        WaterPlantMaterialCreator.CreateWaterPlantMaterials();

        // 等待一帧让材质创建完成
        EditorApplication.delayCall += () =>
        {
            // 创建并应用材质到自来水厂
            WaterPlantMaterialCreator.ApplyMaterialsToWaterPlant();

            Debug.Log("完整的自来水厂（带材质）创建完成！");
        };
    }
}
