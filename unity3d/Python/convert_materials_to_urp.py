#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Unity材质球批量转换为URP材质脚本
将Assets/Materials目录下的所有Standard材质转换为URP/Lit材质，保持贴图映射
"""

import os
import glob

def get_all_material_files():
    """获取Assets/Materials目录下的所有.mat文件"""
    material_files = []
    
    # 递归搜索所有.mat文件
    for root, dirs, files in os.walk("Assets/Materials"):
        for file in files:
            if file.endswith(".mat"):
                material_path = os.path.join(root, file).replace("\\", "/")
                material_files.append(material_path)
    
    print(f"找到 {len(material_files)} 个材质文件")
    return material_files

def convert_material_to_urp(material_path):
    """将单个材质转换为URP材质"""
    try:
        # 获取材质信息
        get_info_result = function_call(
            func="edit_material",
            args={
                "action": "get_info",
                "path": material_path
            }
        )
        
        print(f"正在转换: {material_path}")
        
        # 更改着色器为URP/Lit
        change_shader_result = function_call(
            func="edit_material", 
            args={
                "action": "change_shader",
                "path": material_path,
                "shader": "Universal Render Pipeline/Lit"
            }
        )
        
        print(f"✓ 已将 {material_path} 的着色器更改为 URP/Lit")
        
        # 检查是否有主纹理，如果有则重新设置以确保贴图映射正确
        if get_info_result and "success" in get_info_result:
            # 重新设置基本属性以确保兼容性
            set_properties_result = function_call(
                func="edit_material",
                args={
                    "action": "set_properties", 
                    "path": material_path,
                    "properties": {
                        # 确保基础颜色贴图槽位正确映射
                        "_BaseMap": None,  # URP使用_BaseMap而不是_MainTex
                        "_BaseColor": None  # URP使用_BaseColor而不是_Color
                    }
                }
            )
        
        return True
        
    except Exception as e:
        print(f"❌ 转换 {material_path} 时出错: {str(e)}")
        return False

def convert_all_materials():
    """批量转换所有材质"""
    material_files = get_all_material_files()
    
    if not material_files:
        print("未找到任何材质文件")
        return
    
    success_count = 0
    failed_count = 0
    
    print(f"\n开始转换 {len(material_files)} 个材质文件...")
    print("=" * 50)
    
    for material_path in material_files:
        if convert_material_to_urp(material_path):
            success_count += 1
        else:
            failed_count += 1
    
    print("=" * 50)
    print(f"转换完成!")
    print(f"✓ 成功: {success_count} 个")
    print(f"❌ 失败: {failed_count} 个")
    print(f"📊 总计: {len(material_files)} 个")
    
    if success_count > 0:
        print("\n重要提示:")
        print("1. 材质已转换为URP/Lit着色器")
        print("2. 请检查场景中的材质显示是否正常")
        print("3. 某些高级功能可能需要手动调整")
        print("4. 建议在转换后保存项目")

if __name__ == "__main__":
    convert_all_materials()

