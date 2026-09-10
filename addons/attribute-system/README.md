# Attribute System

Godot 4 C# 属性系统插件（源码分发）：为游戏实体提供可订阅变更、支持表达式派生的数值层。核心组件四个：`AttributeSpec`（定义属性）→ `AttributeSpecLibrary`（注册中心）→ `AttributeOwnerBuilder`（构建）→ `AttributeOwner`（使用）。下面按完整使用流程走一遍。

## 安装

1. 把 `addons/attribute-system/` 目录复制到你的 Godot 4 C# 项目中（插件为纯 C# 代码，命名空间 `AttributeSystem`，不依赖场景树）。
2. 宿主项目 csproj 需引用表达式解析包 `NCalc`（可照抄本仓库 `attribute-system.csproj` 中的 `PackageReference`）。

## 第 1 步：配置值类型（可选）

打开 `addons/attribute-system/AttributeValueType.cs`，整个插件只有这一行声明值类型：

```csharp
global using AttributeValueType = System.Single; // 默认 float
```

改成任意数值类型（`System.Int32`、`System.Double`、`System.Decimal` 等）后重新编译宿主项目，插件所有 API 的数值类型随之切换，其它源码一行都不用动。

> 别名是编译期展开的，所以插件必须以源码方式随宿主一起编译——预编译 DLL 方式无法切换值类型。

## 第 2 步：定义并注册 AttributeSpec

每个 `AttributeSpec` 描述一种属性（名字 + 区间），创建后登记到 `AttributeSpecLibrary`：

```csharp
using AttributeSystem;

var library = new AttributeSpecLibrary();

// 名字 + 区间（区间可选，缺省不限制）
library.Register(new AttributeSpec("strength", 0, 100));
library.Register(new AttributeSpec("dexterity", 0, 100));
library.Register(new AttributeSpec("intelligence", 0, 100));
library.Register(new AttributeSpec("attack", 0, 999));
```

`AttributeName` 库内唯一：同名登记会被拒绝。

## 第 3 步：用 Builder 组装 AttributeOwner（属性集合 + 表达式声明）

告诉 builder 本场景需要哪些属性：只给名字就是 base，多传一个表达式就是 derived（同一个属性名在不同构建里可以用不同公式）：

```csharp
var warrior = new AttributeOwnerBuilder(library)
    .Require("strength")                          // base：只要名字
    .Require("dexterity")
    .Require("attack", "strength+0.5*dexterity")  // derived：多传一个表达式
    .Build();

// 同一个库、同一个 attack spec，法师声明另一条公式
var mage = new AttributeOwnerBuilder(library)
    .Require("intelligence")
    .Require("attack", "intelligence*1.2")
    .Build();
```

场景公式建议收在使用方自己的静态工厂里（如 `OwnerFactory.Warrior(library)`），避免散落。构建期会完成全部校验，任何问题都直接抛异常、不会带到运行期：

| 错误 | 异常信息包含 |
|---|---|
| Require 的属性名未登记 | 缺失的属性名 |
| 同一属性名冲突声明（base/derived 混合或重复表达式） | 属性名 |
| Require 传入空白表达式（传入时即抛） | 参数名 |
| 表达式语法错误（无法解析） | 表达式原文 |
| 表达式引用了本次未构建的属性名 | 表达式原文 + 属性名 |
| 表达式循环依赖（含自引用） | 环路上的属性链 |

同一属性名重复声明为 base 是幂等的，其余重复声明一律在构建期报错。

### 用原型克隆批量生产 owner

`Build()` 的产物可以当原型：`Clone()` 得到同构成的新实例——值从初始值开始，缓存与事件订阅全新，实例间互不影响。批量生产同类实体（小怪、召唤物等）时先构建原型、再逐个克隆：

```csharp
var warriorPrototype = new AttributeOwnerBuilder(library)
    .Require("strength")
    .Require("attack", "strength * 2")
    .Build();

var hero1 = warriorPrototype.Clone();
var hero2 = warriorPrototype.Clone();   // 互相独立；克隆也可继续作为原型再克隆
```

注意：克隆体与原型共享内部编译产物，**并发求值不安全**——插件所有 API 仅主线程使用。

## 第 4 步：使用 Owner

owner 运行时只认 attributeName：

```csharp
// base：直接写，写入值自动钳制到 [min, max]
warrior.SetValue("strength", 10);
warrior.SetValue("dexterity", 6);

// derived：只读，首次读取时计算并缓存
var attack = warrior.GetValue("attack");   // 10 + 0.5*6 = 13
// warrior.SetValue("attack", 999);        // 抛异常：derived 不可写

// 修改依赖后 derived 自动失效，下次读取用新值重算
warrior.SetValue("strength", 15);
attack = warrior.GetValue("attack");       // 15 + 3 = 18
```

需要属性对象本身时按名获取：`warrior.Get("attack")` 或 `warrior["attack"]`（名字不存在时抛异常）。

## 第 5 步：订阅变化

```csharp
// base：值变化时收到旧值与新值；写入相同的值不触发
warrior.Get("strength").ValueChanged += (_, e) =>
    GD.Print($"{e.Name}: {e.OldValue} -> {e.NewValue}");

// derived：依赖变化时收到"失效"通知。
// e.NewValue 是惰性的——处理器里访问它才触发重算，不访问不求值
warrior.Get("attack").ValueChanged += (_, e) =>
    GD.Print($"attack 已失效，新值是 {e.NewValue}");
```

说明：

- 通知按依赖顺序分发：`attack` 先于依赖它的 `total` 收到。
- 处理器里再做订阅/退订是安全的（当次分发不受影响）。
- 处理器抛出的异常会向上传播给触发变更的调用方。

## 表达式速查

```text
strength+0.5*dexterity              四则运算、括号、数值字面量
Max(attack - debuff, 0)             NCalc 内置函数：Max/Min/Abs 等
if(health < 0.2, attack*2, attack)  三元条件
total = attack + 5                  可以引用其它 derived 属性
```

- 标识符与属性名按字面精确匹配（区分大小写）。
- 整型别名下注意除法截断（`/` 按整型规则），建议用乘法与整数字面量。
- 表达式基于 NCalc 对标准数值类型求值；若别名指向 NCalc 不支持的奇异 `INumber` 实现，表达式会在转换时报错——此类项目建议不用表达式，以代码侧逻辑替代派生计算。

## 注意事项

- 所有 API 仅主线程使用（内部无锁）。
- spec 的 min/max 是硬性安全护栏（防负数、防溢出），写入与派生结果都会被钳制；"血量上限"这类会变化的游戏上限请建模为独立 attribute，再通过表达式参与计算（如 `Clamp(health, 0, maxHealth)`）。
- derived 的值永远等于"用依赖当前值重算"的结果，把它当只读视图即可。

## 运行测试

```bash
dotnet test
```

测试为纯逻辑测试（无需启动 Godot）；Godot 编辑器内的测试面板由 `addons/gdUnit4/` 提供，已随仓库启用。

