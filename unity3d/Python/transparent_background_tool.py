#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
图片透明背景处理工具
使用AI模型自动移除图片背景，生成透明背景的PNG图片
支持批量处理和多种图片格式
"""

import os
import sys
from pathlib import Path
from PIL import Image
import requests
from io import BytesIO

# 尝试导入rembg库进行背景移除
try:
    from rembg import remove, new_session
    REMBG_AVAILABLE = True
except ImportError:
    REMBG_AVAILABLE = False
    print("⚠️  rembg库未安装，将尝试使用在线API或其他方法")

class TransparentBackgroundProcessor:
    """透明背景处理器"""
    
    def __init__(self, output_dir="Python/output/transparent"):
        self.output_dir = Path(output_dir)
        self.output_dir.mkdir(parents=True, exist_ok=True)
        
        # 支持的图片格式
        self.supported_formats = {'.jpg', '.jpeg', '.png', '.bmp', '.tiff', '.webp'}
        
        # 初始化rembg会话（如果可用）
        if REMBG_AVAILABLE:
            try:
                self.rembg_session = new_session('u2net')
                print("✅ rembg模型初始化成功")
            except Exception as e:
                print(f"⚠️  rembg模型初始化失败: {e}")
                self.rembg_session = None
        else:
            self.rembg_session = None
    
    def remove_background_rembg(self, image_path):
        """使用rembg移除背景"""
        if not REMBG_AVAILABLE or not self.rembg_session:
            return None
            
        try:
            with open(image_path, 'rb') as input_file:
                input_data = input_file.read()
            
            # 使用rembg移除背景
            output_data = remove(input_data, session=self.rembg_session)
            
            # 转换为PIL Image
            output_image = Image.open(BytesIO(output_data))
            return output_image
            
        except Exception as e:
            print(f"❌ rembg处理失败: {e}")
            return None
    
    def remove_background_simple(self, image_path, threshold=30):
        """简单的背景移除（基于颜色相似度）"""
        try:
            image = Image.open(image_path)
            image = image.convert("RGBA")
            
            data = image.getdata()
            
            # 假设背景是图片四角的主要颜色
            width, height = image.size
            corners = [
                image.getpixel((0, 0)),
                image.getpixel((width-1, 0)), 
                image.getpixel((0, height-1)),
                image.getpixel((width-1, height-1))
            ]
            
            # 选择最常见的角落颜色作为背景色
            bg_color = max(set(corners), key=corners.count)[:3]  # 只取RGB，忽略Alpha
            
            new_data = []
            for item in data:
                # 计算与背景色的差异
                diff = sum(abs(item[i] - bg_color[i]) for i in range(3))
                
                if diff < threshold:
                    # 背景色，设为透明
                    new_data.append((item[0], item[1], item[2], 0))
                else:
                    # 前景色，保持不变
                    new_data.append(item)
            
            image.putdata(new_data)
            return image
            
        except Exception as e:
            print(f"❌ 简单背景移除失败: {e}")
            return None
    
    def process_single_image(self, image_path, method='auto'):
        """处理单张图片"""
        image_path = Path(image_path)
        
        if not image_path.exists():
            print(f"❌ 文件不存在: {image_path}")
            return None
            
        if image_path.suffix.lower() not in self.supported_formats:
            print(f"❌ 不支持的格式: {image_path.suffix}")
            return None
        
        print(f"🔄 处理图片: {image_path.name}")
        
        # 根据方法选择处理方式
        result_image = None
        
        if method == 'auto' or method == 'rembg':
            result_image = self.remove_background_rembg(image_path)
            
        if result_image is None and (method == 'auto' or method == 'simple'):
            print("🔄 尝试简单背景移除方法...")
            result_image = self.remove_background_simple(image_path)
        
        if result_image is None:
            print(f"❌ 所有方法都失败了: {image_path.name}")
            return None
        
        # 保存结果
        output_filename = f"{image_path.stem}_transparent.png"
        output_path = self.output_dir / output_filename
        
        try:
            result_image.save(output_path, "PNG")
            print(f"✅ 成功保存: {output_path}")
            return output_path
        except Exception as e:
            print(f"❌ 保存失败: {e}")
            return None
    
    def process_batch(self, input_dir, method='auto'):
        """批量处理图片"""
        input_dir = Path(input_dir)
        
        if not input_dir.exists():
            print(f"❌ 输入目录不存在: {input_dir}")
            return []
        
        # 查找所有支持的图片文件
        image_files = []
        for ext in self.supported_formats:
            image_files.extend(input_dir.glob(f"*{ext}"))
            image_files.extend(input_dir.glob(f"*{ext.upper()}"))
        
        if not image_files:
            print(f"❌ 在 {input_dir} 中未找到支持的图片文件")
            return []
        
        print(f"📁 找到 {len(image_files)} 个图片文件")
        
        results = []
        for i, image_file in enumerate(image_files, 1):
            print(f"\n[{i}/{len(image_files)}] 处理: {image_file.name}")
            result = self.process_single_image(image_file, method)
            if result:
                results.append(result)
        
        print(f"\n🎉 批量处理完成！成功处理 {len(results)}/{len(image_files)} 张图片")
        return results
    
    def install_dependencies(self):
        """安装必要的依赖库"""
        dependencies = ['Pillow', 'requests', 'rembg']
        
        print("📦 检查并安装依赖库...")
        
        for dep in dependencies:
            try:
                __import__(dep.lower().replace('-', '_'))
                print(f"✅ {dep} 已安装")
            except ImportError:
                print(f"📥 正在安装 {dep}...")
                import subprocess
                try:
                    subprocess.check_call([sys.executable, '-m', 'pip', 'install', dep])
                    print(f"✅ {dep} 安装成功")
                except subprocess.CalledProcessError as e:
                    print(f"❌ {dep} 安装失败: {e}")

def main():
    """主函数"""
    print("🎨 图片透明背景处理工具")
    print("=" * 50)
    
    # 创建处理器实例
    processor = TransparentBackgroundProcessor()
    
    # 示例用法
    print("\n📖 使用方法:")
    print("1. 处理单张图片:")
    print('   processor.process_single_image("path/to/image.jpg")')
    print("\n2. 批量处理:")
    print('   processor.process_batch("path/to/images/")')
    print("\n3. 安装依赖:")
    print('   processor.install_dependencies()')
    
    # 检查是否有图片文件可以处理
    test_dirs = ['Assets/Textures', 'Images', 'Pictures']
    for test_dir in test_dirs:
        if Path(test_dir).exists():
            print(f"\n🔍 发现图片目录: {test_dir}")
            choice = input(f"是否批量处理 {test_dir} 中的图片？(y/n): ")
            if choice.lower() == 'y':
                processor.process_batch(test_dir)
                break
    else:
        print("\n💡 提示: 请将图片放在以下目录之一:")
        for dir_name in test_dirs:
            print(f"   - {dir_name}/")
    
    return processor

if __name__ == "__main__":
    processor = main()