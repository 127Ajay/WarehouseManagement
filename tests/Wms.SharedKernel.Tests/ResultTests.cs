using Wms.SharedKernel;

namespace Wms.SharedKernel.Tests;

public sealed class ResultTests
{
    private static readonly Error SampleError = Error.Rule("INB_OVER_RECEIPT", "Over-receipt tolerance exceeded.", "qty");

    [Fact]
    public void Ok_result_exposes_its_value()
    {
        Result<int> result = 42;

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(42);
        result.Error.ShouldBeNull();
    }

    [Fact]
    public void Failed_result_exposes_its_error_and_refuses_value_access()
    {
        Result<int> result = SampleError;

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SampleError);
        Should.Throw<InvalidOperationException>(() => result.Value)
            .Message.ShouldContain("INB_OVER_RECEIPT");
    }

    [Fact]
    public void Map_transforms_success_and_passes_failure_through()
    {
        Result<int>.Ok(2).Map(x => x * 10).Value.ShouldBe(20);
        Result<int>.Fail(SampleError).Map(x => x * 10).Error.ShouldBe(SampleError);
    }

    [Fact]
    public void Match_picks_the_right_branch()
    {
        Result<int>.Ok(1).Match(_ => "ok", e => e.Code).ShouldBe("ok");
        Result<int>.Fail(SampleError).Match(_ => "ok", e => e.Code).ShouldBe("INB_OVER_RECEIPT");
    }

    [Fact]
    public void Unit_results_work_without_a_value()
    {
        Result.Ok().IsSuccess.ShouldBeTrue();
        Result.Fail(SampleError).Error!.Type.ShouldBe(ErrorType.Rule);
    }
}

public sealed class EntityTests
{
    private sealed class Sample(Guid id) : Entity<Guid>(id);

    private sealed class Other(Guid id) : Entity<Guid>(id);

    [Fact]
    public void Entities_are_equal_by_type_and_id()
    {
        var id = Ids.New();

        new Sample(id).ShouldBe(new Sample(id));
        new Sample(id).ShouldNotBe(new Sample(Ids.New()));
        new Sample(id).Equals(new Other(id)).ShouldBeFalse();
    }

    [Fact]
    public void Ids_are_version_7()
    {
        // UUID v7 orders by millisecond; IDs created in the same millisecond are not ordered,
        // so only the version is asserted here.
        Ids.New().Version.ShouldBe(7);
        Ids.New().ShouldNotBe(Ids.New());
    }
}
