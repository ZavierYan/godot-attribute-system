# 本地信息
`LOCAL.md`用于补充本地信息，不由git管理。该文件可能不存在，如果存在，确保知晓其内容。

# 工作流程
- 修改任何文件之前，先切换到对应工作分支
- 每完成一个任务，充填完checkbox后，commit
- opsx:archive后，push并提交pr

# 开发文档
用户侧功能与用法见 `README.md`（随发布 zip 分发的用户手册）；开发向文档在 `docs/`：

- [docs/architecture.md](docs/architecture.md) — 系统分层、组件职责与关键机制
- [docs/structure.md](docs/structure.md) — 仓库布局、双工程结构与分发边界
- [docs/installation.md](docs/installation.md) — 宿主项目接入步骤、值类型切换与常见问题
- [docs/decisions.md](docs/decisions.md) — 技术决策与被否方案
- [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md) — 构建、测试、覆盖率、质量扫描、发布