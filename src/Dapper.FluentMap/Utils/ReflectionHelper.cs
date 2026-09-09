using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using Dapper.FluentMap.Mapping;

namespace Dapper.FluentMap.Utils
{
    /// <summary>
    /// Provides helper methods for reflection operations.
    /// </summary>
    public static class ReflectionHelper
    {
        /// <summary>
        /// Returns the <see cref="T:System.Reflection.MemberInfo"/> for the specified lamba expression.
        /// </summary>
        /// <param name="lambda">A lamba expression containing a MemberExpression.</param>
        /// <returns>A <see cref="MemberInfo"/> object for the member in the specified lambda expression.</returns>
        public static MemberInfo GetMemberInfo(LambdaExpression lambda)
        {
            return GetMemberPath(lambda).PropertyInfo;
        }

        internal static MemberPath GetMemberPath(LambdaExpression lambda)
        {
            if (lambda == null)
            {
                throw new ArgumentNullException(nameof(lambda));
            }

            var properties = new Stack<PropertyInfo>();
            var expr = RemoveConvert(lambda.Body);

            while (true)
            {
                if (TryReadPropertyAccess(lambda, expr, properties, out var nextExpression))
                {
                    expr = nextExpression;
                    continue;
                }

                if (expr != null && expr.NodeType == ExpressionType.Parameter && properties.Count > 0)
                {
                    return MemberPath.FromProperties(properties);
                }

                throw new ArgumentException($"Expression '{lambda}' must resolve to a property path.", nameof(lambda));
            }
        }

        private static bool TryReadPropertyAccess(
            LambdaExpression lambda,
            Expression expression,
            Stack<PropertyInfo> properties,
            out Expression nextExpression)
        {
            nextExpression = null;

            if (expression == null || expression.NodeType != ExpressionType.MemberAccess)
            {
                return false;
            }

            var memberExpression = (MemberExpression)expression;
            var member = memberExpression.Member;

            if (!(member is PropertyInfo propertyInfo))
            {
                throw new ArgumentException($"Expression '{lambda}' refers to member '{member.Name}', which is not a property.", nameof(lambda));
            }

            if (propertyInfo.GetIndexParameters().Length > 0)
            {
                throw new ArgumentException($"Expression '{lambda}' refers to indexed property '{member.Name}', which is not supported.", nameof(lambda));
            }

            properties.Push(propertyInfo);
            nextExpression = RemoveConvert(memberExpression.Expression);
            return true;
        }

        private static Expression RemoveConvert(Expression expression)
        {
            while (expression != null &&
                   (expression.NodeType == ExpressionType.Convert ||
                    expression.NodeType == ExpressionType.ConvertChecked))
            {
                expression = ((UnaryExpression)expression).Operand;
            }

            return expression;
        }
    }
}
