using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper.FluentMap.Configuration;
using Dapper.FluentMap.Mapping;
using Dapper.FluentMap.Materialization;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Dapper.FluentMap.Tests
{
    public class CompatibilityBridgeTests
    {
        [Fact]
        [Trait("Category", "Integration")]
        public void StaticAndConfigurationAwareQueriesShouldProduceSameResult()
        {
            ResetMapper(typeof(BridgeCustomer));

            try
            {
                FluentMapper.Initialize(configuration => configuration.AddMap(new BridgeCustomerMap()));
                var runtime = FluentMapper.Configuration.CreateRuntime();

                using (var connection = OpenConnection())
                {
                    var legacy = connection.QueryMappedSingle<BridgeCustomer>(
                        "SELECT 1 AS bridge_id, 'Ada' AS bridge_name;");
                    var isolated = runtime.QueryMappedSingle<BridgeCustomer>(
                        connection,
                        "SELECT 1 AS bridge_id, 'Ada' AS bridge_name;");

                    Assert.Equal(legacy.Id, isolated.Id);
                    Assert.Equal(legacy.Name, isolated.Name);
                    Assert.Same(FluentMapper.Configuration, FluentMapper.Runtime.Configuration);
                }
            }
            finally
            {
                ResetMapper(typeof(BridgeCustomer));
            }
        }

        [Fact]
        public void RepeatedInitializeShouldPublishAdditiveDefaultConfiguration()
        {
            ResetMapper(typeof(FirstBridgeEntity), typeof(SecondBridgeEntity));

            try
            {
                FluentMapper.Initialize(configuration => configuration.AddMap(new FirstBridgeMap()));
                var firstRuntime = FluentMapper.Runtime;

                FluentMapper.Initialize(configuration => configuration.AddMap(new SecondBridgeMap()));

                Assert.NotSame(firstRuntime, FluentMapper.Runtime);
                Assert.True(FluentMapper.Configuration.EntityMaps.ContainsKey(typeof(FirstBridgeEntity)));
                Assert.True(FluentMapper.Configuration.EntityMaps.ContainsKey(typeof(SecondBridgeEntity)));
                Assert.IsType<FirstBridgeMap>(FluentMapper.EntityMaps[typeof(FirstBridgeEntity)]);
                Assert.IsType<SecondBridgeMap>(FluentMapper.EntityMaps[typeof(SecondBridgeEntity)]);
            }
            finally
            {
                ResetMapper(typeof(FirstBridgeEntity), typeof(SecondBridgeEntity));
            }
        }

        [Fact]
        public async Task ConcurrentInitializeShouldBeSerializedForDefaultConfiguration()
        {
            ResetMapper(typeof(ConcurrentFirstBridgeEntity), typeof(ConcurrentSecondBridgeEntity));

            try
            {
                var start = new Barrier(2);
                var cancellationToken = TestContext.Current.CancellationToken;
                var first = Task.Run(() =>
                {
                    start.SignalAndWait();
                    FluentMapper.Initialize(configuration => configuration.AddMap(new ConcurrentFirstBridgeMap()));
                }, cancellationToken);
                var second = Task.Run(() =>
                {
                    start.SignalAndWait();
                    FluentMapper.Initialize(configuration => configuration.AddMap(new ConcurrentSecondBridgeMap()));
                }, cancellationToken);

                await Task.WhenAll(first, second);

                FluentMapper.Validate();
                Assert.True(FluentMapper.Configuration.EntityMaps.ContainsKey(typeof(ConcurrentFirstBridgeEntity)));
                Assert.True(FluentMapper.Configuration.EntityMaps.ContainsKey(typeof(ConcurrentSecondBridgeEntity)));
            }
            finally
            {
                ResetMapper(typeof(ConcurrentFirstBridgeEntity), typeof(ConcurrentSecondBridgeEntity));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void LegacyMutableDictionaryChangesShouldNotRewritePublishedRuntime()
        {
            ResetMapper(typeof(DictionaryBridgeEntity));

            try
            {
                FluentMapper.EntityMaps.TryAdd(typeof(DictionaryBridgeEntity), new DictionaryBridgeMap());

                using (var connection = OpenConnection())
                {
                    var beforePublish = connection.QueryMappedSingle<DictionaryBridgeEntity>(
                        "SELECT 5 AS dictionary_id;");

                    FluentMapper.Initialize(_ => { });

                    var afterPublish = connection.QueryMappedSingle<DictionaryBridgeEntity>(
                        "SELECT 5 AS dictionary_id;");

                    Assert.Equal(0, beforePublish.Id);
                    Assert.Equal(5, afterPublish.Id);
                }
            }
            finally
            {
                ResetMapper(typeof(DictionaryBridgeEntity));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void DefaultBridgeShouldPreserveProfilesConvertersAndGeneratedMaterializers()
        {
            ResetMapper(typeof(BridgeConversionCustomer));

            try
            {
                FluentMapper.Initialize(configuration =>
                {
                    configuration.AddMap(new BridgeConversionMap());
                    configuration.AddProfile<BridgeProfileMap>();
                    configuration.AddGeneratedMaterializer(
                        new[]
                        {
                            GeneratedMaterializerColumn.Map("bridge_id", nameof(BridgeConversionCustomer.Id)),
                            GeneratedMaterializerColumn.Map("bridge_name", nameof(BridgeConversionCustomer.Name))
                        },
                        record => new BridgeConversionCustomer
                        {
                            Id = Convert.ToInt32(record.GetValue(0)),
                            Name = "generated:" + Convert.ToString(record.GetValue(1))
                        });
                });

                using (var connection = OpenConnection())
                {
                    var generated = connection.QueryMappedSingle<BridgeConversionCustomer>(
                        "SELECT 7 AS bridge_id, 'ada' AS bridge_name;");
                    var profile = connection.QueryMappedSingle<BridgeConversionCustomer, BridgeProfile>(
                        "SELECT 8 AS profile_id, 'grace' AS profile_name;");

                    Assert.Equal("generated:ada", generated.Name);
                    Assert.Equal("GRACE", profile.Name);
                    Assert.Equal(1, FluentMapper.Runtime.GeneratedMaterializerCount);
                }
            }
            finally
            {
                ResetMapper(typeof(BridgeConversionCustomer));
            }
        }

        private static SqliteConnection OpenConnection()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();
            return connection;
        }

        private static void ResetMapper(params Type[] types)
        {
            FluentMapper.Reset(types);
        }

        private sealed class BridgeCustomer
        {
            public int Id { get; set; }

            public string Name { get; set; }
        }

        private sealed class BridgeCustomerMap : EntityMap<BridgeCustomer>
        {
            public BridgeCustomerMap()
            {
                Map(customer => customer.Id).ToColumn("bridge_id");
                Map(customer => customer.Name).ToColumn("bridge_name");
            }
        }

        private sealed class FirstBridgeEntity
        {
            public int Id { get; set; }
        }

        private sealed class FirstBridgeMap : EntityMap<FirstBridgeEntity>
        {
            public FirstBridgeMap()
            {
                Map(entity => entity.Id).ToColumn("first_id");
            }
        }

        private sealed class SecondBridgeEntity
        {
            public int Id { get; set; }
        }

        private sealed class SecondBridgeMap : EntityMap<SecondBridgeEntity>
        {
            public SecondBridgeMap()
            {
                Map(entity => entity.Id).ToColumn("second_id");
            }
        }

        private sealed class ConcurrentFirstBridgeEntity
        {
            public int Id { get; set; }
        }

        private sealed class ConcurrentFirstBridgeMap : EntityMap<ConcurrentFirstBridgeEntity>
        {
            public ConcurrentFirstBridgeMap()
            {
                Map(entity => entity.Id).ToColumn("first_concurrent_id");
            }
        }

        private sealed class ConcurrentSecondBridgeEntity
        {
            public int Id { get; set; }
        }

        private sealed class ConcurrentSecondBridgeMap : EntityMap<ConcurrentSecondBridgeEntity>
        {
            public ConcurrentSecondBridgeMap()
            {
                Map(entity => entity.Id).ToColumn("second_concurrent_id");
            }
        }

        private sealed class DictionaryBridgeEntity
        {
            public int Id { get; set; }
        }

        private sealed class DictionaryBridgeMap : EntityMap<DictionaryBridgeEntity>
        {
            public DictionaryBridgeMap()
            {
                Map(entity => entity.Id).ToColumn("dictionary_id");
            }
        }

        private sealed class BridgeProfile : IMappingProfile
        {
        }

        private sealed class BridgeConversionCustomer
        {
            public int Id { get; set; }

            public string Name { get; set; }
        }

        private sealed class BridgeConversionMap : EntityMap<BridgeConversionCustomer>
        {
            public BridgeConversionMap()
            {
                Map(customer => customer.Id).ToColumn("bridge_id");
                Map(customer => customer.Name).ToColumn("bridge_name");
            }
        }

        private sealed class BridgeProfileMap :
            EntityMap<BridgeConversionCustomer>,
            IProfileMap<BridgeProfile>
        {
            public BridgeProfileMap()
            {
                Map(customer => customer.Id).ToColumn("profile_id");
                Map(customer => customer.Name).ToColumn("profile_name")
                    .ConvertFromDatabaseUsing<UpperBridgeNameConverter, string>();
            }
        }

        private sealed class UpperBridgeNameConverter : IReadPropertyConverter<string, string>
        {
            public string ConvertFromDatabase(string value)
            {
                return value.ToUpperInvariant();
            }
        }
    }
}
