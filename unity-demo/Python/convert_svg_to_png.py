import os
from PIL import Image
from cairosvg import svg2png
import io

# SVG文件目录
svg_dir = "Assets/Pics/Temp1"
png_dir = "Assets/Pics/Temp1/converted"

# 创建输出目录
os.makedirs(png_dir, exist_ok=True)

# SVG文件列表
svg_files = [
    "avatar_render.svg",
    "dock_bar.svg", 
    "figma_logo.svg",
    "speech_bubble.svg",
    "top_bar.svg"
]

for svg_file in svg_files:
    svg_path = os.path.join(svg_dir, svg_file)
    if os.path.exists(svg_path):
        # 生成PNG文件名
        png_file = svg_file.replace('.svg', '.png')
        png_path = os.path.join(png_dir, png_file)
        
        try:
            # 读取SVG文件并转换为PNG
            with open(svg_path, 'rb') as svg_file_obj:
                svg_content = svg_file_obj.read()
            
            # 转换SVG到PNG，设置较高的分辨率
            png_data = svg2png(
                bytestring=svg_content,
                output_width=1024,  # 设置输出宽度
                output_height=1024  # 设置输出高度
            )
            
            # 保存PNG文件
            with open(png_path, 'wb') as png_file_obj:
                png_file_obj.write(png_data)
                
            print(f"转换成功: {svg_file} -> {png_file}")
            
        except Exception as e:
            print(f"转换失败 {svg_file}: {e}")
    else:
        print(f"文件不存在: {svg_path}")

print("SVG到PNG转换完成！")
