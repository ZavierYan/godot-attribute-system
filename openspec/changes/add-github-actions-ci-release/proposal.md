# Proposal

## Why

仓库托管在 GitHub（ZavierYan/godot-attribute-system）但没有任何 CI：测试（116 个 gdUnit4 纯逻辑用例）与质量扫描只在本地跑，PR 合并前无法自动验证；插件目前没有可下载的发布物，用户只能手工复制 `addons/attribute-system/` 目录。

## What Changes

- 新增 `.github/workflows/ci.yml`：push 到 master 与 PR 触发，ubuntu runner 上执行 `dotnet build` + `dotnet test` + 覆盖率采集（dotnet-coverage / cobertura），并接入 SonarCloud 质量门禁（复用本地已验证的 `sonarscanner begin → build → collect → end` 配方，`SONAR_TOKEN` 来自 GitHub secret）。
- 新增 `.github/workflows/release.yml`：push `v*` tag 触发，先跑测试守门，再把 `addons/attribute-system/` 打成源码 zip（内含目录前缀，解压即用），创建 GitHub Release 并附上 `attribute-system-<tag>.zip`。
- CI 不安装 Godot 引擎、不设置 `GODOT_BIN`：gdUnit4.api 5.x 对纯逻辑测试进程内运行，已在模拟全新 checkout 的环境中验证 116/116 通过（含被 gitignore 的 gdUnit4 addon 缺席场景）。
- 版本号唯一来源为 git tag；不引入额外的版本清单文件。

## Capabilities

### New Capabilities

- `ci-pipeline`: push/PR 触发的持续集成行为——构建、测试、覆盖率与 SonarCloud 质量门禁，PR 上可见门禁结果。
- `release-automation`: tag 触发的发布行为——测试守门、插件源码 zip 打包、GitHub Release 发布。

### Modified Capabilities

（无——现有 4 个 spec 均为运行期领域行为，不受构建管线影响。）

## Impact

- 新增文件：`.github/workflows/ci.yml`、`.github/workflows/release.yml`。
- 外部依赖：GitHub Actions（ubuntu-latest）、NuGet（Godot.NET.Sdk 4.7.2、gdUnit4.api 5.1.0-rc6、NCalc 7.1.0）、SonarCloud（需仓库配置 `SONAR_TOKEN` secret，项目 key 沿用 `attribute-system`）。
- 不改动任何插件源码与测试代码；`dotnet build` 对全新 checkout 已验证可行（gitignore 掉的 gdUnit4 桥接目录缺席不阻塞编译）。
- 前置条件（工作流之外，需人工完成）：SonarCloud 建组织/项目并生成 token；建议首次 release 前补 LICENSE。
- 后续可选（本变更不做）：Windows runner 矩阵、`dotnet format` 检查、分支保护规则。
