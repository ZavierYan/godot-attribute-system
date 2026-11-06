# Spec Delta

## MODIFIED Requirements

### Requirement: 发布物为解压即用的插件源码 zip
每个 Release SHALL 恰好附带一个名为 `attribute-system-<tag 去掉 v 前缀>.zip` 的发布物；zip SHALL 包含 `addons/attribute-system/` 目录前缀及其下全部 git 追踪文件，并 SHALL 在 zip 顶层包含仓库根的 `README.md` 用户手册（插件目录内不含 README，手册单一来源在仓库根），解压后按手册的安装步骤即可完成安装（与根 README 的安装步骤一致）；zip MUST NOT 包含构建产物、`.uid` 文件等 git 未追踪内容。

#### Scenario: zip 布局与安装一致
- **WHEN** 用户下载发布物并解压
- **THEN** 得到顶层的 `README.md` 用户手册与 `addons/attribute-system/` 完整目录（含 csproj，不含 README），把插件目录复制进宿主 Godot 4 C# 项目后，按手册的 ProjectReference 方式即可引用编译

#### Scenario: zip 内容与 git 追踪状态一致
- **WHEN** 发布物打包完成
- **THEN** zip 内容恰为 `addons/attribute-system/` 下的 git 追踪文件加上仓库根的 `README.md`，不含 bin/obj、`.uid` 等未追踪文件
