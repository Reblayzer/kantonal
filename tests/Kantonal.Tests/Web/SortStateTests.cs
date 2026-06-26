using Kantonal.Web.Models;

namespace Kantonal.Tests.Web;

public class SortStateTests
{
    [Fact]
    public void Default_IsMunicipalityNameAscending()
    {
        Assert.Equal("MunicipalityName", SortState.Default.Field);
        Assert.Equal(SortState.Asc, SortState.Default.Direction);
    }

    [Fact]
    public void Toggle_SameField_FlipsDirection()
    {
        var asc = new SortState("Year", SortState.Asc);
        var flipped = asc.Toggle("Year");
        Assert.Equal("Year", flipped.Field);
        Assert.Equal(SortState.Desc, flipped.Direction);
        Assert.Equal(SortState.Asc, flipped.Toggle("Year").Direction);
    }

    [Fact]
    public void Toggle_NewField_SelectsItAscending()
    {
        var state = new SortState("Year", SortState.Desc);
        var next = state.Toggle("SelfFinancingRatio");
        Assert.Equal("SelfFinancingRatio", next.Field);
        Assert.Equal(SortState.Asc, next.Direction);
    }
}
