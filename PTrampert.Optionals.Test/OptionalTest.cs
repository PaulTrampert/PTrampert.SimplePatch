namespace PTrampert.Optionals.Test;

public class OptionalTest
{
    [Test]
    public void DefaultValue_IsNotNull()
    {
        Optional<string> optional = default;
        Assert.That(optional.HasValue, Is.False);
    }
    
    [Test]
    public void DefaultConstructor_ShouldHaveNoValue()
    {
        var optional = new Optional<string>();
        Assert.That(optional.HasValue, Is.False);
    }
    
    [Test]
    public void ConstructorWithValue_ShouldHaveValue()
    {
        var optional = new Optional<string>("test");
        Assert.Multiple(() =>
        {
            Assert.That(optional.HasValue, Is.True);
            Assert.That(optional.Value, Is.EqualTo("test"));
        });
    }

    [Test]
    public void ConstructorWithNullableValue_ShouldHaveValue()
    {
        var optional = new Optional<string?>(null);
        Assert.Multiple(() =>
        {
            Assert.That(optional.HasValue, Is.True);
            Assert.That(optional.Value, Is.Null);
        });
    }
    
    [Test]
    public void EqualityOperator_ShouldReturnTrueForEqualValues()
    {
        var optional = new Optional<string>("test");
        Assert.That(optional == "test", Is.True);
    }

    [Test]
    public void EqualityOperator_ShouldReturnFalseForDifferentValues()
    {
        var optional = new Optional<string>("test");
        Assert.That(optional == "different", Is.False);
    }

    [Test]
    public void InequalityOperator_ShouldReturnTrueForDifferentValues()
    {
        var optional = new Optional<string>("test");
        Assert.That(optional != "different", Is.True);
    }

    [Test]
    public void EqualityOperator_ShouldReturnFalseWhenOptionalHasNoValue()
    {
        var optional = new Optional<string?>();
        Assert.That(optional == null, Is.False);
    }
    
    [Test]
    public void EqualityOperator_ShouldReturnTrueWhenOptionalHasNullValue()
    {
        var optional = new Optional<string?>(null);
        Assert.That(optional == null, Is.True);
    }
    
    [Test]
    public void ImplicitConversion_ShouldCreateOptionalFromValue()
    {
        Optional<string> optional = "test";
        Assert.Multiple(() =>
        {
            Assert.That(optional.HasValue, Is.True);
            Assert.That(optional.Value, Is.EqualTo("test"));
        });
    }
    
    [Test]
    public void ImplicitConversion_ShouldCreateOptionalFromNull()
    {
        Optional<string?> optional = null;
        Assert.Multiple(() =>
        {
            Assert.That(optional.HasValue, Is.True);
            Assert.That(optional.Value, Is.Null);
        });
    }
}