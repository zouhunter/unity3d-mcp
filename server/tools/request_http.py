"""
Unity网络请求工具，包含HTTP请求、文件下载、API调用等功能。
"""
from typing import Annotated, Dict, Any, Optional, List
from pydantic import Field
from mcp.server.fastmcp import FastMCP, Context
from .call_up import get_common_call_response


def register_request_http_tools(mcp: FastMCP):
    @mcp.tool("request_http")
    def request_http(
        ctx: Context,
        action: Annotated[str, Field(
            title="请求操作类型",
            description="要执行的网络操作: get(GET请求), post(POST请求), put(PUT请求), delete(DELETE请求), download(下载文件), upload(上传文件), ping(网络测试), batch_download(批量下载)",
            examples=["get", "post", "put", "delete", "download", "upload", "ping", "batch_download"]
        )],
        url: Annotated[Optional[str], Field(
            title="请求URL",
            description="请求的URL地址",
            default=None,
            examples=["https://api.github.com/repos/unity3d/unity", "https://httpbin.org/get"]
        )] = None,
        data: Annotated[Optional[Dict[str, Any]], Field(
            title="请求数据",
            description="请求数据（用于POST/PUT，JSON格式）",
            default=None,
            examples=[{"name": "test", "value": 123}, {"key": "value"}]
        )] = None,
        headers: Annotated[Optional[Dict[str, str]], Field(
            title="请求头",
            description="请求头字典",
            default=None,
            examples=[{"Content-Type": "application/json", "Authorization": "Bearer token"}]
        )] = None,
        save_path: Annotated[Optional[str], Field(
            title="保存路径",
            description="保存路径（用于下载，相对于Assets或绝对路径）",
            default=None,
            examples=["Assets/Downloads/file.zip", "D:/Downloads/image.png"]
        )] = None,
        file_path: Annotated[Optional[str], Field(
            title="文件路径",
            description="文件路径（用于上传）",
            default=None,
            examples=["Assets/Data/config.json", "D:/Files/document.pdf"]
        )] = None,
        timeout: Annotated[Optional[int], Field(
            title="超时时间",
            description="超时时间（秒），默认30秒",
            default=30,
            ge=1,
            le=300
        )] = 30,
        method: Annotated[Optional[str], Field(
            title="HTTP方法",
            description="HTTP方法（GET, POST, PUT, DELETE等）",
            default=None,
            examples=["GET", "POST", "PUT", "DELETE", "PATCH"]
        )] = None,
        content_type: Annotated[Optional[str], Field(
            title="内容类型",
            description="内容类型，默认application/json",
            default="application/json",
            examples=["application/json", "application/x-www-form-urlencoded", "multipart/form-data"]
        )] = "application/json",
        user_agent: Annotated[Optional[str], Field(
            title="用户代理",
            description="用户代理字符串",
            default=None,
            examples=["Unity-MCP/1.0", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36"]
        )] = None,
        accept_certificates: Annotated[Optional[bool], Field(
            title="接受证书",
            description="是否接受所有证书（用于测试）",
            default=False
        )] = False,
        follow_redirects: Annotated[Optional[bool], Field(
            title="跟随重定向",
            description="是否跟随重定向",
            default=True
        )] = True,
        encoding: Annotated[Optional[str], Field(
            title="文本编码",
            description="文本编码，默认UTF-8",
            default="UTF-8",
            examples=["UTF-8", "GBK", "ISO-8859-1"]
        )] = "UTF-8",
        form_data: Annotated[Optional[Dict[str, str]], Field(
            title="表单数据",
            description="表单数据（键值对）",
            default=None,
            examples=[{"username": "user", "password": "pass"}, {"field1": "value1"}]
        )] = None,
        query_params: Annotated[Optional[Dict[str, str]], Field(
            title="查询参数",
            description="查询参数（键值对）",
            default=None,
            examples=[{"page": "1", "limit": "10"}, {"search": "unity"}]
        )] = None,
        auth_token: Annotated[Optional[str], Field(
            title="认证令牌",
            description="认证令牌（Bearer Token）",
            default=None,
            examples=["eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...", "your-api-key"]
        )] = None,
        basic_auth: Annotated[Optional[str], Field(
            title="基本认证",
            description="基本认证（用户名:密码）",
            default=None,
            examples=["username:password", "admin:secret123"]
        )] = None,
        retry_count: Annotated[Optional[int], Field(
            title="重试次数",
            description="重试次数，默认0",
            default=0,
            ge=0,
            le=5
        )] = 0,
        retry_delay: Annotated[Optional[int], Field(
            title="重试延迟",
            description="重试延迟（秒），默认1秒",
            default=1,
            ge=1,
            le=10
        )] = 1,
        urls: Annotated[Optional[List[str]], Field(
            title="URL列表",
            description="URL数组（用于批量下载）",
            default=None,
            examples=[["https://example.com/file1.zip", "https://example.com/file2.zip"]]
        )] = None
    ) -> Dict[str, Any]:
        """Unity网络请求工具，用于执行各种网络操作。（二级工具）

        支持多种网络操作，适用于：
        - HTTP请求：GET、POST、PUT、DELETE等标准HTTP方法
        - 文件操作：下载和上传文件
        - API调用：与外部API进行交互
        - 网络测试：ping和连接测试
        - 批量操作：批量下载多个文件
        """
        
        return get_common_call_response("request_http")
