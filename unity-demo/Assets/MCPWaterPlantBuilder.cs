using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class MCPWaterPlantBuilder : MonoBehaviour
{
    [MenuItem("MCP Tools/Build Complete Water Treatment Plant")]
    public static void BuildCompleteWaterTreatmentPlant()
    {
        Debug.Log("开始使用MCP构建完整的自来水厂...");

        // 步骤1: 创建基础设施
        CreateWaterTreatmentFacility();

        // 步骤2: 创建并应用材质
        EditorApplication.delayCall += () =>
        {
            ApplyAdvancedMaterials();
            Debug.Log("✓ 自来水厂建设完成！包含完整的建筑结构和高质量材质。");
        };
    }

    private static void CreateWaterTreatmentFacility()
    {
        Debug.Log("正在创建自来水厂基础设施...");

        // 创建主容器
        GameObject waterPlant = new GameObject("MCP Water Treatment Plant");

        // 创建各个功能区域
        CreateProcessingZone(waterPlant.transform);
        CreateAdministrativeZone(waterPlant.transform);
        CreateUtilityZone(waterPlant.transform);
        CreateStorageZone(waterPlant.transform);
        CreateInfrastructureZone(waterPlant.transform);

        Selection.activeGameObject = waterPlant;
        Debug.Log("✓ 基础设施创建完成");
    }

    private static void CreateProcessingZone(Transform parent)
    {
        GameObject processingZone = new GameObject("Processing Zone");
        processingZone.transform.SetParent(parent);
        processingZone.transform.localPosition = Vector3.zero;

        // 主处理建筑
        CreateBuilding(processingZone.transform, "Main Treatment Building",
                      new Vector3(0, 0.1f, 0), new Vector3(15f, 8f, 12f), "MainBuilding");

        // 沉淀池群
        GameObject sedimentationArea = new GameObject("Sedimentation Complex");
        sedimentationArea.transform.SetParent(processingZone.transform);
        sedimentationArea.transform.localPosition = new Vector3(-20f, 0.1f, 15f);

        for (int i = 0; i < 4; i++)
        {
            Vector3 tankPos = new Vector3(i * 7f, 0, 0);
            CreateWaterTank(sedimentationArea.transform, $"Sedimentation Tank {i + 1}",
                           tankPos, new Vector3(6f, 2.5f, 6f), "SedimentationTank");

            // 添加污水
            CreateWaterLayer(sedimentationArea.transform, $"Dirty Water {i + 1}",
                           tankPos + new Vector3(0, 1.2f, 0), new Vector3(5.8f, 0.2f, 5.8f), "DirtyWater");
        }

        // 过滤池群
        GameObject filtrationArea = new GameObject("Filtration Complex");
        filtrationArea.transform.SetParent(processingZone.transform);
        filtrationArea.transform.localPosition = new Vector3(-20f, 0.1f, -15f);

        for (int i = 0; i < 3; i++)
        {
            Vector3 tankPos = new Vector3(i * 9f, 0, 0);
            CreateWaterTank(filtrationArea.transform, $"Filtration Tank {i + 1}",
                           tankPos, new Vector3(8f, 3.5f, 7f), "FiltrationTank");

            // 过滤介质层
            CreateFilterMedia(filtrationArea.transform, $"Filter Media {i + 1}",
                            tankPos + new Vector3(0, 1f, 0), new Vector3(7.5f, 1.5f, 6.5f));

            // 清水层
            CreateWaterLayer(filtrationArea.transform, $"Filtered Water {i + 1}",
                           tankPos + new Vector3(0, 2.8f, 0), new Vector3(7.8f, 0.3f, 6.8f), "CleanWater");
        }
    }

    private static void CreateAdministrativeZone(Transform parent)
    {
        GameObject adminZone = new GameObject("Administrative Zone");
        adminZone.transform.SetParent(parent);
        adminZone.transform.localPosition = new Vector3(-15f, 0.1f, 25f);

        // 办公楼
        CreateBuilding(adminZone.transform, "Administration Building",
                      Vector3.zero, new Vector3(12f, 10f, 8f), "OfficeBuilding");

        // 控制室
        CreateBuilding(adminZone.transform, "Control Room",
                      new Vector3(15f, 0, 0), new Vector3(8f, 6f, 6f), "ControlRoom");

        // 实验室
        CreateBuilding(adminZone.transform, "Water Quality Lab",
                      new Vector3(-15f, 0, 0), new Vector3(10f, 6f, 8f), "Laboratory");
    }

    private static void CreateUtilityZone(Transform parent)
    {
        GameObject utilityZone = new GameObject("Utility Zone");
        utilityZone.transform.SetParent(parent);
        utilityZone.transform.localPosition = new Vector3(20f, 0.1f, -20f);

        // 泵站
        CreateBuilding(utilityZone.transform, "Main Pump Station",
                      Vector3.zero, new Vector3(12f, 8f, 10f), "PumpStation");

        // 泵设备
        for (int i = 0; i < 6; i++)
        {
            CreatePumpUnit(utilityZone.transform, $"Pump Unit {i + 1}",
                          new Vector3(-4f + (i % 3) * 4f, 1.5f, -2f + (i / 3) * 4f));
        }

        // 电力设施
        CreateBuilding(utilityZone.transform, "Power Distribution",
                      new Vector3(0, 0, 15f), new Vector3(8f, 6f, 6f), "PowerBuilding");

        // 变压器
        for (int i = 0; i < 2; i++)
        {
            CreateElectricalEquipment(utilityZone.transform, $"Transformer {i + 1}",
                                    new Vector3(-3f + i * 6f, 1f, 18f));
        }
    }

    private static void CreateStorageZone(Transform parent)
    {
        GameObject storageZone = new GameObject("Storage Zone");
        storageZone.transform.SetParent(parent);
        storageZone.transform.localPosition = new Vector3(25f, 0.1f, 5f);

        // 清水池
        CreateWaterTank(storageZone.transform, "Clear Water Reservoir",
                       Vector3.zero, new Vector3(15f, 6f, 15f), "ClearWaterTank");

        CreateWaterLayer(storageZone.transform, "Stored Clean Water",
                        new Vector3(0, 4.5f, 0), new Vector3(14.5f, 1.5f, 14.5f), "StoredCleanWater");

        // 化学品储存区
        GameObject chemicalArea = new GameObject("Chemical Storage Area");
        chemicalArea.transform.SetParent(storageZone.transform);
        chemicalArea.transform.localPosition = new Vector3(-20f, 0, 0);

        string[] chemicals = { "Chlorine", "Aluminum Sulfate", "Lime", "Activated Carbon" };
        Color[] chemColors = { Color.yellow, Color.cyan, Color.white, Color.black };

        for (int i = 0; i < chemicals.Length; i++)
        {
            CreateChemicalTank(chemicalArea.transform, chemicals[i] + " Tank",
                             new Vector3(i * 5f, 0, 0), chemColors[i]);
        }
    }

    private static void CreateInfrastructureZone(Transform parent)
    {
        GameObject infraZone = new GameObject("Infrastructure Zone");
        infraZone.transform.SetParent(parent);

        // 创建完整的管道网络
        CreatePipelineNetwork(infraZone.transform);

        // 创建围墙系统
        CreatePerimeterSystem(infraZone.transform);

        // 创建道路网络
        CreateRoadNetwork(infraZone.transform);

        // 创建绿化景观
        CreateLandscapeSystem(infraZone.transform);
    }

    // 辅助方法
    private static void CreateBuilding(Transform parent, string name, Vector3 position, Vector3 scale, string tag)
    {
        GameObject building = GameObject.CreatePrimitive(PrimitiveType.Cube);
        building.name = name;
        building.tag = tag;
        building.transform.SetParent(parent);
        building.transform.localPosition = position + new Vector3(0, scale.y / 2f, 0);
        building.transform.localScale = scale;

        // 添加屋顶
        GameObject roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
        roof.name = name + " Roof";
        roof.tag = "Roof";
        roof.transform.SetParent(building.transform);
        roof.transform.localPosition = new Vector3(0, 0.55f, 0);
        roof.transform.localScale = new Vector3(1.1f, 0.1f, 1.1f);

        // 添加基本窗户
        if (scale.y > 6f) // 高建筑才添加窗户
        {
            CreateWindows(building.transform, scale);
        }
    }

    private static void CreateWaterTank(Transform parent, string name, Vector3 position, Vector3 scale, string tag)
    {
        GameObject tank = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tank.name = name;
        tank.tag = tag;
        tank.transform.SetParent(parent);
        tank.transform.localPosition = position + new Vector3(0, scale.y / 2f, 0);
        tank.transform.localScale = scale;

        // 移除顶部碰撞器（开放式水池）
        Collider collider = tank.GetComponent<Collider>();
        if (collider != null)
        {
            DestroyImmediate(collider);
        }
    }

    private static void CreateWaterLayer(Transform parent, string name, Vector3 position, Vector3 scale, string tag)
    {
        GameObject water = GameObject.CreatePrimitive(PrimitiveType.Cube);
        water.name = name;
        water.tag = tag;
        water.transform.SetParent(parent);
        water.transform.localPosition = position;
        water.transform.localScale = scale;

        // 移除碰撞器
        DestroyImmediate(water.GetComponent<Collider>());
    }

    private static void CreateFilterMedia(Transform parent, string name, Vector3 position, Vector3 scale)
    {
        GameObject media = GameObject.CreatePrimitive(PrimitiveType.Cube);
        media.name = name;
        media.tag = "FilterMedia";
        media.transform.SetParent(parent);
        media.transform.localPosition = position;
        media.transform.localScale = scale;
    }

    private static void CreatePumpUnit(Transform parent, string name, Vector3 position)
    {
        GameObject pump = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pump.name = name;
        pump.tag = "Equipment";
        pump.transform.SetParent(parent);
        pump.transform.localPosition = position;
        pump.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);

        // 添加电机
        GameObject motor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        motor.name = name + " Motor";
        motor.tag = "Equipment";
        motor.transform.SetParent(pump.transform);
        motor.transform.localPosition = new Vector3(0, 1.2f, 0);
        motor.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);
    }

    private static void CreateElectricalEquipment(Transform parent, string name, Vector3 position)
    {
        GameObject equipment = GameObject.CreatePrimitive(PrimitiveType.Cube);
        equipment.name = name;
        equipment.tag = "Equipment";
        equipment.transform.SetParent(parent);
        equipment.transform.localPosition = position;
        equipment.transform.localScale = new Vector3(2f, 3f, 1.5f);
    }

    private static void CreateChemicalTank(Transform parent, string name, Vector3 position, Color color)
    {
        GameObject tank = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        tank.name = name;
        tank.tag = "ChemicalTank";
        tank.transform.SetParent(parent);
        tank.transform.localPosition = position + new Vector3(0, 2.5f, 0);
        tank.transform.localScale = new Vector3(3f, 2.5f, 3f);

        // 设置基本颜色
        Renderer renderer = tank.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material material = new Material(Shader.Find("Standard"));
            material.color = color;
            material.SetFloat("_Metallic", 0.7f);
            material.SetFloat("_Glossiness", 0.8f);
            renderer.material = material;
        }
    }

    private static void CreateWindows(Transform building, Vector3 buildingScale)
    {
        // 在建筑正面添加窗户
        int windowCount = Mathf.FloorToInt(buildingScale.x / 3f);
        for (int i = 0; i < windowCount; i++)
        {
            GameObject window = GameObject.CreatePrimitive(PrimitiveType.Cube);
            window.name = "Window " + (i + 1);
            window.tag = "Window";
            window.transform.SetParent(building.transform);
            window.transform.localPosition = new Vector3(
                -0.4f + (0.8f / windowCount) * i, 0.2f, 0.51f);
            window.transform.localScale = new Vector3(0.15f, 0.3f, 0.02f);

            // 设置窗户材质
            SetBasicGlassMaterial(window);
        }
    }

    private static void CreatePipelineNetwork(Transform parent)
    {
        GameObject pipeNetwork = new GameObject("Pipeline Network");
        pipeNetwork.transform.SetParent(parent);

        // 主输水管道
        CreatePipeline(pipeNetwork.transform, "Main Input Pipeline",
                      new Vector3(-35f, 1.5f, 0), new Vector3(-20f, 1.5f, 15f), 0.6f);
        CreatePipeline(pipeNetwork.transform, "Processing Pipeline",
                      new Vector3(-20f, 1.5f, 0), new Vector3(0f, 1.5f, 0), 0.5f);
        CreatePipeline(pipeNetwork.transform, "Output Pipeline",
                      new Vector3(15f, 1.5f, 0), new Vector3(35f, 1.5f, 0), 0.8f);
    }

    private static void CreatePerimeterSystem(Transform parent)
    {
        GameObject perimeterSystem = new GameObject("Perimeter System");
        perimeterSystem.transform.SetParent(parent);

        // 围墙
        Vector3[] wallSections = {
            new Vector3(-40f, 1.5f, 35f), new Vector3(40f, 1.5f, 35f),   // 北墙
            new Vector3(-40f, 1.5f, -35f), new Vector3(40f, 1.5f, -35f), // 南墙
            new Vector3(-40f, 1.5f, 0f), new Vector3(40f, 1.5f, 0f)      // 东西墙部分
        };

        for (int i = 0; i < wallSections.Length; i++)
        {
            CreateWallSection(perimeterSystem.transform, $"Wall Section {i + 1}", wallSections[i]);
        }

        // 主入口大门
        CreateGate(perimeterSystem.transform, "Main Gate", new Vector3(0, 2f, 35.5f));
    }

    private static void CreateRoadNetwork(Transform parent)
    {
        GameObject roadNetwork = new GameObject("Road Network");
        roadNetwork.transform.SetParent(parent);

        // 主干道
        CreateRoad(roadNetwork.transform, "Main Road",
                  new Vector3(0, 0.05f, 30f), new Vector3(80f, 0.1f, 6f));

        // 内部道路
        CreateRoad(roadNetwork.transform, "Internal Road 1",
                  new Vector3(0, 0.05f, 10f), new Vector3(60f, 0.1f, 4f));
        CreateRoad(roadNetwork.transform, "Internal Road 2",
                  new Vector3(0, 0.05f, -10f), new Vector3(60f, 0.1f, 4f));
    }

    private static void CreateLandscapeSystem(Transform parent)
    {
        GameObject landscapeSystem = new GameObject("Landscape System");
        landscapeSystem.transform.SetParent(parent);

        // 绿化区域
        for (int i = 0; i < 12; i++)
        {
            Vector3 grassPos = new Vector3(
                Random.Range(-35f, 35f), 0.05f, Random.Range(-30f, 30f));
            CreateGrassArea(landscapeSystem.transform, $"Grass Area {i + 1}", grassPos);
        }

        // 树木
        for (int i = 0; i < 20; i++)
        {
            Vector3 treePos = new Vector3(
                Random.Range(-38f, 38f), 0.1f, Random.Range(-32f, 32f));
            CreateTree(landscapeSystem.transform, $"Tree {i + 1}", treePos);
        }
    }

    // 更多辅助方法...
    private static void CreatePipeline(Transform parent, string name, Vector3 start, Vector3 end, float diameter)
    {
        GameObject pipeline = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pipeline.name = name;
        pipeline.tag = "Pipeline";
        pipeline.transform.SetParent(parent);

        Vector3 center = (start + end) / 2f;
        pipeline.transform.localPosition = center;

        Vector3 direction = (end - start).normalized;
        pipeline.transform.localRotation = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 90, 0);

        float length = Vector3.Distance(start, end);
        pipeline.transform.localScale = new Vector3(diameter, length / 2f, diameter);
    }

    private static void CreateWallSection(Transform parent, string name, Vector3 position)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.tag = "Wall";
        wall.transform.SetParent(parent);
        wall.transform.localPosition = position;
        wall.transform.localScale = new Vector3(0.5f, 3f, 12f);
    }

    private static void CreateGate(Transform parent, string name, Vector3 position)
    {
        GameObject gate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        gate.name = name;
        gate.tag = "Gate";
        gate.transform.SetParent(parent);
        gate.transform.localPosition = position;
        gate.transform.localScale = new Vector3(8f, 4f, 0.3f);
    }

    private static void CreateRoad(Transform parent, string name, Vector3 position, Vector3 scale)
    {
        GameObject road = GameObject.CreatePrimitive(PrimitiveType.Cube);
        road.name = name;
        road.tag = "Road";
        road.transform.SetParent(parent);
        road.transform.localPosition = position;
        road.transform.localScale = scale;
    }

    private static void CreateGrassArea(Transform parent, string name, Vector3 position)
    {
        GameObject grass = GameObject.CreatePrimitive(PrimitiveType.Cube);
        grass.name = name;
        grass.tag = "Grass";
        grass.transform.SetParent(parent);
        grass.transform.localPosition = position;
        grass.transform.localScale = new Vector3(
            Random.Range(4f, 8f), 0.1f, Random.Range(3f, 6f));
    }

    private static void CreateTree(Transform parent, string name, Vector3 position)
    {
        GameObject tree = new GameObject(name);
        tree.transform.SetParent(parent);
        tree.transform.localPosition = position;

        // 树干
        GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trunk.name = "Trunk";
        trunk.tag = "TreeTrunk";
        trunk.transform.SetParent(tree.transform);
        trunk.transform.localPosition = new Vector3(0, 2f, 0);
        trunk.transform.localScale = new Vector3(0.4f, 2f, 0.4f);

        // 树冠
        GameObject leaves = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        leaves.name = "Leaves";
        leaves.tag = "TreeLeaves";
        leaves.transform.SetParent(tree.transform);
        leaves.transform.localPosition = new Vector3(0, 4.5f, 0);
        leaves.transform.localScale = new Vector3(3f, 3f, 3f);
    }

    private static void SetBasicGlassMaterial(GameObject obj)
    {
        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material glassMaterial = new Material(Shader.Find("Standard"));
            glassMaterial.color = new Color(0.7f, 0.9f, 1f, 0.6f);
            glassMaterial.SetFloat("_Mode", 3); // Transparent
            glassMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            glassMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            glassMaterial.SetInt("_ZWrite", 0);
            glassMaterial.renderQueue = 3000;
            renderer.material = glassMaterial;
        }
    }

    private static void ApplyAdvancedMaterials()
    {
        Debug.Log("正在应用高级材质和纹理...");

        GameObject waterPlant = GameObject.Find("MCP Water Treatment Plant");
        if (waterPlant == null) return;

        // 创建材质文件夹
        string materialFolder = "Assets/Materials/WaterPlant";
        if (!System.IO.Directory.Exists(materialFolder))
        {
            System.IO.Directory.CreateDirectory(materialFolder);
            AssetDatabase.Refresh();
        }

        // 批量应用材质
        ApplyMaterialsByTag(waterPlant, "MainBuilding", CreateTextureMaterial("concrete.jpg", "MainBuildingMaterial", Color.white, 0.2f, 0.4f));
        ApplyMaterialsByTag(waterPlant, "SedimentationTank", CreateTextureMaterial("concrete.jpg", "TankMaterial", new Color(0.9f, 0.9f, 0.85f), 0.1f, 0.3f));
        ApplyMaterialsByTag(waterPlant, "FiltrationTank", CreateTextureMaterial("industrial_floor.jpg", "IndustrialMaterial", new Color(0.8f, 0.8f, 0.8f), 0.3f, 0.6f));
        ApplyMaterialsByTag(waterPlant, "DirtyWater", CreateTextureMaterial("water.jpg", "DirtyWaterMaterial", new Color(0.4f, 0.5f, 0.3f, 0.8f), 0.0f, 0.9f));
        ApplyMaterialsByTag(waterPlant, "CleanWater", CreateTextureMaterial("water_surface.jpg", "CleanWaterMaterial", new Color(0.3f, 0.7f, 1f, 0.8f), 0.0f, 0.95f));
        ApplyMaterialsByTag(waterPlant, "Pipeline", CreateTextureMaterial("metal_pipe.jpg", "PipelineMaterial", new Color(0.6f, 0.6f, 0.7f), 0.8f, 0.7f));
        ApplyMaterialsByTag(waterPlant, "Equipment", CreateTextureMaterial("metal.jpg", "EquipmentMaterial", new Color(0.7f, 0.7f, 0.8f), 0.9f, 0.8f));
        ApplyMaterialsByTag(waterPlant, "Roof", CreateTextureMaterial("roof.jpg", "RoofMaterial", new Color(0.7f, 0.5f, 0.4f), 0.1f, 0.5f));
        ApplyMaterialsByTag(waterPlant, "Wall", CreateTextureMaterial("brick.jpg", "WallMaterial", new Color(0.8f, 0.7f, 0.6f), 0.0f, 0.4f));
        ApplyMaterialsByTag(waterPlant, "Road", CreateBasicMaterial("AsphaltMaterial", new Color(0.3f, 0.3f, 0.3f), 0.1f, 0.2f));
        ApplyMaterialsByTag(waterPlant, "Grass", CreateTextureMaterial("grass_texture.jpg", "GrassMaterial", new Color(0.3f, 0.8f, 0.2f), 0.0f, 0.2f));
        ApplyMaterialsByTag(waterPlant, "TreeTrunk", CreateBasicMaterial("TreeTrunkMaterial", new Color(0.4f, 0.2f, 0.1f), 0.0f, 0.3f));
        ApplyMaterialsByTag(waterPlant, "TreeLeaves", CreateBasicMaterial("TreeLeavesMaterial", new Color(0.2f, 0.6f, 0.1f), 0.0f, 0.2f));

        Debug.Log("✓ 高级材质应用完成");
    }

    private static Material CreateTextureMaterial(string textureName, string materialName, Color color, float metallic, float smoothness)
    {
        Material material = new Material(Shader.Find("Standard"));
        material.name = materialName;
        material.color = color;
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Glossiness", smoothness);

        // 尝试加载纹理
        string texturePath = $"Assets/Textures/WaterPlant/{textureName}";
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (texture != null)
        {
            material.mainTexture = texture;
        }

        // 设置透明度
        if (color.a < 1.0f)
        {
            SetupTransparentMaterial(material);
        }

        // 保存材质
        string materialPath = $"Assets/Materials/WaterPlant/{materialName}.mat";
        AssetDatabase.CreateAsset(material, materialPath);

        return material;
    }

    private static Material CreateBasicMaterial(string materialName, Color color, float metallic, float smoothness)
    {
        Material material = new Material(Shader.Find("Standard"));
        material.name = materialName;
        material.color = color;
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Glossiness", smoothness);

        string materialPath = $"Assets/Materials/WaterPlant/{materialName}.mat";
        AssetDatabase.CreateAsset(material, materialPath);

        return material;
    }

    private static void SetupTransparentMaterial(Material material)
    {
        material.SetFloat("_Mode", 3);
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.renderQueue = 3000;
    }

    private static void ApplyMaterialsByTag(GameObject parent, string tag, Material material)
    {
        Transform[] allTransforms = parent.GetComponentsInChildren<Transform>(true);
        foreach (Transform t in allTransforms)
        {
            if (t.gameObject.tag == tag)
            {
                Renderer renderer = t.GetComponent<Renderer>();
                if (renderer != null && material != null)
                {
                    renderer.material = material;
                }
            }
        }
    }
}
