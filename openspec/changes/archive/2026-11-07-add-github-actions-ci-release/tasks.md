# Tasks

## 1. 人工前置（与 2、3 组并行，不阻塞 workflow 文件编写）

- [x] 1.1 生成 token 配置为仓库 secret `SONAR_TOKEN`。验证：仓库 Settings → Secrets and variables → Actions 中可见（组织与项目已于 2026-11-06 导入完成：org `zavieryan`、key `ZavierYan_godot-attribute-system`，见 design D2 修正）
- [x] 1.2 （建议，首个正式 release 前）仓库根补 LICENSE 文件并提交。验证：GitHub 仓库首页显示许可证标识（2026-11-06 用户已在 GitHub 上以 MIT 添加）

## 2. ci.yml——CI 管线（capability: ci-pipeline）

- [x] 2.1 创建 `.github/workflows/ci.yml` 骨架：`push`（master）+ `pull_request`（master）触发、`concurrency` 按 ref 取消旧运行、`actions/checkout` + `actions/setup-dotnet`（.NET `8.0.x`）。验证：YAML 结构与缩进检查（本地解析通过），事件/并发声明与 design D5 一致
- [x] 2.2 构建与测试步骤：`dotnet build attribute-system.sln` + `dotnet test attribute-system.sln --no-build`。验证：在本地全新 checkout 模拟目录按相同命令序列执行，116/116 通过
- [x] 2.3 SonarCloud 扫描步骤：安装 `dotnet-sonarscanner` 与 `dotnet-coverage`，按 design D2 四步走（begin 携带 `/k:"ZavierYan_godot-attribute-system"`、`/o:"zavieryan"`、`sonar.cs.cobertura.reportsPaths`、`sonar.exclusions=reports/**`、`sonar.qualityGate.wait=true`；build；`dotnet-coverage collect -f cobertura`；end）；步骤开头校验 `SONAR_TOKEN` 非空，缺失即失败并输出配置指引。验证：逐参数与本地 SonarQube 配方及 SonarCloud 官方模板比对一致（属性名必须是 `reportsPaths`），等价命令序列已在本地容器验证过（QG 通过）
- [x] 2.4 push 分支后确认 GitHub Actions 识别到该 workflow（列表可见、无 YAML 解析错误）——PR #2 run 42246554910 成功，全部步骤绿

## 3. release.yml——发布管线（capability: release-automation）

- [x] 3.1 创建 `.github/workflows/release.yml` 骨架：`push` tags `v*` 触发、`permissions: contents: write`、测试守门步骤（与 2.2 相同命令）。验证：YAML 解析通过，触发器与权限声明与 design D5/D4 一致
- [x] 3.2 打包步骤：`git archive --format=zip` 对 tag 打包 `addons/attribute-system`，输出 `attribute-system-<tag 去 v 前缀>.zip`。验证：本地对当前 HEAD 模拟同一命令，解包检查 zip 恰含 `addons/attribute-system/` 前缀与全部追踪文件、不含 bin/obj/`.uid`
- [x] 3.3 发布步骤：`gh release create <tag> <zip> --generate-notes --latest`。验证：命令形态与 design D4 一致；真实发布在集成组验证

## 4. 集成验证（依赖 1、2、3 组）

- [x] 4.1 开 PR 验证 ci.yml：`pull_request` 事件触发、build+test 步骤全绿。验证：PR 检查区 CI 通过（PR #2 run 42246554910：success，116 用例）
- [x] 4.2 验证 SonarCloud 接入（依赖 1.1）：PR 检查含质量门禁且通过，SonarCloud 项目页可见覆盖率数据（≈89.7% 口径）。验证：门禁状态绿 + 覆盖率非空（"SonarCloud Code Analysis" check success；qualityGate.wait=true 下 end 步成功即门禁通过；日志见 Cobertura 报告被导入解析）
- [x] 4.3 验证 release.yml（依赖 3.x）：用临时 tag（如 `v0.0.0-rc1`）触发，确认 Release 页面出现、zip 名与内容正确、宿主项目解压后按 README 可 ProjectReference 编译。验证完成后删除该测试 Release 与 tag（run 42247363554 全绿；资产 `attribute-system-0.0.0-rc1.zip` 19 文件与追踪清单一致；宿主 ProjectReference 编译 0 错误——注意宿主需按惯例加 `<Compile Remove="addons/attribute-system/**/*.cs" />` 才无 CS0436 重复类型警告，README 待补此说明）

## Workflow follow-up

- PR 合入 master（squash 或按小步提交保留历史由维护者定夺）。
- 完成项目评审要求后运行 `/opsx:archive` 归档本变更。
