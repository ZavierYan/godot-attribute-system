## Why

AttributeSpec 目前同时承担三个角色：属性身份（AttributeName）、约束（最值）、场景行为（Expression）。"同名属性在不同场景用不同算法"靠登记多个共享 AttributeName 的 spec 实现，迫使身份与约束随场景复制——warrior-attack 与 mage-attack 各自携带一份 attack 的最值，漂移与不一致是这个复制的必然结果；SpecName 这个消歧键也由此而生（归档 design.md 自己记录了双名易混淆的风险）。本变更把概念切分修正为三层正交职责：定义（spec 只回答"一个属性是什么"）、组装（builder 回答"这类实体里它怎么算"）、实例（owner 克隆回答"这个实体当前的状态"）。

## What Changes

- **BREAKING** `AttributeSpec` 移除 `SpecName`、`Expression`、`IsDerived`：spec 收敛为 {AttributeName, MinValue, MaxValue}；base/derived 之别不再属于定义层。
- **BREAKING** `AttributeSpecLibrary` 改按 AttributeName 登记/检索；同名 AttributeName 登记从"允许"反转为"拒绝"（这正是消灭不一致的机制）。`Attribute.SourceSpecName` 调试属性一并移除。
- **BREAKING** `AttributeOwnerBuilder.Require` 改按属性名；新增 `WithExpression(attributeName, expression)` 在组装层注入表达式（格式错误即时抛，目标未 Require / 重复注入在 Build 抛）；"同一次构建属性名重复"的校验随库键唯一化而结构性消失。
- 新增 `AttributeOwner.Clone()`（prototype 模式）：Build 产出的 owner 可作原型，克隆共享 spec 引用、编译求值委托与只读依赖图，值/缓存/事件订阅全新、从初始值开始；一次组装、多次实例化，多实体场景免重复编译与拓扑排序。
- 测试、README 同步改写；`CompiledExpression`/失效传播/事件协议等运行时行为不变。

## Capabilities

### New Capabilities

（无）

### Modified Capabilities

- `attribute-definition`: AttributeSpec 定义属性（移除 Expression/SpecName/双名共享）、非法 spec 被拒绝（删 SpecName/表达式空白场景）、spec 库按 SpecName 检索 → 按 AttributeName 检索且同名拒绝（含 RENAMED）
- `attribute-owner`: 按 SpecName 集合构建 Owner → 按属性名集合与表达式注入构建（含 RENAMED）、缺失 spec/未知引用/循环依赖三条措辞适配、移除"构建期拒绝重复的属性名"、新增 prototype 克隆
- `derived-attributes`: 表达式求值、非法表达式在构建期暴露两条的来源措辞从"spec 表达式"改为"构建期注入的表达式"（依赖声明、懒缓存、失效传播、钳制的行为不变）

## Impact

- `addons/attribute-system/AttributeSpec.cs`、`AttributeSpecLibrary.cs`、`AttributeOwnerBuilder.cs`、`AttributeOwner.cs`、`Attribute.cs`（公开 API 面：构造签名、Require 参数语义、新 WithExpression/Clone；DerivedCalculation 提升 internal、装配绑定移入 owner 构造）。
- 测试套件改写：删 6 例（双名/表达式/SpecName 校验）、增 9 例（克隆 7 + 注入错误 2）、其余存活用例的构造路径适配，57 → 60。
- `addons/attribute-system/README.md` 第 2/3 步、构建期校验表、文件结构说明。
- 不引入新依赖；NCalc 用法不变。
