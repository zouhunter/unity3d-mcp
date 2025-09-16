import requests
import os
from urllib.parse import urlparse

def download_pyramid_texture():
    """下载金字塔贴图"""
    
    # 金字塔石材贴图URL（免费资源）
    texture_urls = [
        {
            "url": "https://images.unsplash.com/photo-1539650116574-75c0c6d73d0e?ixlib=rb-4.0.3&w=1024&h=1024&fit=crop",
            "filename": "pyramid_stone_texture.jpg",
            "description": "石材纹理"
        },
        {
            "url": "https://images.unsplash.com/photo-1584464491033-06628f3a6b7b?ixlib=rb-4.0.3&w=1024&h=1024&fit=crop",
            "filename": "pyramid_sandstone_texture.jpg", 
            "description": "砂石纹理"
        }
    ]
    
    # 确保Pics目录存在
    pics_dir = "Assets/Pics"
    os.makedirs(pics_dir, exist_ok=True)
    
    downloaded_files = []
    
    for texture_info in texture_urls:
        try:
            print(f"下载 {texture_info['description']}: {texture_info['url']}")
            
            # 发送HTTP请求
            headers = {
                'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36'
            }
            response = requests.get(texture_info['url'], headers=headers, stream=True)
            response.raise_for_status()
            
            # 保存文件
            file_path = os.path.join(pics_dir, texture_info['filename'])
            with open(file_path, 'wb') as f:
                for chunk in response.iter_content(chunk_size=8192):
                    if chunk:
                        f.write(chunk)
            
            print(f"✓ 已下载: {file_path}")
            downloaded_files.append(file_path)
            
        except Exception as e:
            print(f"✗ 下载失败 {texture_info['url']}: {str(e)}")
            
            # 尝试备用URL
            try:
                backup_url = f"https://picsum.photos/1024/1024?random={len(downloaded_files)}"
                print(f"尝试备用贴图: {backup_url}")
                
                response = requests.get(backup_url, headers=headers)
                response.raise_for_status()
                
                file_path = os.path.join(pics_dir, f"pyramid_texture_{len(downloaded_files)}.jpg")
                with open(file_path, 'wb') as f:
                    f.write(response.content)
                
                print(f"✓ 备用贴图已下载: {file_path}")
                downloaded_files.append(file_path)
                
            except Exception as backup_e:
                print(f"✗ 备用下载也失败: {str(backup_e)}")
    
    if downloaded_files:
        print(f"\n成功下载了 {len(downloaded_files)} 个贴图文件:")
        for file_path in downloaded_files:
            print(f"  - {file_path}")
        return downloaded_files[0]  # 返回第一个成功下载的文件
    else:
        print("\n没有成功下载任何贴图文件")
        return None

if __name__ == "__main__":
    download_pyramid_texture()



