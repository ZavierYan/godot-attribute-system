## 1. 计算与声明基座

- [x] 1.1 新建类型基座：public `ContributionKind`（Flat/Additive/Multiplicative）与 `ContributionHandle`（绑定 owner、一次性撤销）；internal `IAttributeCalculation` 接口（Dependencies + Calculator），`DerivedCalculation` 加接口声明；internal `StackedCalculation`（seed + 三桶贡献列表 + 依赖并集随增撤全量重算 + `Compute` 三段折叠，返回未钳制原始值）。验证：`dotnet build` 0 错误（既有 CS8600 除外），既有 80 测试全绿（纯基座无行为变化）。
- [x] 1.2 builder 聚合栈声明：`RequireStacked(attributeName, seedExpression)`（空白 seed 声明时即时抛）、声明形态记录（base / plain 表达式 / 聚合栈 seed）、同名冲突网扩展（相同 seed 幂等、不同 seed 与 plain/聚合栈混合归入 `_conflictedNames` 在 Build 报冲突）、Build 为聚合栈成员产出 `StackedCalculation`、`BindCalculations` 闭包改为捕获计算对象并调用时取当前 Dependencies。验证：新增测试四例（聚合栈构建成功且无贡献时读 seed 钳制值、相同 seed 重复声明幂等、plain 与聚合栈混合声明 Build 抛、两个不同 seed Build 抛）全绿，既有测试不回归。

## 2. owner 运行时变异原语

- [x] 2.1 Add 族与写时复制：`AddBase(AttributeSpec, initialValue?)`（初值即时钳制）、`AddDerived(AttributeSpec, expression)`、`AddStacked(AttributeSpec, seedExpression)`；同名拒绝、表达式（含 seed）语法错误与未知引用在添加调用时抛（错误信息含表达式文本与未解析名）；COW 基座——`_tablesSharedWithClones` 标志、`EnsureMutableTables()` 深拷贝三张表（含内层 List 与 StackedCalculation，CompiledExpression/委托/DerivedCalculation 继续共享）、克隆路径传播标志；拓扑序全量重排例程。验证：新增测试约七例（运行时添加 base 读写与钳制、添加 plain derived 求值且依赖变化失效、添加聚合栈后可投贡献、同名拒绝且原属性不受影响、未知引用抛、语法错误抛、越界初值钳制）全绿。
- [x] 2.2 贡献与撤销：`Contribute(targetName, kind, expression)` 返回 `ContributionHandle`（目标不存在或非聚合栈抛、贡献表达式语法错误/未知引用抛、依赖可达目标即环抛且错误信息含环路属性名、贡献未生效）；失效目标自身及下游（复用两阶段流程与惰性新值事件）；`RemoveContribution(handle)`（重复撤销、跨 owner 句柄抛）。验证：新增测试约八例（投贡献后失效重算且事件触发、贡献依赖变化失效重算、向 plain derived 与 base 投贡献均抛、贡献未知引用抛、贡献成环抛、按句柄撤销后句柄失效、重复撤销抛、跨 owner 句柄抛）全绿。
- [x] 2.3 移除属性：`RemoveAttribute(name)`——遍历 `_calculations` 依赖并集，被引用即拒绝并指明引用方；移除后名字不可访问、其余属性不受影响。验证：新增测试三例（移除未被引用的属性成功且后续访问抛、移除被引用属性拒绝且信息含双方名字、撤销全部贡献后可移除）全绿。
- [x] 2.4 克隆族隔离锁定：跨 Add 族与 Contribute/Remove 验证 COW 语义。验证：新增测试三例（原型变异不波及既有克隆——构成与值均不变、克隆上的变异不影响原型、变异后的 owner 再克隆继承注入的属性与贡献且 base 继承彼时当前值）全绿。

## 3. 协议行为锁定

- [x] 3.1 聚合栈协议测试全套（对应 derived-attributes delta 场景）：无贡献时等于 seed 钳制值（seed 越上限）、flat 贡献叠加、additive 同池求和、multiplicative 独立复利、三类混合（281.25 例）、贡献项为动态条件表达式（`hp < 30 ? 0.5 : 0` 随 hp 变化失效重算）、桶内交换律（两种投入顺序同值）、additive 池和为负时最终钳制（Min=0 → 0）、撤销一条 flat 贡献后回退到 seed + 剩余贡献。验证：九例全绿，全量测试通过（预计约 114）。

## 4. 文档与规格

- [x] 4.1 README 更新：核心组件清单补聚合栈与注入原语；新增「聚合栈与运行时注入」章节（RequireStacked 声明示例、协议公式、AddBase/Contribute/Remove 示例、桶内交换律与"分段用 derived 阶段链"指引、COW 克隆说明）；构建期校验表补运行时变异各行（同名、未知引用、语法错误、成环、目标非聚合栈、删除被引用属性）。验证：文档评审通过、与实现 API 一致。
- [x] 4.2 主规格 Purpose 直改：attribute-owner 与 derived-attributes 两处 Purpose 各扩写运行时注入与聚合栈一句。验证：`openspec validate --specs --strict` 通过。
