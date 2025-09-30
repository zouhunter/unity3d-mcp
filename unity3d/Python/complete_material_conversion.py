#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
完成剩余所有材质的URP转换
"""

import os

def get_all_materials():
    """获取所有材质文件"""
    material_files = []
    for root, dirs, files in os.walk("Assets/Materials"):
        for file in files:
            if file.endswith(".mat"):
                material_path = os.path.join(root, file).replace("\\", "/")
                material_files.append(material_path)
    return material_files

def get_conversion_status():
    """显示转换状态"""
    all_materials = get_all_materials()
    
    # 已转换的材质（前15个）
    converted_materials = [
        "Assets/Materials/BlackMaterial.mat",
        "Assets/Materials/BlueMaterial.mat", 
        "Assets/Materials/BlueWindow.mat",
        "Assets/Materials/BrickWall.mat",
        "Assets/Materials/BrownDoor.mat",
        "Assets/Materials/DarkGrayChimney.mat",
        "Assets/Materials/DemoMaterial.mat",
        "Assets/Materials/DogMaterial.mat",
        "Assets/Materials/dog_texture.mat",
        "Assets/Materials/DoorMaterial.mat",
        "Assets/Materials/GrassMaterial.mat",
        "Assets/Materials/GreenGarden.mat",
        "Assets/Materials/GreenMaterial.mat",
        "Assets/Materials/Group01_Material.mat",
        "Assets/Materials/Group02_Material.mat"
    ]
    
    remaining_materials = [m for m in all_materials if m not in converted_materials]
    
    print(f"📊 转换状态总览:")
    print(f"✅ 已转换: {len(converted_materials)} 个材质")
    print(f"⏳ 剩余: {len(remaining_materials)} 个材质")
    print(f"📋 总计: {len(all_materials)} 个材质")
    print(f"📈 进度: {len(converted_materials)/len(all_materials)*100:.1f}%")
    
    if remaining_materials:
        print(f"\n🔄 剩余需要转换的材质:")
        for i, material in enumerate(remaining_materials[:10]):  # 只显示前10个
            print(f"  {i+1}. {material}")
        if len(remaining_materials) > 10:
            print(f"  ... 还有 {len(remaining_materials) - 10} 个材质")
    
    return remaining_materials

def create_final_batch_calls(materials):
    """创建最终批量转换调用"""
    print(f"\n🚀 准备批量转换 {len(materials)} 个材质...")
    
    batch_calls = []
    for material_path in materials:
        batch_calls.append({
            "func": "edit_material",
            "args": {
                "action": "change_shader",
                "path": material_path,
                "shader": "Universal Render Pipeline/Lit"
            }
        })
    
    print("✅ 批量转换调用已准备完成")
    return batch_calls

if __name__ == "__main__":
    remaining = get_conversion_status()
    
    if remaining:
        batch_calls = create_final_batch_calls(remaining)
        print(f"\n💡 提示: 将使用MCP批量函数调用转换剩余的 {len(remaining)} 个材质")
        
        # 保存到全局变量
        globals()['remaining_materials'] = remaining
        globals()['final_batch_calls'] = batch_calls
    else:
        print("\n🎉 所有材质都已转换完成！")
