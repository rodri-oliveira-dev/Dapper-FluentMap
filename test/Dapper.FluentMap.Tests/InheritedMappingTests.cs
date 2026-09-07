using System;
using Dapper;
using Dapper.FluentMap.Conventions;
using Dapper.FluentMap.Mapping;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Dapper.FluentMap.Tests
{
    public class InheritedMappingTests
    {
        [Fact]
        public void IncludedBaseMappingShouldResolveInheritedProperty()
        {
            PreTest(typeof(SimpleBaseUser), typeof(SimpleAdminUser));

            FluentMapper.Initialize(c =>
            {
                c.AddMap(new SimpleBaseUserMap());
                c.AddMap(new SimpleAdminUserMap());
            });

            var member = SqlMapper.GetTypeMap(typeof(SimpleAdminUser)).GetMember("user_id");

            Assert.NotNull(member);
            Assert.Equal(typeof(SimpleBaseUser).GetProperty(nameof(SimpleBaseUser.Id)), member.Property);
        }

        [Fact]
        public void DerivedMappingShouldResolveOwnPropertyWithIncludedBase()
        {
            PreTest(typeof(DerivedPropertyBaseUser), typeof(DerivedPropertyAdminUser));

            FluentMapper.Initialize(c =>
            {
                c.AddMap(new DerivedPropertyBaseUserMap());
                c.AddMap(new DerivedPropertyAdminUserMap());
            });

            var baseMember = SqlMapper.GetTypeMap(typeof(DerivedPropertyAdminUser)).GetMember("user_id");
            var derivedMember = SqlMapper.GetTypeMap(typeof(DerivedPropertyAdminUser)).GetMember("admin_permission");

            Assert.NotNull(baseMember);
            Assert.NotNull(derivedMember);
            Assert.Equal(typeof(DerivedPropertyBaseUser).GetProperty(nameof(DerivedPropertyBaseUser.Id)), baseMember.Property);
            Assert.Equal(typeof(DerivedPropertyAdminUser).GetProperty(nameof(DerivedPropertyAdminUser.Permission)), derivedMember.Property);
        }

        [Fact]
        public void DerivedMappingShouldOverrideIncludedBaseForSameMemberPath()
        {
            PreTest(typeof(OverrideBaseUser), typeof(OverrideAdminUser));

            FluentMapper.Initialize(c =>
            {
                c.AddMap(new OverrideBaseUserMap());
                c.AddMap(new OverrideAdminUserMap());
            });

            var derivedMember = SqlMapper.GetTypeMap(typeof(OverrideAdminUser)).GetMember("admin_id");
            var baseMember = SqlMapper.GetTypeMap(typeof(OverrideAdminUser)).GetMember("user_id");

            Assert.NotNull(derivedMember);
            Assert.Null(baseMember);
            Assert.Equal(typeof(OverrideBaseUser).GetProperty(nameof(OverrideBaseUser.Id)), derivedMember.Property);
        }

        [Fact]
        public void IncludedBaseMappingShouldTakePrecedenceOverConventionForSameMemberPath()
        {
            PreTest(typeof(ConventionBaseUser), typeof(ConventionAdminUser));

            FluentMapper.Initialize(c =>
            {
                c.AddMap(new ConventionBaseUserMap());
                c.AddMap(new ConventionAdminUserMap());
                c.AddConvention<PrefixConvention>().ForEntity<ConventionAdminUser>();
            });

            var inheritedExplicitMember = SqlMapper.GetTypeMap(typeof(ConventionAdminUser)).GetMember("user_id");
            var conventionForInheritedMember = SqlMapper.GetTypeMap(typeof(ConventionAdminUser)).GetMember("colId");
            var conventionForDerivedMember = SqlMapper.GetTypeMap(typeof(ConventionAdminUser)).GetMember("colPermission");

            Assert.NotNull(inheritedExplicitMember);
            Assert.Null(conventionForInheritedMember);
            Assert.NotNull(conventionForDerivedMember);
            Assert.Equal(typeof(ConventionBaseUser).GetProperty(nameof(ConventionBaseUser.Id)), inheritedExplicitMember.Property);
            Assert.Equal(typeof(ConventionAdminUser).GetProperty(nameof(ConventionAdminUser.Permission)), conventionForDerivedMember.Property);
        }

        [Fact]
        public void IncludedBaseMappingShouldPreserveInheritedMemberPath()
        {
            PreTest(typeof(MemberPathBaseUser), typeof(MemberPathAdminUser));

            FluentMapper.Initialize(c =>
            {
                c.AddMap(new MemberPathBaseUserMap());
                c.AddMap(new MemberPathAdminUserMap());
            });

            var member = SqlMapper.GetTypeMap(typeof(MemberPathAdminUser)).GetMember("rank_level");
            var explanation = FluentMapper.Explain<MemberPathAdminUser>();

            Assert.Null(member);
            Assert.Contains(
                explanation.Members,
                m => m.MemberPath == "Rank.Level" &&
                     m.ColumnName == "rank_level" &&
                     m.PropertyInfo == typeof(InheritedRankInfo).GetProperty(nameof(InheritedRankInfo.Level)));
        }

        [Fact]
        public void MultipleInheritanceLevelsShouldComposeNearestMappingsBeforeBaseMappings()
        {
            PreTest(typeof(MultiLevelBaseUser), typeof(MultiLevelStaffUser), typeof(MultiLevelAdminUser));

            FluentMapper.Initialize(c =>
            {
                c.AddMap(new MultiLevelBaseUserMap());
                c.AddMap(new MultiLevelStaffUserMap());
                c.AddMap(new MultiLevelAdminUserMap());
            });

            var baseMember = SqlMapper.GetTypeMap(typeof(MultiLevelAdminUser)).GetMember("user_id");
            var intermediateMember = SqlMapper.GetTypeMap(typeof(MultiLevelAdminUser)).GetMember("staff_code");
            var derivedMember = SqlMapper.GetTypeMap(typeof(MultiLevelAdminUser)).GetMember("admin_permission");

            Assert.NotNull(baseMember);
            Assert.NotNull(intermediateMember);
            Assert.NotNull(derivedMember);
            Assert.Equal(typeof(MultiLevelBaseUser).GetProperty(nameof(MultiLevelBaseUser.Id)), baseMember.Property);
            Assert.Equal(typeof(MultiLevelStaffUser).GetProperty(nameof(MultiLevelStaffUser.StaffCode)), intermediateMember.Property);
            Assert.Equal(typeof(MultiLevelAdminUser).GetProperty(nameof(MultiLevelAdminUser.Permission)), derivedMember.Property);
        }

        [Fact]
        public void MissingBaseMapShouldThrowConfigurationException()
        {
            PreTest(typeof(MissingBaseUser), typeof(MissingBaseAdminUser));

            var exception = Assert.Throws<FluentMapConfigurationException>(() =>
                FluentMapper.Initialize(c => c.AddMap(new MissingBaseAdminUserMap())));

            Assert.Contains(typeof(MissingBaseAdminUser).FullName, exception.Message);
            Assert.Contains(typeof(MissingBaseUser).FullName, exception.Message);
            Assert.Contains("Register the base map before the derived map", exception.Message);
        }

        [Fact]
        public void InvalidBaseTypeShouldThrowConfigurationException()
        {
            var exception = Assert.Throws<FluentMapConfigurationException>(() => new InvalidBaseAdminUserMap());

            Assert.Contains(typeof(UnrelatedUser).FullName, exception.Message);
            Assert.Contains(typeof(InvalidBaseAdminUser).FullName, exception.Message);
            Assert.Contains("base class", exception.Message);
        }

        [Fact]
        public void ColumnConflictBetweenDerivedAndIncludedBaseShouldThrowConfigurationException()
        {
            PreTest(typeof(ConflictBaseUser), typeof(ConflictAdminUser));

            var exception = Assert.Throws<FluentMapConfigurationException>(() =>
                FluentMapper.Initialize(c =>
                {
                    c.AddMap(new ConflictBaseUserMap());
                    c.AddMap(new ConflictAdminUserMap());
                }));

            Assert.Contains("shared_column", exception.Message);
            Assert.Contains(nameof(ConflictBaseUser.Id), exception.Message);
            Assert.Contains(nameof(ConflictAdminUser.Permission), exception.Message);
            Assert.Contains(typeof(ConflictAdminUser).FullName, exception.Message);
        }

        [Fact]
        public void DerivedMapMustBeRegisteredAfterIncludedBaseMap()
        {
            PreTest(typeof(RegistrationBaseUser), typeof(RegistrationAdminUser));

            Assert.Throws<FluentMapConfigurationException>(() =>
                FluentMapper.Initialize(c => c.AddMap(new RegistrationAdminUserMap())));

            FluentMapper.Initialize(c =>
            {
                c.AddMap(new RegistrationBaseUserMap());
                c.AddMap(new RegistrationAdminUserMap());
            });

            var member = SqlMapper.GetTypeMap(typeof(RegistrationAdminUser)).GetMember("user_id");

            Assert.NotNull(member);
            Assert.Equal(typeof(RegistrationBaseUser).GetProperty(nameof(RegistrationBaseUser.Id)), member.Property);
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void IncludedBaseMappingShouldMaterializeWithDapper()
        {
            PreTest(typeof(IntegrationBaseUser), typeof(IntegrationAdminUser));

            try
            {
                FluentMapper.Initialize(c =>
                {
                    c.AddMap(new IntegrationBaseUserMap());
                    c.AddMap(new IntegrationAdminUserMap());
                });

                using (var connection = OpenConnection())
                {
                    var entity = connection.QuerySingle<IntegrationAdminUser>(
                        "SELECT 42 AS user_id, 'deploy' AS admin_permission;");

                    Assert.Equal(42, entity.Id);
                    Assert.Equal("deploy", entity.Permission);
                }
            }
            finally
            {
                PreTest(typeof(IntegrationBaseUser), typeof(IntegrationAdminUser));
            }
        }

        private static SqliteConnection OpenConnection()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();
            return connection;
        }

        private static void PreTest(params Type[] types)
        {
            FluentMapper.Reset(types);
        }

        private class SimpleBaseUser
        {
            public int Id { get; set; }
        }

        private class SimpleAdminUser : SimpleBaseUser
        {
        }

        private class SimpleBaseUserMap : EntityMap<SimpleBaseUser>
        {
            public SimpleBaseUserMap()
            {
                Map(e => e.Id).ToColumn("user_id");
            }
        }

        private class SimpleAdminUserMap : EntityMap<SimpleAdminUser>
        {
            public SimpleAdminUserMap()
            {
                IncludeBase<SimpleBaseUser>();
            }
        }

        private class DerivedPropertyBaseUser
        {
            public int Id { get; set; }
        }

        private class DerivedPropertyAdminUser : DerivedPropertyBaseUser
        {
            public string Permission { get; set; }
        }

        private class DerivedPropertyBaseUserMap : EntityMap<DerivedPropertyBaseUser>
        {
            public DerivedPropertyBaseUserMap()
            {
                Map(e => e.Id).ToColumn("user_id");
            }
        }

        private class DerivedPropertyAdminUserMap : EntityMap<DerivedPropertyAdminUser>
        {
            public DerivedPropertyAdminUserMap()
            {
                IncludeBase<DerivedPropertyBaseUser>();
                Map(e => e.Permission).ToColumn("admin_permission");
            }
        }

        private class OverrideBaseUser
        {
            public int Id { get; set; }
        }

        private class OverrideAdminUser : OverrideBaseUser
        {
        }

        private class OverrideBaseUserMap : EntityMap<OverrideBaseUser>
        {
            public OverrideBaseUserMap()
            {
                Map(e => e.Id).ToColumn("user_id");
            }
        }

        private class OverrideAdminUserMap : EntityMap<OverrideAdminUser>
        {
            public OverrideAdminUserMap()
            {
                IncludeBase<OverrideBaseUser>();
                Map(e => e.Id).ToColumn("admin_id");
            }
        }

        private class ConventionBaseUser
        {
            public int Id { get; set; }
        }

        private class ConventionAdminUser : ConventionBaseUser
        {
            public string Permission { get; set; }
        }

        private class ConventionBaseUserMap : EntityMap<ConventionBaseUser>
        {
            public ConventionBaseUserMap()
            {
                Map(e => e.Id).ToColumn("user_id");
            }
        }

        private class ConventionAdminUserMap : EntityMap<ConventionAdminUser>
        {
            public ConventionAdminUserMap()
            {
                IncludeBase<ConventionBaseUser>();
            }
        }

        private class MemberPathBaseUser
        {
            public InheritedRankInfo Rank { get; set; }
        }

        private class MemberPathAdminUser : MemberPathBaseUser
        {
        }

        private class InheritedRankInfo
        {
            public int Level { get; set; }
        }

        private class MemberPathBaseUserMap : EntityMap<MemberPathBaseUser>
        {
            public MemberPathBaseUserMap()
            {
                Map(e => e.Rank.Level).ToColumn("rank_level");
            }
        }

        private class MemberPathAdminUserMap : EntityMap<MemberPathAdminUser>
        {
            public MemberPathAdminUserMap()
            {
                IncludeBase<MemberPathBaseUser>();
            }
        }

        private class MultiLevelBaseUser
        {
            public int Id { get; set; }
        }

        private class MultiLevelStaffUser : MultiLevelBaseUser
        {
            public string StaffCode { get; set; }
        }

        private class MultiLevelAdminUser : MultiLevelStaffUser
        {
            public string Permission { get; set; }
        }

        private class MultiLevelBaseUserMap : EntityMap<MultiLevelBaseUser>
        {
            public MultiLevelBaseUserMap()
            {
                Map(e => e.Id).ToColumn("user_id");
            }
        }

        private class MultiLevelStaffUserMap : EntityMap<MultiLevelStaffUser>
        {
            public MultiLevelStaffUserMap()
            {
                IncludeBase<MultiLevelBaseUser>();
                Map(e => e.StaffCode).ToColumn("staff_code");
            }
        }

        private class MultiLevelAdminUserMap : EntityMap<MultiLevelAdminUser>
        {
            public MultiLevelAdminUserMap()
            {
                IncludeBase<MultiLevelStaffUser>();
                Map(e => e.Permission).ToColumn("admin_permission");
            }
        }

        private class MissingBaseUser
        {
            public int Id { get; set; }
        }

        private class MissingBaseAdminUser : MissingBaseUser
        {
        }

        private class MissingBaseAdminUserMap : EntityMap<MissingBaseAdminUser>
        {
            public MissingBaseAdminUserMap()
            {
                IncludeBase<MissingBaseUser>();
            }
        }

        private class InvalidBaseAdminUser
        {
        }

        private class UnrelatedUser
        {
        }

        private class InvalidBaseAdminUserMap : EntityMap<InvalidBaseAdminUser>
        {
            public InvalidBaseAdminUserMap()
            {
                IncludeBase<UnrelatedUser>();
            }
        }

        private class ConflictBaseUser
        {
            public int Id { get; set; }
        }

        private class ConflictAdminUser : ConflictBaseUser
        {
            public string Permission { get; set; }
        }

        private class ConflictBaseUserMap : EntityMap<ConflictBaseUser>
        {
            public ConflictBaseUserMap()
            {
                Map(e => e.Id).ToColumn("shared_column");
            }
        }

        private class ConflictAdminUserMap : EntityMap<ConflictAdminUser>
        {
            public ConflictAdminUserMap()
            {
                IncludeBase<ConflictBaseUser>();
                Map(e => e.Permission).ToColumn("shared_column");
            }
        }

        private class RegistrationBaseUser
        {
            public int Id { get; set; }
        }

        private class RegistrationAdminUser : RegistrationBaseUser
        {
        }

        private class RegistrationBaseUserMap : EntityMap<RegistrationBaseUser>
        {
            public RegistrationBaseUserMap()
            {
                Map(e => e.Id).ToColumn("user_id");
            }
        }

        private class RegistrationAdminUserMap : EntityMap<RegistrationAdminUser>
        {
            public RegistrationAdminUserMap()
            {
                IncludeBase<RegistrationBaseUser>();
            }
        }

        private class IntegrationBaseUser
        {
            public int Id { get; set; }
        }

        private class IntegrationAdminUser : IntegrationBaseUser
        {
            public string Permission { get; set; }
        }

        private class IntegrationBaseUserMap : EntityMap<IntegrationBaseUser>
        {
            public IntegrationBaseUserMap()
            {
                Map(e => e.Id).ToColumn("user_id");
            }
        }

        private class IntegrationAdminUserMap : EntityMap<IntegrationAdminUser>
        {
            public IntegrationAdminUserMap()
            {
                IncludeBase<IntegrationBaseUser>();
                Map(e => e.Permission).ToColumn("admin_permission");
            }
        }

        private class PrefixConvention : Convention
        {
            public PrefixConvention()
            {
                Properties()
                    .Configure(c => c.HasPrefix("col"));
            }
        }
    }
}
