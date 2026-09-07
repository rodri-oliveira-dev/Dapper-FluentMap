using System;
using System.Linq;
using System.Threading.Tasks;
using Dapper.FluentMap.Mapping;
using Xunit;

namespace Dapper.FluentMap.Tests
{
    public class MappingRegistryTests
    {
        [Fact]
        public void CacheShouldReuseEntryForSameTypeColumnAndOptions()
        {
            FluentMapper.Reset(typeof(CacheHitEntity));

            try
            {
                FluentMapper.Initialize(c => c.AddMap(new CacheHitMap()));

                var first = FluentMapper.Registry.GetFluentPropertyInfo(typeof(CacheHitEntity), "cache_id");
                var second = FluentMapper.Registry.GetFluentPropertyInfo(typeof(CacheHitEntity), "cache_id");

                Assert.Equal(typeof(CacheHitEntity).GetProperty(nameof(CacheHitEntity.Id)), first);
                Assert.Same(first, second);
                Assert.Equal(1, FluentMapper.Registry.CacheEntryCount);
            }
            finally
            {
                FluentMapper.Reset(typeof(CacheHitEntity));
            }
        }

        [Fact]
        public void CacheShouldUseDistinctKeysForDistinctTypes()
        {
            FluentMapper.Reset(typeof(FirstSameColumnEntity), typeof(SecondSameColumnEntity));

            try
            {
                FluentMapper.Initialize(c =>
                {
                    c.AddMap(new FirstSameColumnMap());
                    c.AddMap(new SecondSameColumnMap());
                });

                var first = FluentMapper.Registry.GetFluentPropertyInfo(typeof(FirstSameColumnEntity), "shared_column");
                var second = FluentMapper.Registry.GetFluentPropertyInfo(typeof(SecondSameColumnEntity), "shared_column");

                Assert.Equal(typeof(FirstSameColumnEntity).GetProperty(nameof(FirstSameColumnEntity.Id)), first);
                Assert.Equal(typeof(SecondSameColumnEntity).GetProperty(nameof(SecondSameColumnEntity.Name)), second);
                Assert.Equal(2, FluentMapper.Registry.CacheEntryCount);
            }
            finally
            {
                FluentMapper.Reset(typeof(FirstSameColumnEntity), typeof(SecondSameColumnEntity));
            }
        }

        [Fact]
        public void CacheShouldUseDistinctKeysForDistinctColumnNames()
        {
            FluentMapper.Reset(typeof(DistinctColumnsEntity));

            try
            {
                FluentMapper.Initialize(c => c.AddMap(new DistinctColumnsMap()));

                var id = FluentMapper.Registry.GetFluentPropertyInfo(typeof(DistinctColumnsEntity), "id_column");
                var name = FluentMapper.Registry.GetFluentPropertyInfo(typeof(DistinctColumnsEntity), "name_column");

                Assert.Equal(typeof(DistinctColumnsEntity).GetProperty(nameof(DistinctColumnsEntity.Id)), id);
                Assert.Equal(typeof(DistinctColumnsEntity).GetProperty(nameof(DistinctColumnsEntity.Name)), name);
                Assert.Equal(2, FluentMapper.Registry.CacheEntryCount);
            }
            finally
            {
                FluentMapper.Reset(typeof(DistinctColumnsEntity));
            }
        }

        [Fact]
        public void CacheShouldPreserveCurrentCaseSensitiveBehavior()
        {
            FluentMapper.Reset(typeof(CaseSensitiveCacheEntity));

            try
            {
                FluentMapper.Initialize(c => c.AddMap(new CaseSensitiveCacheMap()));

                var exact = FluentMapper.Registry.GetFluentPropertyInfo(typeof(CaseSensitiveCacheEntity), "case_id");
                var wrongCase = FluentMapper.Registry.GetFluentPropertyInfo(typeof(CaseSensitiveCacheEntity), "CASE_ID");

                Assert.Equal(typeof(CaseSensitiveCacheEntity).GetProperty(nameof(CaseSensitiveCacheEntity.Id)), exact);
                Assert.Null(wrongCase);
                Assert.Equal(2, FluentMapper.Registry.CacheEntryCount);
            }
            finally
            {
                FluentMapper.Reset(typeof(CaseSensitiveCacheEntity));
            }
        }

        [Fact]
        public void ResetShouldInvalidateCachedMappings()
        {
            FluentMapper.Reset(typeof(ReconfiguredEntity));

            try
            {
                FluentMapper.Initialize(c => c.AddMap(new ReconfiguredIdMap()));
                var first = FluentMapper.Registry.GetFluentPropertyInfo(typeof(ReconfiguredEntity), "shared_column");

                FluentMapper.Reset(typeof(ReconfiguredEntity));
                FluentMapper.Initialize(c => c.AddMap(new ReconfiguredNameMap()));
                var second = FluentMapper.Registry.GetFluentPropertyInfo(typeof(ReconfiguredEntity), "shared_column");

                Assert.Equal(typeof(ReconfiguredEntity).GetProperty(nameof(ReconfiguredEntity.Id)), first);
                Assert.Equal(typeof(ReconfiguredEntity).GetProperty(nameof(ReconfiguredEntity.Name)), second);
                Assert.Equal(1, FluentMapper.Registry.CacheEntryCount);
            }
            finally
            {
                FluentMapper.Reset(typeof(ReconfiguredEntity));
            }
        }

