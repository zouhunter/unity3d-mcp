#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
图片编辑和下载脚本
模拟网络请求，上传图片编辑任务，然后下载处理后的图片
"""

import requests
import json
import os
import time
import concurrent.futures
from typing import Optional, Dict, Any, Tuple
from urllib.parse import urlparse


class ImageEditorDownloader:
    def __init__(self):
        self.api_url = "https://queue.fal.run/fal-ai/gemini-25-flash-image/edit"
        self.api_key = "18b92434-10b2-4edf-9e3e-ce4b8f19548a:de87c849896380e0b93f16be35e73751"
        self.headers = {
            "Authorization": f"Key {self.api_key}",
            "Content-Type": "application/json"
        }
        self.session = requests.Session()
        self.session.headers.update(self.headers)

    def upload_edit_request(self, prompt: str, image_urls: list) -> Optional[Dict[Any, Any]]:
        """
        上传图片编辑请求
        
        Args:
            prompt: 编辑指令
            image_urls: 图片URL列表
            
        Returns:
            服务器响应的JSON数据，如果失败则返回None
        """
        data = {
            "prompt": prompt,
            "image_urls": image_urls
        }
        
        try:
            print(f"正在发送编辑请求...")
            print(f"API地址: {self.api_url}")
            print(f"编辑指令: {prompt}")
            print(f"源图片: {image_urls}")
            
            response = self.session.post(
                self.api_url,
                json=data,
                timeout=30
            )
            
            print(f"响应状态码: {response.status_code}")
            
            if response.status_code == 200:
                response_data = response.json()
                print("请求发送成功!")
                print(f"响应数据: {json.dumps(response_data, indent=2, ensure_ascii=False)}")
                return response_data
            else:
                print(f"请求失败，状态码: {response.status_code}")
                print(f"错误信息: {response.text}")
                return None
                
        except requests.exceptions.RequestException as e:
            print(f"网络请求异常: {e}")
            return None
        except json.JSONDecodeError as e:
            print(f"JSON解析错误: {e}")
            return None

    def check_task_status(self, request_id: str) -> Optional[Dict[Any, Any]]:
        """
        查询任务状态
        
        Args:
            request_id: 任务请求ID
            
        Returns:
            状态查询响应数据，如果失败则返回None
        """
        status_url = f"https://queue.fal.run/fal-ai/gemini-25-flash-image/requests/{request_id}/status"
        
        try:
            print(f"正在查询任务状态...")
            print(f"状态查询URL: {status_url}")
            print(f"请求ID: {request_id}")
            
            response = self.session.get(status_url, timeout=30)
            
            print(f"状态查询响应码: {response.status_code}")
            
            if response.status_code == 200:
                status_data = response.json()
                print("状态查询成功!")
                print(f"状态数据: {json.dumps(status_data, indent=2, ensure_ascii=False)}")
                return status_data
            else:
                print(f"状态查询失败，状态码: {response.status_code}")
                print(f"错误信息: {response.text}")
                return None
                
        except requests.exceptions.RequestException as e:
            print(f"状态查询网络异常: {e}")
            return None
        except json.JSONDecodeError as e:
            print(f"状态响应JSON解析错误: {e}")
            return None

    def poll_task_status(self, request_id: str, max_wait_time: int = 300) -> Optional[Dict[Any, Any]]:
        """
        轮询任务状态直到完成
        
        Args:
            request_id: 任务请求ID
            max_wait_time: 最大等待时间（秒），默认5分钟
            
        Returns:
            最终状态数据，如果超时或失败则返回None
        """
        print(f"\n开始轮询任务状态...")
        print(f"请求ID: {request_id}")
        print(f"最大等待时间: {max_wait_time} 秒")
        
        start_time = time.time()
        poll_count = 0
        
        while time.time() - start_time < max_wait_time:
            poll_count += 1
            print(f"\n--- 第 {poll_count} 次状态查询 ---")
            
            status_data = self.check_task_status(request_id)
            
            if status_data is None:
                print("状态查询失败，等待3秒后重试...")
                time.sleep(3)
                continue
            
            # 检查任务状态
            status = status_data.get('status', '').lower()
            print(f"当前任务状态: {status}")
            
            if status in ['completed', 'success', 'done']:
                print("✅ 任务已完成!")
                return status_data
            elif status in ['failed', 'error']:
                print("❌ 任务失败!")
                error_msg = status_data.get('error', status_data.get('message', '未知错误'))
                print(f"错误信息: {error_msg}")
                return status_data
            elif status in ['pending', 'queued', 'processing', 'in_progress']:
                elapsed_time = int(time.time() - start_time)
                print(f"⏳ 任务进行中... (已等待 {elapsed_time} 秒)")
                
                # 显示进度信息（如果有的话）
                if 'progress' in status_data:
                    progress = status_data['progress']
                    print(f"进度: {progress}")
                if 'eta' in status_data:
                    eta = status_data['eta']
                    print(f"预计完成时间: {eta}")
                if 'queue_position' in status_data:
                    queue_pos = status_data['queue_position']
                    print(f"队列位置: {queue_pos}")
                
                print("等待3秒后再次查询...")
                time.sleep(3)
            else:
                print(f"⚠️ 未知状态: {status}")
                print("等待3秒后再次查询...")
                time.sleep(3)
        
        print(f"\n⏰ 轮询超时 (超过 {max_wait_time} 秒)")
        return None

    def extract_response_url(self, response_data: Dict[Any, Any]) -> Optional[str]:
        """
        从响应数据中提取response_url
        
        Args:
            response_data: 服务器响应的JSON数据
            
        Returns:
            提取的图片URL，如果找不到则返回None
        """
        try:
            # 尝试多种可能的字段名
            possible_fields = ['response_url', 'image_url', 'result_url', 'download_url', 'url']
            
            for field in possible_fields:
                if field in response_data:
                    url = response_data[field]
                    print(f"找到图片URL字段 '{field}': {url}")
                    return url
            
            # 如果直接字段不存在，尝试在嵌套对象中查找
            if 'data' in response_data:
                for field in possible_fields:
                    if field in response_data['data']:
                        url = response_data['data'][field]
                        print(f"在data中找到图片URL字段 '{field}': {url}")
                        return url
            
            # 如果还是找不到，打印所有可能的键
            print("未找到图片URL，响应数据的键包括:")
            print(f"顶级键: {list(response_data.keys())}")
            if 'data' in response_data and isinstance(response_data['data'], dict):
                print(f"data中的键: {list(response_data['data'].keys())}")
            
            return None
            
        except Exception as e:
            print(f"提取URL时出错: {e}")
            return None

    def download_image(self, image_url: str, save_path: str = None, max_retries: int = 50) -> bool:
        """
        下载图片（带重试机制）
        
        Args:
            image_url: 图片下载地址
            save_path: 保存路径，如果不指定则自动生成
            max_retries: 最大重试次数，默认50次（避免无限循环）
            
        Returns:
            下载是否成功
        """
        if not save_path:
            # 从URL中提取文件名，如果没有则使用时间戳
            parsed_url = urlparse(image_url)
            filename = os.path.basename(parsed_url.path)
            if not filename or '.' not in filename:
                filename = f"edited_image_{int(time.time())}.png"
            save_path = filename
        
        print(f"正在下载图片...")
        print(f"下载地址: {image_url}")
        print(f"保存路径: {save_path}")
        print(f"最大重试次数: {max_retries}")
        
        retry_count = 0
        
        while retry_count <= max_retries:
            try:
                if retry_count > 0:
                    print(f"\n第 {retry_count} 次重试下载...")
                
                # 使用相同的Authorization头部下载图片
                response = self.session.get(image_url, timeout=60)
                
                if response.status_code == 200:
                    # 检查响应内容类型
                    content_type = response.headers.get('content-type', '').lower()
                    
                    # 如果是JSON响应，尝试提取实际图片URL
                    if 'application/json' in content_type or self._is_json_response(response.content):
                        print("🔍 检测到JSON响应，尝试提取实际图片URL...")
                        
                        try:
                            json_data = response.json()
                            print(f"JSON响应内容: {json.dumps(json_data, indent=2, ensure_ascii=False)}")
                            
                            # 提取图片URL
                            actual_image_url = self._extract_image_url_from_json(json_data)
                            
                            if actual_image_url:
                                print(f"✅ 找到实际图片URL: {actual_image_url}")
                                
                                # 递归下载实际图片（但不超过3层递归）
                                if not hasattr(self, '_download_depth'):
                                    self._download_depth = 0
                                
                                if self._download_depth < 3:
                                    self._download_depth += 1
                                    print(f"🔄 下载实际图片（递归深度: {self._download_depth}）...")
                                    
                                    try:
                                        result = self.download_image(actual_image_url, save_path, max_retries)
                                        return result
                                    finally:
                                        self._download_depth -= 1
                                else:
                                    print("❌ 递归深度过深，停止下载")
                                    return False
                            else:
                                print("❌ JSON响应中未找到有效的图片URL")
                                return False
                                
                        except json.JSONDecodeError as e:
                            print(f"❌ JSON解析失败: {e}")
                            return False
                    else:
                        # 正常的图片响应
                        # 确保保存目录存在
                        os.makedirs(os.path.dirname(save_path) if os.path.dirname(save_path) else '.', exist_ok=True)
                        
                        with open(save_path, 'wb') as f:
                            f.write(response.content)
                        
                        file_size = len(response.content)
                        success_msg = f"图片下载成功! 文件大小: {file_size} 字节"
                        if retry_count > 0:
                            success_msg += f"（重试 {retry_count} 次后成功）"
                        print(success_msg)
                        print(f"保存位置: {os.path.abspath(save_path)}")
                        return True
                    
                elif response.status_code == 400:
                    retry_count += 1
                    print(f"收到400错误，状态码: {response.status_code}")
                    print(f"错误信息: {response.text}")
                    
                    if retry_count <= max_retries:
                        print(f"将在3秒后进行第 {retry_count} 次重试...")
                        time.sleep(3)
                    else:
                        print(f"已达到最大重试次数 ({max_retries})，下载失败")
                        return False
                else:
                    # 其他状态码，不重试
                    print(f"图片下载失败，状态码: {response.status_code}")
                    print(f"错误信息: {response.text}")
                    print("非400错误，不进行重试")
                    return False
                    
            except requests.exceptions.RequestException as e:
                print(f"下载请求异常: {e}")
                retry_count += 1
                
                if retry_count <= max_retries:
                    print(f"网络异常，将在3秒后进行第 {retry_count} 次重试...")
                    time.sleep(3)
                else:
                    print(f"已达到最大重试次数 ({max_retries})，下载失败")
                    return False
                    
            except IOError as e:
                print(f"文件保存异常: {e}")
                return False
        
        return False

    def _is_json_response(self, content: bytes) -> bool:
        """
        检测响应内容是否为JSON格式
        
        Args:
            content: 响应内容（字节）
            
        Returns:
            是否为JSON格式
        """
        try:
            # 尝试解析前1000字节来判断是否为JSON
            sample = content[:1000].decode('utf-8', errors='ignore').strip()
            return sample.startswith('{') or sample.startswith('[')
        except Exception:
            return False

    def _extract_image_url_from_json(self, json_data: Dict[Any, Any]) -> Optional[str]:
        """
        从JSON响应中提取图片URL
        
        Args:
            json_data: JSON响应数据
            
        Returns:
            提取的图片URL，如果找不到则返回None
        """
        try:
            # 检查是否有images数组
            if 'images' in json_data and isinstance(json_data['images'], list):
                images = json_data['images']
                
                if len(images) > 0:
                    first_image = images[0]
                    
                    # 从第一个图片对象中提取URL
                    if isinstance(first_image, dict) and 'url' in first_image:
                        url = first_image['url']
                        print(f"📋 图片信息:")
                        
                        # 显示图片详细信息
                        if 'content_type' in first_image:
                            print(f"   内容类型: {first_image['content_type']}")
                        if 'file_name' in first_image:
                            print(f"   文件名: {first_image['file_name']}")
                        if 'file_size' in first_image:
                            print(f"   文件大小: {first_image['file_size']} 字节")
                        if 'width' in first_image and first_image['width']:
                            print(f"   宽度: {first_image['width']}px")
                        if 'height' in first_image and first_image['height']:
                            print(f"   高度: {first_image['height']}px")
                        
                        # 显示描述信息（如果有）
                        if 'description' in json_data:
                            print(f"📝 描述: {json_data['description']}")
                        
                        return url
            
            # 备选方案：检查其他可能的字段
            possible_url_fields = [
                'image_url', 'url', 'download_url', 'result_url', 
                'output_url', 'file_url', 'media_url'
            ]
            
            for field in possible_url_fields:
                if field in json_data:
                    url = json_data[field]
                    if isinstance(url, str) and url.startswith('http'):
                        print(f"✅ 在字段 '{field}' 中找到URL: {url}")
                        return url
            
            # 递归搜索嵌套对象
            for key, value in json_data.items():
                if isinstance(value, dict):
                    nested_url = self._extract_image_url_from_json(value)
                    if nested_url:
                        print(f"✅ 在嵌套字段 '{key}' 中找到URL: {nested_url}")
                        return nested_url
            
            print("⚠️ JSON响应结构:")
            print(f"   顶级键: {list(json_data.keys())}")
            
            return None
            
        except Exception as e:
            print(f"❌ 提取图片URL时出错: {e}")
            return None

    def extract_request_id(self, response_data: Dict[Any, Any]) -> Optional[str]:
        """
        从响应数据中提取request_id
        
        Args:
            response_data: 服务器响应的JSON数据
            
        Returns:
            提取的request_id，如果找不到则返回None
        """
        try:
            # 尝试多种可能的字段名
            possible_fields = ['request_id', 'requestId', 'id', 'task_id', 'taskId', 'job_id', 'jobId']
            
            for field in possible_fields:
                if field in response_data:
                    request_id = response_data[field]
                    print(f"找到请求ID字段 '{field}': {request_id}")
                    return request_id
            
            # 如果直接字段不存在，尝试在嵌套对象中查找
            if 'data' in response_data:
                for field in possible_fields:
                    if field in response_data['data']:
                        request_id = response_data['data'][field]
                        print(f"在data中找到请求ID字段 '{field}': {request_id}")
                        return request_id
            
            print("未找到请求ID，响应数据的键包括:")
            print(f"顶级键: {list(response_data.keys())}")
            if 'data' in response_data and isinstance(response_data['data'], dict):
                print(f"data中的键: {list(response_data['data'].keys())}")
            
            return None
            
        except Exception as e:
            print(f"提取请求ID时出错: {e}")
            return None

    def check_both_urls_parallel(self, request_id: str, response_url: str = None) -> Tuple[Optional[Dict], Optional[Dict]]:
        """
        并行访问status_url和response_url
        
        Args:
            request_id: 任务请求ID
            response_url: 响应URL（如果有的话）
            
        Returns:
            (status_data, response_data) 元组
        """
        print(f"\n🔄 并行访问状态URL和响应URL...")
        
        def fetch_status():
            """获取状态信息"""
            try:
                status_url = f"https://queue.fal.run/fal-ai/gemini-25-flash-image/requests/{request_id}/status"
                print(f"📊 访问状态URL: {status_url}")
                
                response = self.session.get(status_url, timeout=30)
                if response.status_code == 200:
                    data = response.json()
                    print("✅ 状态URL访问成功")
                    return data
                else:
                    print(f"❌ 状态URL访问失败: {response.status_code}")
                    return None
            except Exception as e:
                print(f"❌ 状态URL访问异常: {e}")
                return None
        
        def fetch_response():
            """获取响应信息"""
            if not response_url:
                print("⚠️ 未提供响应URL，跳过")
                return None
            
            try:
                print(f"📥 访问响应URL: {response_url}")
                
                response = self.session.get(response_url, timeout=30)
                if response.status_code == 200:
                    # 尝试解析为JSON，如果失败则返回原始内容
                    try:
                        data = response.json()
                        print("✅ 响应URL访问成功 (JSON格式)")
                        return data
                    except json.JSONDecodeError:
                        print("✅ 响应URL访问成功 (非JSON格式)")
                        return {"content": response.content, "headers": dict(response.headers)}
                else:
                    print(f"❌ 响应URL访问失败: {response.status_code}")
                    return None
            except Exception as e:
                print(f"❌ 响应URL访问异常: {e}")
                return None
        
        # 并行执行两个请求
        with concurrent.futures.ThreadPoolExecutor(max_workers=2) as executor:
            # 提交两个任务
            status_future = executor.submit(fetch_status)
            response_future = executor.submit(fetch_response) if response_url else None
            
            # 获取结果
            status_data = status_future.result()
            response_data = response_future.result() if response_future else None
        
        print(f"\n📋 并行访问结果:")
        print(f"  状态数据: {'✅ 成功' if status_data else '❌ 失败'}")
        print(f"  响应数据: {'✅ 成功' if response_data else '❌ 失败或未提供URL'}")
        
        return status_data, response_data

    def monitor_task_with_parallel_access(self, request_id: str, response_url: str = None, max_wait_time: int = 300) -> Optional[Dict[Any, Any]]:
        """
        监控任务并并行访问多个URL
        
        Args:
            request_id: 任务请求ID
            response_url: 响应URL（可选）
            max_wait_time: 最大等待时间（秒）
            
        Returns:
            最终的任务数据
        """
        print(f"\n🔍 开始监控任务（并行访问模式）...")
        print(f"请求ID: {request_id}")
        if response_url:
            print(f"响应URL: {response_url}")
        print(f"最大等待时间: {max_wait_time} 秒")
        
        start_time = time.time()
        check_count = 0
        
        while time.time() - start_time < max_wait_time:
            check_count += 1
            print(f"\n{'='*50}")
            print(f"第 {check_count} 次并行检查")
            print(f"{'='*50}")
            
            # 并行访问两个URL
            status_data, response_data = self.check_both_urls_parallel(request_id, response_url)
            
            # 分析状态数据
            if status_data:
                status = status_data.get('status', '').lower()
                print(f"\n📊 任务状态: {status}")
                
                # 显示详细信息
                if 'progress' in status_data:
                    print(f"   进度: {status_data['progress']}")
                if 'eta' in status_data:
                    print(f"   预计完成: {status_data['eta']}")
                if 'queue_position' in status_data:
                    print(f"   队列位置: {status_data['queue_position']}")
                
                # 检查是否完成
                if status in ['completed', 'success', 'done']:
                    print("🎉 任务已完成!")
                    
                    # 如果状态中有最新的下载URL，使用它
                    final_download_url = self.extract_response_url(status_data)
                    if final_download_url:
                        print(f"📥 从状态数据中找到下载URL: {final_download_url}")
                        return {"download_url": final_download_url, "status_data": status_data, "response_data": response_data}
                    
                    # 否则使用原始响应URL
                    if response_url:
                        print(f"📥 使用原始响应URL: {response_url}")
                        return {"download_url": response_url, "status_data": status_data, "response_data": response_data}
                    
                    return status_data
                    
                elif status in ['failed', 'error']:
                    print("❌ 任务失败!")
                    error_msg = status_data.get('error', status_data.get('message', '未知错误'))
                    print(f"   错误信息: {error_msg}")
                    return status_data
            
            # 分析响应数据
            if response_data:
                print(f"\n📥 响应数据状态: 可用")
                if isinstance(response_data, dict) and 'content' not in response_data:
                    # 如果是JSON格式，可能包含有用信息
                    print("   响应包含结构化数据")
                else:
                    print("   响应包含原始内容")
            
            elapsed_time = int(time.time() - start_time)
            print(f"\n⏱️ 已等待: {elapsed_time} 秒")
            print("等待3秒后继续...")
            time.sleep(3)
        
        print(f"\n⏰ 监控超时 (超过 {max_wait_time} 秒)")
        return None

    def process_image_edit(self, prompt: str, image_urls: list, save_path: str = None, wait_for_completion: bool = True, use_parallel_access: bool = True) -> bool:
        """
        完整的图片编辑和下载流程（带并行状态监控）
        
        Args:
            prompt: 编辑指令
            image_urls: 图片URL列表
            save_path: 保存路径
            wait_for_completion: 是否等待任务完成，默认True
            use_parallel_access: 是否使用并行访问模式，默认True
            
        Returns:
            整个流程是否成功
        """
        print("=" * 70)
        print("开始图片编辑和下载流程（并行状态监控）")
        print("=" * 70)
        
        # 第一步：上传编辑请求
        print("\n🚀 步骤1: 发送编辑请求")
        response_data = self.upload_edit_request(prompt, image_urls)
        if not response_data:
            print("❌ 上传编辑请求失败，流程终止")
            return False
        
        print("\n" + "-" * 50)
        
        # 第二步：提取请求信息
        print("\n🔍 步骤2: 提取请求信息")
        request_id = self.extract_request_id(response_data)
        initial_response_url = self.extract_response_url(response_data)
        
        print(f"请求ID: {request_id if request_id else '未找到'}")
        print(f"初始响应URL: {initial_response_url if initial_response_url else '未找到'}")
        
        if not request_id:
            print("⚠️ 未找到请求ID，无法进行状态监控")
            if initial_response_url:
                print("🔄 尝试直接下载初始响应URL...")
                success = self.download_image(initial_response_url, save_path)
                return success
            else:
                print("❌ 既没有请求ID也没有响应URL，流程终止")
                return False
        
        if not wait_for_completion:
            print("⚠️ 选择不等待完成，尝试直接下载...")
            if initial_response_url:
                success = self.download_image(initial_response_url, save_path)
                return success
            else:
                print("❌ 没有可下载的URL")
                return False
        
        print("\n" + "-" * 50)
        
        # 第三步：监控任务状态
        if use_parallel_access:
            print("\n⚡ 步骤3: 并行监控任务状态")
            final_result = self.monitor_task_with_parallel_access(request_id, initial_response_url)
        else:
            print("\n⏳ 步骤3: 传统监控任务状态")
            final_result = self.poll_task_status(request_id)
        
        if final_result is None:
            print("❌ 任务监控超时或失败")
            return False
        
        # 第四步：确定下载URL
        print("\n" + "-" * 50)
        print("\n📥 步骤4: 确定下载URL")
        
        download_url = None
        
        # 如果是并行访问的结果
        if isinstance(final_result, dict) and 'download_url' in final_result:
            download_url = final_result['download_url']
            print(f"✅ 从并行监控结果中获取下载URL: {download_url}")
        else:
            # 传统方式提取URL
            if isinstance(final_result, dict):
                status = final_result.get('status', '').lower()
                if status in ['completed', 'success', 'done']:
                    download_url = self.extract_response_url(final_result)
                    if download_url:
                        print(f"✅ 从最终状态中提取下载URL: {download_url}")
                    elif initial_response_url:
                        download_url = initial_response_url
                        print(f"✅ 使用初始响应URL: {download_url}")
                else:
                    print(f"❌ 任务最终状态不是成功: {status}")
                    return False
        
        if not download_url:
            print("❌ 无法确定图片下载URL，流程终止")
            return False
        
        print("\n" + "-" * 50)
        
        # 第五步：下载图片
        print("\n📥 步骤5: 下载处理后的图片")
        success = self.download_image(download_url, save_path)
        
        print("\n" + "=" * 70)
        if success:
            print("🎉 图片编辑和下载流程完成!")
        else:
            print("❌ 图片下载失败")
        print("=" * 70)
        
        return success


def main():
    """主函数"""
    # 初始化下载器
    downloader = ImageEditorDownloader()
    
    # 示例1: 并行监控的完整图片编辑流程
    print("示例1: 并行监控的完整图片编辑和下载流程")
    prompt = "变成狗头"
    image_urls = ["https://img.itouxiang.com/m12/de/54/833614a69a28.jpg"]
    save_path = "edited_dog_image.png"
    
    # 执行完整流程（使用并行访问）
    success = downloader.process_image_edit(
        prompt=prompt, 
        image_urls=image_urls, 
        save_path=save_path, 
        use_parallel_access=True
    )
    
    if success:
        print("\n✅ 并行监控的图片编辑和下载流程完成成功!")
    else:
        print("\n❌ 并行监控的图片编辑和下载过程中出现错误")
    
    print("\n" + "=" * 90)
    
    # 示例2: 并行查询特定任务状态
    print("\n示例2: 并行查询特定任务状态和响应")
    specific_request_id = "44012ac5-a7f0-4ea2-8607-d959f407175a"
    
    print(f"🔍 查询请求ID: {specific_request_id}")
    
    # 先尝试单独的状态查询
    status_result = downloader.check_task_status(specific_request_id)
    
    if status_result:
        status = status_result.get('status', 'unknown')
        print(f"\n📊 当前任务状态: {status}")
        
        # 检查是否有响应URL可以并行访问
        response_url = downloader.extract_response_url(status_result)
        
        if response_url:
            print(f"📥 找到响应URL: {response_url}")
            
            # 演示并行访问
            print("\n🔄 演示并行访问状态URL和响应URL...")
            status_data, response_data = downloader.check_both_urls_parallel(specific_request_id, response_url)
            
            if status_data and response_data:
                print("✅ 并行访问成功!")
                print("可以根据需要进一步处理两个数据源...")
            elif status_data:
                print("⚠️ 只有状态数据可用")
            elif response_data:
                print("⚠️ 只有响应数据可用")
            else:
                print("❌ 并行访问失败")
        
        # 如果任务已完成，尝试下载
        if status.lower() in ['completed', 'success', 'done']:
            final_download_url = downloader.extract_response_url(status_result)
            if final_download_url:
                print(f"\n📥 找到下载链接: {final_download_url}")
                download_success = downloader.download_image(final_download_url, "parallel_check_image.png")
                if download_success:
                    print("✅ 基于并行查询的图片下载成功!")
                else:
                    print("❌ 基于并行查询的图片下载失败")
            else:
                print("⚠️ 任务已完成但未找到下载链接")
        elif status.lower() in ['pending', 'queued', 'processing', 'in_progress']:
            print("\n⏳ 任务还在进行中...")
            print("💡 可以使用monitor_task_with_parallel_access()方法等待完成")
            # 示例：
            # final_result = downloader.monitor_task_with_parallel_access(specific_request_id)
        else:
            print(f"\n⚠️ 任务状态为: {status}")
    else:
        print("❌ 状态查询失败")


def query_specific_task():
    """查询特定任务的便利函数"""
    downloader = ImageEditorDownloader()
    request_id = "44012ac5-a7f0-4ea2-8607-d959f407175a"
    
    print("=" * 50)
    print("查询特定任务状态")
    print("=" * 50)
    
    # 单次状态查询
    status_data = downloader.check_task_status(request_id)
    
    if status_data:
        status = status_data.get('status', 'unknown')
        print(f"\n✅ 状态查询成功!")
        print(f"任务状态: {status}")
        
        # 如果是进行中的任务，提供轮询选项
        if status.lower() in ['pending', 'queued', 'processing', 'in_progress']:
            print("\n任务正在进行中，开始轮询状态...")
            final_status = downloader.poll_task_status(request_id)
            
            if final_status:
                final_state = final_status.get('status', 'unknown')
                if final_state.lower() in ['completed', 'success', 'done']:
                    download_url = downloader.extract_response_url(final_status)
                    if download_url:
                        success = downloader.download_image(download_url, "polled_task_image.png")
                        if success:
                            print("✅ 轮询完成并成功下载图片!")
                        else:
                            print("❌ 轮询完成但下载失败")
        elif status.lower() in ['completed', 'success', 'done']:
            download_url = downloader.extract_response_url(status_data)
            if download_url:
                success = downloader.download_image(download_url, "completed_task_image.png")
                if success:
                    print("✅ 直接下载完成的任务图片成功!")
    else:
        print("❌ 状态查询失败")


def demo_parallel_monitoring():
    """演示并行监控功能的专用函数"""
    downloader = ImageEditorDownloader()
    request_id = "44012ac5-a7f0-4ea2-8607-d959f407175a"
    
    print("=" * 60)
    print("🔄 并行监控演示")
    print("=" * 60)
    
    # 首先检查任务状态
    print("第一步：检查任务当前状态...")
    status_data = downloader.check_task_status(request_id)
    
    if status_data:
        response_url = downloader.extract_response_url(status_data)
        
        if response_url:
            print(f"\n第二步：开始并行监控...")
            print(f"状态URL: https://queue.fal.run/fal-ai/gemini-25-flash-image/requests/{request_id}/status")
            print(f"响应URL: {response_url}")
            
            # 使用并行监控
            final_result = downloader.monitor_task_with_parallel_access(request_id, response_url)
            
            if final_result and isinstance(final_result, dict) and 'download_url' in final_result:
                download_url = final_result['download_url']
                print(f"\n第三步：下载最终图片...")
                success = downloader.download_image(download_url, "parallel_monitored_image.png")
                if success:
                    print("🎉 并行监控流程完成!")
                else:
                    print("❌ 最终下载失败")
            else:
                print("⚠️ 并行监控未能获取到有效的下载URL")
        else:
            print("⚠️ 状态中未找到响应URL，无法进行并行监控")
    else:
        print("❌ 无法获取初始状态")


def quick_parallel_demo():
    """快速并行访问演示"""
    downloader = ImageEditorDownloader()
    request_id = "44012ac5-a7f0-4ea2-8607-d959f407175a"
    
    print("🚀 快速并行访问演示")
    print("-" * 40)
    
    # 直接进行一次并行访问
    status_data, response_data = downloader.check_both_urls_parallel(request_id)
    
    print(f"\n📊 结果汇总:")
    print(f"状态数据: {'✅ 获取成功' if status_data else '❌ 获取失败'}")
    print(f"响应数据: {'✅ 获取成功' if response_data else '❌ 获取失败'}")
    
    if status_data:
        status = status_data.get('status', 'unknown')
        print(f"任务状态: {status}")
    
    if response_data:
        print("响应数据类型:", type(response_data).__name__)
        if isinstance(response_data, dict) and 'content' in response_data:
            print("包含原始内容数据")
        elif isinstance(response_data, dict):
            print("包含结构化JSON数据")


def demo_json_response_handling():
    """演示JSON响应处理功能"""
    downloader = ImageEditorDownloader()
    
    print("=" * 60)
    print("🔍 JSON响应处理演示")
    print("=" * 60)
    
    # 模拟JSON响应数据
    sample_json = {
        "images": [
            {
                "url": "https://v3.fal.media/files/panda/aBTFgCfUNLG0AuXQ5IoCx_output.png",
                "content_type": "image/png",
                "file_name": "output.png",
                "file_size": 931121,
                "width": 1024,
                "height": 1024
            }
        ],
        "description": "好的，这是你想要的图片："
    }
    
    print("📋 模拟JSON响应数据:")
    print(json.dumps(sample_json, indent=2, ensure_ascii=False))
    
    print("\n🔍 测试URL提取功能...")
    extracted_url = downloader._extract_image_url_from_json(sample_json)
    
    if extracted_url:
        print(f"\n✅ 成功提取URL: {extracted_url}")
        
        # 可以选择是否真实下载（用户可以取消注释）
        print("\n💡 如需测试实际下载，请取消下面这行的注释：")
        print("# success = downloader.download_image(extracted_url, 'demo_extracted_image.png')")
        # success = downloader.download_image(extracted_url, 'demo_extracted_image.png')
    else:
        print("❌ URL提取失败")
    
    print("\n" + "=" * 60)
    print("JSON响应处理演示完成")
    print("=" * 60)


if __name__ == "__main__":
    # 运行主函数（包含完整流程和并行状态查询示例）
    main()
    
    print("\n" + "🔧" * 30 + " 其他演示选项 " + "🔧" * 30)
    print("如需运行其他演示，请取消下面相应行的注释：")
    print("# query_specific_task()          # 传统的任务状态查询")
    print("# demo_parallel_monitoring()     # 完整的并行监控演示") 
    print("# quick_parallel_demo()          # 快速并行访问演示")
    print("# demo_json_response_handling()  # JSON响应处理演示")
    
    # 如果只想查询特定任务状态，可以取消下面这行的注释：
    # query_specific_task()
    
    # 如果想看完整的并行监控演示，可以取消下面这行的注释：
    # demo_parallel_monitoring()
    
    # 如果想看快速并行访问演示，可以取消下面这行的注释：
    # quick_parallel_demo()
    
    # 如果想看JSON响应处理演示，可以取消下面这行的注释：
    # demo_json_response_handling()
