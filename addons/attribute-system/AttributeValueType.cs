// ============================================================================
// AttributeValueType —— 属性系统全局值类型别名（全插件唯一的类型声明点）
//
// 切换值类型：把下方的 System.Single 改为任意满足 INumber<T> 约束的数值类型
// （如 System.Int32、System.Double、System.Decimal），然后重新编译宿主项目。
// 插件内所有属性 API（Attribute / AttributeOwner / AttributeSpec / 事件参数 /
// 计算委托）的值类型都会随之切换，无需修改插件其它源码。
//
// 注意：别名是编译期展开的，因此本插件以源码形式随宿主项目一起编译；
// 预编译 DLL 分发方式无法更换值类型。
// ============================================================================
global using AttributeValueType = System.Single;
