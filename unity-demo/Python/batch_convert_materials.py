#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
批量转换所有材质为URP材质的脚本
"""

import os

def get_remaining_material_files():
    """获取需要转换的材质文件列表"""
    material_files = []
    
    # 递归搜索所有.mat文件
    for root, dirs, files in os.walk("Assets/Materials"):
        for file in files:
            if file.endswith(".mat"):
                material_path = os.path.join(root, file).replace("\\", "/")
                material_files.append(material_path)
    
    # 排除已经转换的前5个
    converted_files = [
        "Assets/Materials/BlackMaterial.mat",
        "Assets/Materials/BlueMaterial.mat", 
        "Assets/Materials/BlueWindow.mat",
        "Assets/Materials/BrickWall.mat",
        "Assets/Materials/BrownDoor.mat"
    ]
    
    remaining_files = [f for f in material_files if f not in converted_files]
    
    print(f"总共 {len(material_files)} 个材质文件")
    print(f"已转换 {len(converted_files)} 个")
    print(f"剩余 {len(remaining_files)} 个需要转换")
    
    return remaining_files

def create_batch_calls(material_files, batch_size=10):
    """创建批量调用数据"""
    batches = []
    
    for i in range(0, len(material_files), batch_size):
        batch = material_files[i:i+batch_size]
        batch_calls = []
        
        for material_path in batch:
            batch_calls.append({
                "func": "edit_material",
                "args": {
                    "action": "change_shader",
                    "path": material_path,
                    "shader": "Universal Render Pipeline/Lit"
                }
            })
        
        batches.append({
            "batch_number": len(batches) + 1,
            "calls": batch_calls,
            "material_count": len(batch)
        })
    
    return batches

def main():
    """主函数"""
    remaining_files = get_remaining_material_files()
    
    if not remaining_files:
        print("所有材质都已转换完成！")
        return
    
    # 创建批量调用数据（每批10个）
    batches = create_batch_calls(remaining_files, batch_size=10)
    
    print(f"\n将分 {len(batches)} 批进行转换")
    for i, batch in enumerate(batches):
        print(f"批次 {i+1}: {batch['material_count']} 个材质")
    
    # 将批次数据保存到全局变量
    globals()['conversion_batches'] = batches
    globals()['remaining_files'] = remaining_files
    
    return batches

if __name__ == "__main__":
    result = main()
    print("\n✓ 批量转换数据准备完成")
