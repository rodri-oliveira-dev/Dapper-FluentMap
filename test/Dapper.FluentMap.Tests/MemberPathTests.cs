using System;
using System.Linq;
using System.Linq.Expressions;
using Dapper.FluentMap.Utils;
using Xunit;

namespace Dapper.FluentMap.Tests
{
    public class MemberPathTests
    {
        [Fact]
        public void GetMemberPathShouldReturnSimplePath()
        {
            Expression<Func<MemberPathEntity, object>> expression = e => e.Name;

            var memberPath = ReflectionHelper.GetMemberPath(expression);

            Assert.False(memberPath.IsNested);
            Assert.Equal("Name", memberPath.ToString());
            Assert.Equal(typeof(MemberPathEntity).GetProperty(nameof(MemberPathEntity.Name)), memberPath.PropertyInfo);
            Assert.Equal(new[] { "Name" }, memberPath.Properties.Select(p => p.Name));
        }

        [Fact]
        public void GetMemberPathShouldReturnNestedPathInOrder()
        {
            Expression<Func<MemberPathEntity, object>> expression = e => e.Address.City;

            var memberPath = ReflectionHelper.GetMemberPath(expression);

            Assert.True(memberPath.IsNested);
            Assert.Equal("Address.City", memberPath.ToString());
            Assert.Equal(typeof(MemberPathEntity).GetProperty(nameof(MemberPathEntity.Address)), memberPath.Properties[0]);
            Assert.Equal(typeof(AddressInfo).GetProperty(nameof(AddressInfo.City)), memberPath.Properties[1]);
            Assert.Equal(typeof(AddressInfo).GetProperty(nameof(AddressInfo.City)), memberPath.PropertyInfo);
        }

        [Fact]
        public void MemberPathShouldDistinguishPathsWithSameTerminalPropertyName()
        {
            Expression<Func<MemberPathEntity, object>> rankExpression = e => e.Rank.Level;
            Expression<Func<MemberPathEntity, object>> seniorityExpression = e => e.Seniority.Level;

            var rankPath = ReflectionHelper.GetMemberPath(rankExpression);
            var seniorityPath = ReflectionHelper.GetMemberPath(seniorityExpression);

            Assert.NotEqual(rankPath, seniorityPath);
            Assert.Equal("Rank.Level", rankPath.ToString());
            Assert.Equal("Seniority.Level", seniorityPath.ToString());
            Assert.Equal(rankPath.PropertyInfo.Name, seniorityPath.PropertyInfo.Name);
        }

        [Fact]
        public void MemberPathShouldTreatSamePathAsEqual()
        {
            Expression<Func<MemberPathEntity, object>> firstExpression = e => e.Rank.Level;
            Expression<Func<MemberPathEntity, object>> secondExpression = e => e.Rank.Level;

            var firstPath = ReflectionHelper.GetMemberPath(firstExpression);
            var secondPath = ReflectionHelper.GetMemberPath(secondExpression);

            Assert.Equal(firstPath, secondPath);
            Assert.Equal(firstPath.GetHashCode(), secondPath.GetHashCode());
        }

        [Fact]
        public void GetMemberPathShouldHandleConvertForValueTypes()
        {
            Expression<Func<MemberPathEntity, object>> expression = e => e.Rank.Level;

            var memberPath = ReflectionHelper.GetMemberPath(expression);

            Assert.Equal("Rank.Level", memberPath.ToString());
            Assert.Equal(typeof(int), memberPath.PropertyInfo.PropertyType);
        }

        [Fact]
        public void GetMemberPathShouldThrowArgumentExceptionForInvalidExpression()
        {
            Expression<Func<MemberPathEntity, object>> expression = e => e.Name.ToString();

            var exception = Assert.Throws<ArgumentException>(() => ReflectionHelper.GetMemberPath(expression));

            Assert.Contains("property path", exception.Message);
        }

        private class MemberPathEntity
        {
            public string Name { get; set; }

            public AddressInfo Address { get; set; }

            public RankInfo Rank { get; set; }

            public SeniorityInfo Seniority { get; set; }
        }

        private class AddressInfo
        {
            public string City { get; set; }
        }

        private class RankInfo
        {
            public int Level { get; set; }
        }

        private class SeniorityInfo
        {
            public int Level { get; set; }
        }
    }
}
