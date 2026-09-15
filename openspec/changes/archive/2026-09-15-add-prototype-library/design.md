## Context

见 proposal.md——动机是从"零散持有原型变量"走向"按名登记的模板库"，并给模板场景一个初值声明入口。

现状约束：三层职责已确立（定义=AttributeSpec / 组装=builder / 实例=owner 克隆）；`Clone()` 当前契约是"不继承原型当前值、一律重置为钳制后的默认值"（上一支 change 刚写入规格）；`Require` 家族已有三个重载（base / base+表达式即 derived / 批量）；同名冲突声明在 Build 时统一暴露，表达式空白在声明调用时即时抛。

## Goals / Non-Goals

**Goals:**

- 模板工作流一条路走通：配置期 `Register(name, builder)` → 创建期 `Clone(name)`。
- 初值声明与克隆继承语义组合后，"登记的初值随克隆带出"不引入任何额外的共享结构。
- 原型不可误用：库外拿不到原型引用，误把模板当运行实体的错误不可表达。

**Non-Goals:**

- 不做原型的 `Unregister` / 替换 / 热更新（spec 库同样没有对应物；需要时另立 change）。
- 不做 Godot Resource 序列化（策划在编辑器里配模板）——初值作为登记数据已是可序列化的形状，但序列化本身不在本期。
- 不做原型级元数据（图标、描述等 UI 展示信息）——游戏侧自行包一层。

## Decisions

### D1：初值入口在 `Require` 重载，不在 AttributeSpec、不在 Register 参数

`Require(attributeName, initialValue)` 与 `Require(attributeName, expression)` 靠第二参数类型（float vs string）天然区分，无签名歧义；derived 没有初值入口——非法状态不可表达。该区分依赖 AttributeValueType 恒为数值类型——此不变量由 attribute-definition 的 INumber 编译期门槛兜底（别名若替换为 string，`NumberOps<T>` 的约束处先编译失败），重载签名冲突对合法配置不可达。

- 否掉 **AttributeSpec 携带初值**：初值是组装层的事（同一个 `strength`，warrior 从 15 起、mage 从 8 起），放回 spec 是 decouple-scenario-from-spec 刚拆掉的耦合回归。
- 否掉 **Register 钥匙孔**（`Register(name, builder, ("strength",15), ...)`）：与"原型不出库"打架——初值若是对运行实例的临时操作，就需要一个逃逸口（受限配置器或登记参数），口子一开就收不回。初值进声明后 `Register` 只剩最简形态，钥匙孔问题整个消失。

### D2：重复声明的初值规则——并集语义，仅显式矛盾报错

- 均未声明初值 / 初值一致 → 幂等（沿用现状）。
- 仅一方声明初值（任一顺序）→ 合并取显式值。支撑"批量 `Require(集合)` 后对个别属性补初值"的真实用法，且与顺序无关，延续"无调用顺序约束"的约定。
- 两方声明**不同**初值 → 记入 `_conflictedNames`，Build 时随既有冲突信息一并抛出（错误措辞扩展为"base/derived 混合、重复声明表达式或矛盾初值"一类）。

否掉的备选：**严格相等才算幂等**（`Require(n)` + `Require(n, 15)` 也报错）——过严，把"补初值"逼成先撤销再声明的手工操作，而它并不比既有的幂等重复声明更危险。

实现上 `_initialValuesByAttributeName` 与 `_expressionsByAttributeName` 平行：初值键必为已声明名且必为 base——互斥并非字典结构自动保证，而是由 `AddRequired` 重复分支既有的混合声明检查（`_expressionsByAttributeName.ContainsKey`）拦截 `Require(n, "expr")` 与 `Require(n, 15)` 的交叉时序；扩展 AddRequired 时必须保留该检查，否则交叉时序会静默合并、初值无声丢失。初值一致性比较经 `EqualityComparer<T>.Default`（NaN 与 NaN 视为相等，`==` 会把两个 NaN 误判为冲突）；NaN 穿透钳制与既有 base 写值行为一致，不另设校验。