        [Fact]
        public void RegistrationShouldInvalidateCachedMissesForSameType()
        {
            FluentMapper.Reset(typeof(MissInvalidationEntity));

            try
            {
                var miss = FluentMapper.Registry.GetFluentPropertyInfo(typeof(MissInvalidationEntity), "late_column");

                FluentMapper.Initialize(c => c.AddMap(new MissInvalidationMap()));
                var hit = FluentMapper.Registry.GetFluentPropertyInfo(typeof(MissInvalidationEntity), "late_column");

                Assert.Null(miss);
                Assert.Equal(typeof(MissInvalidationEntity).GetProperty(nameof(MissInvalidationEntity.Name)), hit);
                Assert.Equal(1, FluentMapper.Registry.CacheEntryCount);
            }
            finally
            {
                FluentMapper.Reset(typeof(MissInvalidationEntity));
            }
        }

        [Fact]
        public void TypeMapResolutionShouldRemainStableUnderConcurrentReads()
        {
            FluentMapper.Reset(typeof(ConcurrentCacheEntity));

            try
            {
                FluentMapper.Initialize(c => c.AddMap(new ConcurrentCacheMap()));
                var typeMap = SqlMapper.GetTypeMap(typeof(ConcurrentCacheEntity));
                var properties = new string[100];

                Parallel.For(0, properties.Length, i =>
                {
                    properties[i] = typeMap.GetMember("concurrent_id").Property.Name;
                });

                Assert.True(properties.All(name => name == nameof(ConcurrentCacheEntity.Id)));
                Assert.Equal(1, FluentMapper.Registry.CacheEntryCount);
            }
            finally
            {
                FluentMapper.Reset(typeof(ConcurrentCacheEntity));
            }
        }

        private class CacheHitEntity
        {
            public int Id { get; set; }
        }

        private class CacheHitMap : EntityMap<CacheHitEntity>
        {
            public CacheHitMap()
            {
                Map(e => e.Id).ToColumn("cache_id");
            }
        }

        private class FirstSameColumnEntity
        {
            public int Id { get; set; }
        }

        private class FirstSameColumnMap : EntityMap<FirstSameColumnEntity>
        {
            public FirstSameColumnMap()
            {
                Map(e => e.Id).ToColumn("shared_column");
            }
        }

        private class SecondSameColumnEntity
        {
            public string Name { get; set; }
        }

        private class SecondSameColumnMap : EntityMap<SecondSameColumnEntity>
        {
            public SecondSameColumnMap()
            {
                Map(e => e.Name).ToColumn("shared_column");
            }
        }

        private class DistinctColumnsEntity
        {
            public int Id { get; set; }

            public string Name { get; set; }
        }

        private class DistinctColumnsMap : EntityMap<DistinctColumnsEntity>
        {
            public DistinctColumnsMap()
            {
                Map(e => e.Id).ToColumn("id_column");
                Map(e => e.Name).ToColumn("name_column");
            }
        }

        private class CaseSensitiveCacheEntity
        {
            public int Id { get; set; }
        }

        private class CaseSensitiveCacheMap : EntityMap<CaseSensitiveCacheEntity>
        {
            public CaseSensitiveCacheMap()
            {
                Map(e => e.Id).ToColumn("case_id");
            }
        }

        private class ReconfiguredEntity
        {
            public int Id { get; set; }

            public string Name { get; set; }
        }

        private class ReconfiguredIdMap : EntityMap<ReconfiguredEntity>
        {
            public ReconfiguredIdMap()
            {
                Map(e => e.Id).ToColumn("shared_column");
            }
        }

        private class ReconfiguredNameMap : EntityMap<ReconfiguredEntity>
        {
            public ReconfiguredNameMap()
            {
                Map(e => e.Name).ToColumn("shared_column");
            }
        }

        private class MissInvalidationEntity
        {
            public string Name { get; set; }
        }

        private class MissInvalidationMap : EntityMap<MissInvalidationEntity>
        {
            public MissInvalidationMap()
            {
                Map(e => e.Name).ToColumn("late_column");
            }
        }

        private class ConcurrentCacheEntity
        {
            public int Id { get; set; }
        }

        private class ConcurrentCacheMap : EntityMap<ConcurrentCacheEntity>
        {
            public ConcurrentCacheMap()
            {
                Map(e => e.Id).ToColumn("concurrent_id");
            }
        }
    }
}
