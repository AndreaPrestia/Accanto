using Accanto.Admin.Application.Diagnostics;
using FluentAssertions;

namespace Accanto.Admin.Tests;

public class AdminTechnicalLogStoreTests
{
    [Fact]
    public void List_returns_only_the_most_recent_bounded_safe_events()
    {
        var store = new InMemoryAdminTechnicalLogStore();

        for (var index = 0; index < 205; index++)
            store.RecordUnhandledApiError();

        var result = store.List(500);

        result.Limit.Should().Be(200);
        result.RetainedCount.Should().Be(200);
        result.Items.Should().HaveCount(200);
        result.Items.Should().OnlyContain(entry =>
            entry.Level == "Error" &&
            entry.Component == "Admin API" &&
            entry.Event == "Unhandled API error" &&
            entry.Code == "ADMIN_API_500");
    }
}