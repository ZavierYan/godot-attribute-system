# 项目结构

仓库布局、双工程结构与分发边界。

## 仓库布局

```text
├── addons/
│   ├── attribute-system/           # 插件本体（随发布 zip 分发的唯一内容物）
│   │   ├── AttributeSystem.csproj  # 插件工程：纯 C# 逻辑库（Microsoft.NET.Sdk，零 Godot API）
│   │   └── *.cs                    # 插件源码，逐文件职责见下表
│   └── gdUnit4/                    # gdUnit4 运行时与编辑器桥接（仅仓库，不随插件分发）
├── tests/                          # gdUnit4 纯逻辑测试（编译进 Godot 主程序集，不随插件分发）
├── docs/                           # 开发文档（本目录）
├── openspec/
│   ├── changes/                    # OpenSpec 变更工作流：进行中的变更提案与任务
│   └── specs/                      # 已归档的能力规格
├── .github/workflows/              # ci.yml（构建+测试+SonarCloud）、release.yml（tag 触发发布）
├── attribute-system.csproj         # Godot 主工程（Godot.NET.Sdk/4.7.2）
├── attribute-system.sln
├── Directory.Build.targets         # 所有 csproj 通配后统一剔除 **/bin/**、**/obj/**
├── project.godot
├── README.md                       # 用户手册（随发布 zip 分发）
└── AGENTS.md                       # agent 协作约定（工作分支 / 提交纪律）

bin/、obj/、.godot/、TestResults/、reports/、temp/ 等为本地产物，不入库。
```

## 双工程结构

| 工程 | SDK | 角色 |
|---|---|---|
| `addons/attribute-system/AttributeSystem.csproj` | Microsoft.NET.Sdk | 插件逻辑库；宿主以 ProjectReference 引用，NCalc 依赖随引用传递 |
| `attribute-system.csproj`（根） | Godot.NET.Sdk/4.7.2 | Godot 主工程：ProjectReference 引插件工程；tests/ 源码经隐式通配编入本程序集；`Compile Remove="addons/**/*.cs"` 防插件源码被通配重复编译（CS0436），但重新包含 gdUnit4 桥接类 |

测试源码物理在 `tests/`、逻辑上属主程序集：gdUnit4 编辑器面板的 C# 测试发现只扫描主程序集；插件 csproj 经 `InternalsVisibleTo("attribute-system")` 让测试可见 internal 成员。约束详情见 [DEVELOPMENT.md](DEVELOPMENT.md)。

两个 csproj 均含 Android 条件框架（`GodotTargetPlatform == android` 时 net9.0，否则 net8.0），宿主导出 Android 时保持一致。

## 插件源码逐文件职责

| 文件 | 职责 |
|---|---|
| AttributeValueType.cs | 值类型别名（全插件唯一声明点，切换数值类型只改这一行） |
| AttributeSpec.cs / AttributeSpecLibrary.cs | 属性定义与注册中心 |
| AttributeOwnerBuilder.cs | 构建器：声明收集、构建期校验、owner 装配 |
| AttributeOwner.cs | 属性容器：失效传播、克隆与写时复制、运行时注入原语 |
| Attribute.cs | 运行时属性：读写钳制、derived 缓存、变更/失效事件 |
| CompiledExpression.cs | NCalc 表达式封装：解析、依赖提取、求值、编译为委托 |
| AttributeNames.cs | 属性名比较语义收敛点（全库统一 Ordinal 精确匹配） |
| IAttributeCalculation.cs | 求值方案统一契约（plain 与聚合栈同签名） |
| DerivedCalculation.cs | plain derived 求值方案（依赖列表 + 统一求值委托） |
| StackedCalculation.cs | 聚合栈求值方案：三桶贡献、依赖并集、三段折叠 |
| ContributionKind.cs | 贡献类型枚举（flat / additive / multiplicative） |
| ContributionHandle.cs | 贡献句柄：一次性撤销凭证 |
| AttributeTopology.cs | 计算表拓扑序构建（构建期与运行期共用） |
| AttributePrototypeLibrary.cs | 原型模板库：按名登记、按名克隆 |
| Guard.cs / NumberOps.cs | 参数校验 / 泛型数学数值工具 |

系统层面的组件协作见 [architecture.md](architecture.md)。

## 测试布局

`tests/` 下测试文件与被测组件一一对应：AttributeSpecTest、AttributeSpecLibraryTest、AttributeOwnerTest、DerivedAttributeTest、StackedProtocolTest、RuntimeInjectionTest、AttributeOwnerCloneTest、AttributePrototypeLibraryTest、CompiledExpressionTest、NumberOpsTest、ObservationTest，另有 TestHelpers / TestNames / ValueTypeName 三个辅助文件。运行方式见 [DEVELOPMENT.md](DEVELOPMENT.md)。

## 分发边界

release.yml 以 `git archive` 打包，zip 内只有：顶层 `README.md` + `addons/attribute-system/`（源码 + 插件 csproj）。

**不随 zip 分发**：tests/、docs/、openspec/、.github/workflows、addons/gdUnit4。

推论：README 必须自洽覆盖安装与使用的全部用户侧信息（zip 用户拿不到 docs/）；开发文档只存在于仓库。

## 放置规则

- `addons/attribute-system/` 只放纯 C# 逻辑：禁止引入 Godot API（引擎不从引用程序集发现 C# 脚本类）；`Node`/`Resource` 派生类型放宿主项目
- 新测试一律放 `tests/`，不要另立测试工程（gdUnit4 主程序集约束，详见 [DEVELOPMENT.md](DEVELOPMENT.md)）
- 新开发文档放 `docs/`；变更提案走 `openspec/` 工作流
