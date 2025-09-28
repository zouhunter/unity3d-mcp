"""
Unity代码运行工具，包含Python代码执行和C#代码编译执行功能。
"""
from typing import Annotated, Dict, Any, Optional
from pydantic import Field
from mcp.server.fastmcp import FastMCP, Context
from .call_up import get_common_call_response


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

        
        """
        
        # ⚠️ 重要提示：此函数仅用于提供参数说明和文档
        # 实际调用请使用 single_call 函数
        # 示例：single_call(func="python_runner", args={"action": "execute", "code": "print('Hello')"})
        
        return get_common_call_response("python_runner")


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
        """Unity C#代码运行工具，支持编译执行C#代码和语法验证。（二级工具）

        提供完整的Unity API访问权限，适用于：
        - 快速原型：测试Unity API调用
        - 脚本验证：验证C#代码语法正确性
        - 自动化操作：执行复杂的Unity对象操作
        - 调试工具：运行调试和分析代码
        """
        
        return get_common_call_response("code_runner")
