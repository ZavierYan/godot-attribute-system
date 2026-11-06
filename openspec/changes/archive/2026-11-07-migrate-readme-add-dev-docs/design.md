# Design

## Context

现状与动机见 proposal.md。约束事实：发布 zip 由 `git archive --format=zip <tag> -- addons/attribute-system` 生成（pathspec 保留 `addons/attribute-system/` 前缀）；`release-automation` 能力的需求文本目前只存在于未归档变更 add-github-actions-ci-release 的 delta 中；开发操作知识目前只能从 ci.yml / release.yml 反推。

## Goals / Non-Goals

**Goals:**

- 手册单一事实来源：根 README，GitHub 首页即手册
- 发布 zip 不丢失安装文档
- 开发操作知识（测试/覆盖率/Sonar/发布/gdUnit4 约束）有单一可读入口

**Non-Goals:**

- 不建文档站、不写 CONTRIBUTING.md、不改 ci.yml 的任何逻辑
- 不为文档文件本身立规格（proposal 已说明）
- DEVELOPMENT.md 不含机器特定信息、不提及 LOCAL.md（仅向 agent 植入本地环境）

## Decisions

1. **完整迁移，插件目录不留 stub**（用户拍板的方案 A）。备选：插件内留一行指回仓库的 stub——已否决，zip 顶层直接放完整手册优于任何绕路；双份完整README 会漂移，也否决。
2. **zip 布局：pathspec 追加 `README.md`**。`git archive` 多路径打包得到「顶层 README.md + addons/attribute-system/」，手册比现状（埋在插件目录内）更醒目；插件目录内容保持纯净代码。
3. **用 `git mv` 迁移**，保留文件历史。
4. **归档顺序约束**：add-github-actions-ci-release 先归档（`release-automation` 同步进主规格），本变更后归档——否则本变更的 MODIFIED delta 在主规格中无锚点。
5. **DEVELOPMENT.md 内容边界**：只写仓库通用事实——运行测试、覆盖率采集、SonarCloud（CI 所需 secret 与参数）、本地 SonarQube 通用容器流程、发布链路（tag → 守门 → 打包 → Release）、gdUnit4 测试必须在 Godot 主程序集的约束（历史上拆分 csproj 导致测试发现失效后回退，需防重蹈）。

## Risks / Trade-offs

- [CI 变更晚于本变更归档 → MODIFIED 无锚点] → 归档顺序写入 tasks 并在归档时检查
- [极老 release zip 的用户按旧路径找插件内 README → 落空] → 仅影响历史下载版本，各自 tag 的 zip 内容不变，可接受
- [文档描述与 workflow 日后漂移] → 发布/测试流程简单稳定，DEVELOPMENT.md 以命令原文呈现，漂移风险低

## Migration Plan

纯文档 + 一行 workflow 改动，PR 合并即生效，无回滚需求（revert 即回滚）；下个 `v*` tag 发布时按规格场景核对 zip 布局。

## Open Questions

（无）
