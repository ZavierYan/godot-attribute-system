# Proposal

## Why

仓库根没有 README，GitHub 首页空白，完整用户手册埋在 `addons/attribute-system/` 里；开发操作知识（运行测试、覆盖率采集、Sonar、发布流程）散落在 workflow 文件中，没有任何可读入口。发布管线刚在 add-github-actions-ci-release 中建成，正是固化文档结构的时机。

## What Changes

- README.md 从 `addons/attribute-system/` 完整迁移到仓库根（GitHub 首页展示）：
  - 移除末尾「运行测试」小节（迁入开发文档），补一行指向 `docs/DEVELOPMENT.md` 的链接
  - 调整对 `tests/` 的路径表述（迁移后 README 本身就在仓库根）
- 删除 `addons/attribute-system/README.md`——手册单一事实来源在根目录，插件目录不留 stub
- `.github/workflows/release.yml` 打包命令的 pathspec 补入 `README.md`：发布 zip 顶层携带用户手册，zip 用户安装文档不丢失（zip 内布局：顶层 `README.md` + `addons/attribute-system/`）
- 新增 `docs/DEVELOPMENT.md` 开发文档，承载：运行测试（gdUnit4 进程内、无需 Godot）、覆盖率采集、SonarCloud（CI）与本地 SonarQube 扫描、发布流程（`v*` tag 触发的完整链路）、gdUnit4 测试必须在 Godot 主程序集的约束。不提及 LOCAL.md（该文件仅用于向 agent 植入本地环境信息，不是项目文档）

## Capabilities

### New Capabilities

（无——文档文件本身不立规格，避免为凑数发明需求）

### Modified Capabilities

- `release-automation`：发布物打包需求更新——zip 在 `addons/attribute-system/` 前缀之外 SHALL 于顶层携带根 README.md 用户手册；插件目录内不再含 README，规格场景中「含 csproj 与 README」的表述随之更新。注意：该能力的需求文本目前存在于未归档变更 add-github-actions-ci-release 的 delta 中，本变更的 MODIFIED delta 须在其归档（specs 同步）之后归档。

## Impact

- `.github/workflows/release.yml`：打包命令一行（pathspec 追加 `README.md`）
- `addons/attribute-system/README.md`：删除（git mv 至根）
- `README.md`（根，新增）：迁移 + 两处内容微调
- `docs/DEVELOPMENT.md`：新增
- 无代码、无 API、无依赖、无测试行为变化；CI（ci.yml）不受影响
