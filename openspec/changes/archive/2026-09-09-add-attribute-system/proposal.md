## Why

本项目要成为一个可复用于 Godot 4 C# 游戏的属性系统插件（addon），但当前仓库只有一个空的 `AttributeSpec.cs` 桩。游戏实体（角色、装备、Buff 等）需要一个统一的数值层：数值可订阅变更、可由其它数值推导、且值类型可由使用方按项目需求（int/float/double 等）一次性指定。现在从零建立这套核心系统，为后续玩法系统（战斗、成长、UI 绑定）提供地基。

## What Changes

- 新增全局值类型别名 `AttributeValueType`（源码分发 + `global using` + `INumber<T>` 泛型数学约束），使用方改一行即可切换 int/float/double/decimal 等数值类型。
- 新增 `AttributeSpec`：以数据形式定义一个属性——`SpecName`（spec 的注册/检索键，供 builder 构建期选定定义）、`AttributeName`（属性名，owner 运行时与表达式中引用的标识）、最值（min/max）、以及（derived 用的）字符串计算表达式。不同场景下同名属性可由不同 spec 提供不同计算方式。
- 新增 Attribute 运行时模型：分 **base**（独立数值，可直接读写并受最值钳制）与 **derived**（由其它 attribute 计算而来，缓存-懒加载）两类。
- 新增观察者模式支持：外部可订阅任意 attribute 的值变化；base 变化会把依赖它的 derived 缓存标记为脏并发出失效通知，derived 的新值在被读取时才惰性计算。
- 新增 derived 计算装配：spec 的字符串表达式经 **NCalc** 在构建期解析为强类型计算委托（表达式同时声明计算方法与依赖关系——标识符即依赖属性的 AttributeName，构建期提取依赖、检测循环），由 builder 注入 derived 属性，实现缓存-懒加载求值。
- 新增 `AttributeOwnerBuilder`：传入所需 SpecName 集合，从 `AttributeSpec` 库取定义，以各 spec 的 AttributeName 为属性名，构建并校验（缺失 spec、表达式引用未知属性、同次构建属性名重复、循环依赖均报错）出 `AttributeOwner`。owner 运行时一律以 AttributeName 标识属性（访问、事件、表达式引用），SpecName 仅在构建期使用。
- 新增 GdUnit4 测试工程配置与核心逻辑测试。

**Non-goals（本期明确不做）**：修改器/modifier 桶（buff 加成聚合）、基于 Godot Signal 的事件通道、编辑器 Inspector 可配置化、多线程安全（约定仅主线程使用）。

## Capabilities

### New Capabilities

- `attribute-definition`: 值类型别名 `AttributeValueType` 的约束与切换方式；`AttributeSpec` 的字段（SpecName/AttributeName/最值/表达式）与校验规则；spec 库（按 SpecName 检索、SpecName 重名拒绝、AttributeName 可重复）。
- `attribute-owner`: `AttributeOwnerBuilder` 的构建流程与错误校验（缺失 spec、表达式引用了构建集合外的属性名、同次构建 AttributeName 重复、循环依赖）；`AttributeOwner` 的 base 属性读写、最值钳制与按 AttributeName 访问。
- `derived-attributes`: derived 属性的求值模型——表达式（NCalc）与委托注入两条通道、表达式即依赖声明、缓存懒加载、依赖图失效传播、最值钳制。
- `attribute-observation`: 观察者模式——订阅/退订、base 变更事件载荷、derived 失效事件及 `NewValue` 的惰性求值语义。

### Modified Capabilities

（无——仓库尚无任何既有 spec。）

## Impact

- **代码**：`addons/attribute-system/` 下新增全部插件源码（`AttributeValueType.cs`、`AttributeSpec.cs`（现有桩重写）、`Attribute.cs`、`AttributeOwner.cs`、`AttributeOwnerBuilder.cs`、表达式相关类等）；空桩 `AttributeSpec.cs` 将被完整实现替换。
- **依赖**：主工程 NuGet 引入 `NCalc`（表达式解析）；测试引入 `GdUnit4` 相关包与测试运行配置。
- **兼容性**：全新插件，无既有 API 需要破坏；`AttributeValueType` 是编译期别名，切换类型需重新编译，属于预期工作流。
- **分发形态**：以源码形式安装到目标项目 `addons/attribute-system/`，随宿主项目一起编译。
