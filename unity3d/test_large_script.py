#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Unity MCP 大型数据传输测试脚本
生成大量输出数据来测试socket通信协议的稳定性和长度前缀机制
"""

import os
import time
import json
import sys
from datetime import datetime

def generate_large_data_structure():
    """生成大型数据结构用于测试"""
    print("🚀 开始生成大型数据结构...")
    
    # 生成大量数据
    large_data = {
        "timestamp": datetime.now().isoformat(),
        "test_info": {
            "purpose": "测试Unity MCP socket通信协议",
            "protocol": "长度前缀协议 (4字节大端序)",
            "expected_behavior": "能够正确传输大量数据而不丢失"
        },
        "large_arrays": [],
        "text_data": [],
        "nested_objects": {},
        "statistics": {}
    }
    
    # 1. 生成大型数组数据
    print("📊 生成大型数组数据...")
    for i in range(1000):  # 1000个数组
        array_data = []
        for j in range(100):  # 每个数组100个元素
            array_data.append({
                "id": i * 100 + j,
                "value": f"数据项_{i}_{j}",
                "timestamp": datetime.now().isoformat(),
                "metadata": {
                    "category": f"类别_{i % 10}",
                    "priority": j % 5,
                    "description": f"这是第{i}组第{j}个测试数据项，用于验证大数据传输",
                    "tags": [f"标签_{k}" for k in range(5)],
                    "properties": {
                        "size": j * 10,
                        "weight": i * 0.1 + j * 0.01,
                        "active": (i + j) % 2 == 0,
                        "color": ["红色", "绿色", "蓝色", "黄色", "紫色"][j % 5],
                        "coordinates": [i * 10, j * 10, (i + j) * 5]
                    }
                }
            })
        large_data["large_arrays"].append({
            "array_id": f"数组_{i}",
            "size": len(array_data),
            "data": array_data
        })
        
        if (i + 1) % 100 == 0:
            print(f"   ✓ 已生成 {i + 1}/1000 个数组")

    # 2. 生成大量文本数据
    print("📝 生成大量文本数据...")
    text_templates = [
        "这是一个用于测试Unity MCP socket通信协议的长文本字符串，包含中文字符以验证UTF-8编码的正确性。",
        "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.",
        "Unity MCP使用长度前缀协议来确保数据包的完整性，防止TCP分包导致的数据丢失问题。",
        "大型数据传输测试：我们需要验证socket连接能够处理超过常规缓冲区大小的数据包。",
        "🎮 游戏开发中经常需要传输大量的网格数据、纹理信息、动画数据等，这些都可能超过标准缓冲区大小。"
    ]
    
    for i in range(5000):  # 5000个文本条目
        large_data["text_data"].append({
            "text_id": f"text_{i}",
            "content": text_templates[i % len(text_templates)] * (i % 10 + 1),  # 重复1-10次
            "length": len(text_templates[i % len(text_templates)] * (i % 10 + 1)),
            "encoding": "UTF-8",
            "language": ["中文", "英文"][i % 2]
        })
        
        if (i + 1) % 1000 == 0:
            print(f"   ✓ 已生成 {i + 1}/5000 个文本条目")

    # 3. 生成嵌套对象
    print("🔗 生成复杂嵌套对象...")
    for i in range(200):  # 200个嵌套对象
        nested_obj = {
            "level_0": {
                "id": i,
                "name": f"嵌套对象_{i}",
                "level_1": {}
            }
        }
        
        for j in range(20):  # 每个对象20个子级
            level_1_obj = {
                "id": j,
                "data": f"Level1数据_{i}_{j}",
                "level_2": {}
            }
            
            for k in range(10):  # 每个子级10个子子级
                level_1_obj["level_2"][f"item_{k}"] = {
                    "value": f"Level2数据_{i}_{j}_{k}",
                    "attributes": {
                        "x": i * j + k,
                        "y": i + j * k,
                        "z": i * k + j,
                        "metadata": [f"属性_{m}" for m in range(5)]
                    }
                }
            
            nested_obj["level_0"]["level_1"][f"group_{j}"] = level_1_obj
        
        large_data["nested_objects"][f"nested_{i}"] = nested_obj
        
        if (i + 1) % 50 == 0:
            print(f"   ✓ 已生成 {i + 1}/200 个嵌套对象")

    return large_data

def print_large_output(data):
    """输出大量数据到标准输出"""
    print("\n" + "="*80)
    print("🔥 开始输出大型数据结构 (这将产生大量输出)")
    print("="*80)
    
    # 将整个数据结构转换为JSON字符串
    json_str = json.dumps(data, ensure_ascii=False, indent=2)
    json_size = len(json_str.encode('utf-8'))
    
    print(f"📈 JSON数据大小: {json_size:,} 字节 ({json_size/1024/1024:.2f} MB)")
    print(f"🔢 数组数量: {len(data['large_arrays'])}")
    print(f"📝 文本条目数量: {len(data['text_data'])}")
    print(f"🔗 嵌套对象数量: {len(data['nested_objects'])}")
    
    print("\n开始输出完整JSON数据...")
    print(json_str)
    
    print("\n" + "="*80)
    print("✅ 大型数据输出完成！")
    print("="*80)

def generate_statistics():
    """生成统计信息"""
    print("\n📊 生成测试统计信息...")
    
    stats = {
        "execution_time": time.time(),
        "python_version": sys.version,
        "platform": sys.platform,
        "encoding": sys.getdefaultencoding(),
        "test_results": {
            "data_generation": "成功",
            "json_serialization": "成功", 
            "utf8_encoding": "成功",
            "large_output": "成功"
        },
        "memory_info": {
            "estimated_memory_usage": "约50-100MB",
            "json_size_estimate": "约10-20MB"
        },
        "socket_test_info": {
            "protocol": "TCP with 4-byte big-endian length prefix",
            "max_message_size": "100MB",
            "encoding": "UTF-8",
            "expected_behavior": "完整传输无数据丢失"
        }
    }
    
    return stats

def main():
    """主函数"""
    print("🎯 Unity MCP 大型数据传输测试开始")
    print(f"⏰ 开始时间: {datetime.now()}")
    
    start_time = time.time()
    
    try:
        # 1. 生成大型数据
        large_data = generate_large_data_structure()
        
        # 2. 生成统计信息
        stats = generate_statistics()
        large_data["statistics"] = stats
        
        # 3. 输出大量数据
        print_large_output(large_data)
        
        # 4. 最终统计
        end_time = time.time()
        execution_time = end_time - start_time
        
        print(f"\n⚡ 执行完成!")
        print(f"⏱️  总执行时间: {execution_time:.2f} 秒")
        print(f"🎯 测试目的: 验证Unity MCP socket协议能否正确处理大数据包")
        print(f"📦 协议特性: 4字节大端序长度前缀 + UTF-8编码数据")
        print(f"✨ 如果您能看到这条消息，说明大数据传输测试成功!")
        
    except Exception as e:
        print(f"❌ 测试过程中发生错误: {e}")
        import traceback
        traceback.print_exc()

if __name__ == "__main__":
    main()