### D3：克隆语义 X——克隆继承原型的 base 当前值

`CloneAttribute()` 分叉：base 分支复制当前 `_value`；derived 分支维持全新（缓存空，惰性重算）。derived 重算依赖的是克隆自身继承来的 base 值，结果与原型的 derived 值一致，行为自洽。

- 选 X 的理由：单一真相源（初值就长在原型上，不需要库内旁路表）；"复制一个当前状态的实体"（复制打了一半的敌人）自动成立且与模板克隆共享同一条规则；**X 严格泛化现状**——未声明初值的原型当前值即 `Clamp(default)`，克隆行为与今天完全一致，不是推翻是放宽。
- 否掉 **Y（Clone 不动，库存旁路初值表、克隆后逐个 SetValue）**：同一概念两份表示（原型的属性值永远是默认值 + 库的旁路字典）；且"复制当前实体"用例没有着落。

契约反转趁现在做：插件未发布，改契约的成本此刻最低。既有测试中"克隆从初始值开始"的断言随之反转。

实现细节：初值经构造器写入（`Attribute.CreateBase(spec, initialValue)` 一类），不走 Value setter——避免构建/登记路径上发出无人订阅的事件并触发无意义的失效级联；钳制复用 `NumberOps.Clamp`。

### D4：原型不出库——Register 内部构建并独占引用

`Register(name, builder)` 调 `builder.Build()` 后私有持有，库外无路径取得原型。好处：把原型误当运行实例用（SetValue、订阅事件）的错误不可表达；builder 的一切构建期错误在登记时暴露，落在配置期而非首次创建角色时。builder 本身无状态消耗（Build 可重复调用、各次产物独立），登记后用户留存 builder 再 Build 出的只是无关的兄弟实例，无泄漏风险。

### D5：克隆 requirement 走 REMOVED+ADDED（换标题）

「克隆从初始值开始」场景在 X 语义下断言反转，场景名必须换；MODIFIED 按场景名逐一比对、丢弃现存场景会被 `validate --strict` 拒绝，故整条走 REMOVED（Reason/Migration）+ ADDED 新标题「prototype 克隆继承 base 当前值」。构建 requirement 场景只增不减，走 MODIFIED 携带全部十个场景。

### D6：库命名 `AttributePrototypeLibrary`，表面五方法

`Register` / `Clone`（缺失抛 `KeyNotFoundException`，与 spec 库 Get 的语义一致）/ `TryClone`（`bool + out` 形态，与 spec 库 TryGet 一致）/ `Contains` / `PrototypeNames` 枚举（创建界面列出可选项的硬需求；返回快照 `IReadOnlyCollection<string>`，当次枚举不受后续登记影响）。`Register` 另对 null builder 抛 `ArgumentNullException`，对齐 `AttributeSpecLibrary.Register` 的参数守卫。否掉 `OwnerPrototypeLibrary`：owner 是插件行话，`AttributePrototypeLibrary` 在用户视角读作"属性模板库"。原型名比较沿用 `AttributeNames.Comparer`（Ordinal），与全插件统一。

## Risks / Trade-offs

- [克隆契约反转动既有调用方] → 插件未发布、调用方仅自家测试与 README 示例；断言反转随本 change 一并完成，测试全绿即闭环。
- [`Require(n, 15)` 与 `Require(n, "expr")` 第二参数语义不同] → 类型系统已区分（float/string 无重载歧义），XML 注释明确各自语义；若日后仍觉混淆，可在评审中重估命名。
- [并集语义静默合并 `Require(n)` + `Require(n, 15)`] → 与既有幂等重复声明同级风险（复制粘贴错名），且仅显式矛盾才报错的边界更清晰；错误信息列出冲突属性名的机制不变。
- [线程边界不变] → 克隆共享求值委托的并发警告原样保留（README 克隆节、Clone 注释），初值与原型库不引入新的共享可变状态。
