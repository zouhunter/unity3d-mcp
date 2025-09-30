# SimpleUI - UI Toolkit Implementation

这是基于Unity UI Toolkit系统的SimpleUI界面实现，包含完整的UXML结构、USS样式和C#脚本逻辑。

## 📁 文件结构

```
Assets/UIToolkit/
├── UXML/
│   └── SimpleUI.uxml          # UI结构定义文件
├── USS/
│   └── SimpleUI.uss           # 样式表文件
├── Scripts/
│   └── SimpleUIController.cs  # 控制器脚本
└── README.md                  # 说明文档
```

## 🚀 使用方法

### 1. 创建UI Document
1. 在场景中创建一个空的GameObject
2. 添加`UIDocument`组件
3. 将`SimpleUI.uxml`文件拖拽到`Source Asset`字段
4. **重要**：将`SimpleUI.uss`文件拖拽到`Style Sheets`列表中
   - 点击`Style Sheets`列表的`+`按钮
   - 选择`Assets/UIToolkit/USS/SimpleUI.uss`文件

### 2. 添加控制器脚本
1. 在同一个GameObject上添加`SimpleUIController`脚本
2. 将UIDocument组件拖拽到脚本的`UI Document`字段
3. **样式表设置**（三种方式任选其一）：
   - **方式A**: 将`SimpleUI.uss`拖拽到脚本的`Style Sheet`字段
   - **方式B**: 将`SimpleUI.uss`放入`Assets/Resources/`文件夹（自动加载）
   - **方式C**: 在UIDocument组件的Style Sheets列表中添加
4. 设置图片纹理资源到对应的字段

### 3. 图片资源设置
根据Figma设计稿，需要设置以下图片资源：
- `Image 1 Texture`: 对应节点ID `1:4` (圆角30px)
- `Image 2 Texture`: 对应节点ID `1:5` (圆角4px) 
- `Image 3 Texture`: 对应节点ID `1:6` (圆角4px)

图片资源已下载到：`Assets/Pics/SimpleUI/`

## 🔄 USS样式表自动加载功能

SimpleUIController现在支持多种方式自动加载USS样式表：

### 加载优先级
1. **Inspector设置优先** - 如果在脚本的Style Sheet字段中设置了样式表，优先使用
2. **Resources自动加载** - 自动从`Assets/Resources/SimpleUI.uss`加载
3. **AssetDatabase搜索** - 在Editor中自动搜索项目中的SimpleUI样式表

### 使用方法
```csharp
// 手动重新加载样式表
simpleUIController.ReloadStyleSheet();

// 设置自定义样式表
simpleUIController.SetCustomStyleSheet(myCustomStyleSheet);
```

### 调试信息
控制台会显示样式表加载状态：
- `[SimpleUI] 通过Inspector加载样式表完成`
- `[SimpleUI] 自动加载Resources中的样式表完成`
- `[SimpleUI] 通过AssetDatabase加载样式表完成`

## 🎨 设计特色

### UXML结构
- **语义化命名**: 使用清晰的元素名称和类名
- **层次化布局**: 合理的元素嵌套结构
- **可维护性**: 便于修改和扩展的结构设计

### USS样式
- **现代Web标准**: 兼容现代CSS语法
- **精确布局**: 使用绝对定位精确反映Figma设计稿
- **坐标转换**: Figma Unity坐标系转换为UI Toolkit左上角坐标系
- **圆角支持**: 完整的border-radius实现
- **响应式设计**: 支持多种屏幕尺寸适配

### C#脚本逻辑
- **事件驱动**: 完整的鼠标事件处理
- **资源管理**: 动态图片加载和更新
- **交互效果**: 鼠标悬停缩放效果
- **公共接口**: 便于外部调用的方法

## 📐 精确布局信息

基于Figma设计稿的精确坐标转换：

### 坐标系转换
- **Figma**: 使用中心点(0,0)为原点的Unity坐标系
- **UI Toolkit**: 使用左上角(0,0)为原点的坐标系
- **转换公式**: 
  - X: `UIToolkit_X = (容器宽度/2) + Figma_X`
  - Y: `UIToolkit_Y = (容器高度/2) - Figma_Y` (需要根据实际效果调整)

### 元素位置映射
| 元素 | Figma坐标 | UI Toolkit坐标 | 尺寸 |
|------|-----------|----------------|------|
| 标题文本 | [-160, 168] | [440px, 36px] | 716×216 |
| 图片1 | [-362, -356.38] | [238px, 282px] | 300×437 |
| 图片2 | [-14, -244.67] | [586px, 407px] | 300×213 |
| 图片3 | [334, -251.96] | [934px, 400px] | 300×228 |
| 装饰元素 | [282, 60] | [882px, 300px] | 134×252 |

## 📱 响应式设计

支持以下分辨率的自动适配：
- **桌面**: 1920x1080, 2560x1440, 3840x2160
- **笔记本**: 1366x768
- **平板**: 1024x768
- **手机**: 375x667, 414x896

## 🔧 自定义扩展

### 修改样式
编辑`SimpleUI.uss`文件来调整：
- 颜色方案
- 字体大小
- 布局间距
- 动画效果

### 添加交互
在`SimpleUIController.cs`中添加：
- 新的事件处理方法
- 动画和过渡效果
- 数据绑定逻辑
- 外部API集成

### 扩展元素
在`SimpleUI.uxml`中添加：
- 新的UI元素
- 按钮和输入框
- 列表和滚动视图
- 自定义控件

## 🎯 性能优化

- **Vector图形**: 支持可缩放的矢量图形
- **内存管理**: 合理的资源加载和释放
- **事件优化**: 高效的事件注册和注销
- **样式缓存**: USS样式的智能缓存机制

## 📋 开发指南

### 最佳实践
1. 使用语义化的类名和ID
2. 保持样式和逻辑分离
3. 合理使用Flexbox布局
4. 注意内存泄漏的防范

### 调试技巧
1. 使用Unity的UI Debugger
2. 在Console中查看调试日志
3. 利用USS的调试功能
4. 测试不同分辨率的显示效果

## 🌟 特性亮点

- ✅ **现代化设计**: 基于UI Toolkit的最新特性
- ✅ **响应式布局**: 自动适配不同屏幕尺寸
- ✅ **交互丰富**: 完整的鼠标事件处理
- ✅ **易于维护**: 清晰的代码结构和文档
- ✅ **性能优化**: 高效的渲染和内存使用
- ✅ **可扩展性**: 便于添加新功能和修改

---

*基于Unity UI Toolkit系统开发，遵循现代Web标准，支持Vector图形和响应式设计。*
