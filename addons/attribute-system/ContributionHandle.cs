namespace AttributeSystem;

/// <summary>
/// 贡献句柄：向聚合栈投贡献的返回凭证，撤销对应贡献条目的唯一入口。
/// 句柄在签发时绑定 owner（<see cref="Owner"/>）与目标属性名（<see cref="TargetName"/>，
/// 供 owner 的撤销原语定位宿主聚合栈），且是一次性凭证——撤销后
/// （<see cref="Revoked"/> 为真）即失效；跨 owner 撤销、重复撤销由 owner 守卫抛异常，
/// 本类只承载状态。句柄对象身份即贡献条目身份：聚合栈按对象引用匹配撤销目标，
/// 不引入额外 id。
/// <para>设计注记：写时复制/克隆复制桶列表时，贡献条目与句柄对象原样保留
/// （桶 List 深拷贝、条目共享），因此原型上签发的句柄在克隆上使用会因
/// <see cref="Owner"/> 不符被"跨 owner"守卫拒绝——这正是规格要求的克隆族隔离行为。</para>
/// </summary>
public sealed class ContributionHandle
{
    /// <summary>签发本句柄的 owner；句柄只在该 owner 上可用。</summary>
    internal AttributeOwner Owner { get; }

    /// <summary>贡献的目标属性名（宿主聚合栈），撤销时据此定位。</summary>
    internal string TargetName { get; }

    /// <summary>是否已撤销。撤销后句柄一次性失效。</summary>
    internal bool Revoked { get; set; }

    internal ContributionHandle(AttributeOwner owner, string targetName)
    {
        Owner = owner;
        TargetName = targetName;
    }
}
