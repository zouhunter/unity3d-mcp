using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class WaterTreatmentPlantCreator : MonoBehaviour
{
    [MenuItem("Tools/Create Water Treatment Plant")]
    public static void CreateWaterTreatmentPlant()
    {
        // 创建主父对象
        GameObject waterPlant = new GameObject("Water Treatment Plant");

        // 创建地基
        CreateFoundation(waterPlant.transform);

        // 创建主处理建筑
        CreateMainTreatmentBuilding(waterPlant.transform);

        // 创建沉淀池
        CreateSedimentationTanks(waterPlant.transform);

        // 创建过滤池
        CreateFiltrationTanks(waterPlant.transform);

        // 创建清水池
        CreateClearWaterTank(waterPlant.transform);

        // 创建泵房
        CreatePumpStation(waterPlant.transform);

        // 创建管道系统
        CreatePipelineSystem(waterPlant.transform);

        // 创建化学品存储区
        CreateChemicalStorage(waterPlant.transform);

        // 创建办公楼
        CreateOfficeBuilding(waterPlant.transform);

        // 创建围墙和大门
        CreatePerimeterWall(waterPlant.transform);

        // 创建绿化区域
        CreateLandscaping(waterPlant.transform);

        // 创建道路
        CreateRoads(waterPlant.transform);

        // 选中创建的对象
        Selection.activeGameObject = waterPlant;

        Debug.Log("自来水厂已创建完成！包含完整的水处理设施、建筑和基础设施。");
    }

    private static void CreateFoundation(Transform parent)
    {
        GameObject foundation = GameObject.CreatePrimitive(PrimitiveType.Cube);
        foundation.name = "Foundation";
        foundation.transform.SetParent(parent);
        foundation.transform.localPosition = Vector3.zero;
        foundation.transform.localScale = new Vector3(50f, 0.2f, 40f);

        // 设置基础材质
        SetMaterial(foundation, new Color(0.8f, 0.8f, 0.8f), "Concrete Foundation");
    }

    private static void CreateMainTreatmentBuilding(Transform parent)
    {
        GameObject building = new GameObject("Main Treatment Building");
        building.transform.SetParent(parent);
        building.transform.localPosition = new Vector3(0, 0.1f, 0);

        // 主建筑体
        GameObject mainStructure = GameObject.CreatePrimitive(PrimitiveType.Cube);
        mainStructure.name = "Main Structure";
        mainStructure.transform.SetParent(building.transform);
        mainStructure.transform.localPosition = new Vector3(0, 3f, 0);
        mainStructure.transform.localScale = new Vector3(12f, 6f, 8f);
        SetMaterial(mainStructure, new Color(0.9f, 0.9f, 0.85f), "Building Wall");

        // 屋顶
        GameObject roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
        roof.name = "Roof";
        roof.transform.SetParent(building.transform);
        roof.transform.localPosition = new Vector3(0, 6.2f, 0);
        roof.transform.localScale = new Vector3(12.5f, 0.4f, 8.5f);
        SetMaterial(roof, new Color(0.6f, 0.4f, 0.3f), "Roof Material");

        // 添加窗户
        for (int i = 0; i < 4; i++)
        {
            GameObject window = GameObject.CreatePrimitive(PrimitiveType.Cube);
            window.name = "Window " + (i + 1);
            window.transform.SetParent(building.transform);
            window.transform.localPosition = new Vector3(-5.5f + i * 3f, 4f, 4.1f);
            window.transform.localScale = new Vector3(1.5f, 2f, 0.1f);
            SetMaterial(window, new Color(0.7f, 0.9f, 1f, 0.8f), "Window Glass");
        }
    }

    private static void CreateSedimentationTanks(Transform parent)
    {
        GameObject sedimentArea = new GameObject("Sedimentation Tanks");
        sedimentArea.transform.SetParent(parent);
        sedimentArea.transform.localPosition = new Vector3(-15f, 0.1f, 10f);

        // 创建3个沉淀池
        for (int i = 0; i < 3; i++)
        {
            GameObject tank = CreateWaterTank("Sedimentation Tank " + (i + 1), sedimentArea.transform,
                                            new Vector3(i * 8f, 0, 0), new Vector3(6f, 2f, 6f));

            // 添加水
            GameObject water = GameObject.CreatePrimitive(PrimitiveType.Cube);
            water.name = "Water";
            water.transform.SetParent(tank.transform);
            water.transform.localPosition = new Vector3(0, 0.8f, 0);
            water.transform.localScale = new Vector3(0.95f, 0.1f, 0.95f);
            SetMaterial(water, new Color(0.3f, 0.5f, 0.8f, 0.7f), "Dirty Water");
        }
    }

    private static void CreateFiltrationTanks(Transform parent)
    {
        GameObject filtrationArea = new GameObject("Filtration Tanks");
        filtrationArea.transform.SetParent(parent);
        filtrationArea.transform.localPosition = new Vector3(-15f, 0.1f, -10f);

        // 创建2个过滤池
        for (int i = 0; i < 2; i++)
        {
            GameObject tank = CreateWaterTank("Filtration Tank " + (i + 1), filtrationArea.transform,
                                            new Vector3(i * 10f, 0, 0), new Vector3(8f, 3f, 6f));

            // 添加过滤介质层
            GameObject filterMedia = GameObject.CreatePrimitive(PrimitiveType.Cube);
            filterMedia.name = "Filter Media";
            filterMedia.transform.SetParent(tank.transform);
            filterMedia.transform.localPosition = new Vector3(0, 0.5f, 0);
            filterMedia.transform.localScale = new Vector3(0.9f, 0.8f, 0.9f);
            SetMaterial(filterMedia, new Color(0.7f, 0.6f, 0.4f), "Filter Sand");

            // 添加清水
            GameObject cleanWater = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cleanWater.name = "Clean Water";
            cleanWater.transform.SetParent(tank.transform);
            cleanWater.transform.localPosition = new Vector3(0, 1.2f, 0);
            cleanWater.transform.localScale = new Vector3(0.95f, 0.1f, 0.95f);
            SetMaterial(cleanWater, new Color(0.4f, 0.7f, 1f, 0.8f), "Clean Water");
        }
    }

    private static void CreateClearWaterTank(Transform parent)
    {
        GameObject clearWaterArea = new GameObject("Clear Water Storage");
        clearWaterArea.transform.SetParent(parent);
        clearWaterArea.transform.localPosition = new Vector3(15f, 0.1f, 0);

        GameObject tank = CreateWaterTank("Clear Water Tank", clearWaterArea.transform,
                                        Vector3.zero, new Vector3(12f, 4f, 12f));

        // 添加储存的清水
        GameObject storedWater = GameObject.CreatePrimitive(PrimitiveType.Cube);
        storedWater.name = "Stored Clean Water";
        storedWater.transform.SetParent(tank.transform);
        storedWater.transform.localPosition = new Vector3(0, 1.5f, 0);
        storedWater.transform.localScale = new Vector3(0.95f, 0.8f, 0.95f);
        SetMaterial(storedWater, new Color(0.5f, 0.8f, 1f, 0.9f), "Clean Water");
    }

    private static void CreatePumpStation(Transform parent)
    {
        GameObject pumpStation = new GameObject("Pump Station");
        pumpStation.transform.SetParent(parent);
        pumpStation.transform.localPosition = new Vector3(8f, 0.1f, -15f);

        // 泵房建筑
        GameObject building = GameObject.CreatePrimitive(PrimitiveType.Cube);
        building.name = "Pump House";
        building.transform.SetParent(pumpStation.transform);
        building.transform.localPosition = new Vector3(0, 2f, 0);
        building.transform.localScale = new Vector3(6f, 4f, 4f);
        SetMaterial(building, new Color(0.8f, 0.7f, 0.6f), "Pump House Wall");

        // 创建几个泵设备
        for (int i = 0; i < 3; i++)
        {
            GameObject pump = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pump.name = "Pump " + (i + 1);
            pump.transform.SetParent(pumpStation.transform);
            pump.transform.localPosition = new Vector3(-1.5f + i * 1.5f, 0.8f, 0);
            pump.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);
            SetMaterial(pump, new Color(0.3f, 0.3f, 0.3f), "Metal Equipment");
        }
    }

    private static void CreatePipelineSystem(Transform parent)
    {
        GameObject pipelineSystem = new GameObject("Pipeline System");
        pipelineSystem.transform.SetParent(parent);

        // 创建主要管道连接各个设施
        CreatePipe(pipelineSystem.transform, new Vector3(-15f, 1f, 7f), new Vector3(-15f, 1f, -7f), "Main Pipeline 1");
        CreatePipe(pipelineSystem.transform, new Vector3(-5f, 1f, -10f), new Vector3(12f, 1f, -10f), "Main Pipeline 2");
        CreatePipe(pipelineSystem.transform, new Vector3(15f, 1f, -6f), new Vector3(15f, 1f, 6f), "Storage Pipeline");
        CreatePipe(pipelineSystem.transform, new Vector3(15f, 1f, 0), new Vector3(25f, 1f, 0), "Output Pipeline");
    }

    private static void CreateChemicalStorage(Transform parent)
    {
        GameObject chemicalArea = new GameObject("Chemical Storage");
        chemicalArea.transform.SetParent(parent);
        chemicalArea.transform.localPosition = new Vector3(-20f, 0.1f, -18f);

        // 创建化学品储罐
        for (int i = 0; i < 3; i++)
        {
            GameObject tank = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tank.name = "Chemical Tank " + (i + 1);
            tank.transform.SetParent(chemicalArea.transform);
            tank.transform.localPosition = new Vector3(i * 3f, 3f, 0);
            tank.transform.localScale = new Vector3(2f, 3f, 2f);

            Color[] colors = { Color.yellow, Color.cyan, Color.magenta };
            SetMaterial(tank, colors[i], "Chemical Tank " + (i + 1));
        }

        // 化学品储存建筑
        GameObject storage = GameObject.CreatePrimitive(PrimitiveType.Cube);
        storage.name = "Chemical Storage Building";
        storage.transform.SetParent(chemicalArea.transform);
        storage.transform.localPosition = new Vector3(4f, 2f, 4f);
        storage.transform.localScale = new Vector3(8f, 4f, 6f);
        SetMaterial(storage, new Color(0.9f, 0.85f, 0.8f), "Storage Building");
    }

    private static void CreateOfficeBuilding(Transform parent)
    {
        GameObject office = new GameObject("Office Building");
        office.transform.SetParent(parent);
        office.transform.localPosition = new Vector3(-8f, 0.1f, 18f);

        // 办公楼主体
        GameObject building = GameObject.CreatePrimitive(PrimitiveType.Cube);
        building.name = "Office Main";
        building.transform.SetParent(office.transform);
        building.transform.localPosition = new Vector3(0, 3f, 0);
        building.transform.localScale = new Vector3(8f, 6f, 6f);
        SetMaterial(building, new Color(0.95f, 0.95f, 0.9f), "Office Wall");

        // 入口
        GameObject entrance = GameObject.CreatePrimitive(PrimitiveType.Cube);
        entrance.name = "Entrance";
        entrance.transform.SetParent(office.transform);
        entrance.transform.localPosition = new Vector3(0, 1.5f, 3.2f);
        entrance.transform.localScale = new Vector3(2f, 3f, 0.4f);
        SetMaterial(entrance, new Color(0.4f, 0.3f, 0.2f), "Entrance Door");
    }

    private static void CreatePerimeterWall(Transform parent)
    {
        GameObject wallSystem = new GameObject("Perimeter Wall");
        wallSystem.transform.SetParent(parent);

        // 创建围墙段
        Vector3[] wallPositions = {
            new Vector3(-25f, 1f, 20f), new Vector3(25f, 1f, 20f),   // 北墙
            new Vector3(-25f, 1f, -20f), new Vector3(25f, 1f, -20f), // 南墙
            new Vector3(-25f, 1f, 0f), new Vector3(25f, 1f, 0f)      // 侧墙中段
        };

        foreach (Vector3 pos in wallPositions)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Wall Section";
            wall.transform.SetParent(wallSystem.transform);
            wall.transform.localPosition = pos;
            wall.transform.localScale = new Vector3(0.3f, 2f, 8f);
            SetMaterial(wall, new Color(0.7f, 0.7f, 0.7f), "Concrete Wall");
        }

        // 创建大门
        GameObject gate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        gate.name = "Main Gate";
        gate.transform.SetParent(wallSystem.transform);
        gate.transform.localPosition = new Vector3(0, 1.5f, 20.2f);
        gate.transform.localScale = new Vector3(4f, 3f, 0.2f);
        SetMaterial(gate, new Color(0.3f, 0.5f, 0.3f), "Metal Gate");
    }

    private static void CreateLandscaping(Transform parent)
    {
        GameObject landscape = new GameObject("Landscaping");
        landscape.transform.SetParent(parent);

        // 创建草坪区域
        for (int i = 0; i < 5; i++)
        {
            GameObject grass = GameObject.CreatePrimitive(PrimitiveType.Cube);
            grass.name = "Grass Area " + (i + 1);
            grass.transform.SetParent(landscape.transform);
            grass.transform.localPosition = new Vector3(
                Random.Range(-20f, 20f), 0.05f, Random.Range(15f, 19f));
            grass.transform.localScale = new Vector3(
                Random.Range(3f, 6f), 0.1f, Random.Range(2f, 3f));
            SetMaterial(grass, new Color(0.3f, 0.7f, 0.2f), "Grass");
        }

        // 创建树木
        for (int i = 0; i < 8; i++)
        {
            CreateTree(landscape.transform, new Vector3(
                Random.Range(-22f, 22f), 0.1f, Random.Range(-18f, 18f)));
        }
    }

    private static void CreateRoads(Transform parent)
    {
        GameObject roadSystem = new GameObject("Road System");
        roadSystem.transform.SetParent(parent);

        // 主干道
        GameObject mainRoad = GameObject.CreatePrimitive(PrimitiveType.Cube);
        mainRoad.name = "Main Road";
        mainRoad.transform.SetParent(roadSystem.transform);
        mainRoad.transform.localPosition = new Vector3(0, 0.05f, 15f);
        mainRoad.transform.localScale = new Vector3(50f, 0.1f, 4f);
        SetMaterial(mainRoad, new Color(0.3f, 0.3f, 0.3f), "Asphalt Road");

        // 连接道路
        GameObject connectingRoad = GameObject.CreatePrimitive(PrimitiveType.Cube);
        connectingRoad.name = "Connecting Road";
        connectingRoad.transform.SetParent(roadSystem.transform);
        connectingRoad.transform.localPosition = new Vector3(0, 0.05f, 5f);
        connectingRoad.transform.localScale = new Vector3(4f, 0.1f, 20f);
        SetMaterial(connectingRoad, new Color(0.3f, 0.3f, 0.3f), "Asphalt Road");
    }

    private static GameObject CreateWaterTank(string name, Transform parent, Vector3 position, Vector3 scale)
    {
        GameObject tank = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tank.name = name;
        tank.transform.SetParent(parent);
        tank.transform.localPosition = position;
        tank.transform.localScale = scale;

        SetMaterial(tank, new Color(0.8f, 0.8f, 0.85f), "Concrete Tank");

        return tank;
    }

    private static void CreatePipe(Transform parent, Vector3 start, Vector3 end, string name)
    {
        GameObject pipe = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pipe.name = name;
        pipe.transform.SetParent(parent);

        Vector3 center = (start + end) / 2f;
        pipe.transform.localPosition = center;

        Vector3 direction = (end - start).normalized;
        pipe.transform.localRotation = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 90, 0);

        float distance = Vector3.Distance(start, end);
        pipe.transform.localScale = new Vector3(0.3f, distance / 2f, 0.3f);

        SetMaterial(pipe, new Color(0.4f, 0.4f, 0.4f), "Metal Pipe");
    }

    private static void CreateTree(Transform parent, Vector3 position)
    {
        GameObject tree = new GameObject("Tree");
        tree.transform.SetParent(parent);
        tree.transform.localPosition = position;

        // 树干
        GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trunk.name = "Trunk";
        trunk.transform.SetParent(tree.transform);
        trunk.transform.localPosition = new Vector3(0, 1.5f, 0);
        trunk.transform.localScale = new Vector3(0.3f, 1.5f, 0.3f);
        SetMaterial(trunk, new Color(0.4f, 0.2f, 0.1f), "Tree Trunk");

        // 树叶
        GameObject leaves = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        leaves.name = "Leaves";
        leaves.transform.SetParent(tree.transform);
        leaves.transform.localPosition = new Vector3(0, 3.5f, 0);
        leaves.transform.localScale = new Vector3(2f, 2f, 2f);
        SetMaterial(leaves, new Color(0.2f, 0.6f, 0.1f), "Tree Leaves");
    }

    private static void SetMaterial(GameObject obj, Color color, string materialName)
    {
        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material material = new Material(Shader.Find("Standard"));
            material.name = materialName;
            material.color = color;

            // 如果是透明材质
            if (color.a < 1.0f)
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

            // 为水材质添加一些特殊效果
            if (materialName.Contains("Water"))
            {
                material.SetFloat("_Metallic", 0.0f);
                material.SetFloat("_Glossiness", 0.8f);
            }

            renderer.material = material;
        }
    }
}

