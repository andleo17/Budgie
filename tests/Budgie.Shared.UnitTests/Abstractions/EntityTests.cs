using Budgie.Shared.Abstractions;
using FluentAssertions;
using Xunit;

namespace Budgie.Shared.UnitTests.Abstractions;

public class EntityTests
{
    private sealed class TestEntity : Entity
    {
        public void RaiseTestEvent(IDomainEvent domainEvent) => RaiseDomainEvent(domainEvent);
    }

    private sealed class TestDomainEvent : IDomainEvent
    {
    }

    [Fact]
    public void CanBeInstantiatedViaTestSubclass()
    {
        var entity = new TestEntity();
        entity.Should().NotBeNull();
    }

    [Fact]
    public void GetDomainEvents_InitiallyEmpty()
    {
        var entity = new TestEntity();
        entity.GetDomainEvents().Should().BeEmpty();
    }

    [Fact]
    public void RaiseDomainEvent_AddsEvent()
    {
        var entity = new TestEntity();
        var domainEvent = new TestDomainEvent();

        entity.RaiseTestEvent(domainEvent);

        entity.GetDomainEvents().Should().ContainSingle().Which.Should().Be(domainEvent);
    }

    [Fact]
    public void ClearDomainEvents_RemovesAllEvents()
    {
        var entity = new TestEntity();
        entity.RaiseTestEvent(new TestDomainEvent());

        entity.ClearDomainEvents();

        entity.GetDomainEvents().Should().BeEmpty();
    }
}