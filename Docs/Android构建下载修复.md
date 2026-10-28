# Android Maven 下载修复

本次失败位置：Gradle 解析 Android 构建工具依赖。
错误特征：repo.maven.apache.org 和 plugins.gradle.org 转发请求返回 HTTP 403。
上一轮 TutorialVegetationSetup 的 C# 编译错误已通过；这次失败发生在更后的 Android 打包阶段。

项目新增 Assets/Editor/AndroidMavenRepositories.cs。Unity 生成 Android Gradle 项目后，它会在插件仓库和运行依赖仓库中优先加入：

- https://maven.aliyun.com/repository/google
- https://maven.aliyun.com/repository/public

这项修复只作用于该项目生成的 settings.gradle。脚本通过 Android 编辑器编译检查。
已用实际 GET 验证 Kotlin 1.7.10、ASM 9.2、Android Gradle Plugin 7.4.2 及其插件标记在对应镜像上返回 200。

切回 Unity 等待脚本导入完成后，后续 Build And Run 会自动应用镜像。首次构建仍需下载缺少的依赖。
appID is not configured 是另一条平台服务警告，不是这次 Maven 下载失败的原因。

实际验证日志位于 .codex-backups/android-maven-mirror-20261028/gradle-help.log。
实际验证结果：Gradle 依赖解析和 assembleRelease 均成功，112 个任务执行完成；APK 签名校验通过。安装包：Builds/Android/CraftFight-PICO.apk。尚未安装到 PICO，真机运行未验证。

