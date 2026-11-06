# Design

## Context

- 工程结构（见 proposal Why）：根 `attribute-system.csproj`（Godot.NET.Sdk 4.7.2，测试编入主程序集）+ `addons/attribute-system/AttributeSystem.csproj`（纯 C# classlib）。
- 已实验验证（探索阶段，2026-11-06）：模拟全新 checkout——只保留 git 追踪文件、去掉被 gitignore 的 `addons/gdUnit4/`——`dotnet test` 直接通过 116/116（466ms），无需 Godot 引擎、无需 `GODOT_BIN`。主 csproj 的 `<Compile Include="addons/gdUnit4/src/dotnet/*.cs" />` 在 addon 缺席时空匹配，无代码引用该桥接类，编译不受影响。
- 本地已跑通的 Sonar 扫描配方（复验于 2026-11-02 csproj 拆分后）：`dotnet sonarscanner begin → dotnet build → dotnet-coverage collect -f cobertura → sonarscanner end`；覆盖率属性名必须是 `sonar.cs.cobertura.reportsPaths`（旧名会被静默忽略）；`sonar.exclusions=reports/**` 防止 gdUnit4 HTML 报告被当 JS 分析；无需 `SonarQubeTestProject=false`（主程序集因含 Test.Sdk 自然判为 TEST，插件工程为 PROD）。
- 真实覆盖率 89.7%（诚实口径），本地质量门禁通过；Sonar way 默认门禁预期同样通过。
- 分发模型：源码 zip（`AttributeValueType` 别名编译期展开，DLL/NuGet 分发不可行），README 安装步骤 = 复制目录 + ProjectReference。

## Goals / Non-Goals

**Goals:**

- 两个 workflow 文件达成全部规格行为（`specs/ci-pipeline`、`specs/release-automation`），不改任何插件源码与测试代码。
- 只用官方 first-party Actions（checkout、setup-dotnet），发布用 runner 预装 gh CLI，Sonar 用 dotnet tool——最小化第三方 Action 供应链面。
- 复用本地验证过的扫描配方，不在 CI 上重新发明阈值或属性名。

**Non-Goals:**

- 不做 Windows/macOS runner 矩阵、`dotnet format` 检查、分支保护规则、NuGet lock 文件缓存（后续可独立演进）。
- 不在 release zip 中注入版本号到 csproj（源码分发，版本唯一来源是 tag）。
- 不迁移本地 SonarQube 容器的历史数据；SonarCloud 首扫即为基线。

## Decisions

### D1：runner = ubuntu-latest，不装 Godot 引擎
测试是纯逻辑 gdUnit4 用例，VSTest 适配器进程内执行；实验证明全新 checkout 即可跑通。备选方案（windows runner 贴近开发机；godot headless + GdUnitCmdTool 贴近编辑器面板路径）均被否：前者更慢更贵且无平台相关代码可验证，后者需要安装被 gitignore 的 gdUnit4 addon 和引擎二进制，只为跑不依赖它们的测试。

### D2：SonarCloud 接入用 dotnet CLI 工具链，不用第三方 scan action
`dotnet tool install dotnet-sonarscanner / dotnet-coverage` 后按本地配方四步走。备选 `sonarsource/sonarqube-scan-action` 对 .NET 的覆盖率绑定支持不佳，且引入第三方 Action。

2026-11-06 对照 SonarCloud 官方模板（temp/donet.yml）修正：GitHub 导入项目的 key 由 SonarCloud 生成为 `ZavierYan_godot-attribute-system`（非本地容器的 `attribute-system`），组织 `zavieryan`；两者非敏感，直接硬编码进 workflow，仓库只需配 `SONAR_TOKEN` 一个 secret（不再需要 `SONAR_ORG` 变量）。begin 额外携带 `/d:sonar.qualityGate.wait=true`——scanner 默认不等待门禁结果，缺它则门禁失败时 CI 仍绿，违反 ci-pipeline 规格。样例的其余工程实践一并采纳：Actions 按完整 SHA 钉版（checkout v4.3.1、setup-java v4.8.0、cache v4.3.0）、JDK 21（scanner 为 Java 引擎，不依赖 runner 镜像预装版本）、`fetch-depth: 0`（blame/新代码检测需完整历史）、`~/.sonar/cache` 缓存、PR 限定 `types: [opened, synchronize, reopened]`。

刻意保留的偏离：ubuntu runner（D1）、离散步骤（失败定位到步）、`dotnet-coverage` 覆盖率采集（规格要求）。扫描步骤开头校验 `SONAR_TOKEN` 非空，缺失时报错并输出一次性配置指引（fail loudly，优于静默跳过导致覆盖率规格落空）。

### D3：release 打包用 `git archive`
`git archive --format=zip <tag> -- addons/attribute-system` 天然满足规格：内容恰为 git 追踪文件、自带 `addons/attribute-system/` 路径前缀、不含 bin/obj 与 `.uid`。zip 文件名 `attribute-system-<tag 去 v>.zip`。备选 Compress-Archive 基于 checkout 目录打包，需手动排除未追踪文件，可靠性差。

### D4：发布用 `gh release create --generate-notes`
runner 预装 gh，`GITHUB_TOKEN` 内建即用（workflow 声明 `permissions: contents: write`）。备选 `softprops/action-gh-release` 为第三方 Action，否。

### D5：触发与并发
- `ci.yml`：`push`（master）+ `pull_request`（master），配 `concurrency` 按 ref 取消进行中的旧运行。
- `release.yml`：`push` tags `v*`。
- .NET 版本钉 `8.0.x`：csproj 的 android 条件 TFM（net9.0）只在宿主导出 Android 时由 Godot 传入 `GodotTargetPlatform` 激活，CI 不触发。

## Risks / Trade-offs

- [SonarCloud token 未就绪时 ci.yml 必失败] → 扫描步骤 fail-fast 并打印配置指引；人工前置步骤在 tasks.md 明确列出，不阻塞 workflow 文件先行合入。
- [Sonar way 门禁阈值与本地 QG 不完全一致，首扫可能报新问题] → 门禁失败属规格预期行为（CI 红），在 SonarCloud 控制台按需调阈值即可，仓库无需改动。
- [Linux CI 与本地 Windows 环境差异] → 纯托管代码，`.gitattributes` 已统一 LF；实验虽在 Windows 执行，但产物为同一程序集。若出现 Linux 特有问题会在 CI 首跑即暴露。
- [setup-dotnet 的 NuGet 缓存需要 packages.lock.json，仓库未启用 lock 文件] → 放弃缓存；restore 实测 ~1s，收益不抵 lock 文件维护成本。

## Migration Plan

实现于工作分支 `openspec/add-github-actions-ci-release`（本文档所在分支），合入 master 后：① 配置 `SONAR_TOKEN` secret → ② push 一次提交验证 ci.yml 全绿 → ③ 打首个 `v*` tag 验证 release.yml。回滚 = 删除两个 workflow 文件，无数据迁移。

## Open Questions

（无——组织名等一次性配置项通过仓库变量注入，不构成设计分叉。）
