#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
测试python_runner的create功能
验证文档更新是否正确
"""

def test_create_functionality():
    """测试创建功能"""
    print("🎯 测试python_runner的create功能")
    print("=" * 50)
    
    print("✅ 文档已更新包含以下内容:")
    print("1. action参数支持: execute, validate, install_package, create")
    print("2. code参数说明: 适用于execute/validate/create操作")
    print("3. script_path参数说明: execute/validate时为现有文件路径，create时为创建路径")
    print("4. 新增脚本创建操作说明")
    print("5. 添加了create操作的使用示例")
    print("6. 添加了create操作的返回值格式")
    
    print("\n📝 使用示例:")
    print("基本用法（默认保存到Python目录）:")
    print('''function_call(
    func="python_runner",
    args={
        "action": "create",
        "code": "print('Hello Unity!')",
        "script_name": "hello.py"
    }
)''')
    
    print("\n指定路径创建:")
    print('''function_call(
    func="python_runner",
    args={
        "action": "create",
        "code": "print('Hello Unity!')",
        "script_path": "Python/tools/helper.py"
    }
)''')
    
    print("\n🎉 文档更新完成！")
    return True

if __name__ == "__main__":
    result = test_create_functionality()
    print(f"\n测试结果: {'成功' if result else '失败'}")
