# BoatAttack 教学关卡场景底板

打开 `Scenes/Tutorial_BoatAttack.unity`，或使用 Unity 菜单 `Tools → CraftFight → BoatAttack Tutorial → Open Tutorial Scene`。

## 保留范围

场景取自 BoatAttack 岛屿东侧，与参考截图中的 FisherBay、DistantVillage 两片村落及前方 Pillar3 石柱对应。保留附近房屋、码头、海滩、山体、岩石和植被。地形裁剪范围为原场景坐标 X=5～230、Z=-185～-35，尺寸为 225×150 米；部分边缘山体保留完整网格作为背景，因此模型外包围范围会略大于地形。

移除了海水、浮标、赛道指示、赛车、演示镜头、比赛逻辑、海草粒子及区域之外的其他岛屿。海水原来的位置显示原地形，不依赖 BoatAttack Water System、Cinemachine、Addressables 或 BoatAttack 的自定义 Renderer Feature。

## 场景对象

- `BoatAttack Beach Environment`：两片村落及附近环境，已解除原预制体嵌套，方便继续精简和编辑。
- `Beach Terrain - Cropped`：独立 TerrainData 和 TerrainCollider，不引用原项目整张地形。
- `Tutorial Placement Points/PlayerSpawn`：教学玩家的建议出生位置。
- `Tutorial Placement Points/BattleEntry - Placeholder`：后续战斗入口的位置标记，没有附加传送或任务逻辑。
- `Preview Camera`：编辑和检查环境用的相机。
- `Sun`：本场景使用的方向光。
- `Sky Fill`：无阴影的辅助光，补足脱离原项目烘焙数据后的暗部。
- `BoatAttack Beach Environment/Boundary Scenery`：外围岩石、灌木、棕榈和木栅栏，模型采用现有资源，木栅栏合并为一个网格。
- `BoatAttack Beach Environment/Tutorial Activity Boundary`：沿外围闭合布置的实体碰撞边界，覆盖相邻段接缝和地形高度变化。

`TutorialVegetationSetup` 恢复植被材质所需的位置参数，并提供环境光，避免依赖原场景的烘焙光照数据。

出生点位于两片村落之间的开阔沙滩，朝向房屋和码头。移除海水后，该处保留原地形高度，Y 坐标低于原海平面属于正常情况。边界覆盖两片村落及附近海滩，外围布景与中间教学活动区保持距离；边界碰撞用于阻挡实体移动。后续接入 XR 瞬移时，应只把边界内的地面设置为可传送区域。

山坡按高度、坡度与缓变噪声混合岩石、碎石植被和苔藓地表，平缓低地保留沙滩。四个独立地形层保存在 `Generated/Terrain`，不修改原 BoatAttack 地形。补充的 `RockPlants` 贴图同样取自 BoatAttack。

可以使用 `Tools → CraftFight → BoatAttack Tutorial → Validate Current Tutorial Scene` 检查当前教学场景的模型、材质、着色器、出生点和地形碰撞，并生成俯视与出生视角截图，结果位于项目的 `Logs/BoatAttackTutorialValidation`。该检查不会重新布置或保存场景。

## 后续接入

当前交付范围是环境迁移。教学任务、工作台、练习目标、XR 玩家和完成教学后跳转到 `Assets/Scenes/SampleScene.unity` 的逻辑尚未接入。接入 XR 玩家时替换 `Preview Camera`，确保只有一套玩家相机和 AudioListener。

教学场景添加在 Build Settings 的现有战斗场景之后；迁移没有改变原有启动场景顺序。完成教学流程后，可以再将教学场景调整为启动场景。

## 资源与授权

`Source` 保存本场景需要的 BoatAttack 模型、贴图、Shader Graph 和地形层，保留资源 GUID；`Generated` 保存裁剪地形和场景材质副本。原 BoatAttack 项目中的场景、预制体和地形保持原样。

BoatAttack 来源：https://github.com/Unity-Technologies/BoatAttack 。授权说明保存在同目录 `BoatAttack-LICENSE.md`。迁入的 TIFF 原图体积较大，实际设备内存和包体以 Unity 导入及 Android 构建设置为准。
