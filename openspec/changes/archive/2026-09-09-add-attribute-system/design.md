## Context

仓库是一个全新的 Godot 4.6.3 / .NET 8（`Godot.NET.Sdk/4.6.3`，`net8.0`）插件工程，目前仅有空的 `AttributeSpec.cs` 桩。插件以**源码形式**安装在宿主项目的 `addons/attribute-system/` 下、随宿主一起编译（见 proposal.md - Why）。以下决策中标注「已确认」的来自与需求方的澄清问答。

## Goals / Non-Goals

**Goals:**

- 单一 `AttributeValueType` 别名贯穿全部 API，切换值类型只改一行、只受编译期约束。
- 核心运行时（Attribute / AttributeOwner / Builder）为纯 C# 类，不依赖 Godot 类型，可在 GdUnit4 下以纯逻辑方式测试。
- derived 求值：NCalc 表达式在构建期编译为强类型委托 `AttributeCalculator`，由 builder 装配进 derived 属性（"注入计算方法"的本体是内部装配缝 `BindEvaluator`，不设对外注入接口）。
- 缓存-懒加载 + 失效传播的确定论语义：一次失效至多重算一次；通知按拓扑顺序。
- SpecName / AttributeName 双名机制：同一属性名在不同场景下可由不同 spec 提供不同算法，owner 运行时只感知属性名。

**Non-Goals:**

- modifier/buff 修改器桶、Godot Signal 通道、编辑器可视化、线程安全（约定仅主线程使用）——见 proposal.md Non-goals。
- 表达式引擎的通用化（变量作用域、自定义函数注册表等）；只支持属性引用、字面量、四则与 NCalc 内置函数。
- spec 库按 AttributeName 的反查检索（v1 明确不做，已确认）。

## Decisions

### D1. 值类型别名：`AttributeValueType.cs` 单点 `global using`（已确认）

- 插件源码中唯一的类型声明点：`global using AttributeValueType = System.Single;`（默认 float）。
- 插件内部所有 API、字段、委托签名均使用 `AttributeValueType`。由于别名在编译期展开为具体类型，`a + b` 等运算符直接可用；钳制与字面量转换走内部工具类 `NumberOps<T> where T : INumber<T>`（`T.Min/T.Max`、`T.CreateChecked`）——这两个真实代码路径天然构成 `INumber<T>` 编译期门槛，无需人造的类型门卫类。
- 备选「泛型 `Attribute<T>`」被否：用户代码将处处携带泛型参数，违背"全局统一别名"的意图。备选「DLL 分发」被否：编译期别名无法被宿主更换。

### D2. 纯 C# POCO + C# event（已确认）

- `Attribute`、`AttributeOwner`、`AttributeOwnerBuilder`、`AttributeSpec`、事件参数等均不继承 Godot 类型；观察者用 C# `event EventHandler<AttributeChangedEventArgs>`。
- 全部类型置于命名空间 `AttributeSystem` 下，缓解类名 `Attribute` 与 `System.Attribute` 的潜在歧义（必要时用户可 `using Attr = AttributeSystem.Attribute;`）。
- 备选「Godot Object 派生 + Signal」被否：生命周期与场景树耦合、单测需启动引擎，且纯逻辑层无此必要。

### D3. 表达式引擎：NCalc（已确认）

- NuGet 引入 `NCalc`（社区维护版，实现期取最新稳定），构建期一次解析、提取依赖、编译为求值通道：
  - **依赖提取**：`Expression` 解析为语法树后以 `ILogicalExpressionVisitor<T>` 遍历，收集全部 `Identifier` 节点得到依赖名——NCalc 内置函数名在语法树中是 `Function` 节点而非 `Identifier`，天然不参与依赖，无需剔除步骤；收集结果与本次构建的属性名（AttributeName）集合求差，差集非空即"未知引用"构建错误（见 attribute-owner spec）。**表达式是依赖关系的唯一声明来源**（例：`strength+0.5*dexterity` → 依赖 {strength, dexterity}）；标识符与属性名按字面精确匹配（大小写敏感）。
  - **求值**：以依赖属性**当前值**作为参数传入求值；结果经 `AttributeValueType.CreateChecked` 转换（转换失败抛出带表达式上下文的异常）。
