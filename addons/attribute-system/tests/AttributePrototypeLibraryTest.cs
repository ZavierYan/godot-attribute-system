using GdUnit4;
using static GdUnit4.Assertions;
using static AttributeSystem.Tests.TestHelpers;

namespace AttributeSystem.Tests;

[TestSuite]
public class AttributePrototypeLibraryTest // NOSONAR S1118: gdUnit4 fixture 要求公共无参构造器，与静态类/保护构造器冲突
{
    private static AttributeSpecLibrary CreateSpecLibrary()
    {
        var library = new AttributeSpecLibrary();
        library.Register(new AttributeSpec(TestNames.Strength, 0, 100));
        library.Register(new AttributeSpec("attack", 0, 999));
        return library;
    }

    private static AttributeOwnerBuilder CreateWarriorBuilder(AttributeSpecLibrary specLibrary)
        => new AttributeOwnerBuilder(specLibrary)
            .Require(TestNames.Strength, (AttributeValueType)15)
            .Require("attack", "strength * 2");

    [TestCase]
    public static void RegisterThenCloneCarriesInitialValues()
    {
        var library = new AttributePrototypeLibrary();
        library.Register(TestNames.Warrior, CreateWarriorBuilder(CreateSpecLibrary()));

        var warrior = library.Clone(TestNames.Warrior);

        AssertThat(warrior.GetValue(TestNames.Strength)).IsEqual((AttributeValueType)15);
        AssertThat(warrior.GetValue("attack")).IsEqual((AttributeValueType)30);
    }

    [TestCase]
    public static void RegisterDuplicatePrototypeNameIsRejected()
    {
        var specLibrary = CreateSpecLibrary();
        var library = new AttributePrototypeLibrary();
        library.Register(TestNames.Warrior, CreateWarriorBuilder(specLibrary));

        var caught = Catch(() => library.Register(
            TestNames.Warrior, new AttributeOwnerBuilder(specLibrary).Require(TestNames.Strength)));

        AssertThat(caught).IsInstanceOf<InvalidOperationException>();
        AssertThat(caught!.Message).Contains(TestNames.Warrior);
    }

    [TestCase]
    public static void RegisterBlankPrototypeNameThrowsImmediately()
    {
        var library = new AttributePrototypeLibrary();
        var builder = CreateWarriorBuilder(CreateSpecLibrary());

        var emptyName = Catch(() => library.Register("", builder));
        var blankName = Catch(() => library.Register("   ", builder));

        AssertThat(emptyName).IsInstanceOf<ArgumentException>();
        AssertThat(blankName).IsInstanceOf<ArgumentException>();
    }

    [TestCase]
    public static void RegisterNullBuilderThrows()
    {
        var library = new AttributePrototypeLibrary();

        var caught = Catch(() => library.Register(TestNames.Warrior, null!));

        AssertThat(caught).IsInstanceOf<ArgumentNullException>();
    }

    [TestCase]
    public static void CloneUnregisteredPrototypeNameThrowsWithName()
    {
        var library = new AttributePrototypeLibrary();

        var caught = Catch(() => library.Clone(TestNames.Rogue));

        AssertThat(caught).IsInstanceOf<KeyNotFoundException>();
        AssertThat(caught!.Message).Contains(TestNames.Rogue);
    }

    [TestCase]
    public static void TryCloneAndContainsReflectRegistration()
    {
        var library = new AttributePrototypeLibrary();
        library.Register(TestNames.Warrior, CreateWarriorBuilder(CreateSpecLibrary()));

        AssertBool(library.TryClone(TestNames.Warrior, out var warrior)).IsTrue();
        AssertThat(warrior.GetValue(TestNames.Strength)).IsEqual((AttributeValueType)15);
        AssertBool(library.Contains(TestNames.Warrior)).IsTrue();
        AssertBool(library.TryClone(TestNames.Rogue, out _)).IsFalse();
        AssertBool(library.Contains(TestNames.Rogue)).IsFalse();
    }

    [TestCase]
    public static void BuildErrorsSurfaceAtRegistration()
    {
        var library = new AttributePrototypeLibrary();

        // 声明了 spec 库中不存在的属性名：登记时暴露，而非留到首次克隆
        var caught = Catch(() => library.Register(
            "broken", new AttributeOwnerBuilder(CreateSpecLibrary()).Require("hero-mana")));

        AssertThat(caught).IsInstanceOf<KeyNotFoundException>();
        AssertThat(caught!.Message).Contains("hero-mana");
    }

    [TestCase]
    public static void PrototypeNamesEnumeratesRegisteredPrototypes()
    {
        var specLibrary = CreateSpecLibrary();
        var library = new AttributePrototypeLibrary();
        library.Register(TestNames.Warrior, CreateWarriorBuilder(specLibrary));
        library.Register("mage", new AttributeOwnerBuilder(specLibrary).Require(TestNames.Strength));

        var names = library.PrototypeNames();

        AssertThat(names.Count).IsEqual(2);
        AssertBool(names.Contains(TestNames.Warrior)).IsTrue();
        AssertBool(names.Contains("mage")).IsTrue();
    }
}
