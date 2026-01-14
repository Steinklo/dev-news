using DevNews.Domain.Common;
using DevNews.Domain.NewsItem.Enums;

namespace DevNews.Domain.NewsItem.ValueObjects;

/// <summary>
/// Value object representing a typed news category to avoid magic strings
/// </summary>
public class NewsCategory : ValueObject
{
    public CategoryEnum Value { get; private set; }

    private NewsCategory(CategoryEnum value)
    {
        Value = value;
    }

    public static ResultResponse<NewsCategory> Create(CategoryEnum category)
    {
        if (!Enum.IsDefined(typeof(CategoryEnum), category))
            return ResultResponse<NewsCategory>.Failure("Invalid category value");

        return ResultResponse<NewsCategory>.Success(new NewsCategory(category));
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();

    public static implicit operator CategoryEnum(NewsCategory category) => category.Value;

    internal static NewsCategory Reconstitute(CategoryEnum value) => new(value);
}
