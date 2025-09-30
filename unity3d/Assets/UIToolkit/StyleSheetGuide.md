# SimpleUI样式表关联指南

## 📋 如何让SimpleUI正确应用SimpleUI.uss样式

### 🎯 方法1：通过UIDocument组件关联（推荐）

这是最标准和推荐的方式：

#### 步骤：
1. **创建GameObject并添加UIDocument组件**
   ```
   GameObject → UI Toolkit → UIDocument
   ```

2. **设置Source Asset**
   - 将 `SimpleUI.uxml` 拖拽到 `Source Asset` 字段

3. **添加样式表到Style Sheets列表**
   - 点击 `Style Sheets` 列表的 `+` 按钮
   - 将 `SimpleUI.uss` 拖拽到列表中

4. **添加控制器脚本**
   - 在同一个GameObject上添加 `SimpleUIController` 脚本

### 🎯 方法2：在UXML中直接引用（高级）

在UXML文件顶部添加样式引用：

```xml
<ui:UXML>
    <Style src="Assets/UIToolkit/USS/SimpleUI.uss" />
    <!-- 其他UI元素 -->
</ui:UXML>
```

### 🎯 方法3：通过代码动态加载

在SimpleUIController.cs中动态加载样式：

```csharp
void Start()
{
    // 获取UIDocument
    var uiDocument = GetComponent<UIDocument>();
    
    // 加载样式表
    var styleSheet = Resources.Load<StyleSheet>("SimpleUI");
    uiDocument.rootVisualElement.styleSheets.Add(styleSheet);
    
    // 继续其他初始化...
    InitializeUI();
}
```

## 🔍 样式应用验证

### 检查样式是否正确应用：

1. **类选择器验证**
   ```css
   .main-container { /* 应用到root-container */ }
   .title-label { /* 应用到title-text */ }
   .image-card { /* 应用到所有图片元素 */ }
   ```

2. **ID选择器验证**
   ```css
   #image-1 { /* 应用到name="image-1"的元素 */ }
   #image-2 { /* 应用到name="image-2"的元素 */ }
   #image-3 { /* 应用到name="image-3"的元素 */ }
   ```

3. **在Unity Editor中检查**
   - 运行场景
   - 在Hierarchy中选择UIDocument对象
   - 在Inspector中查看样式是否正确应用

## 🐛 常见问题排查

### 问题1：样式没有应用
**原因**: USS文件没有正确关联
**解决**: 确保USS文件在UIDocument的Style Sheets列表中

### 问题2：部分样式不生效
**原因**: 选择器名称不匹配
**解决**: 检查UXML中的name和class属性是否与USS中的选择器匹配

### 问题3：位置不正确
**原因**: 坐标计算错误或容器设置问题
**解决**: 检查容器的position属性是否设置为relative

## 📐 样式选择器映射表

| UXML元素 | name属性 | class属性 | USS选择器 |
|----------|----------|-----------|-----------|
| root-container | "root-container" | "main-container" | .main-container |
| title-text | "title-text" | "title-label" | .title-label |
| images-container | "images-container" | "images-layout" | .images-layout |
| image-1 | "image-1" | "image-card" | #image-1, .image-card |
| image-2 | "image-2" | "image-card" | #image-2, .image-card |
| image-3 | "image-3" | "image-card" | #image-3, .image-card |
| vector-decoration | "vector-decoration" | "decoration-element" | .decoration-element |

## 🎨 样式优先级

UI Toolkit中的样式优先级（从高到低）：

1. **内联样式** (element.style.xxx)
2. **ID选择器** (#image-1)
3. **类选择器** (.image-card)
4. **元素选择器** (VisualElement)

## 🔧 调试技巧

### 1. 使用UI Debugger
```
Window → UI Toolkit → Debugger
```

### 2. 检查样式计算值
在UI Debugger中可以看到：
- 应用的样式规则
- 计算后的最终值
- 样式来源

### 3. 控制台调试
```csharp
Debug.Log($"Element class: {element.GetClasses()}");
Debug.Log($"Element name: {element.name}");
Debug.Log($"Computed style: {element.resolvedStyle.width}");
```

## ✅ 最佳实践

1. **使用UIDocument组件**：这是最稳定的方式
2. **保持命名一致**：UXML的name/class与USS选择器保持一致
3. **使用相对路径**：避免绝对路径导致的移植问题
4. **分离关注点**：样式写在USS中，逻辑写在C#中
5. **测试不同分辨率**：确保响应式设计正常工作

---

*遵循这些指南，SimpleUI将正确应用SimpleUI.uss样式表中的所有样式规则。*
