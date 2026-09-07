using System;
using System.Linq.Expressions;
using System.Reflection;
using Dapper.FluentMap.Utils;
using Xunit;

namespace Dapper.FluentMap.Tests
{
    public class ReflectionHelperTests
    {
        [Fact]
        public void GetMemberInfo_ReturnsProperty()
        {
            // Arrange
            Expression<Func<TestEntity, object>> expression = e => e.Id;

            // Act
            var memberInfo = ReflectionHelper.GetMemberInfo(expression);

            // Assert
            Assert.Equal(typeof(TestEntity).GetProperty("Id"), memberInfo);
            Assert.Equal(typeof(int), ((PropertyInfo)memberInfo).PropertyType);
        }

        [Fact]
        public void GetMemberInfo_ReturnsProperty_OfDerivedType()
        {
            // Arrange
            Expression<Func<DerivedTestEntity, object>> expression = e => e.Id;

            // Act
            var memberInfo = ReflectionHelper.GetMemberInfo(expression);

            // Assert
            Assert.Equal(typeof(TestEntity).GetProperty("Id"), memberInfo);
            Assert.Equal(typeof(TestEntity), memberInfo.DeclaringType);
            Assert.Equal(typeof(int), ((PropertyInfo)memberInfo).PropertyType);
        }

        [Fact]
        public void GetMemberInfo_ReturnsProperty_OfNullableSystemType()
        {
            // Arrange
            Expression<Func<TestEntityWithNullable, object>> expression = e => e.Value;

            // Act
            var memberInfo = ReflectionHelper.GetMemberInfo(expression);

            // Assert
            Assert.Equal(typeof(TestEntityWithNullable).GetProperty("Value"), memberInfo);
            Assert.Equal(typeof(TestEntityWithNullable), memberInfo.DeclaringType);
            Assert.Equal(typeof(decimal?), ((PropertyInfo)memberInfo).PropertyType);
        }

        [Fact]
        public void GetMemberInfo_ReturnsValueTypeProperty()
        {
            // Arrange
            Expression<Func<ValueObjectTestEntity, object>> expression = e => e.Email.Address;

            // Act
            var memberInfo = ReflectionHelper.GetMemberInfo(expression);

            // Assert
            Assert.Equal(typeof(EmailTestValueObject).GetProperty(nameof(EmailTestValueObject.Address)), memberInfo);
            Assert.Equal(typeof(EmailTestValueObject), memberInfo.DeclaringType);
            Assert.Equal(typeof(string), ((PropertyInfo)memberInfo).PropertyType);
        }

        [Fact]
        public void GetMemberInfo_ReturnsValueTypeProperty_WithSystemTypeNames()
        {
            // Arrange
            Expression<Func<ValueObjectTestEntity, object>> expression = e => e.String.Length;

            // Act
            var memberInfo = ReflectionHelper.GetMemberInfo(expression);

            // Assert
            Assert.Equal(typeof(SameMemberAsStringObject).GetProperty(nameof(SameMemberAsStringObject.Length)), memberInfo);
            Assert.Equal(typeof(SameMemberAsStringObject), memberInfo.DeclaringType);
            Assert.Equal(typeof(string), ((PropertyInfo)memberInfo).PropertyType);
        }

        [Fact]
        public void GetMemberInfo_ReturnsProperty_WhenPropertyNameMatchesSystemMember()
        {
            // Arrange
            Expression<Func<EntityWithStringFormatProperty, object>> expression = e => e.Format;

            // Act
            var memberInfo = ReflectionHelper.GetMemberInfo(expression);

            // Assert
            Assert.Equal(typeof(EntityWithStringFormatProperty).GetProperty(nameof(EntityWithStringFormatProperty.Format)), memberInfo);
            Assert.Equal(typeof(string), ((PropertyInfo)memberInfo).PropertyType);
        }

        [Fact]
        public void GetMemberInfo_ReturnsValueTypeProperty_WhenPropertyNameMatchesSystemMember()
        {
            // Arrange
            Expression<Func<EntityWithTimeSpanDurationProperty, object>> expression = e => e.Duration;

            // Act
            var memberInfo = ReflectionHelper.GetMemberInfo(expression);

            // Assert
            Assert.Equal(typeof(EntityWithTimeSpanDurationProperty).GetProperty(nameof(EntityWithTimeSpanDurationProperty.Duration)), memberInfo);
            Assert.Equal(typeof(TimeSpan), ((PropertyInfo)memberInfo).PropertyType);
        }

        [Fact]
        public void GetMemberInfo_ThrowsArgumentException_WhenExpressionIsNotPropertyAccess()
        {
            // Arrange
            Expression<Func<TestEntity, object>> expression = e => e.Id.ToString();

            // Act
            var exception = Assert.Throws<ArgumentException>(() => ReflectionHelper.GetMemberInfo(expression));

            // Assert
            Assert.Contains("property", exception.Message);
        }

        private class EntityWithStringFormatProperty
        {
            public string Format { get; set; }
        }

        private class EntityWithTimeSpanDurationProperty
        {
            public TimeSpan Duration { get; set; }
        }
    }
}
