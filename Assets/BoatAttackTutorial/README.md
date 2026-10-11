# BoatAttack 教学关卡场景底板

打开 `Scenes/Tutorial_BoatAttack.unity`，或使用 Unity 菜单 `Tools → CraftFight → BoatAttack Tutorial → Open Tutorial Scene`。

## 保留范围

场景取自 BoatAttack 岛屿东侧，与参考截图中的 FisherBay、DistantVillage 两片村落及前方 Pillar3 石柱对应。保留附近房屋、码头、海滩、山体、岩石和植被。地形裁剪范围为原场景坐标 X=5～230、Z=-185～-35，尺寸为 225×150 米；部分边缘山体保留完整网格作为背景，因此模型外包围范围会略大于地形。

移除了浮标、赛道指示、赛车、演示镜头、比赛逻辑、海草粒子及区域之外的其他岛屿。海面已恢复，采用 BoatAttack Water System 的原始水面网格、贴图、颜色配置和波浪着色器，并由本项目的 `TutorialOcean` 负责场景内的渲染。不依赖 Cinemachine、Addressables、船只浮力或 BoatAttack 的自定义 Renderer Feature。

## 场景对象

- `BoatAttack Beach Environment`：两片村落及附近环境，已解除原预制体嵌套，方便继续精简和编辑。
- `Beach Terrain - Cropped`：独立 TerrainData 和 TerrainCollider，不引用原项目整张地形。
- `Tutorial Placement Points/PlayerSpawn`：教学玩家的建议出生位置。
- `Tutorial Placement Points/BattleEntry - Placeholder`：后续战斗入口的位置标记，没有附加传送或任务逻辑。
- `Preview Camera`：编辑和检查环境用的相机。
- `Sun`：本场景使用的方向光。
- `Sky Fill`：无阴影的辅助光，补足脱离原项目烘焙数据后的暗部。
- `BoatAttack Ocean`：海面，可通过禁用该对象暂时隐藏；平均海平面为 Y=-0.25，波幅调整为较平缓的 0.3 米。保留波浪、细节法线、岸边泡沫、深浅水颜色和 Game 相机的折射。
- `Submerged Seafloor Extension`：裁剪地形之外逐渐下沉的沙质海床，缓解水下地形截断形成的矩形色块；不添加地面碰撞或瞬移区域。
- `BoatAttack Beach Environment/Boundary Scenery`：外围岩石、灌木、棕榈和木栅栏，模型采用现有资源，木栅栏合并为一个网格。
- `BoatAttack Beach Environment/Tutorial Activity Boundary`：沿外围闭合布置的实体碰撞边界，覆盖相邻段接缝和地形高度变化。
- `BoatAttack Beach Environment/Shoreline Activity Boundary`：根据沙滩、岩石和码头表面补充的近岸碰撞边界，限制从岸边走入海中。

`TutorialVegetationSetup` 恢复植被材质所需的位置参数，并提供环境光，避免依赖原场景的烘焙光照数据。

出生点和战斗入口标记已调整到高于海平面的干燥沙滩，朝向房屋和码头。此前布置在海平面以下的部分边界装饰已隐藏，水中的栅栏段已移除；山侧的布景和原外围碰撞保留。后续接入 XR 瞬移时，应只把岸边边界内的干燥地面和合适的码头表面设置为可传送区域，海面不作为落点。

`TutorialOcean` 只为本场景的游戏相机启用深度和不透明颜色拷贝，使用预先生成的海床高度图和静态环境反射，不修改战斗场景的全局渲染配置。为适配 URP 14，水面着色器改用支持立体渲染的屏幕纹理采样，并修正裁剪后海床坐标；不引入原演示项目的平面反射、船只尾流和水下焦散通道。Scene 视图使用简化的沙质海床预览，完整折射以 Game 视图为准。修改地形高度后，需要同步更新 `Generated/Water/Tutorial Seafloor Height.asset`；当前高度图对应本次场景地形。

山坡按高度、坡度与缓变噪声混合岩石、碎石植被和苔藓地表，平缓低地保留沙滩。四个独立地形层保存在 `Generated/Terrain`，不修改原 BoatAttack 地形。补充的 `RockPlants` 贴图同样取自 BoatAttack。

可以使用 `Tools → CraftFight → BoatAttack Tutorial → Validate Current Tutorial Scene` 检查当前教学场景的模型、材质、着色器、出生点和地形碰撞，并生成俯视与出生视角截图，结果位于项目的 `Logs/BoatAttackTutorialValidation`。该检查不会重新布置或保存场景。

海面迁移的截图和检查记录在 `Logs/BoatAttackTutorialOcean`：已确认运行模式下波浪变化、水面着色器无错误、出生点至入口的 21 个干燥地面与角色空间采样通过，以及 454 段近岸碰撞的横穿射线被阻挡。未进行 PICO 头显或 Android 包的画面、帧率测试。运行检查中还出现原迁移房屋负缩放 BoxCollider 的警告，这部分房屋碰撞尚未调整。

## 后续接入

当前交付范围是环境迁移。教学任务、工作台、练习目标、XR 玩家和完成教学后跳转到 `Assets/Scenes/SampleScene.unity` 的逻辑尚未接入。接入 XR 玩家时替换 `Preview Camera`，确保只有一套玩家相机和 AudioListener。

教学场景添加在 Build Settings 的现有战斗场景之后；迁移没有改变原有启动场景顺序。完成教学流程后，可以再将教学场景调整为启动场景。

## 资源与授权

`Source` 保存本场景需要的 BoatAttack 模型、贴图、Shader Graph 和地形层，保留资源 GUID；`Water` 保存选取的原 BoatAttack Water System 资源；`Generated` 保存裁剪地形、场景材质副本及水面配套资源。水面数据类型中的 `float2` 替换为 `Vector2`，避免为视觉海面增加数学库依赖。原 BoatAttack 项目中的场景、预制体和地形保持原样。

BoatAttack 来源：https://github.com/Unity-Technologies/BoatAttack 。授权说明保存在同目录 `BoatAttack-LICENSE.md`。迁入的 TIFF 原图体积较大，实际设备内存和包体以 Unity 导入及 Android 构建设置为准。
