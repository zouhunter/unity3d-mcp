"""
Unity代码运行工具，包含Python代码执行和C#代码编译执行功能。
"""
import json
from typing import Annotated, Dict, Any, Optional
from pydantic import Field
from mcp.server.fastmcp import FastMCP, Context
from unity_connection import get_unity_connection


def register_run_code_tools(mcp: FastMCP):
    @mcp.tool("python_runner")
    def python_runner(
        ctx: Context,
        action: Annotated[str, Field(
            title="Python操作类型",
            description="要执行的Python操作: execute(执行代码), validate(验证代码), install_package(安装包)",
            examples=["execute", "validate", "install_package"]
        )],
        code: Annotated[Optional[str], Field(
            title="Python代码",
            description="要执行或验证的Python代码，支持UTF-8编码",
            default=None,
            examples=[
                "print('Hello Unity!')",
                "import numpy as np\nprint(np.array([1,2,3]))",
                "# 创建3D模型\nimport trimesh\nmesh = trimesh.creation.box()"
            ]
        )] = None,
        package_name: Annotated[Optional[str], Field(
            title="包名称",
            description="要安装的Python包名称，仅在install_package操作时使用",
            default=None,
            examples=["numpy", "trimesh", "matplotlib", "opencv-python"]
        )] = None,
        version: Annotated[Optional[str], Field(
            title="包版本",
            description="要安装的包版本号，留空安装最新版本",
            default=None,
            examples=["1.21.0", ">=1.0.0", "~=2.1.0"]
        )] = None,
        timeout: Annotated[Optional[int], Field(
            title="超时时间",
            description="代码执行或包安装的超时时间（秒）",
            default=30,
            ge=1,
            le=300
        )] = 30,
        cleanup: Annotated[bool, Field(
            title="自动清理",
            description="执行完成后是否自动清理临时文件和变量",
            default=True
        )] = True
    ) -> Dict[str, Any]:
        """Unity Python代码运行工具，支持执行Python代码、验证语法和安装包。

        提供完整的Python运行环境，适用于：
        - 数据处理：使用NumPy、Pandas等库处理数据
        - 3D建模：使用Trimesh、Open3D等库创建模型
        - 图像处理：使用OpenCV、PIL等库处理图像
        - 机器学习：运行TensorFlow、PyTorch等模型

        Returns:
            包含Python执行结果的字典：
            {
                "success": bool,        # 执行是否成功
                "message": str,         # 操作结果描述
                "data": Any,           # 执行结果数据
                "error": str|None,     # 错误信息（如果有的话）
                "output": str,         # 标准输出内容
                "stderr": str          # 错误输出内容
            }
        """
        
        try:
            # 验证操作类型
            valid_actions = ["execute", "validate", "install_package"]
            if action not in valid_actions:
                return {
                    "success": False,
                    "error": f"无效的操作类型: '{action}'。支持的操作: {valid_actions}",
                    "data": None
                }
            
            # 验证代码执行和验证参数
            if action in ["execute", "validate"]:
                if not code:
                    return {
                        "success": False,
                        "error": f"操作'{action}'需要提供code参数",
                        "data": None
                    }
                
                if not isinstance(code, str):
                    return {
                        "success": False,
                        "error": "code参数必须是字符串类型",
                        "data": None
                    }
            
            # 验证包安装参数
            if action == "install_package":
                if not package_name:
                    return {
                        "success": False,
                        "error": "install_package操作需要提供package_name参数",
                        "data": None
                    }
                
                if not isinstance(package_name, str):
                    return {
                        "success": False,
                        "error": "package_name参数必须是字符串类型",
                        "data": None
                    }
            
            # 验证超时参数
            if timeout and (timeout < 1 or timeout > 300):
                return {
                    "success": False,
                    "error": "超时时间必须在1-300秒之间",
                    "data": None
                }
            
            # 获取Unity连接实例
            bridge = get_unity_connection()
            
            if bridge is None:
                return {
                    "success": False,
                    "error": "无法获取Unity连接",
                    "data": None
                }
            
            # 准备发送给Unity的参数
            params = {"action": action}
            
            # 添加代码参数
            if code:
                params["code"] = code
            
            # 添加包安装参数
            if package_name:
                params["package_name"] = package_name
            if version:
                params["version"] = version
            
            # 添加执行配置参数
            if timeout:
                params["timeout"] = timeout
            params["cleanup"] = cleanup
            
            # 使用带重试机制的命令发送
            result = bridge.send_command_with_retry("python_runner", params, max_retries=2)
            
            # 确保返回结果包含success标志
            if isinstance(result, dict):
                return result
            else:
                return {
                    "success": True,
                    "message": f"Python {action} 操作执行完成",
                    "data": result,
                    "error": None
                }
                
        except json.JSONDecodeError as e:
            return {
                "success": False,
                "error": f"参数序列化失败: {str(e)}",
                "data": None
            }
        except ConnectionError as e:
            return {
                "success": False,
                "error": f"Unity连接错误: {str(e)}",
                "data": None
            }
        except Exception as e:
            return {
                "success": False,
                "error": f"Python代码执行失败: {str(e)}",
                "data": None
            }

    @mcp.tool("code_runner")
    def code_runner(
        ctx: Context,
        action: Annotated[str, Field(
            title="C#操作类型",
            description="要执行的C#操作: execute(编译执行), validate(验证语法)",
            examples=["execute", "validate"]
        )],
        code: Annotated[str, Field(
            title="C#代码",
            description="要编译执行或验证的C#代码，支持完整的Unity API访问",
            examples=[
                "Debug.Log(\"Hello from C#!\");",
                "var go = new GameObject(\"TestObject\"); go.transform.position = Vector3.zero;",
                "// 创建材质\nvar material = new Material(Shader.Find(\"Standard\"));"
            ]
        )],
        using_statements: Annotated[Optional[str], Field(
            title="Using语句",
            description="额外的using语句，用分号分隔",
            default=None,
            examples=["using System.Collections.Generic;", "using UnityEngine.UI; using TMPro;"]
        )] = None,
        timeout: Annotated[Optional[int], Field(
            title="超时时间",
            description="代码编译和执行的超时时间（秒）",
            default=30,
            ge=1,
            le=120
        )] = 30,
        assembly_references: Annotated[Optional[str], Field(
            title="程序集引用",
            description="额外需要引用的程序集名称，用分号分隔",
            default=None,
            examples=["UnityEngine.UI", "TMPro", "Cinemachine"]
        )] = None
    ) -> Dict[str, Any]:
        """Unity C#代码运行工具，支持编译执行C#代码和语法验证。

        提供完整的Unity API访问权限，适用于：
        - 快速原型：测试Unity API调用
        - 脚本验证：验证C#代码语法正确性
        - 自动化操作：执行复杂的Unity对象操作
        - 调试工具：运行调试和分析代码

        Returns:
            包含C#执行结果的字典：
            {
                "success": bool,        # 执行是否成功
                "message": str,         # 操作结果描述
                "data": Any,           # 执行结果数据
                "error": str|None,     # 错误信息（如果有的话）
                "compilation_output": str, # 编译输出信息
                "runtime_output": str   # 运行时输出信息
            }
        """
        
        try:
            # 验证操作类型
            valid_actions = ["execute", "validate"]
            if action not in valid_actions:
                return {
                    "success": False,
                    "error": f"无效的操作类型: '{action}'。支持的操作: {valid_actions}",
                    "data": None
                }
            
            # 验证代码参数
            if not code:
                return {
                    "success": False,
                    "error": "code参数不能为空",
                    "data": None
                }
            
            if not isinstance(code, str):
                return {
                    "success": False,
                    "error": "code参数必须是字符串类型",
                    "data": None
                }
            
            # 验证超时参数
            if timeout and (timeout < 1 or timeout > 120):
                return {
                    "success": False,
                    "error": "超时时间必须在1-120秒之间",
                    "data": None
                }
            
            # 获取Unity连接实例
            bridge = get_unity_connection()
            
            if bridge is None:
                return {
                    "success": False,
                    "error": "无法获取Unity连接",
                    "data": None
                }
            
            # 准备发送给Unity的参数
            params = {
                "action": action,
                "code": code
            }
            
            # 添加可选参数
            if using_statements:
                params["using_statements"] = using_statements
            if timeout:
                params["timeout"] = timeout
            if assembly_references:
                params["assembly_references"] = assembly_references
            
            # 使用带重试机制的命令发送
            result = bridge.send_command_with_retry("code_runner", params, max_retries=2)
            
            # 确保返回结果包含success标志
            if isinstance(result, dict):
                return result
            else:
                return {
                    "success": True,
                    "message": f"C# {action} 操作执行完成",
                    "data": result,
                    "error": None
                }
                
        except json.JSONDecodeError as e:
            return {
                "success": False,
                "error": f"参数序列化失败: {str(e)}",
                "data": None
            }
        except ConnectionError as e:
            return {
                "success": False,
                "error": f"Unity连接错误: {str(e)}",
                "data": None
            }
        except Exception as e:
            return {
                "success": False,
                "error": f"C#代码执行失败: {str(e)}",
                "data": None
            }
