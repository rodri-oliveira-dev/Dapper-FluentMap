using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dapper;
using Dapper.FluentMap.Mapping;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Dapper.FluentMap.Tests
{
    public class MappingRegistrationTests
    {
        [Fact]
        public void InstanceRegistrationShouldContinueToAddEntityMap()
        {
            ResetMapper(typeof(InstanceRegistrationEntity));

            try
            {
                FluentMapper.Initialize(c => c.AddMap(new InstanceRegistrationMap()));

                var entityMap = FluentMapper.EntityMaps.Single();
                Assert.Equal(typeof(InstanceRegistrationEntity), entityMap.Key);
                Assert.IsType<InstanceRegistrationMap>(entityMap.Value);
            }
            finally
            {
                ResetMapper(typeof(InstanceRegistrationEntity));
            }
        }

        [Fact]
        public void GenericRegistrationShouldAddEntityMapAndDapperTypeMap()
        {
            ResetMapper(typeof(GenericRegistrationEntity));

            try
            {
                FluentMapper.Initialize(c => c.AddMap<GenericRegistrationMap>());

                Assert.IsType<GenericRegistrationMap>(FluentMapper.EntityMaps[typeof(GenericRegistrationEntity)]);
                var typeMap = SqlMapper.GetTypeMap(typeof(GenericRegistrationEntity));
                var member = typeMap.GetMember("generic_id");
                Assert.Equal(typeof(GenericRegistrationEntity).GetProperty(nameof(GenericRegistrationEntity.Id)), member.Property);

                var property = FluentMapper.Registry.GetFluentPropertyInfo(typeof(GenericRegistrationEntity), "generic_id");
                Assert.Equal(typeof(GenericRegistrationEntity).GetProperty(nameof(GenericRegistrationEntity.Id)), property);
            }
            finally
            {
                ResetMapper(typeof(GenericRegistrationEntity));
            }
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void GenericRegistrationShouldMaterializeConfiguredColumnWithDapper()
        {
            ResetMapper(typeof(GenericIntegrationEntity));

            try
            {
                FluentMapper.Initialize(c => c.AddMap<GenericIntegrationMap>());

                using (var connection = OpenConnection())
                {
                    var entity = connection.QuerySingle<GenericIntegrationEntity>(
                        "SELECT 31 AS integration_id, 'modern' AS Name;");

                    Assert.Equal(31, entity.Id);
                    Assert.Equal("modern", entity.Name);
                }
            }
            finally
            {
                ResetMapper(typeof(GenericIntegrationEntity));
            }
        }

        [Fact]
        public void GenericRegistrationShouldChainMultipleExplicitMappings()
        {
            ResetMapper(typeof(FirstExplicitEntity), typeof(SecondExplicitEntity));

            try
            {
                FluentMapper.Initialize(c => c
                    .AddMap<FirstExplicitMap>()
                    .AddMap<SecondExplicitMap>());

                Assert.Equal(2, FluentMapper.EntityMaps.Count);
                Assert.IsType<FirstExplicitMap>(FluentMapper.EntityMaps[typeof(FirstExplicitEntity)]);
                Assert.IsType<SecondExplicitMap>(FluentMapper.EntityMaps[typeof(SecondExplicitEntity)]);
            }
            finally
            {
                ResetMapper(typeof(FirstExplicitEntity), typeof(SecondExplicitEntity));
            }
        }

        [Fact]
        public void AddMapsFromAssemblyShouldRegisterDiscoveredMaps()
        {
            ResetMapper(
                typeof(MappingRegistrationScan.Basic.Customer),
                typeof(MappingRegistrationScan.Basic.Order));

            try
            {
                FluentMapper.Initialize(c => c.AddMapsFromAssembly(
                    typeof(MappingRegistrationScan.Basic.Marker).GetTypeInfo().Assembly,
                    typeof(MappingRegistrationScan.Basic.Marker).Namespace));

                Assert.Equal(2, FluentMapper.EntityMaps.Count);
                Assert.IsType<MappingRegistrationScan.Basic.CustomerMap>(
                    FluentMapper.EntityMaps[typeof(MappingRegistrationScan.Basic.Customer)]);
                Assert.IsType<MappingRegistrationScan.Basic.OrderMap>(
                    FluentMapper.EntityMaps[typeof(MappingRegistrationScan.Basic.Order)]);
            }
            finally
            {
                ResetMapper(
                    typeof(MappingRegistrationScan.Basic.Customer),
                    typeof(MappingRegistrationScan.Basic.Order));
            }
        }

        [Fact]
        public void AddMapsFromAssemblyContainingShouldUseMarkerAssembly()
        {
            ResetMapper(typeof(MappingRegistrationScan.MarkerType.MarkerEntity));

            try
            {
                FluentMapper.Initialize(c => c.AddMapsFromAssemblyContaining<MappingRegistrationScan.MarkerType.Marker>(
                    typeof(MappingRegistrationScan.MarkerType.Marker).Namespace));

                var property = FluentMapper.Registry.GetFluentPropertyInfo(
                    typeof(MappingRegistrationScan.MarkerType.MarkerEntity),
                    "marker_id");

                Assert.Equal(typeof(MappingRegistrationScan.MarkerType.MarkerEntity).GetProperty(nameof(MappingRegistrationScan.MarkerType.MarkerEntity.Id)), property);
            }
            finally
            {
                ResetMapper(typeof(MappingRegistrationScan.MarkerType.MarkerEntity));
            }
        }

        [Fact]
        public void AddMapsFromAssemblyShouldIgnoreAbstractMaps()
        {
            ResetMapper(typeof(MappingRegistrationScan.AbstractOnly.AbstractEntity));

            try
            {
                FluentMapper.Initialize(c => c.AddMapsFromAssemblyContaining<MappingRegistrationScan.AbstractOnly.Marker>(
                    typeof(MappingRegistrationScan.AbstractOnly.Marker).Namespace));

                Assert.Empty(FluentMapper.EntityMaps);
            }
            finally
            {
                ResetMapper(typeof(MappingRegistrationScan.AbstractOnly.AbstractEntity));
            }
        }

        [Fact]
        public void GenericRegistrationShouldRejectInvalidMapType()
        {
            ResetMapper();

            var exception = Assert.Throws<FluentMapConfigurationException>(
                () => FluentMapper.Initialize(c => c.AddMap<NonGenericEntityMap>()));

            Assert.Contains("exactly one closed IEntityMap<TEntity>", exception.Message);
        }

        [Fact]
        public void RegisteringSameMapTwiceShouldThrow()
        {
            ResetMapper(typeof(DuplicateRegistrationEntity));

            try
            {
                var exception = Assert.Throws<FluentMapConfigurationException>(() =>
                    FluentMapper.Initialize(c => c
                        .AddMap<DuplicateRegistrationMap>()
                        .AddMap<DuplicateRegistrationMap>()));

                Assert.Contains("already has a configured entity map", exception.Message);
            }
            finally
            {
                ResetMapper(typeof(DuplicateRegistrationEntity));
            }
        }

        [Fact]
        public void RegisteringDifferentMapsForSameEntityShouldThrow()
        {
            ResetMapper(typeof(DuplicateEntity));

            try
            {
                var exception = Assert.Throws<FluentMapConfigurationException>(() =>
                    FluentMapper.Initialize(c => c
                        .AddMap<FirstDuplicateEntityMap>()
                        .AddMap<SecondDuplicateEntityMap>()));

                Assert.Contains("already has a configured entity map", exception.Message);
            }
            finally
            {
                ResetMapper(typeof(DuplicateEntity));
            }
        }

        [Fact]
        public void ScanningDuplicateEntityMapsShouldThrowBeforeRegistration()
        {
            ResetMapper(typeof(MappingRegistrationScan.DuplicateScan.DuplicateScanEntity));

            try
            {
                var exception = Assert.Throws<FluentMapConfigurationException>(() =>
                    FluentMapper.Initialize(c => c.AddMapsFromAssemblyContaining<MappingRegistrationScan.DuplicateScan.Marker>(
                        typeof(MappingRegistrationScan.DuplicateScan.Marker).Namespace)));

                Assert.Contains("Multiple entity maps were discovered", exception.Message);
                Assert.Empty(FluentMapper.EntityMaps);
            }
            finally
            {
                ResetMapper(typeof(MappingRegistrationScan.DuplicateScan.DuplicateScanEntity));
            }
        }

        [Fact]
        public void ScanningAfterExplicitRegistrationShouldThrowDuplicate()
        {
            ResetMapper(typeof(MappingRegistrationScan.ExplicitThenScan.ExplicitScanEntity));

            try
            {
                var exception = Assert.Throws<FluentMapConfigurationException>(() =>
                    FluentMapper.Initialize(c =>
                    {
                        c.AddMap<ExplicitScanMap>();
                        c.AddMapsFromAssemblyContaining<MappingRegistrationScan.ExplicitThenScan.Marker>(
                            typeof(MappingRegistrationScan.ExplicitThenScan.Marker).Namespace);
                    }));

                Assert.Contains("already has a configured entity map", exception.Message);
            }
            finally
            {
                ResetMapper(typeof(MappingRegistrationScan.ExplicitThenScan.ExplicitScanEntity));
            }
        }

        [Fact]
        public void GenericRegistrationShouldWrapConstructorErrors()
        {
            ResetMapper(typeof(ThrowingConstructorEntity));

            try
            {
                var exception = Assert.Throws<FluentMapConfigurationException>(
                    () => FluentMapper.Initialize(c => c.AddMap<ThrowingConstructorMap>()));

                Assert.Contains("could not be created", exception.Message);
                Assert.NotNull(exception.InnerException);
            }
            finally
            {
                ResetMapper(typeof(ThrowingConstructorEntity));
            }
        }

        [Fact]
        public void GenericRegistrationShouldUseExistingValidation()
        {
            ResetMapper(typeof(ValidationRegistrationEntity));

            try
            {
                var exception = Assert.Throws<FluentMapConfigurationException>(
                    () => FluentMapper.Initialize(c => c.AddMap<ValidationRegistrationMap>()));

                Assert.Contains("configured for more than one property path", exception.Message);
            }
            finally
            {
                ResetMapper(typeof(ValidationRegistrationEntity));
            }
        }

        [Fact]
        public void AssemblyScanningShouldRegisterIncludedBaseMapsBeforeDerivedMaps()
        {
            ResetMapper(
                typeof(MappingRegistrationScan.InheritedScan.BaseScanEntity),
                typeof(MappingRegistrationScan.InheritedScan.DerivedScanEntity));

            try
            {
                FluentMapper.Initialize(c => c.AddMapsFromAssemblyContaining<MappingRegistrationScan.InheritedScan.Marker>(
                    typeof(MappingRegistrationScan.InheritedScan.Marker).Namespace));

                var typeMap = SqlMapper.GetTypeMap(typeof(MappingRegistrationScan.InheritedScan.DerivedScanEntity));
                var inheritedMember = typeMap.GetMember("base_id");
                var derivedMember = typeMap.GetMember("derived_name");

                Assert.Equal(typeof(MappingRegistrationScan.InheritedScan.BaseScanEntity).GetProperty(nameof(MappingRegistrationScan.InheritedScan.BaseScanEntity.Id)), inheritedMember.Property);
                Assert.Equal(typeof(MappingRegistrationScan.InheritedScan.DerivedScanEntity).GetProperty(nameof(MappingRegistrationScan.InheritedScan.DerivedScanEntity.Name)), derivedMember.Property);
            }
            finally
            {
                ResetMapper(
                    typeof(MappingRegistrationScan.InheritedScan.BaseScanEntity),
                    typeof(MappingRegistrationScan.InheritedScan.DerivedScanEntity));
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

        private class InstanceRegistrationEntity
        {
            public int Id { get; set; }
        }

        private class InstanceRegistrationMap : EntityMap<InstanceRegistrationEntity>
        {
            public InstanceRegistrationMap()
            {
                Map(e => e.Id).ToColumn("instance_id");
            }
        }

        private class GenericRegistrationEntity
        {
            public int Id { get; set; }
        }

        private class GenericRegistrationMap : EntityMap<GenericRegistrationEntity>
        {
            public GenericRegistrationMap()
            {
                Map(e => e.Id).ToColumn("generic_id");
            }
        }

        private class GenericIntegrationEntity
        {
            public int Id { get; set; }

            public string Name { get; set; }
        }

        private class GenericIntegrationMap : EntityMap<GenericIntegrationEntity>
        {
            public GenericIntegrationMap()
            {
                Map(e => e.Id).ToColumn("integration_id");
            }
        }

        private class FirstExplicitEntity
        {
            public int Id { get; set; }
        }

        private class FirstExplicitMap : EntityMap<FirstExplicitEntity>
        {
            public FirstExplicitMap()
            {
                Map(e => e.Id).ToColumn("first_id");
            }
        }

        private class SecondExplicitEntity
        {
            public string Name { get; set; }
        }

        private class SecondExplicitMap : EntityMap<SecondExplicitEntity>
        {
            public SecondExplicitMap()
            {
                Map(e => e.Name).ToColumn("second_name");
            }
        }

        private class NonGenericEntityMap : IEntityMap
        {
            public IList<IPropertyMap> PropertyMaps { get; } = new List<IPropertyMap>();
        }

        private class DuplicateRegistrationEntity
        {
            public int Id { get; set; }
        }

        private class DuplicateRegistrationMap : EntityMap<DuplicateRegistrationEntity>
        {
            public DuplicateRegistrationMap()
            {
                Map(e => e.Id).ToColumn("duplicate_id");
            }
        }

        private class DuplicateEntity
        {
            public int Id { get; set; }

            public string Name { get; set; }
        }

        private class FirstDuplicateEntityMap : EntityMap<DuplicateEntity>
        {
            public FirstDuplicateEntityMap()
            {
                Map(e => e.Id).ToColumn("duplicate_id");
            }
        }

        private class SecondDuplicateEntityMap : EntityMap<DuplicateEntity>
        {
            public SecondDuplicateEntityMap()
            {
                Map(e => e.Name).ToColumn("duplicate_name");
            }
        }

        private class ExplicitScanMap : EntityMap<MappingRegistrationScan.ExplicitThenScan.ExplicitScanEntity>
        {
            public ExplicitScanMap()
            {
                Map(e => e.Id).ToColumn("explicit_id");
            }
        }

        private class ThrowingConstructorEntity
        {
            public int Id { get; set; }
        }

        private class ThrowingConstructorMap : EntityMap<ThrowingConstructorEntity>
        {
            public ThrowingConstructorMap()
            {
                throw new InvalidOperationException("Constructor failed.");
            }
        }

        private class ValidationRegistrationEntity
        {
            public int Id { get; set; }

            public int OtherId { get; set; }
        }

        private class ValidationRegistrationMap : EntityMap<ValidationRegistrationEntity>
        {
            public ValidationRegistrationMap()
            {
                Map(e => e.Id).ToColumn("same_column");
                Map(e => e.OtherId).ToColumn("same_column");
            }
        }
    }
}

namespace Dapper.FluentMap.Tests.MappingRegistrationScan.Basic
{
    public class Marker
    {
    }

    public class Customer
    {
        public int Id { get; set; }
    }

    public class Order
    {
        public string Number { get; set; }
    }

    public class CustomerMap : EntityMap<Customer>
    {
        public CustomerMap()
        {
            Map(e => e.Id).ToColumn("customer_id");
        }
    }

    public class OrderMap : EntityMap<Order>
    {
        public OrderMap()
        {
            Map(e => e.Number).ToColumn("order_number");
        }
    }
}

namespace Dapper.FluentMap.Tests.MappingRegistrationScan.MarkerType
{
    public class Marker
    {
    }

    public class MarkerEntity
    {
        public int Id { get; set; }
    }

    public class MarkerEntityMap : EntityMap<MarkerEntity>
    {
        public MarkerEntityMap()
        {
            Map(e => e.Id).ToColumn("marker_id");
        }
    }
}

namespace Dapper.FluentMap.Tests.MappingRegistrationScan.AbstractOnly
{
    public class Marker
    {
    }

    public class AbstractEntity
    {
        public int Id { get; set; }
    }

    public abstract class AbstractEntityMap : EntityMap<AbstractEntity>
    {
    }
}

namespace Dapper.FluentMap.Tests.MappingRegistrationScan.DuplicateScan
{
    public class Marker
    {
    }

    public class DuplicateScanEntity
    {
        public int Id { get; set; }

        public string Name { get; set; }
    }

    public class FirstDuplicateScanMap : EntityMap<DuplicateScanEntity>
    {
        public FirstDuplicateScanMap()
        {
            Map(e => e.Id).ToColumn("duplicate_id");
        }
    }

    public class SecondDuplicateScanMap : EntityMap<DuplicateScanEntity>
    {
        public SecondDuplicateScanMap()
        {
            Map(e => e.Name).ToColumn("duplicate_name");
        }
    }
}

namespace Dapper.FluentMap.Tests.MappingRegistrationScan.ExplicitThenScan
{
    public class Marker
    {
    }

    public class ExplicitScanEntity
    {
        public int Id { get; set; }

        public string Name { get; set; }
    }

    public class ScannedExplicitEntityMap : EntityMap<ExplicitScanEntity>
    {
        public ScannedExplicitEntityMap()
        {
            Map(e => e.Name).ToColumn("scanned_name");
        }
    }
}

namespace Dapper.FluentMap.Tests.MappingRegistrationScan.InheritedScan
{
    public class Marker
    {
    }

    public class BaseScanEntity
    {
        public int Id { get; set; }
    }

    public class DerivedScanEntity : BaseScanEntity
    {
        public string Name { get; set; }
    }

    public class ADerivedScanMap : EntityMap<DerivedScanEntity>
    {
        public ADerivedScanMap()
        {
            IncludeBase<BaseScanEntity>();
            Map(e => e.Name).ToColumn("derived_name");
        }
    }

    public class ZBaseScanMap : EntityMap<BaseScanEntity>
    {
        public ZBaseScanMap()
        {
            Map(e => e.Id).ToColumn("base_id");
        }
    }
}