- 求值统一签名：`public delegate AttributeValueType AttributeCalculator(IReadOnlyDictionary<string, AttributeValueType> dependencies);` ——表达式在构建期编译为该委托，由 builder 经内部装配缝 `Attribute.BindEvaluator` 注入 derived 属性（对应原始需求"可以注入用于计算数值的方法"——注入是构建期的内部装配动作，不提供对外的运行时注入接口）。
- 备选「自研 Pratt 解析器」被否（用户选择 NCalc，换取现成的函数库与语法覆盖）；备选「Roslyn」被否：包体与编译延迟对游戏插件过重。

### D4. 失效传播：标记脏 + 拓扑序通知，重算留在读取时（已确认）

- Owner 构建时建立**反向依赖表**（`dep -> dependents`）并做 DFS 三色标记拓扑排序（`BuildTopologicalOrder`）：既用于环检测（attribute-owner spec 的循环依赖错误，报出环路属性链），也固化"通知顺序"索引。
- `base.SetValue(v)`：钳制 → 与当前值比较 → 相同则直接返回（不发事件）→ 否则更新值、快照 invocation list 后发自身变更事件 → `InvalidateDependents` **两阶段处理**受影响 derived：阶段一按拓扑序把全部受影响 derived 标脏并收集失效快照；阶段二按拓扑序分发失效通知。保证即使某个处理器抛出异常，所有缓存也已失效（后续读取必得重算值）；异常向上传播，系统不捕获。
- `derived` 读取：`脏 || 从未计算` → 通过 `AttributeCalculator` 取依赖值（依赖是 derived 时递归走同一懒加载路径）→ 钳制 → 写缓存 → 返回。
- 事件参数 `AttributeChangedEventArgs`：`Name`（即 AttributeName——事件按属性名标识，与 SpecName 无关）；`OldValue`（`AttributeValueType?`，derived 为失效前缓存快照，从未算过则为 `null`，base 恒有值）；`NewValue` 为**惰性属性**——base 直接返回已知新值，derived 的 getter 触发重算（满足 attribute-observation spec 的惰性语义）。多订阅方至多重算一次（重算先写缓存，后续 getter 读缓存）。

### D5. 双名机制、Builder 与 spec 库（已确认）

- `AttributeSpec` 携带双名：`SpecName`（spec 在库中的注册/检索键，唯一）与 `AttributeName`（属性在 owner 中的名称、表达式引用的标识，库内可重复）。引入双名的原因：不同场景下同名属性需要不同计算方式（如 `warrior-attack` 与 `mage-attack` 都产出属性 `attack`），属性名不足以唯一定位定义。原需求"builder 传入所需 attribute 的命名"据此调整为传入 SpecName。
- `AttributeSpecLibrary`：以 SpecName 为键的字典薄封装，`Register`（SpecName 重名抛异常）/ `Get`（缺失抛异常）/ `Contains`。仅按 SpecName 检索、不提供按 AttributeName 反查（v1 YAGNI，已确认）。实例而非静态全局，便于多套定义共存与测试。
- `AttributeOwnerBuilder(library)` 流式 `Require(specName)`（或 `Require(IEnumerable<string>)`），`Build()` 统一执行：按 SpecName 查库 → 汇总属性名集合（**同次构建 AttributeName 重复抛异常**——表达式按属性名解析，重名即歧义，已确认）→ 逐个 derived 解析表达式/依赖（语法错误、未知引用在此暴露）→ 建图 + 环检测 → 实例化 `AttributeOwner`。
- owner 运行时一律以 AttributeName 标识属性（访问、事件、表达式求值），不感知 SpecName；`Attribute` 上仅保留来源 SpecName 作只读调试信息。
- `AttributeSpec` 为不可变类（主构造函数内完成校验：SpecName/AttributeName 非空白等，对应 attribute-definition spec 的非法拒绝场景）；`MinValue`/`MaxValue` 参数可选，缺省为 `AttributeValueType` 的类型极值（钳制退化为 no-op）；`Expression` 为 `string?`，非空即 derived。

