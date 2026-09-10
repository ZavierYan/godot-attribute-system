## Context

见 proposal.md 的 Why。现状代码事实：`spec.Expression` 的全插件唯一消费点是 `AttributeOwnerBuilder.ValidateSpecExpression`（AttributeOwnerBuilder.cs:105），迁移源码改动面小；SpecName 渗透库的全部 4 个方法、builder 的 Require 签名与 `Attribute.SourceSpecName`；Clone/Prototype 零存量。归档 change（2026-09-09-add-attribute-system）的 design.md 记录了双名机制的动机（"属性名不足以唯一定位定义"）与易混淆风险——本变更正是抽掉该动机后的收敛。

## Goals / Non-Goals

**Goals:**

- spec 收敛为 {AttributeName, MinValue, MaxValue}；库按 AttributeName 唯一登记
- 表达式经 `WithExpression` 在组装层注入；构建期校验语义与现状等价（缺失/未知引用/循环/语法，去重校验消失）
- `AttributeOwner.Clone()`：一次组装、多次实例化，克隆共享编译产物与只读图

**Non-Goals:**

- 场景对象/Kit 数据类型（可 JSON 化的场景注册）——将来需要时在 WithExpression 之上加薄层
- 跨 owner 引用、运行时增删属性、多线程安全（维持主线程约定）
- 修改器/modifier 桶等既列非目标

## Decisions

### D1. SpecName 彻底移除，而非保留为可选别名

双名本为"同名属性多场景二选一"的消歧而生；场景移入组装层后，两个名字指同一物、留有漂移空间。库键改 AttributeName 后，"同名登记拒绝"从机制上消灭最值漂移（原先 warrior-attack/mage-attack 各带一份 attack 最值）。`Attribute.SourceSpecName` 随之移除（spec 无 SpecName 后该调试属性无意义）。替代方案"保留 SpecName 仅作显示别名"被否：无消歧职责后纯属冗余键。

### D2. 表达式走 builder 内联注入（WithExpression），不引入场景对象

`WithExpression(attributeName, expression)` 与 `Require(attributeName)` 同为链式 API。场景（warrior/mage）由使用方静态工厂封装（如 `OwnerFactory.Warrior(library)`）。引入 Scenario/Kit 数据对象一步到位数据驱动，但显著放大本次改动面与规格面；WithExpression 是它将来的天然地基（场景对象最终只是一张 {属性集, 表达式表} 的载体）。YAGNI，后加不难。

### D3. WithExpression 的校验时机：格式即时、语义延迟

注入参数空白（attributeName/expression 为空白）在注入调用时即时抛（与 `Require` 的 Guard.NotBlank 同风格）；"注入目标未 Require"与"重复注入"在 Build 时统一抛——与 builder 既有约定"所有构建期错误在 Build() 中抛出"及 Require 自身行为（缺失 spec 也是 Build 时经 library.Get 报）对称，且不强制 Require 先于 WithExpression 的链式调用顺序。

### D4. 克隆初值语义：重置，不复制原型当前值

克隆 = "同一构成的全新实例"，base 属性从 `Clamp(default, Min, Max)` 开始。原型因此可安全地非纯净（被人写脏后克隆仍干净）。复制当前值对"模板"用法是意外行为；确需快照式复制时使用方自行逐属性拷贝。

### D5. 克隆共享编译委托——设计强制而非可选优化，及线程边界

`DerivedCalculation` 只含 {Dependencies, Calculator}，不保留表达式文本，Build 后无法重编译，克隆只能共享。这是接受的约束：**共享 CompiledExpression 引入了同族实例间的求值隔离损失**——`CompiledExpression.Evaluate` 每次求值写 `_expression.Parameters`（CompiledExpression.cs:47-48），当前每次 Build new 一个 CompiledExpression，独立构建的 owner 恰好互相隔离；克隆共享后，同族实例并发求值会互相污染参数。单线程语义（Godot 常规，README 既声明"仅主线程"）下安全，且多线程本是非目标，但该边界需在 README 克隆节写明。否决的替代：每克隆重编译（失去 prototype 免编译的意义）；每次求值新建 Expression 实例（derived 读取是热路径，每次分配不可接受）。

### D6. 装配绑定移入 owner 构造，DerivedCalculation 提升 internal

求值委托闭包捕获 owner（`BindEvaluator(() => calculation.Calculator(owner.GetDependencyValues(deps)))`），克隆必须对新 owner 重放绑定。为此把 `DerivedCalculation` 从 builder 私有提升为 internal，owner 构造器增加 calculations 参数并内部完成绑定——builder 与 Clone() 共用同一条装配路径，消除两处绑定的漂移可能。

### D7. 克隆走跳过防御性拷贝的内部构造路径

现有 internal 构造器对 directDependents/topoIndex 做防御性拷贝；克隆改为共享这三张构建后只读的表（含其中的 `List<string>`——"构建后不可变"是约定，无运行时写路径，以注释固化）。防御性拷贝保留给 builder 路径（对外部传入数据的边界）。

### D8. Purpose 措辞直接改主规格

attribute-definition 与 attribute-owner 两个主规格的 Purpose 段提及"双名/SpecName/表达式"，随实现直接编辑 `openspec/specs/*/spec.md`（delta 不携带 Purpose）。derived-attributes/attribute-observation 的 Purpose 措辞重塑后仍准确，不动。

## Risks / Trade-offs

- [本工具链（openspec 1.11.0）的 MODIFIED 按场景名比对、拒绝丢弃现存场景，也无场景级 REMOVED 语法] → 需删场景的 requirement（attribute-definition 三条）走 REMOVED（含 Reason/Migration）+ ADDED 新标题；场景为纯增的（attribute-owner 构建条）保留 RENAMED+MODIFIED；`openspec validate --strict` 已通过，归档时同一套检查再把关一次。
- [公开 API 破坏性变更，约 40/57 用例构造路径受影响] → 一次性完成源码+测试同步改写，以全套件绿为完成标准；不做兼容垫层（插件早期，无外部使用方）。
- [同族实例并发求值不安全（D5）] → README 克隆节明示边界；多线程维持非目标。
- [表达式从数据变代码，场景公式散在使用方] → 静态工厂封装为推荐用法写入 README；将来需要时加场景对象层。

## Migration Plan

单仓库插件、无外部使用方，直接切换，无兼容期。回滚 = revert 提交。
