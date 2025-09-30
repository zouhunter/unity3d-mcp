using UnityEngine;
using UnityEditor;

public class TaijiArraySetup : EditorWindow
{
    [MenuItem("Tools/Setup Taiji Array")]
    public static void ShowWindow()
    {
        GetWindow<TaijiArraySetup>("太极阵法设置");
    }

    private void OnGUI()
    {
        GUILayout.Label("太极阵法设置工具", EditorStyles.boldLabel);

        GUILayout.Space(10);

        if (GUILayout.Button("自动设置太极阵法"))
        {
            SetupTaijiArray();
        }

        GUILayout.Space(10);

        if (GUILayout.Button("应用材质"))
        {
            ApplyMaterials();
        }

        if (GUILayout.Button("添加旋转动画"))
        {
            AddRotationAnimation();
        }
    }

    private static void SetupTaijiArray()
    {
        Debug.Log("开始设置太极阵法...");

        // 查找太极阵法对象
        GameObject taijiArray = GameObject.Find("TaijiArray");
        if (taijiArray == null)
        {
            Debug.LogError("找不到TaijiArray对象！");
            return;
        }

        // 应用材质
        ApplyMaterials();

        // 添加旋转动画
        AddRotationAnimation();

        Debug.Log("太极阵法设置完成！");
    }

    private static void ApplyMaterials()
    {
        // 加载材质
        Material blackMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/BlackMaterial.mat");
        Material whiteMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/WhiteMaterial.mat");

        if (blackMaterial == null || whiteMaterial == null)
        {
            Debug.LogError("找不到黑色或白色材质！请确保材质文件存在。");
            return;
        }

        // 查找各个组件
        GameObject taijiArray = GameObject.Find("TaijiArray");
        if (taijiArray == null) return;

        // 设置主体为白色
        MeshRenderer mainRenderer = taijiArray.GetComponent<MeshRenderer>();
        if (mainRenderer != null)
        {
            mainRenderer.material = whiteMaterial;
            EditorUtility.SetDirty(mainRenderer);
        }

        // 设置阴鱼为黑色
        Transform yinFish = taijiArray.transform.Find("YinFish");
        if (yinFish != null)
        {
            MeshRenderer renderer = yinFish.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.material = blackMaterial;
                EditorUtility.SetDirty(renderer);
            }
        }

        // 设置阳鱼为白色
        Transform yangFish = taijiArray.transform.Find("YangFish");
        if (yangFish != null)
        {
            MeshRenderer renderer = yangFish.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.material = whiteMaterial;
                EditorUtility.SetDirty(renderer);
            }
        }

        // 设置阴中阳点为白色
        Transform yinDot = taijiArray.transform.Find("YinDot");
        if (yinDot != null)
        {
            MeshRenderer renderer = yinDot.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.material = whiteMaterial;
                EditorUtility.SetDirty(renderer);
            }
        }

        // 设置阳中阴点为黑色
        Transform yangDot = taijiArray.transform.Find("YangDot");
        if (yangDot != null)
        {
            MeshRenderer renderer = yangDot.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.material = blackMaterial;
                EditorUtility.SetDirty(renderer);
            }
        }

        Debug.Log("太极阵法材质应用完成！");
    }

    private static void AddRotationAnimation()
    {
        GameObject taijiArray = GameObject.Find("TaijiArray");
        if (taijiArray == null) return;

        // 检查是否已有旋转组件
        TaijiRotator rotator = taijiArray.GetComponent<TaijiRotator>();
        if (rotator == null)
        {
            rotator = taijiArray.AddComponent<TaijiRotator>();
        }

        rotator.rotationSpeed = 30f;
        rotator.enableRotation = true;

        EditorUtility.SetDirty(rotator);
        Debug.Log("太极阵法旋转动画添加完成！");
    }
}

// 简单的旋转组件
public class TaijiRotator : MonoBehaviour
{
    [Header("旋转设置")]
    public float rotationSpeed = 30f;
    public bool enableRotation = true;

    void Update()
    {
        if (enableRotation)
        {
            transform.Rotate(0, rotationSpeed * Time.deltaTime, 0);
        }
    }
}
