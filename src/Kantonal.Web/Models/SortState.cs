namespace Kantonal.Web.Models;

/// <summary>Current table sort. Direction strings match the API's SortDirection enum.</summary>
public sealed record SortState(string Field, string Direction)
{
    public const string Asc = "Asc";
    public const string Desc = "Desc";

    public static readonly SortState Default = new("MunicipalityName", Asc);

    /// <summary>Clicking the active column flips direction; clicking a new column sorts it ascending.</summary>
    public SortState Toggle(string clickedField)
        => clickedField == Field
            ? this with { Direction = Direction == Asc ? Desc : Asc }
            : new SortState(clickedField, Asc);
}
