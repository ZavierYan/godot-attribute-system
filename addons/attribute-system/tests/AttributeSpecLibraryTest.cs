using GdUnit4;
using static GdUnit4.Assertions;
using static AttributeSystem.Tests.TestHelpers;

namespace AttributeSystem.Tests;

[TestSuite]
public class AttributeSpecLibraryTest // NOSONAR S1118: gdUnit4 fixture 要求公共无参构造器，与静态类/保护构造器冲突
{
    [TestCase]
    public static void RegisterThenGetReturnsSameInstance()
    {
        var library = new AttributeSpecLibrary();
        var spec = new AttributeSpec(TestNames.Attack, 0, 999);

        library.Register(spec);

        AssertBool(library.Contains(TestNames.Attack)).IsTrue();
        AssertThat(library.Get(TestNames.Attack)).IsSame(spec);
    }

    [TestCase]
    public static void RegisterDuplicateAttributeNameIsRejected()
    {
        var library = new AttributeSpecLibrary();
        library.Register(new AttributeSpec(TestNames.Attack, 0, 999));

        var caught = Catch(() => library.Register(new AttributeSpec(TestNames.Attack, 0, 100)));

        AssertThat(caught).IsInstanceOf<InvalidOperationException>();
        AssertThat(caught!.Message).Contains(TestNames.Attack);
    }

    [TestCase]
    public static void GetMissingAttributeNameThrowsWithName()
    {
        var library = new AttributeSpecLibrary();

        var caught = Catch(() => library.Get("hero-mana"));

        AssertThat(caught).IsInstanceOf<KeyNotFoundException>();
        AssertThat(caught!.Message).Contains("hero-mana");
    }

    [TestCase]
    public static void TryGetAndContainsReflectRegistration()
    {
        var library = new AttributeSpecLibrary();
        library.Register(new AttributeSpec(TestNames.Attack));

        AssertBool(library.TryGet(TestNames.Attack, out var spec)).IsTrue();
        AssertThat(spec.AttributeName).IsEqual(TestNames.Attack);
        AssertBool(library.TryGet("mana", out _)).IsFalse();
        AssertBool(library.Contains("mana")).IsFalse();
    }
}
