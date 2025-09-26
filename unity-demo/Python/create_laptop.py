
import bpy
import bmesh
from mathutils import Vector

# 清除默认场景
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)

# 创建笔记本电脑底座
bpy.ops.mesh.primitive_cube_add(size=2, location=(0, 0, 0))
laptop_base = bpy.context.active_object
laptop_base.name = "LaptopBase"
laptop_base.scale = (0.35, 0.02, 0.25)

# 创建屏幕
bpy.ops.mesh.primitive_cube_add(size=2, location=(0, 0.15, 0))
laptop_screen = bpy.context.active_object
laptop_screen.name = "LaptopScreen"
laptop_screen.scale = (0.35, 0.02, 0.25)

# 创建屏幕面板
bpy.ops.mesh.primitive_plane_add(size=2, location=(0, 0.15, 0.26))
screen_panel = bpy.context.active_object
screen_panel.name = "ScreenPanel"
screen_panel.scale = (0.8, 0.5, 1)

# 创建键盘
bpy.ops.mesh.primitive_cube_add(size=2, location=(0, 0.01, 0))
keyboard = bpy.context.active_object
keyboard.name = "Keyboard"
keyboard.scale = (0.9, 0.1, 0.7)
keyboard.parent = laptop_base

# 创建触摸板
bpy.ops.mesh.primitive_cube_add(size=2, location=(0, 0.01, -0.05))
touchpad = bpy.context.active_object
touchpad.name = "Touchpad"
touchpad.scale = (0.15, 0.01, 0.1)
touchpad.parent = laptop_base

print("笔记本电脑模型创建完成！")
