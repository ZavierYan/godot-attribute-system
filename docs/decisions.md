# 技术决策

记录"为什么这样做"——取舍、被否掉的替代方案与后果。功能与用法见 [README](../README.md)，日常操作见 [DEVELOPMENT.md](DEVELOPMENT.md)。

## 1. 源码分发，而非预编译 DLL

插件以源码目录 + csproj 分发。根本原因：值类型别名（`AttributeValueType`）是编译期展开的 global using——DLL 用户无法把 float 换成 int/double/decimal；源码随宿主一起编译，所有 API 的数值类型跟随宿主切换。

代价：接入多一步 `Compile Remove`（防 CS0436）、升级靠复制目录。换取：单行切换值类型、用户可直接读改源码。这是本插件最核心的取舍。

## 2. 零 Godot API 依赖

插件程序集不引用任何 Godot 类型（Microsoft.NET.Sdk 而非 Godot.NET.Sdk）。原因：Godot 官方引擎不从引用程序集中发现 C# 脚本类——插件里定义 `Node`/`Resource` 派生类型宿主也用不了。

后果：场景层包装留给宿主实现；换来插件可在任何 .NET 8 环境做纯逻辑测试（gdUnit4 进程内跑，无需启动 Godot、无需设置 `GODOT_BIN`）。

## 3. 仅主线程，内部无锁

全库不加锁。技术根源：表达式编译为捕获 `CompiledExpression` 实例的委托，每次求值写 Parameters——同族克隆共享同一委托实例，并发求值互相污染。且游戏属性层天然主线程驱动（帧逻辑 / 事件分发）。

否掉的方案：per-call 参数隔离或共享委托加锁——复杂度不值得，游戏侧收益存疑。约束显式写进 README、XML doc 与 `AsCalculator` 注释。

## 4. 表达式即依赖声明（NCalc）

derived 的依赖关系不单独声明，从表达式 AST 提取标识符得到（`CompiledExpression` 构造时遍历）。函数名在语法树中不是 Identifier 节点，天然不参与依赖——依赖来源单一，声明与计算永不失配。

否掉的方案：依赖手工声明 + 表达式只做计算——两处声明必然漂移。NCalc 7.1 同时提供解析（语法错误构建期暴露）、求值与内置函数（`Max`/`Min`/`if` 等）。聚合栈的贡献沿用同一条路：贡献表达式自身的依赖并入聚合栈依赖并集。

## 5. 惰性求值 + 失效传播

写操作只做失效（置脏 + 通知），读取时才按依赖当前值重算并缓存。适合"一次变更触发一串派生、但每帧只读一次"的游戏循环。

代价：失效与重算之间的语义必须明确——文档化为"失效事件携带旧值，新值经读取获得"；连续变更经脏状态合并至多通知一次。

## 6. 两阶段快速失败的错误模型

构建期：Build 一次抛齐全部声明错误（缺 spec、冲突声明、语法错误、未知依赖、循环依赖）——构建产物不存在声明错误。运行期：变异先校验后变更，任何失败 owner 状态不变。

否掉的方案：容错跳过 / 返回 bool——错误被静默吞掉后属性图处于半配置状态，调试成本远高于抛异常。异常信息携带定位上下文（表达式原文、属性名、环路链）。

## 7. 克隆 + 写时复制的原型模式

批量实体（小怪、召唤物）先构建原型再 `Clone()`。四张内部表构建后约定不可变 → 克隆零拷贝共享；首次变异才私有化（COW，脏标记 `_tablesSharedWithClones`）。Attribute 级：base 继承当前值，derived 缓存空、按克隆自身 base 重算。

否掉的方案：每次全量 Build——重复编译表达式与拓扑，批量场景浪费；或深拷贝全部表——常态内存翻倍。COW 用一个脏标记同时拿到共享与隔离。

## 8. 拓扑算法单点（AttributeTopology）

builder 构建期与 owner 运行期变异（Add 族 / Contribute）共用同一 DFS 拓扑排序 + 环检测实现。运行期注入引入的新依赖图同样要排序与检环——两份实现漂移就是 bug 温床，故收敛为一个 internal 静态类。

## 9. 测试编入 Godot 主程序集

gdUnit4 编辑器面板的 C# 测试发现只扫描主程序集——测试拆独立 csproj 会让面板失效（试过并回退：fe77716 拆分 → dea033a 恢复）。因此 `tests/` 源码经主工程隐式通配编入 `attribute-system.dll`，插件 csproj 用 `InternalsVisibleTo("attribute-system")` 开放 internal。

代价：测试与主工程耦合、不能独立分发；收益：编辑器面板可用 + 进程内纯逻辑测试。操作约束见 [DEVELOPMENT.md](DEVELOPMENT.md)。

## 10. Directory.Build.targets 统一剔除 bin/obj

Godot.NET.Sdk 4.7.2 通配编译源码时未排除其它工程目录下的 bin/obj（4.6.3 会排除），且单 csproj 内追加 `DefaultItemExcludes` 对其通配目标不生效。故在所有 target 导入之后统一 `Compile Remove="**/bin/**/*.cs"` 等——否则生成物被重复编译（CS0579 程序集特性重复）。升级 SDK 版本后应复查此文件是否仍需要。

## 11. spec 库实例化，而非静态全局

`AttributeSpecLibrary` 是普通实例：多套定义共存、测试互不污染。场景差异（同名属性不同公式）本来就由构建声明表达，注册中心再全局化只会放大耦合——全局单例强制"全游戏一套属性定义"，没有任何好处。

## 12. 发布：git archive 打包，tag 为版本唯一来源

release.yml 以 `git archive` 打包（zip 顶层 README + `addons/attribute-system/` 前缀，命名 `attribute-system-<版本>.zip`，版本 = tag 去 `v` 前缀）——没有需要同步的版本元数据文件。守门（build + test）失败则不产生 Release。细节见 [DEVELOPMENT.md](DEVELOPMENT.md)。
