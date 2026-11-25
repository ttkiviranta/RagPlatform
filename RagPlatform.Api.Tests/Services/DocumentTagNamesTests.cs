using RagPlatform.Api.Services;

namespace RagPlatform.Api.Tests.Services;

[TestClass]
[TestCategory("Unit")]
public sealed class DocumentTagNamesTests
{
    [TestMethod]
    public void Normalize_TrimsSortsAndKeepsCaseDistinct()
    {
        var actual = DocumentTagNames.Normalize([" security ", "Identity", "identity"]);

        CollectionAssert.AreEqual(new[] { "Identity", "identity", "security" }, actual.ToArray());
        Assert.AreEqual(0, DocumentTagNames.Normalize(null).Count);
    }

    [TestMethod]
    public void Normalize_RejectsInvalidNamesDuplicatesAndExcessiveCount()
    {
        IReadOnlyCollection<string?>[] invalid =
        [
            [null],
            ["   "],
            [new string('a', 101)],
            ["one\ntwo"],
            ["tag", " tag "],
            Enumerable.Repeat<string?>("tag", 51).ToArray()
        ];

        foreach (var tags in invalid)
        {
            var exception = Assert.ThrowsExactly<ArgumentException>(() => DocumentTagNames.Normalize(tags));
            Assert.AreEqual("tags", exception.ParamName);
        }

        Assert.AreEqual(100, DocumentTagNames.Normalize([new string('a', 100)]).Single().Length);
        Assert.AreEqual(50, DocumentTagNames.Normalize(Enumerable.Range(0, 50).Select(index => (string?)$"tag{index}").ToArray()).Count);
    }
}
