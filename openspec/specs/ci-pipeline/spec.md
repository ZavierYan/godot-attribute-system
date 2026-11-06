# ci-pipeline Specification

## Purpose

定义持续集成管线的对外行为：push 与 PR 自动触发构建与测试，采集覆盖率并接入 SonarCloud 质量门禁，使合并前的问题在 PR 上可见、可阻塞。

## Requirements

### Requirement: push 与 PR 自动触发 CI
对 master 的 push 以及任何针对 master 的 PR，CI SHALL 自动运行并产出该提交/PR 的通过或失败状态；PR 页面 SHALL 能看到对应的检查结果。

#### Scenario: PR 触发检查
- **WHEN** 向 master 发起 PR 或向已有 PR push 新提交
- **THEN** 自动启动一次 CI 运行，PR 检查区出现对应检查项，运行结束后呈现通过或失败

#### Scenario: push 到 master 触发检查
- **WHEN** 直接 push 提交到 master
- **THEN** 自动启动一次 CI 运行，并在该提交上留下通过或失败状态

### Requirement: 测试失败即 CI 失败
CI 中的测试步骤 SHALL 以全部 116 个 gdUnit4 纯逻辑用例通过为通过标准；任一用例失败或编译失败时，整个 CI 运行 MUST 判定为失败。

#### Scenario: 测试失败阻塞 CI
- **WHEN** 某提交导致任一测试用例失败
- **THEN** CI 运行判定为失败，该提交/PR 状态为失败

#### Scenario: 全部通过时 CI 通过
- **WHEN** 某提交编译成功且全部测试用例通过
- **THEN** CI 运行判定为通过

### Requirement: CI 无需 Godot 引擎
CI SHALL 在不安装 Godot 引擎二进制、不设置 `GODOT_BIN` 的环境下完成构建与全部测试；测试对 gitignore 的 `addons/gdUnit4/` 目录无依赖（全新 checkout 即可运行）。

#### Scenario: 全新 checkout 可运行
- **WHEN** 在仅含 git 追踪文件的全新 checkout 上运行 CI
- **THEN** 构建与测试全部成功，无需任何引擎下载步骤

### Requirement: 覆盖率采集并上报 SonarCloud
CI SHALL 在测试执行时采集 cobertura 格式覆盖率，并随 SonarCloud 扫描一并提供；扫描 SHALL 使用测试阶段产出的覆盖率报告而非空值。

#### Scenario: 覆盖率随扫描上报
- **WHEN** CI 完成测试与 SonarCloud 扫描
- **THEN** SonarCloud 项目上可见本次运行的覆盖率数据

### Requirement: SonarCloud 质量门禁纳入 CI 结果
CI SHALL 将 SonarCloud 质量门禁结果纳入最终判定：门禁失败时 CI 运行 MUST 判定为失败；PR 上 SHALL 可见门禁状态与问题概览。

#### Scenario: 门禁失败导致 CI 失败
- **WHEN** 某提交引入新问题使质量门禁不通过
- **THEN** CI 运行判定为失败，PR 上可见门禁失败状态

#### Scenario: 门禁通过时 CI 不因扫描受阻
- **WHEN** 构建测试通过且质量门禁通过
- **THEN** CI 运行判定为通过，PR 上可见门禁成功状态
