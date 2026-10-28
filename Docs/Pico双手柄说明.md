# PICO 头显和双手柄跟随

本次修改的项目：`D:\unityfiles\craft\CraftFight`。

## 已接入

- PICO 模式下，头显控制主摄像机的位置、旋转。
- 左右手柄分别控制 Steve 的左右手臂；手腕物品栏和手持物品一起跟随。
- PICO 模式下，不再读取 Q/E 抬手，也不再由鼠标改变视角。
- 没有启用 XR 设备时，电脑上的 Q/E、滚轮和鼠标视角仍按原逻辑工作。
- Android 的 Active Input Handling 从 Both 改为 Input Manager (Old)。手柄姿态通过 Unity XR InputDevices 接口读取。

## 现在如何测试

1. 先保存你当前 Unity 中的工作，然后关闭并重新打开该项目，让输入设置生效。
2. 打开 `Assets/Scenes/MainScene.unity`，等待脚本和资源导入完成。
3. 查看 Console 是否出现新的红色错误。
4. 可以运行 `Tools > CraftFight > Validate PICO hand tracking`。它只检查编辑器中的绑定和姿态计算，不代表真实手柄已经验证。
5. 将 PICO 接到电脑并允许 USB 调试，在 Android Build Settings 中选中设备，然后 Build And Run。
6. 戴上头显，分别移动、旋转左右手柄，确认只影响对应的手，手腕物品栏也一起跟随；转头、侧身、蹲下时检查摄像机跟随。

## 范围

本次只接入头显和左右手柄的位置与旋转。摇杆移动、扳机攻击、握把抓取、物品栏切换、丢弃和村民交易的手柄按键尚未分配；这些仍需要后续适配。

Steve 的手臂是从肩膀到手腕的一整块模型，本次让手腕对齐手柄，没有添加肘关节或逆向运动学。控制器朝向与模型朝向的修正值在玩家的 `Pico Player Tracking` 组件中，通过 `Left/Right Arm Rotation Offset` 调整；当前修正值尚未在真机上确认。

验证区分：代码编译、合成姿态计算、实际场景导入和 PICO 真机体验是不同层次。真机画面和跟随手感必须戴头显检查。

## 修改文件

- `Assets/Scripts/PicoPlayerTracking.cs`：共享坐标空间、头显跟随和独立双手跟随。
- `Assets/Scripts/PlayerHandInventory.cs`：PICO 模式跳过 Q/E 姿态写入。
- `Assets/Scripts/SteveFirstPersonController.cs`：PICO 模式跳过鼠标视角和桌面死亡摄像机跳转。
- `Assets/Prefabs/models/vr_steve.prefab`：绑定摄像机、左右手臂、手腕锚点。
- `Assets/Editor/PicoTrackingValidation.cs`：提供编辑器绑定和姿态检查。
- `ProjectSettings/ProjectSettings.asset`：Active Input Handling 为 Old。

原文件备份：`.codex-backups/pico-tracking-20261028-before/`。
