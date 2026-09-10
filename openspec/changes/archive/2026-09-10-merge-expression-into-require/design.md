## Context

见 proposal.md 的 Why。上一个 change（decouple-scenario-from-spec，已归档）把表达式从 spec 移入 builder 时采用了 `Require(name)` + `WithExpression(name, expression)` 两步式——那是概念切分的机械直译：成员资格与计算行为分属两个概念轴，API 就拆成两个调用。评审结论：概念正交不要求调用正交，两步式让每个 derived 成员的名字传两遍，并催生了一整类只因 API 形状而存在的构建期错误（"注入目标未 Require"）。

## Goals / Non-Goals

**Goals:**

- `Require(attributeName, expression)` 重载：成员资格与派生计算一次声明，属性名只出现一次，每个成员恰好一行
- 冲突规则显式化并首次规格化：重复 base 声明幂等；base/derived 混合、重复表达式声明 → Build 时报错
- 运行时行为（求值、失效传播、克隆、事件）零变化

**Non-Goals:**

- 集合形式的 derived 声明重载（如 `Require(IDictionary<string, string>)`）——将来的场景对象层（上 change D2 预留）是更合适的载体
- 冲突声明的即时报错——维持"所有构建期错误在 Build 抛"约定与无调用顺序约束

## Decisions

### D1. 合并进 Require 重载而非新方法名（如 Derive）

用户裁定：保持 Require 命名一致性。所有成员声明入口统一，调用链读作一份花名册——base 一参、derived 两参，角色一眼可辨。

### D2. 冲突规则的边界：幂等仅限纯 base 重复

- `Require(n)` × 2：幂等（维持既有 HashSet 去重行为；批量 `Require(IEnumerable)` 与逐个声明重叠时依赖此语义）。
- `Require(n)` + `Require(n, e)`（任一顺序）、`Require(n, e1)` + `Require(n, e2)`：Build 时报错。重复声明公式几乎必是 bug，不做"后到覆盖"或"同式幂等"。
- 实现：`AddRequired(name, expression?)` 单一登记点——新名入列；已存在名若"已是 derived 或本次带表达式"记入 `_conflictedNames`，Build 开头统一抛出（原 `_duplicateExpressionNames` 更名）。

### D3. 时机沿袭上 change 的 D3 惯例

格式错误（attributeName/expression 空白）声明时即时抛；语义错误（冲突、缺失 spec、未知引用、环、语法）Build 时统一抛。与 `Require(name)` 缺失 spec 经 `library.Get` 在 Build 报的既有行为对称，且保留任意调用顺序。

### D4. "注入目标未 Require"检查整体删除

合并后表达式键在构造上必为已声明名（`AddRequired` 是唯一登记点，带表达式的名字必然先入 `_requiredAttributeNameSet`），该检查成为不可达代码，连同其错误信息、规格场景与测试用例一并移除。

### D5. delta 机制沿用 REMOVED+ADDED

构建 requirement 的场景集合有删有增（"注入未请求的属性名"消失、"重复注入"拆为两个冲突场景、"重复 base 声明幂等"新增），本工具链 MODIFIED 按场景名比对拒绝丢弃——走 REMOVED（含 Reason/Migration）+ ADDED 新标题「按属性名集合与表达式**声明**构建 Owner」。其余 requirement 均不点名 WithExpression、措辞 API 无关，零 delta；derived-attributes 的"构建期为它注入的表达式"措辞不绑定方法名，不动。Purpose 校验清单直改主规格（沿上 change D8 模式）。

## Risks / Trade-offs

- [公开 API 再一次破坏（WithExpression 移除）] 插件无外部使用方，直接切换；上一 change 同日交付，合入同一提交周期。
- [幂等语义首次显式规格化，可能暴露既有依赖] 既有 `Require(IEnumerable)` + 逐个声明重叠本就依赖静默去重，规格化只是追认现状，附测试钉住。

## Migration Plan

`.Require(n).WithExpression(n, expr)` → `.Require(n, expr)`，机械替换。回滚 = revert 提交。
