using System.Text.Json;
using System.Text.Json.Serialization;

namespace PTrampert.Optionals.Test;

public class OptionalJsonConverterFactoryTests
{
    private JsonSerializerOptions Options { get; set; }

    [SetUp]
    public void Setup()
    {
        Options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
        };
        
        Options.Converters.Add(new OptionalJsonConverterFactory());
    }

    [Test]
    public void Deserialize_WithAllProperties_ReturnsObjectWhereOptionalsAllHaveValues()
    {
        var json = """
        {
            "stringProp": "test",
            "intProp": 42,
            "nullableIntProp": null,
            "nestedObjectProp": {
                "id": 1
            }
        }
        """;
        
        var result = JsonSerializer.Deserialize<TestObject>(json, Options);
        
        Assert.Multiple(() => {
            Assert.That(result, Is.EqualTo(new TestObject
            {
                StringProp = "test",
                IntProp = 42,
                NullableIntProp = null,
                NestedObjectProp = new TestNestedObject { Id = 1 }
            }));
            Assert.That(result?.StringProp.HasValue, Is.True);
            Assert.That(result?.IntProp.HasValue, Is.True);
            Assert.That(result?.NullableIntProp.HasValue, Is.True);
            Assert.That(result?.NestedObjectProp.HasValue, Is.True);
        });
    }
    
    [Test]
    public void Deserialize_WhereStringPropNotGiven_ReturnsObjectWhereStringPropHasNoValue()
    {
        var json = """
                   {
                       "intProp": 42,
                       "nullableIntProp": null,
                       "nestedObjectProp": {
                           "id": 1
                       }
                   }
                   """;
        
        var result = JsonSerializer.Deserialize<TestObject>(json, Options);
        
        Assert.Multiple(() => {
            Assert.That(result, Is.EqualTo(new TestObject
            {
                IntProp = 42,
                NullableIntProp = null,
                NestedObjectProp = new TestNestedObject { Id = 1 }
            }));
            Assert.That(result?.StringProp.HasValue, Is.False);
            Assert.That(result?.IntProp.HasValue, Is.True);
            Assert.That(result?.NullableIntProp.HasValue, Is.True);
            Assert.That(result?.NestedObjectProp.HasValue, Is.True);
        });
    }
    
    [Test]
    public void Deserialize_WhereIntPropNotGiven_ReturnsObjectWhereIntPropHasNoValue()
    {
        var json = """
                   {
                       "stringProp": "test",
                       "nullableIntProp": null,
                       "nestedObjectProp": {
                           "id": 1
                       }
                   }
                   """;
        
        var result = JsonSerializer.Deserialize<TestObject>(json, Options);
        
        Assert.Multiple(() => {
            Assert.That(result, Is.EqualTo(new TestObject
            {
                StringProp = "test",
                NullableIntProp = null,
                NestedObjectProp = new TestNestedObject { Id = 1 }
            }));
            Assert.That(result?.StringProp.HasValue, Is.True);
            Assert.That(result?.IntProp.HasValue, Is.False);
            Assert.That(result?.NullableIntProp.HasValue, Is.True);
            Assert.That(result?.NestedObjectProp.HasValue, Is.True);
        });
    }
    
    [Test]
    public void Deserialize_NullableIntPropNotGiven_ReturnsObjectWhereNullableIntPropHasNoValue()
    {
        var json = """
                   {
                       "stringProp": "test",
                       "intProp": 42,
                       "nestedObjectProp": {
                           "id": 1
                       }
                   }
                   """;
        
        var result = JsonSerializer.Deserialize<TestObject>(json, Options);
        
        Assert.Multiple(() => {
            Assert.That(result, Is.EqualTo(new TestObject
            {
                StringProp = "test",
                IntProp = 42,
                NestedObjectProp = new TestNestedObject { Id = 1 }
            }));
            Assert.That(result?.StringProp.HasValue, Is.True);
            Assert.That(result?.IntProp.HasValue, Is.True);
            Assert.That(result?.NullableIntProp.HasValue, Is.False);
            Assert.That(result?.NestedObjectProp.HasValue, Is.True);
        });
    }
    
    [Test]
    public void Serialize_WithAllProperties_ReturnsJsonWithOptionalsAllHavingValues()
    {
        var obj = new TestObject
        {
            StringProp = "test",
            IntProp = 42,
            NullableIntProp = null,
            NestedObjectProp = new TestNestedObject { Id = 1 }
        };
        
        var json = JsonSerializer.Serialize(obj, Options);
        
        Assert.That(json, Is.EqualTo("""
        {
          "stringProp": "test",
          "intProp": 42,
          "nullableIntProp": null,
          "nestedObjectProp": {
            "id": 1
          }
        }
        """));
    }

    [Test]
    public void Serialize_WhereStringPropNotGiven_ReturnsJsonWithStringPropHavingNoValue()
    {
        var obj = new TestObject
        {
            IntProp = 42,
            NullableIntProp = null,
            NestedObjectProp = new TestNestedObject { Id = 1 }
        };
        
        var json = JsonSerializer.Serialize(obj, Options);
        Assert.That(json, Is.EqualTo("""
        {
          "intProp": 42,
          "nullableIntProp": null,
          "nestedObjectProp": {
            "id": 1
          }
        }
        """));
    }
    
    [Test]
    public void Serialize_WhereNullableIntPropNotGiven_ReturnsJsonWithNullableIntPropHavingNoValue()
    {
        var obj = new TestObject
        {
            StringProp = "test",
            IntProp = 42,
            NestedObjectProp = new TestNestedObject { Id = 1 }
        };
        
        var json = JsonSerializer.Serialize(obj, Options);
        Assert.That(json, Is.EqualTo("""
        {
          "stringProp": "test",
          "intProp": 42,
          "nestedObjectProp": {
            "id": 1
          }
        }
        """));
    }

    private record TestObject
    {
        public Optional<string> StringProp { get; init; }
        public Optional<int> IntProp { get; init; }
        public Optional<int?> NullableIntProp { get; init; }
        public Optional<TestNestedObject> NestedObjectProp { get; init; }
    }
    
    private record TestNestedObject
    {
        public int Id { get; init; }
    }
}