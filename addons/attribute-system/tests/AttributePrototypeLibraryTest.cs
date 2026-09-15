using GdUnit4;
using static GdUnit4.Assertions;
using static AttributeSystem.Tests.TestHelpers;

namespace AttributeSystem.Tests;

[TestSuite]
public class AttributePrototypeLibraryTest
{
    private static AttributeSpecLibrary CreateSpecLibrary()
    {
        var library = new AttributeSpecLibrary();
        library.Register(new AttributeSpec("strength", 0, 100));
        library.Register(new AttributeSpec("attack", 0, 999));
        return library;
    }

    private static AttributeOwnerBuilder CreateWarriorBuilder(AttributeSpecLibrary specLibrary)
        => new AttributeOwnerBuilder(specLibrary)
            .Require("strength", (AttributeValueType)15)
            .Require("attack", "strength * 2");

    [TestCase]
    public void RegisterThenCloneCarriesInitialValues()
    {
        var library = new AttributePrototypeLibrary();
        library.Register("warrior", CreateWarriorBuilder(CreateSpecLibrary()));

        var warrior = library.Clone("warrior");

        AssertThat(warrior.GetValue("strength")).IsEqual((AttributeValueType)15);
        AssertThat(warrior.GetValue("attack")).IsEqual((AttributeValueType)30);
    }

    [TestCase]
    public void RegisterDuplicatePrototypeNameIsRejected()
    {
        var specLibrary = CreateSpecLibrary();
        var library = new AttributePrototypeLibrary();
        library.Register("warrior", CreateWarriorBuilder(specLibrary));

        var caught = Catch(() => library.Register(
            "warrior", new AttributeOwnerBuilder(specLibrary).Require("strength")));

        AssertThat(caught).IsInstanceOf<InvalidOperationException>();
        AssertThat(caught!.Message).Contains("warrior");
    }

    [TestCase]
    public void RegisterBlankPrototypeNameThrowsImmediately()
    {
        var library = new AttributePrototypeLibrary();
        var builder = CreateWarriorBuilder(CreateSpecLibrary());

        var emptyName = Catch(() => library.Register("", builder));
        var blankName = Catch(() => library.Register("   ", builder));

        AssertThat(emptyName).IsInstanceOf<ArgumentException>();
        AssertThat(blankName).IsInstanceOf<ArgumentException>();
    }

    [TestCase]
    public void RegisterNullBuilderThrows()
    {
        var library = new AttributePrototypeLibrary();

        var caught = Catch(() => library.Register("warrior", null!));

        AssertThat(caught).IsInstanceOf<ArgumentNullException>();
    }

    [TestCase]
    public void CloneUnregisteredPrototypeNameThrowsWithName()
    {
        var library = new AttributePrototypeLibrary();

        var caught = Catch(() => library.Clone("rogue"));

        AssertThat(caught).IsInstanceOf<KeyNotFoundException>();
        AssertThat(caught!.Message).Contains("rogue");
    }

    [TestCase]
    public void TryCloneAndContainsReflectRegistration()
    {
        var library = new AttributePrototypeLibrary();
        library.Register("warrior", CreateWarriorBuilder(CreateSpecLibrary()));

        AssertBool(library.TryClone("warrior", out var warrior)).IsTrue();
        AssertThat(warrior!.GetValue("strength")).IsEqual((AttributeValueType)15);
        AssertBool(library.Contains("warrior")).IsTrue();
        AssertBool(library.TryClone("rogue", out _)).IsFalse();
        AssertBool(library.Contains("rogue")).IsFalse();
    }

    [TestCase]
    public void BuildErrorsSurfaceAtRegistration()
    {
        var library = new AttributePrototypeLibrary();

        // 声明了 spec 库中不存在的属性名：登记时暴露，而非留到首次克隆
        var caught = Catch(() => library.Register(
            "broken", new AttributeOwnerBuilder(CreateSpecLibrary()).Require("hero-mana")));

        AssertThat(caught).IsInstanceOf<KeyNotFoundException>();
        AssertThat(caught!.Message).Contains("hero-mana");
    }

    [TestCase]
    public void PrototypeNamesEnumeratesRegisteredPrototypes()
    {
        var specLibrary = CreateSpecLibrary();
        var library = new AttributePrototypeLibrary();
        library.Register("warrior", CreateWarriorBuilder(specLibrary));
        library.Register("mage", new AttributeOwnerBuilder(specLibrary).Require("strength"));

        var names = library.PrototypeNames;

        AssertThat(names.Count).IsEqual(2);
        AssertBool(names.Contains("warrior")).IsTrue();
        AssertBool(names.Contains("mage")).IsTrue();
    }
}
