using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace PTrampert.SimplePatch;

internal interface IOptional
{
    [JsonIgnore]
    object? UntypedValue { get; }
    bool HasValue { get; }
}

/// <summary>
/// Represents an optional value of type T.
/// This struct can be used to represent a value that may or may not be present, distinct from
/// being null. It is useful for scenarios where a value is not always required, such as in patch operations.
/// </summary>
/// <typeparam name="T">The underlying value type. May or may not be nullable.</typeparam>
public record struct Optional<T> : IOptional
{
    /// <summary>
    /// Same as <see cref="Value"/>, but untyped.
    /// </summary>
    public object? UntypedValue => Value;
    
    /// <summary>
    /// The value of type T. If no value is present, this will be the default value for T.
    /// </summary>
    public T Value { get; }
    
    /// <summary>
    /// Indicates whether this Optional has a value.
    /// </summary>
    public bool HasValue { get; }

    /// <summary>
    /// Default constructor for Optional.
    /// Initializes an Optional with no value, meaning HasValue will be false.
    /// </summary>
    public Optional()
    {
        HasValue = false;
    }

    /// <summary>
    /// Initializes an Optional with a value of type T.
    /// This constructor sets the Value property to the provided value and HasValue to true.
    /// If the provided value is null (or the default value for T), this will still create an Optional with HasValue set to true.
    /// </summary>
    /// <param name="value">The value of this Optional.</param>
    public Optional(T value)
    {
        Value = value;
        HasValue = true;
    }
    
    /// <summary>
    /// Allows for checking of equality between an Optional and an instance of T.
    /// If the optional has no value, it will return false.
    /// If the optional has a value, it will compare that value to the provided instance of T.
    /// </summary>
    /// <param name="left">The optional.</param>
    /// <param name="right">The instance of T</param>
    /// <returns>True if the Optional has a value and the values are equal. False otherwise.</returns>
    public static bool operator ==(Optional<T> left, T right)
    {
        ArgumentNullException.ThrowIfNull(left);
        if (!left.HasValue)
            return false;
        if (left.Value != null)
            return left.Value.Equals(right);
        return right == null;
    }
    
    /// <summary>
    /// The exact inverse of the equality operator.
    /// </summary>
    /// <param name="left">The optional.</param>
    /// <param name="right">The instance of T</param>
    /// <returns>False if the optional has no value or the value is not equal to right. True otherwise.</returns>
    public static bool operator !=(Optional<T> left, T right)
    {
        return !(left == right);
    }
    
    /// <summary>
    /// Implicit conversion from an instance of T to an Optional&lt;T&gt;.
    /// This allows for easy creation of an Optional from a value of type T.
    /// If the value is null (or the default value for T), it will still create an Optional with HasValue set to true.
    /// </summary>
    /// <param name="value">The value to create the optional from.</param>
    /// <returns>An Optional&lt;T&gt;</returns>
    public static implicit operator Optional<T>(T value)
    {
        return new Optional<T>(value);
    }
}