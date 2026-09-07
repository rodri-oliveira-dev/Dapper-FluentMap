using System;
using Dapper;
using Dapper.FluentMap.Configuration;
using Dapper.FluentMap.Conventions;
using Dapper.FluentMap.Mapping;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Dapper.FluentMap.Tests
{
    public class ConfigurationLifecycleTests
    {
        [Fact]
        public void InitializeShouldAllowAdditiveConfigurationAcrossRepeatedCalls()
        {
            ResetMapper(typeof(FirstLifecycleEntity), typeof(SecondLifecycleEntity));

            try
            {
                FluentMapper.Initialize(c => c.AddMap(new FirstLifecycleMap()));
                FluentMapper.Initialize(c => c.AddMap(new SecondLifecycleMap()));

                Assert.IsType<FirstLifecycleMap>(FluentMapper.EntityMaps[typeof(FirstLifecycleEntity)]);
                Assert.IsType<SecondLifecycleMap>(FluentMapper.EntityMaps[typeof(SecondLifecycleEntity)]);
                Assert.Equal(2, FluentMapper.EntityMaps.Count);
            }
            finally
            {
                ResetMapper(typeof(FirstLifecycleEntity), typeof(SecondLifecycleEntity));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void PublicConfigurationMutationShouldPublishDapperTypeMapAndRuntime()
        {
            ResetMapper(typeof(PublicConfigurationEntity));

            try
            {
                var configuration = new FluentMapConfiguration();
                configuration.AddMap(new PublicConfigurationMap());

                var member = SqlMapper.GetTypeMap(typeof(PublicConfigurationEntity)).GetMember("public_id");

                using (var connection = OpenConnection())
                {
                    var entity = connection.QueryMappedSingle<PublicConfigurationEntity>(
                        "SELECT 42 AS public_id;");

                    Assert.NotNull(member);
                    Assert.Equal(nameof(PublicConfigurationEntity.Id), member.Property.Name);
                    Assert.Equal(42, entity.Id);
                }
            }
            finally
            {
                ResetMapper(typeof(PublicConfigurationEntity));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void RuntimeRegistrationShouldRemainCompatibleWhenAccessIsSerialized()
        {
            ResetMapper(typeof(RuntimeConventionEntity));

            try
            {
                using (var connection = OpenConnection())
                {
                    var beforeConfiguration = connection.QuerySingle<RuntimeConventionEntity>(
                        "SELECT 1 AS Id, 'before' AS Name;");

                    FluentMapper.Initialize(c => c.AddConvention<RuntimePrefixConvention>().ForEntity<RuntimeConventionEntity>());

                    var afterConfiguration = connection.QuerySingle<RuntimeConventionEntity>(
                        "SELECT 2 AS cfgId, 'after' AS cfgName;");

                    Assert.Equal(1, beforeConfiguration.Id);
                    Assert.Equal("before", beforeConfiguration.Name);
                    Assert.Equal(2, afterConfiguration.Id);
                    Assert.Equal("after", afterConfiguration.Name);
                }
            }
            finally
            {
                ResetMapper(typeof(RuntimeConventionEntity));
            }
        }

        [Fact]
        public void DirectEntityMapsMutationShouldRemainLegacySurfaceAndBypassDapperTypeMapInstallation()
        {
            ResetMapper(typeof(DirectMutationEntity));

            try
            {
                var added = FluentMapper.EntityMaps.TryAdd(typeof(DirectMutationEntity), new DirectMutationMap());
                var member = SqlMapper.GetTypeMap(typeof(DirectMutationEntity)).GetMember("legacy_id");

                Assert.True(added);
                Assert.Null(member);
                Assert.IsType<DirectMutationMap>(FluentMapper.EntityMaps[typeof(DirectMutationEntity)]);
            }
            finally
            {
                ResetMapper(typeof(DirectMutationEntity));
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

        private sealed class FirstLifecycleEntity
        {
            public int Id { get; set; }
        }

        private sealed class FirstLifecycleMap : EntityMap<FirstLifecycleEntity>
        {
            public FirstLifecycleMap()
            {
                Map(entity => entity.Id).ToColumn("first_id");
            }
        }

        private sealed class SecondLifecycleEntity
        {
            public string Name { get; set; }
        }

        private sealed class SecondLifecycleMap : EntityMap<SecondLifecycleEntity>
        {
            public SecondLifecycleMap()
            {
                Map(entity => entity.Name).ToColumn("second_name");
            }
        }

        private sealed class PublicConfigurationEntity
        {
            public int Id { get; set; }
        }

        private sealed class PublicConfigurationMap : EntityMap<PublicConfigurationEntity>
        {
            public PublicConfigurationMap()
            {
                Map(entity => entity.Id).ToColumn("public_id");
            }
        }

        private sealed class RuntimeConventionEntity
        {
            public int Id { get; set; }

            public string Name { get; set; }
        }

        private sealed class RuntimePrefixConvention : Convention
        {
            public RuntimePrefixConvention()
            {
                Properties()
                    .Configure(configuration => configuration.HasPrefix("cfg"));
            }
        }

        private sealed class DirectMutationEntity
        {
            public int Id { get; set; }
        }

        private sealed class DirectMutationMap : EntityMap<DirectMutationEntity>
        {
            public DirectMutationMap()
            {
                Map(entity => entity.Id).ToColumn("legacy_id");
            }
        }
    }
}
