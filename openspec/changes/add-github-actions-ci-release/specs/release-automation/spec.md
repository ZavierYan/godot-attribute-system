# Spec Delta

## Purpose

定义 tag 驱动的自动化发布行为：push 版本 tag 即触发"测试守门 → 插件源码打包 → GitHub Release 发布"的管线，产出解压即用的插件 zip。

## ADDED Requirements

### Requirement: 版本 tag 触发发布
push 匹配 `v*` 形态的 tag SHALL 触发发布流程并在该 tag 上创建 GitHub Release；其它 push 与 PR MUST NOT 触发发布。版本号 SHALL 以 tag 为唯一来源。

#### Scenario: 推送 v1.0.0 触发发布
- **WHEN** push tag `v1.0.0`
- **THEN** 自动启动一次发布运行，结束后 GitHub Release 页面出现对应 `v1.0.0` 的 Release

#### Scenario: 普通推送不触发发布
- **WHEN** push 普通提交到 master 或发起 PR
- **THEN** 不创建任何 Release

### Requirement: 发布前测试守门
发布流程 SHALL 先完整执行测试；测试失败时 MUST 中止发布且不创建 Release。

#### Scenario: 测试失败不发布
- **WHEN** tag 指向的代码存在失败的测试用例
- **THEN** 发布运行判定为失败，该 tag 上不产生 Release

#### Scenario: 测试通过才发布
- **WHEN** tag 指向的代码全部测试通过
- **THEN** 发布流程继续执行打包与发布

### Requirement: 发布物为解压即用的插件源码 zip
每个 Release SHALL 恰好附带一个名为 `attribute-system-<tag 去掉 v 前缀>.zip` 的发布物；zip SHALL 包含 `addons/attribute-system/` 目录前缀及其下全部 git 追踪文件，解压到宿主 Godot 项目根目录即完成安装（与插件 README 的安装步骤一致）；zip MUST NOT 包含构建产物、`.uid` 文件等 git 未追踪内容。

#### Scenario: zip 布局与安装一致
- **WHEN** 用户下载发布物并解压到宿主 Godot 4 C# 项目的根目录
- **THEN** 得到 `addons/attribute-system/` 完整目录（含 csproj 与 README），按 README 的 ProjectReference 方式即可引用编译

#### Scenario: zip 内容与 git 追踪状态一致
- **WHEN** 发布物打包完成
- **THEN** zip 内容恰为 `addons/attribute-system/` 下的 git 追踪文件，不含 bin/obj、`.uid` 等未追踪文件
