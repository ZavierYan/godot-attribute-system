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
        var spec = new AttributeSpec("warrior-attack", "attack", expression: "strength*2");

        library.Register(spec);

        AssertBool(library.Contains("warrior-attack")).IsTrue();
        AssertThat(library.Get("warrior-attack")).IsSame(spec);
    }

    [TestCase]
    public void RegisterDuplicateSpecNameIsRejected()
    {
        var library = new AttributeSpecLibrary();
        library.Register(new AttributeSpec("warrior-attack", "attack", expression: "strength*2"));

        var caught = Catch(() =>
            library.Register(new AttributeSpec("warrior-attack", "attack", expression: "strength*3")));

        AssertThat(caught).IsInstanceOf<InvalidOperationException>();
        AssertThat(caught!.Message).Contains("warrior-attack");
    }

    [TestCase]
    public void GetMissingSpecNameThrowsWithName()
    {
        var library = new AttributeSpecLibrary();

        var caught = Catch(() => library.Get("hero-mana"));

        AssertThat(caught).IsInstanceOf<KeyNotFoundException>();
        AssertThat(caught!.Message).Contains("hero-mana");
    }

    [TestCase]
    public void SpecsSharingAttributeNameCanBeRegistered()
    {
        var library = new AttributeSpecLibrary();
        library.Register(new AttributeSpec("warrior-attack", "attack", expression: "strength*2"));

        library.Register(new AttributeSpec("mage-attack", "attack", expression: "intelligence*1.2"));

        AssertBool(library.Contains("warrior-attack")).IsTrue();
        AssertBool(library.Contains("mage-attack")).IsTrue();
    }
}
