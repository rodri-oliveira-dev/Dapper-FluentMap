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
