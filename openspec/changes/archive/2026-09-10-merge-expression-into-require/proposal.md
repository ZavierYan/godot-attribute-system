## Why

上一个 change（decouple-scenario-from-spec）把表达式从 spec 移到 builder 后，derived 属性需要 `Require(name)` + `WithExpression(name, expression)` 两次调用：同一个属性名传两遍，且"注入目标未 Require"这一整类构建期错误只因 API 允许写出不一致状态而存在。成员资格与派生计算本就是一次声明的两面——合并为一个方法调用让非法状态不可表达，调用点每个成员恰好一行。

## What Changes

- **BREAKING** `AttributeOwnerBuilder` 移除 `WithExpression`，新增重载 `Require(attributeName, expression)`：`Require(name)` 声明 base 成员，`Require(name, expression)` 声明 derived 成员，成员资格与计算方法一次声明。
- 声明冲突规则：同一属性名重复 base 声明幂等（维持既有去重行为，首次规格化）；base/derived 混合声明、同一属性名两条表达式声明在 Build 时报错；表达式空白在声明调用时即时抛出。
- "注入目标未 Require"错误类随合并消失（表达式键在构造上必为已声明名）。
- 测试调用点与 README 同步；运行时行为（求值/失效/克隆/事件）零变化。

## Capabilities

### New Capabilities

（无）

### Modified Capabilities

- `attribute-owner`: 「按属性名集合与表达式注入构建 Owner」重写为「按属性名集合与表达式声明构建 Owner」（REMOVED+ADDED：Require 两重载语义、幂等与冲突规则场景化；"注入未请求的属性名"场景随错误类消失）

## Impact

- `addons/attribute-system/AttributeOwnerBuilder.cs`（唯一源码改动：Require 重载 + AddRequired + 冲突检查，删除 WithExpression 与目标检查）。
- 测试：四个套件约 21 处调用点改 `Require(n, e)`；3 个错误用例改写、新增 1 个幂等用例，62 → 63。
- `addons/attribute-system/README.md` 第 3 步与校验表。
- 主规格 Purpose 直改（校验清单措辞）。
