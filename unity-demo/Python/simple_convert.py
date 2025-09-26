import os
import shutil

# 创建转换目录
svg_dir = "Assets/Pics/Temp1"
converted_dir = "Assets/Pics/Temp1/converted"
os.makedirs(converted_dir, exist_ok=True)

# SVG文件列表
svg_files = [
    "avatar_render.svg",
    "dock_bar.svg", 
    "figma_logo.svg",
    "speech_bubble.svg",
    "top_bar.svg"
]

print("由于SVG转换库的限制，让我们采用以下方案：")
print("1. 将SVG文件复制到converted目录")
print("2. 手动在Unity中设置这些文件的导入类型")

for svg_file in svg_files:
    src_path = os.path.join(svg_dir, svg_file)
    dst_path = os.path.join(converted_dir, svg_file)
    
    if os.path.exists(src_path):
        shutil.copy2(src_path, dst_path)
        print(f"复制: {svg_file}")
    else:
        print(f"文件不存在: {src_path}")

print("\n接下来的步骤：")
print("1. 在Unity中选择每个SVG文件")
print("2. 在Inspector中将Texture Type设置为'Sprite (2D and UI)'") 
print("3. 点击Apply应用设置")
print("4. 然后我们就可以将这些Sprite分配给Image组件了")
