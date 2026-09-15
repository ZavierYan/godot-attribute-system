## Why

目前"原型"只能以代码变量形式零散持有：游戏若要支持多个可配置的角色模板（职业、兵种等）并在创建角色时按名选择，只能自建字典管理 builder 与产物，且原型的持有方式没有任何约束——把原型误当运行实例用、同名模板静默覆盖，都是现在就能写出来的错误。同时，模板场景需要"克隆携带配置的初始属性值"，而现状克隆一律重置为默认值，初始值没有声明入口。

## What Changes

- **BREAKING**：`AttributeOwner.Clone()` 语义反转为继承原型的 base 属性当前值（原先"不继承原型当前值、一律重置为初始值"）；derived 缓存仍为空照常惰性重算，事件订阅仍不继承。从未写值且未配置初值的原型行为不变（其当前值即钳制后的默认值）。
- `Require` 新增初值声明重载 `Require(attributeName, initialValue)`：声明 base 成员并指定初值（构建时钳制到最值区间）。重复声明规则延伸：同一属性名两个不同显式初值在构建时报错；无初值声明与有初值声明合并取显式值（与顺序无关）。
- 新增 `AttributePrototypeLibrary`：`Register(prototypeName, builder)` 在库内执行构建并独占原型引用（原型不出库），`Clone(prototypeName)` / `TryClone` / `Contains` / `PrototypeNames` 支持按名克隆与枚举；同名登记被拒绝，builder 的一切构建期错误在登记时暴露。

## Capabilities

### New Capabilities

（无）

### Modified Capabilities

- `attribute-owner`：
  - 「按属性名集合与表达式声明构建 Owner」MODIFIED——补初值声明重载与重复声明规则的延伸，场景只增不减；
  - 「prototype 克隆」→ 新标题（REMOVED + ADDED）——克隆语义反转为继承 base 当前值，「克隆从初始值开始」场景随之换名为继承语义；
  - ADDED「按名登记原型并从原型克隆」——原型库的登记、按名克隆、枚举与错误语义。

## Impact

- 源码：`AttributeOwnerBuilder`（新重载与冲突规则）、`Attribute`（带初值的构造路径、`CloneAttribute` 的 base 分支改为复制当前值）、新类 `AttributePrototypeLibrary`；另有注释同步：`AttributeOwner.Clone()` 与 builder 类注释的契约措辞。
- 测试：既有克隆用例中"克隆从初始值开始"断言反转；新增初值声明、克隆继承、原型库三组用例（预计 65 → 约 77）。
- README：组装节补初值声明示例与校验表一行；克隆节措辞改为继承当前值；新增原型库使用段（配置期登记 → 创建期按名克隆）。
- 主规格 Purpose 在实现阶段直改：补"原型可按名登记于原型库"一句。
