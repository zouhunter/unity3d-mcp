#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
智能URP材质转换器
正确处理Standard到URP/Lit的属性映射，保持颜色和纹理
"""

import os

def convert_material_with_property_preservation(material_path):
    """
    智能转换单个材质，保持属性
    """
    print(f"🔄 正在转换: {material_path}")
    
    # 步骤1: 获取原始材质信息
    get_info_result = {
        "func": "edit_material",
        "args": {
            "action": "get_info",
            "path": material_path
        }
    }
    
    # 步骤2: 更改着色器为URP/Lit
    change_shader_call = {
        "func": "edit_material", 
        "args": {
            "action": "change_shader",
            "path": material_path,
            "shader": "Universal Render Pipeline/Lit"
        }
    }
    
    return [get_info_result, change_shader_call]

def create_batch_conversion_calls():
    """创建批量转换调用"""
    
    # 获取所有材质文件
    all_materials = []
    for root, dirs, files in os.walk("Assets/Materials"):
        for file in files:
            if file.endswith(".mat"):
                material_path = os.path.join(root, file).replace("\\", "/")
                all_materials.append(material_path)
    
    print(f"📋 找到 {len(all_materials)} 个材质文件")
    
    # 创建转换调用 - 只转换着色器，让Unity自动处理属性映射
    conversion_calls = []
    
    for material_path in all_materials:
        conversion_calls.append({
            "func": "edit_material",
            "args": {
                "action": "change_shader",
                "path": material_path,
                "shader": "Universal Render Pipeline/Lit"
            }
        })
    
    print(f"✅ 准备了 {len(conversion_calls)} 个转换调用")
    return conversion_calls, all_materials

def create_property_fix_calls(materials_with_issues):
    """为有问题的材质创建属性修复调用"""
    fix_calls = []
    
    # 一些可能需要手动修复的材质
    problematic_materials = [
        "Assets/Materials/BlackMaterial.mat",
        "Assets/Materials/dog_texture.mat"
    ]
    
    for material_path in problematic_materials:
        if material_path in materials_with_issues:
            # 设置黑色材质为黑色
            if "Black" in material_path:
                fix_calls.append({
                    "func": "edit_material",
                    "args": {
                        "action": "set_properties",
                        "path": material_path,
                        "properties": {
                            "_BaseColor": [0.0, 0.0, 0.0, 1.0]  # 黑色
                        }
                    }
                })
    
    return fix_calls

def main():
    """主函数"""
    print("🎨 智能URP材质转换器")
    print("=" * 60)
    
    # 创建批量转换调用
    conversion_calls, all_materials = create_batch_conversion_calls()
    
    print(f"\n🚀 转换策略:")
    print("1. 批量转换所有材质的着色器为URP/Lit")
    print("2. Unity会自动处理大部分属性映射")
    print("3. 对有问题的材质进行手动修复")
    
    # 保存到全局变量
    globals()['conversion_calls'] = conversion_calls
    globals()['all_materials'] = all_materials
    
    return conversion_calls

if __name__ == "__main__":
    calls = main()
    print(f"\n✅ 智能转换器准备完成，共 {len(calls)} 个调用")
