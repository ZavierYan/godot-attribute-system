# Tasks

## 1. 开发文档

- [x] 1.1 新建 `docs/DEVELOPMENT.md`：运行测试（`dotnet test attribute-system.sln`，gdUnit4 进程内、无需 GODOT_BIN）、覆盖率采集（dotnet-coverage 命令原文）、SonarCloud（project key、org、SONAR_TOKEN secret、quality gate 等待）、本地 SonarQube 通用容器流程（sonarscanner begin/end、cobertura reportsPaths、排除 reports/**）、发布链路（push `v*` tag → 测试守门 → git archive 打包 → GitHub Release）、gdUnit4 测试必须在 Godot 主程序集的约束。验证：测试与覆盖率命令按文档原文在本地跑通；其余小节与 ci.yml / release.yml 逐字核对一致；全文不提及 LOCAL.md

## 2. README 迁移

- [x] 2.1 `git mv addons/attribute-system/README.md README.md`，内容微调：删除末尾「运行测试」小节、补一行指向 `docs/DEVELOPMENT.md` 的链接、`tests/` 路径表述改为根视角。验证：根 README 存在且插件目录无 README；`git status` 显示 rename；全文无「运行测试」小节残留、无指向已删小节的交叉引用；链接目标文件存在
- [x] 2.2 复查迁移后 README 的安装步骤表述与 zip 布局一致性（用户手册描述的复制路径 `addons/attribute-system/` 不变，无需改安装步骤本身）。验证：通读安装一节，三步操作对 zip 用户与仓库 clone 用户均成立

## 3. 发布打包

- [x] 3.1 `.github/workflows/release.yml` 打包命令 pathspec 追加 `README.md`。验证：本地执行 `git archive --format=zip HEAD -- addons/attribute-system README.md` 并列出条目，确认顶层有 `README.md`、`addons/attribute-system/` 前缀完整、无 bin/obj 与 `.uid` 等未追踪文件

## 4. 集成核对

- [x] 4.1 对照本变更规格的两个场景逐条核对 zip 布局与内容（用 3.1 的本地 archive 产物）。验证：两个场景 THEN 全部成立
- [x] 4.2 `dotnet build attribute-system.sln` + `dotnet test attribute-system.sln` 全绿。验证：两条命令退出码 0

## Workflow follow-up

- 提交惯例：每任务一 commit，conventional commits 中文（如 `docs: 新增 DEVELOPMENT.md 开发文档`）。
- 归档顺序：先归档 add-github-actions-ci-release（`release-automation` 同步进主规格），再归档本变更（design 决策 4）。
- 下个 `v*` tag 实际发布时，按规格场景核对线上 zip 布局。
