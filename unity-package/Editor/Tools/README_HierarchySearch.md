# Unity Hierarchy搜索API参考

## Unity内置的Hierarchy搜索API

### 1. **GameObject.Find(string name)**
- **功能**: 按名称查找单个GameObject
- **特点**: 只查找激活的对象，返回第一个匹配的对象
- **性能**: 较慢，需要遍历整个场景
- **使用场景**: 精确名称查找

```csharp
GameObject obj = GameObject.Find("Player");
```

### 2. **GameObject.FindGameObjectsWithTag(string tag)**
- **功能**: 按标签查找所有GameObject
- **特点**: 只查找激活的对象
- **性能**: 较快，Unity内部优化
- **使用场景**: 按标签批量查找

```csharp
GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
```

### 3. **Object.FindObjectsOfType<T>()**
- **功能**: 按组件类型查找所有对象
- **特点**: 只查找激活的对象
- **性能**: 中等，需要遍历场景
- **使用场景**: 按组件类型查找

```csharp
// 查找所有GameObject
GameObject[] allObjects = Object.FindObjectsOfType<GameObject>();

// 查找所有MeshRenderer
MeshRenderer[] renderers = Object.FindObjectsOfType<MeshRenderer>();

// 查找所有自定义组件
PlayerController[] players = Object.FindObjectsOfType<PlayerController>();
```

### 4. **Resources.FindObjectsOfTypeAll<T>()**
- **功能**: 查找所有对象，包括非激活的
- **特点**: 包括非激活对象和预制体资源
- **性能**: 较慢，搜索范围更大
- **使用场景**: 需要查找非激活对象时

```csharp
// 查找所有GameObject，包括非激活的
GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();

// 查找所有MeshRenderer，包括非激活的
MeshRenderer[] allRenderers = Resources.FindObjectsOfTypeAll<MeshRenderer>();
```

### 5. **EditorUtility.FindObjectsOfType<T>()** (仅编辑器)
- **功能**: 编辑器模式下查找所有对象
- **特点**: 编辑器专用，功能类似Resources.FindObjectsOfTypeAll
- **性能**: 中等
- **使用场景**: 编辑器工具开发

```csharp
#if UNITY_EDITOR
GameObject[] objects = EditorUtility.FindObjectsOfType<GameObject>();
#endif
```

## 性能对比

| API | 性能 | 搜索范围 | 适用场景 |
|-----|------|----------|----------|
| GameObject.Find | 慢 | 激活对象 | 精确名称查找 |
| GameObject.FindGameObjectsWithTag | 快 | 激活对象 | 按标签查找 |
| Object.FindObjectsOfType | 中等 | 激活对象 | 按类型查找 |
| Resources.FindObjectsOfTypeAll | 慢 | 所有对象 | 完整搜索 |
| EditorUtility.FindObjectsOfType | 中等 | 所有对象 | 编辑器工具 |

## 最佳实践

1. **优先使用标签搜索**: 如果可能，给对象添加标签并使用 `FindGameObjectsWithTag`
2. **缓存搜索结果**: 避免频繁调用搜索API
3. **使用合适的API**: 根据需求选择激活对象搜索还是全对象搜索
4. **考虑性能**: 在Update等频繁调用的方法中避免使用慢速搜索API

## 在HierarchySearch工具中的应用

我们的 `HierarchySearch` 工具充分利用了这些Unity内置API：

- **by_name**: 使用 `GameObject.Find` 和 `Object.FindObjectsOfType`
- **by_tag**: 使用 `GameObject.FindGameObjectsWithTag`
- **by_component**: 使用 `Object.FindObjectsOfType` 和 `Resources.FindObjectsOfTypeAll`
- **by_term**: 结合多种API实现通用搜索
- **t:TypeName**: 直接使用 `Object.FindObjectsOfType` 进行类型搜索 