### D6. 事件分发的快照语义

- 采用字段式 event 的 `?.Invoke`：编译器在 invoke 前对多播委托的 invocation list 取快照，天然提供"当次分发按快照继续；处理器内的订阅不对当次事件生效、退订不影响当次分发完整性"的语义（满足 attribute-observation spec 的处理器内订阅/退订安全场景），无需自定义 add/remove 访问器。
- 约定单线程（主线程）使用，不加锁。

### D7. 测试：GdUnit4（已确认）

- 本工程安装 gdUnit4 插件并在 csproj 引入 `gdUnit4.api` NuGet 包；测试套件放在 `addons/attribute-system/tests/`（GdUnit4 `[TestSuite]`）。`Microsoft.NET.Test.Sdk` 与 `gdUnit4.test.adapter` 是 `dotnet test`（VSTest 集成）的必需配套，与运行时依赖（NCalc）同住一个 csproj 是同工程测试的既定取舍。
- 因核心是 POCO，测试不依赖场景树；"求值次数"类断言通过注入委托内计数器实现。
- 别名切换（D1）是编译期行为，无法在单测中覆盖——以手动验证任务（切 `int` 重编译）代替。

### D8. 公开 API 面与 fail-fast 取舍

- `AttributeOwner` 的索引器与 `Get` 并存：索引器是 `Get` 的语法糖（与 BCL 集合类型惯例一致），`Get` 承载 XML 文档与异常语义，二者刻意保留。
- `TryGet`/`Contains`/`SetValue`/`GetValue`/`Attributes` 为常规便利表面：避免使用方被迫以异常做控制流，或为反查 spec 而自行保留 library 引用。
- `Build()` 对空 Require 抛异常：与"一切错误在构建期暴露"的 fail-fast 原则一致——静默产出空 owner 只会把问题推迟到运行期。
- 运行时依赖（NCalc）与测试工具链（Test.Sdk、gdUnit4.*）同住一个 csproj：D7 同工程测试的既定取舍（见 D7）。
- `NumberOps<T>` 仅以 `NumberOps<AttributeValueType>` 一种实参使用：服务于 D1 的"值类型可切换"目标，非投机泛化。
- 参数校验约定：`ArgumentException.ParamName` 一律为实际参数名（camelCase，符合 .NET 惯例）；消息文本指代公共字段时用 PascalCase 字段名。
- 测试夹具按套件自包含（各套件自带 `CreateLibrary`），不做跨套件上收：套件独立可读性优先于去重。

## Risks / Trade-offs

- [NCalc 对整型运算遵循其类型规则（如 int 除法截断），与浮点预期不同] → 在插件 README 与表达式文档中显式说明，建议整型别名下用乘法/显式字面量规避。
- [奇异的 `INumber` 类型（非 NCalc 原生支持）在表达式通道转换失败] → 运行期给出带表达式上下文的明确异常；此类类型推荐走委托注入通道。
- [`global using` 别名只在源码随宿主编译时有效] → 插件定位即源码分发；README 声明不支持以预编译 DLL 方式更换值类型。
- [高频变更 + 深依赖链时逐属性发事件的开销] → v1 接受（通知本身不触发重算，开销为 O(受影响 derived 数)）；未来可在此基础上加批量事务 API，不破坏现有语义。
- [类名 `Attribute` 与 `System.Attribute` 的潜在混淆] → 命名空间 `AttributeSystem` 隔离 + 文档注明别名用法。
- [`SpecName` 与 `AttributeName` 双名易混淆（误用 spec 名做运行时检索或误把属性名当构建键）] → 运行时 API 只接受 AttributeName，spec 名只出现在 library/builder；README 与 XML 注释明确分工。

## Migration Plan

全新插件，无存量迁移。回滚 = 移除 `addons/attribute-system/` 源码与本 change 引入的 NuGet 包引用。

## Open Questions

- NCalc 具体版本号在实现时取当时最新稳定版即可；其 5.x 与更新版本在"语法树遍历/参数注入"上的 API 细节差异由实现期适配，不影响本设计的结构。
