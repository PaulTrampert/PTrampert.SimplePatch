using System.ComponentModel.DataAnnotations;

namespace PTrampert.Optionals;

internal interface IOptional
{
    object UntypedValue { get; }
    bool HasValue { get; }
}

public record struct Optional<T> : IOptional
{
    public object UntypedValue => Value;
    public T Value { get; }
    
    public bool HasValue { get; }

    public Optional()
    {
        HasValue = false;
    }

    public Optional(T value)
    {
        Value = value;
        HasValue = true;
    }
    
    public static bool operator ==(Optional<T> left, T right)
    {
        ArgumentNullException.ThrowIfNull(left);
        if (!left.HasValue)
            return false;
        if (left.Value != null)
            return left.Value.Equals(right);
        return right == null;
    }
    
    public static bool operator !=(Optional<T> left, T right)
    {
        return !(left == right);
    }
    
    public static implicit operator Optional<T>(T value)
    {
        return new Optional<T>(value);
    }
}