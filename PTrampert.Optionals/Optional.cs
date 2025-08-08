using System.ComponentModel.DataAnnotations;

namespace PTrampert.Optionals;

public interface IOptional
{
    Type ValueType { get; }
    object UntypedValue { get; }
    bool HasValue { get; }
}

public record struct Optional<T> : IOptional
{
    public Type ValueType => typeof(T);
    public object UntypedValue => Value;
    public T Value { get; init; }
    
    public bool HasValue { get; init; }

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

    public static bool operator ==(T left, Optional<T> right)
    {
        return right == left;
    }
    
    public static bool operator !=(Optional<T> left, T right)
    {
        return !(left == right);
    }
    
    public static bool operator !=(T left, Optional<T> right)
    {
        return !(left == right);
    }
    
    public static implicit operator Optional<T>(T value)
    {
        return new Optional<T>(value);
    }
}