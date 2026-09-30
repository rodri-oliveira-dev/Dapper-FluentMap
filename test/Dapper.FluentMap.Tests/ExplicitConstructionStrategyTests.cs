using System;
using Dapper.FluentMap.Configuration;
using Dapper.FluentMap.Mapping;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Dapper.FluentMap.Tests
{
    public class ExplicitConstructionStrategyTests
    {
        [Fact]
        [Trait("Category", "Integration")]
        public void QueryMappedShouldUseExplicitFactoryForPrivateConstructor()
        {
            FluentMapper.Reset(typeof(FactoryCustomer));

            try
            {
                FluentMapper.Initialize(configuration => configuration.AddMap(new FactoryCustomerMap()));

                using (var connection = OpenConnection())
                {
                    var customer = connection.QueryMappedSingle<FactoryCustomer>(
                        "SELECT 17 AS customer_id, 'Ada' AS customer_name;");

                    Assert.Equal(17, customer.Id);
                    Assert.Equal("Ada", customer.Name);
                    Assert.True(customer.CreatedByFactory);
                }
            }
            finally
            {
                FluentMapper.Reset(typeof(FactoryCustomer));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void IsolatedRuntimeShouldPreserveExplicitFactory()
        {
            var runtime = new FluentMapConfigurationBuilder()
                .AddMap(new FactoryCustomerMap())
                .Build()
                .CreateRuntime();

            using (var connection = OpenConnection())
            {
                var customer = runtime.QueryMappedSingle<FactoryCustomer>(
                    connection,
                    "SELECT 18 AS customer_id, 'Grace' AS customer_name;");

                Assert.Equal(18, customer.Id);
                Assert.Equal("Grace", customer.Name);
                Assert.True(customer.CreatedByFactory);
            }
        }

        [Fact]
        public void ValidationShouldRejectConstructionBindingWithoutActiveExplicitMap()
        {
            FluentMapper.Reset(typeof(FactoryCustomer));

            try
            {
                var exception = Assert.Throws<FluentMapConfigurationException>(() =>
                    FluentMapper.Initialize(configuration => configuration.AddMap(new IncompleteFactoryCustomerMap())));

                Assert.Contains(nameof(FactoryCustomer.Name), exception.Message);
                Assert.Contains("ConstructUsing", exception.Message);
            }
            finally
            {
                FluentMapper.Reset(typeof(FactoryCustomer));
            }
        }

        [Fact]
        public void DuplicateConstructionStrategyShouldFailDuringMapConfiguration()
        {
            var exception = Assert.Throws<FluentMapConfigurationException>(() => new DuplicateFactoryCustomerMap());

            Assert.Contains(typeof(FactoryCustomer).FullName, exception.Message);
            Assert.Contains("already has", exception.Message);
        }

        [Fact]
        public void ConstructionFactoriesShouldPreserveBindingOrderAcrossSupportedArities()
        {
            var oneValueStrategy = GetConstructionStrategy(new OneValueFactoryCustomerMap());
            var threeValueStrategy = GetConstructionStrategy(new ThreeValueFactoryAggregateMap());
            var fourValueStrategy = GetConstructionStrategy(new FourValueFactoryAggregateMap());

            var customer = (FactoryCustomer)oneValueStrategy.Factory(new object[] { 21 });
            var threeValue = (FactoryAggregate)threeValueStrategy.Factory(new object[] { 1, "two", 3L });
            var fourValue = (FactoryAggregate)fourValueStrategy.Factory(new object[] { 4, "five", 6L, true });

            Assert.Equal(21, customer.Id);
            Assert.Equal(new[] { "Id" }, GetBindingPaths(oneValueStrategy));
            Assert.Equal((1, "two", 3L, false), (threeValue.First, threeValue.Second, threeValue.Third, threeValue.Fourth));
            Assert.Equal(new[] { "First", "Second", "Third" }, GetBindingPaths(threeValueStrategy));
            Assert.Equal((4, "five", 6L, true), (fourValue.First, fourValue.Second, fourValue.Third, fourValue.Fourth));
            Assert.Equal(new[] { "First", "Second", "Third", "Fourth" }, GetBindingPaths(fourValueStrategy));
        }

        [Fact]
        public void ConstructUsingShouldRejectNullFactoriesAcrossSupportedArities()
        {
            var map = new ConstructionFactoryProbeMap();

            Assert.Equal("factory", Assert.Throws<ArgumentNullException>(() => map.ConfigureOne(null)).ParamName);
            Assert.Equal("factory", Assert.Throws<ArgumentNullException>(() => map.ConfigureTwo(null)).ParamName);
            Assert.Equal("factory", Assert.Throws<ArgumentNullException>(() => map.ConfigureThree(null)).ParamName);
            Assert.Equal("factory", Assert.Throws<ArgumentNullException>(() => map.ConfigureFour(null)).ParamName);
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void FactoryFailureShouldIncludeEntityAndBoundPropertyContext()
        {
            var runtime = new FluentMapConfigurationBuilder()
                .AddMap(new ThrowingFactoryCustomerMap())
                .Build()
                .CreateRuntime();

            using (var connection = OpenConnection())
            {
                var exception = Assert.Throws<FluentMapConfigurationException>(() =>
                    runtime.QueryMappedSingle<FactoryCustomer>(
                        connection,
                        "SELECT 19 AS customer_id, 'Failure' AS customer_name;"));

                Assert.Contains(typeof(FactoryCustomer).FullName, exception.Message);
                Assert.Contains(nameof(FactoryCustomer.Id), exception.Message);
                Assert.Contains(nameof(FactoryCustomer.Name), exception.Message);
                Assert.IsType<InvalidOperationException>(exception.InnerException);
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void StrictGeneratedRuntimeShouldRejectRuntimeOnlyFactoryWithoutFallback()
        {
            var runtime = new FluentMapConfigurationBuilder()
                .AddMap(new FactoryCustomerMap())
                .UseStrictGeneratedMaterialization()
                .Build()
                .CreateRuntime();

            using (var connection = OpenConnection())
            {
                var exception = Assert.Throws<FluentMapConfigurationException>(() =>
                    runtime.QueryMappedSingle<FactoryCustomer>(
                        connection,
                        "SELECT 20 AS customer_id, 'Strict' AS customer_name;"));

                Assert.Contains("Strict generated materialization", exception.Message);
                Assert.Contains("runtime fallback", exception.Message);
            }
        }

        private static SqliteConnection OpenConnection()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();
            return connection;
        }

        private static EntityConstructionStrategy GetConstructionStrategy<TEntity>(EntityMap<TEntity> map)
            where TEntity : class
        {
            return ((IEntityMapWithConstructionStrategy)map).ConstructionStrategy;
        }

        private static string[] GetBindingPaths(EntityConstructionStrategy strategy)
        {
            var paths = new string[strategy.Bindings.Count];
            for (var index = 0; index < strategy.Bindings.Count; index++)
            {
                paths[index] = strategy.Bindings[index].MemberPath.ToString();
            }

            return paths;
        }

        private sealed class FactoryCustomer
        {
            private FactoryCustomer(int id, string name)
            {
                Id = id;
                Name = name;
                CreatedByFactory = true;
            }

            public int Id { get; }

            public string Name { get; }

            public bool CreatedByFactory { get; }

            internal static FactoryCustomer Restore(int id, string name)
            {
                return new FactoryCustomer(id, name);
            }

            internal static FactoryCustomer RestoreIdOnly(int id)
            {
                return new FactoryCustomer(id, string.Empty);
            }
        }

        private class FactoryCustomerMap : EntityMap<FactoryCustomer>
        {
            public FactoryCustomerMap()
            {
                Map(customer => customer.Id).ToColumn("customer_id");
                Map(customer => customer.Name).ToColumn("customer_name");
                ConstructUsing(customer => customer.Id, customer => customer.Name, FactoryCustomer.Restore);
            }
        }

        private sealed class IncompleteFactoryCustomerMap : EntityMap<FactoryCustomer>
        {
            public IncompleteFactoryCustomerMap()
            {
                Map(customer => customer.Id).ToColumn("customer_id");
                ConstructUsing(customer => customer.Id, customer => customer.Name, FactoryCustomer.Restore);
            }
        }

        private sealed class OneValueFactoryCustomerMap : EntityMap<FactoryCustomer>
        {
            public OneValueFactoryCustomerMap()
            {
                Map(customer => customer.Id).ToColumn("customer_id");
                ConstructUsing(customer => customer.Id, FactoryCustomer.RestoreIdOnly);
            }
        }

        private sealed class FactoryAggregate
        {
            private FactoryAggregate(int first, string second, long third, bool fourth)
            {
                First = first;
                Second = second;
                Third = third;
                Fourth = fourth;
            }

            public int First { get; }

            public string Second { get; }

            public long Third { get; }

            public bool Fourth { get; }

            internal static FactoryAggregate RestoreThree(int first, string second, long third)
            {
                return new FactoryAggregate(first, second, third, false);
            }

            internal static FactoryAggregate RestoreFour(int first, string second, long third, bool fourth)
            {
                return new FactoryAggregate(first, second, third, fourth);
            }
        }

        private sealed class ThreeValueFactoryAggregateMap : EntityMap<FactoryAggregate>
        {
            public ThreeValueFactoryAggregateMap()
            {
                Map(entity => entity.First);
                Map(entity => entity.Second);
                Map(entity => entity.Third);
                ConstructUsing(
                    entity => entity.First,
                    entity => entity.Second,
                    entity => entity.Third,
                    FactoryAggregate.RestoreThree);
            }
        }

        private sealed class FourValueFactoryAggregateMap : EntityMap<FactoryAggregate>
        {
            public FourValueFactoryAggregateMap()
            {
                Map(entity => entity.First);
                Map(entity => entity.Second);
                Map(entity => entity.Third);
                Map(entity => entity.Fourth);
                ConstructUsing(
                    entity => entity.First,
                    entity => entity.Second,
                    entity => entity.Third,
                    entity => entity.Fourth,
                    FactoryAggregate.RestoreFour);
            }
        }

        private sealed class ConstructionFactoryProbeMap : EntityMap<FactoryAggregate>
        {
            internal void ConfigureOne(Func<int, FactoryAggregate> factory)
            {
                ConstructUsing(entity => entity.First, factory);
            }

            internal void ConfigureTwo(Func<int, string, FactoryAggregate> factory)
            {
                ConstructUsing(entity => entity.First, entity => entity.Second, factory);
            }

            internal void ConfigureThree(Func<int, string, long, FactoryAggregate> factory)
            {
                ConstructUsing(entity => entity.First, entity => entity.Second, entity => entity.Third, factory);
            }

            internal void ConfigureFour(Func<int, string, long, bool, FactoryAggregate> factory)
            {
                ConstructUsing(
                    entity => entity.First,
                    entity => entity.Second,
                    entity => entity.Third,
                    entity => entity.Fourth,
                    factory);
            }
        }

        private sealed class DuplicateFactoryCustomerMap : FactoryCustomerMap
        {
            public DuplicateFactoryCustomerMap()
            {
                ConstructUsing(customer => customer.Id, FactoryCustomer.RestoreIdOnly);
            }
        }

        private sealed class ThrowingFactoryCustomerMap : EntityMap<FactoryCustomer>
        {
            public ThrowingFactoryCustomerMap()
            {
                Map(customer => customer.Id).ToColumn("customer_id");
                Map(customer => customer.Name).ToColumn("customer_name");
                ConstructUsing(
                    customer => customer.Id,
                    customer => customer.Name,
                    (id, name) => throw new InvalidOperationException("Domain factory rejected the values."));
            }
        }
    }
}
