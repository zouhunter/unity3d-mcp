#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
完整的 URP 材质转换器
使用 MCP 批量转换 Assets/Materials 下所有材质为 URP 兼容材质
"""

import os
import json

def get_all_material_files():
    """获取 Assets/Materials 目录下的所有 .mat 文件"""
    material_files = []
    
    # 递归搜索所有 .mat 文件
    for root, dirs, files in os.walk("Assets/Materials"):
        for file in files:
            if file.endswith(".mat"):
                material_path = os.path.join(root, file).replace("\\", "/")
                material_files.append(material_path)
    
    print(f"📋 找到 {len(material_files)} 个材质文件")
    return material_files

def create_urp_conversion_calls(material_files):
    """创建 URP 转换的 MCP 批量调用"""
    if not material_files:
        print("❌ 未找到任何材质文件")
        return []
    
    # 构建批量函数调用列表
    batch_calls = []
    
    print(f"\n🔄 准备转换 {len(material_files)} 个材质...")
    print("=" * 50)
    
    for i, material_path in enumerate(material_files, 1):
        print(f"  {i:3d}. {material_path}")
        
        # 添加材质转换调用
        batch_calls.append({
            "func": "edit_material",
            "args": {
                "action": "change_shader",
                "path": material_path,
                "shader": "Universal Render Pipeline/Lit"
            }
        })
    
    print(f"\n✅ 准备了 {len(batch_calls)} 个转换调用")
    return batch_calls

def create_property_optimization_calls(material_files):
    """创建材质属性优化调用（可选）"""
    optimization_calls = []
    
    # 对一些特殊材质进行属性优化
    special_materials = {
        "BlackMaterial.mat": {"_BaseColor": [0.0, 0.0, 0.0, 1.0]},
        "WhiteMaterial.mat": {"_BaseColor": [1.0, 1.0, 1.0, 1.0]},
        "RedMaterial.mat": {"_BaseColor": [1.0, 0.0, 0.0, 1.0]},
        "GreenMaterial.mat": {"_BaseColor": [0.0, 1.0, 0.0, 1.0]},
        "BlueMaterial.mat": {"_BaseColor": [0.0, 0.0, 1.0, 1.0]},
        "YellowMaterial.mat": {"_BaseColor": [1.0, 1.0, 0.0, 1.0]}
    }
    
    for material_path in material_files:
        material_name = os.path.basename(material_path)
        
        if material_name in special_materials:
            optimization_calls.append({
                "func": "edit_material",
                "args": {
                    "action": "set_properties",
                    "path": material_path,
                    "properties": special_materials[material_name]
                }
            })
    
    if optimization_calls:
        print(f"🎨 准备了 {len(optimization_calls)} 个属性优化调用")
    
    return optimization_calls

def main():
    """主函数 - 执行完整的材质转换流程"""
    print("🎨 Unity URP 材质批量转换器")
    print("=" * 60)
    print("📍 目标: 转换 Assets/Materials 下所有材质为 URP 兼容")
    print()
    
    # 步骤 1: 获取所有材质文件
    material_files = get_all_material_files()
    
    if not material_files:
        print("❌ 未找到任何材质文件，转换结束")
        return None
    
    # 步骤 2: 创建转换调用
    conversion_calls = create_urp_conversion_calls(material_files)
    
    # 步骤 3: 创建属性优化调用（可选）
    optimization_calls = create_property_optimization_calls(material_files)
    
    # 合并所有调用
    all_calls = conversion_calls + optimization_calls
    
    print(f"\n📊 转换统计:")
    print(f"   - 材质文件总数: {len(material_files)}")
    print(f"   - 着色器转换调用: {len(conversion_calls)}")
    print(f"   - 属性优化调用: {len(optimization_calls)}")
    print(f"   - 总调用数: {len(all_calls)}")
    
    # 返回转换数据
    conversion_data = {
        "total_materials": len(material_files),
        "material_files": material_files,
        "conversion_calls": conversion_calls,
        "optimization_calls": optimization_calls,
        "all_calls": all_calls
    }
    
    print("\n🚀 转换数据准备完成，可以执行批量转换")
    
    # 保存到全局变量供 MCP 使用
    globals()['urp_conversion_data'] = conversion_data
    
    return conversion_data

if __name__ == "__main__":
    print("正在准备 URP 材质转换...")
    result = main()
    
    if result:
        print(f"\n✅ 转换准备完成!")
        print(f"   使用 MCP 批量调用工具执行 {result['total_materials']} 个材质的转换")
        print("\n📋 材质列表预览 (前10个):")
        
        for i, material in enumerate(result['material_files'][:10]):
            print(f"   {i+1:2d}. {material}")
        
        if len(result['material_files']) > 10:
            print(f"   ... 还有 {len(result['material_files']) - 10} 个材质")
    else:
        print("❌ 转换准备失败")
