using Order.Shared.Data;
using Tests.Shared.TestBase;

namespace Order.IntegrationTests;

public abstract class OrderIntegrationTestBase(OrderSharedFixture sharedFixture)
    : IntegrationTestBase<Program, OrderDbContext, OrderSharedFixture>(sharedFixture);
