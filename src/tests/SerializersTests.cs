using System.Collections.Generic;
using System.IO;

using NUnit.Framework;

using miloRPC.Serialization;

namespace miloRPC.Tests;

[TestFixture]
public class SerializersTests
{
    [Test]
    public void Serialize_List_Of_Integers()
    {
        List<int> originalList = new() {1, 2, 3};

        using MemoryStream ms = new();
        using BinaryWriter writer = new(ms);

        Serializer<List<int>>.Serialize(writer, originalList);

        ms.Position = 0;

        using BinaryReader reader = new(ms);

        List<int>? deserializedList = Serializer<List<int>>.Deserialize(reader);

        Assert.That(deserializedList, Is.Not.Null.And.Count.EqualTo(3));
        Assert.That(deserializedList![0], Is.EqualTo(1));
        Assert.That(deserializedList[1], Is.EqualTo(2));
        Assert.That(deserializedList[2], Is.EqualTo(3));
    }

    [Test]
    public void Serialize_List_Of_Nullable_Integers()
    {
        List<int?> originalList = new() {1, null, 3};

        using MemoryStream ms = new();
        using BinaryWriter writer = new(ms);

        Serializer<List<int?>>.Serialize(writer, originalList);

        ms.Position = 0;

        using BinaryReader reader = new(ms);

        List<int?>? deserializedList = Serializer<List<int?>>.Deserialize(reader);

        Assert.That(deserializedList, Is.Not.Null.And.Count.EqualTo(3));
        Assert.That(deserializedList![0], Is.Not.Null.And.EqualTo(1));
        Assert.That(deserializedList[1], Is.Null);
        Assert.That(deserializedList[2], Is.Not.Null.And.EqualTo(3));
    }

    [Test]
    public void Serialize_List_Of_Strings()
    {
        List<string?> originalList = new() {"Sergio", null, "Para"};

        using MemoryStream ms = new();
        using BinaryWriter writer = new(ms);

        Serializer<List<string?>>.Serialize(writer, originalList);

        ms.Position = 0;

        using BinaryReader reader = new(ms);

        List<string?>? deserializedList = Serializer<List<string?>>.Deserialize(reader);

        Assert.That(deserializedList, Is.Not.Null.And.Count.EqualTo(3));
        Assert.That(deserializedList![0], Is.Not.Null.And.EqualTo("Sergio"));
        Assert.That(deserializedList[1], Is.Null);
        Assert.That(deserializedList[2], Is.Not.Null.And.EqualTo("Para"));
    }
}
