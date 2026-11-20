namespace Wms.ArchitectureTests;

public sealed class ModuleBoundaryTests
{
    [Fact]
    public void Every_project_in_src_follows_the_reference_rules()
    {
        var projects = ReferenceRules.LoadFrom(RepoPaths.Source);

        projects.ShouldNotBeEmpty();
        ReferenceRules.Check(projects).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("Wms.Inbound.Domain", "Wms.Inventory.Domain")]               // domain to other module
    [InlineData("Wms.Inbound.Application", "Wms.Inventory.Application")]     // application to other module internals
    [InlineData("Wms.Inbound.Application", "Wms.Inventory.Infrastructure")]  // application to infrastructure
    [InlineData("Wms.Inbound.Infrastructure", "Wms.Inventory.Domain")]       // infrastructure to other module domain
    [InlineData("Wms.Inbound.Contracts", "Wms.Inbound.Domain")]              // contracts leaking domain types
    [InlineData("Wms.Inbound.Domain", "Wms.Application.Abstractions")]       // domain to application layer
    [InlineData("Wms.SharedKernel", "Wms.Platform.Contracts")]               // building block to module
    [InlineData("Wms.Inbound.Endpoints", "Wms.Inventory.Contracts")]         // endpoints call own application only
    public void Rules_reject_forbidden_references(string project, string reference)
    {
        var violations = ReferenceRules.Check([new ProjectInfo(project, [reference])]);

        violations.ShouldHaveSingleItem().ShouldBe($"{project} must not reference {reference}");
    }

    [Theory]
    [InlineData("Wms.Inbound.Application", "Wms.Inventory.Contracts")]
    [InlineData("Wms.Inbound.Application", "Wms.Inbound.Domain")]
    [InlineData("Wms.Inbound.Infrastructure", "Wms.Infrastructure.Common")]
    [InlineData("Wms.Inbound.Endpoints", "Wms.Inbound.Infrastructure")]
    [InlineData("Wms.Host.AppApi", "Wms.Inventory.Infrastructure")]
    public void Rules_allow_permitted_references(string project, string reference) =>
        ReferenceRules.Check([new ProjectInfo(project, [reference])]).ShouldBeEmpty();

    [Fact]
    public void Unknown_project_shapes_are_rejected_so_rules_stay_current() =>
        ReferenceRules.Check([new ProjectInfo("Wms.Inbound.Helpers", ["Wms.SharedKernel"])]).ShouldNotBeEmpty();
}

public sealed class BannedApiTests
{
    [Fact]
    public void Production_code_uses_IClock_and_Ids_instead_of_system_APIs() =>
        BannedApis.Scan(RepoPaths.Source).ShouldBeEmpty();
}
