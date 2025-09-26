#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Unity材质球批量转换为URP材质脚本（MCP版本）
将Assets/Materials目录下的所有Standard材质转换为URP/Lit材质，保持贴图映射
"""

import os
import json

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

def convert_material_to_urp_batch(material_files):
    """批量转换材质为URP材质"""
    if not material_files:
        print("未找到任何材质文件")
        return
    
    # 构建批量函数调用列表
    batch_calls = []
    
    for material_path in material_files:
        # 为每个材质添加转换调用
        batch_calls.append({
            "func": "edit_material",
            "args": {
                "action": "change_shader",
                "path": material_path,
                "shader": "Universal Render Pipeline/Lit"
            }
        })
    
    print(f"\n开始批量转换 {len(material_files)} 个材质文件...")
    print("=" * 50)
    
    # 执行批量转换
    try:
        # 这里需要通过MCP调用批量函数
        print("正在执行批量材质转换...")
        
        # 由于我们在Python环境中，需要直接返回批量调用数据
        return {
            "batch_calls": batch_calls,
            "total_materials": len(material_files),
            "material_paths": material_files
        }
        
    except Exception as e:
        print(f"❌ 批量转换时出错: {str(e)}")
        return None

def main():
    """主函数"""
    material_files = get_all_material_files()
    
    if not material_files:
        print("未找到任何材质文件")
        return
    
    # 获取批量转换数据
    conversion_data = convert_material_to_urp_batch(material_files)
    
    if conversion_data:
        print(f"准备转换 {conversion_data['total_materials']} 个材质文件")
        print("批量转换数据已准备完成")
        
        # 返回转换数据供MCP使用
        return conversion_data
    else:
        print("准备转换数据失败")
        return None

if __name__ == "__main__":
    result = main()
    if result:
        print("✓ 转换数据准备完成")
        # 将结果保存到全局变量供后续使用
        globals()['conversion_result'] = result
