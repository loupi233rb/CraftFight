# 怪物 Prefab

- `Zombie.prefab`：僵尸，近战攻击。
- `Skeleton.prefab`：骷髅，弓箭攻击。

两个 Prefab 均包含模型、动画控制器、寻路、碰撞、20 点生命值、受击与死亡处理，以及对应的怪物音效配置。

## 放入场景

将 Prefab 拖入场景，放在已烘焙的 NavMesh 地面上。场景中需要有带 `SteveHealth` 组件的玩家。进入运行后，怪物会自动查找该玩家。

## 代码生成

在 NavMesh 上的生成点实例化 Prefab，并显式指定玩家，适用于后续的波次系统：

```csharp
GameObject monster = Instantiate(monsterPrefab, spawnPosition, Quaternion.identity);
monster.GetComponent<MonsterChaseTarget>().Initialize(playerTransform);
```

`monsterPrefab` 可指定僵尸或骷髅 Prefab；`spawnPosition` 应位于已烘焙的 NavMesh 上。Prefab 的 `Target` 留空，避免绑定某个场景中的玩家。

## 替换音效

在 Prefab 根物体的 `MonsterAudio` 组件中打开 `Profile`，替换音效数组中的 AudioClip。默认配置为 `Assets/Audio/Profiles/ZombieAudio.asset` 和 `SkeletonAudio.asset`，详情见 `Assets/Audio/README.zh-CN.md`。

## 后续战斗流程

用户计划：合成武器 → 第一波战斗 → 强化或合成武器 → 下一波战斗，且每波怪物总数增加。目前合成场景尚未制作；本次先准备可复用的怪物 Prefab，具体波数、怪物比例和数量增长方式待后续确定。
