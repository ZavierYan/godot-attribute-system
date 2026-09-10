using GdUnit4;
using static GdUnit4.Assertions;
using static AttributeSystem.Tests.TestHelpers;

namespace AttributeSystem.Tests;

[TestSuite]
public class AttributeSpecLibraryTest
{
    [TestCase]
    public void RegisterThenGetReturnsSameInstance()
    {
        var library = new AttributeSpecLibrary();
        var spec = new AttributeSpec("attack", 0, 999);

        library.Register(spec);

        AssertBool(library.Contains("attack")).IsTrue();
        AssertThat(library.Get("attack")).IsSame(spec);
    }

    [TestCase]
    public void RegisterDuplicateAttributeNameIsRejected()
    {
        var library = new AttributeSpecLibrary();
        library.Register(new AttributeSpec("attack", 0, 999));

        var caught = Catch(() => library.Register(new AttributeSpec("attack", 0, 100)));

        AssertThat(caught).IsInstanceOf<InvalidOperationException>();
        AssertThat(caught!.Message).Contains("attack");
    }

    [TestCase]
    public void GetMissingAttributeNameThrowsWithName()
    {
        var library = new AttributeSpecLibrary();

        var caught = Catch(() => library.Get("hero-mana"));

        AssertThat(caught).IsInstanceOf<KeyNotFoundException>();
        AssertThat(caught!.Message).Contains("hero-mana");
    }

    [TestCase]
    public void TryGetAndContainsReflectRegistration()
    {
        var library = new AttributeSpecLibrary();
        library.Register(new AttributeSpec("attack"));

        AssertBool(library.TryGet("attack", out var spec)).IsTrue();
        AssertThat(spec!.AttributeName).IsEqual("attack");
        AssertBool(library.TryGet("mana", out _)).IsFalse();
        AssertBool(library.Contains("mana")).IsFalse();
    }
}
