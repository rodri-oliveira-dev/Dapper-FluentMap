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
                if (expr == null)
                {
                    throw new ArgumentException($"Expression '{lambda}' must resolve to a property path.", nameof(lambda));
                }

                switch (expr.NodeType)
                {
                    case ExpressionType.MemberAccess:
                        var memberExpression = (MemberExpression)expr;
                        var member = memberExpression.Member;

                        if (member is PropertyInfo propertyInfo)
                        {
                            if (propertyInfo.GetIndexParameters().Length > 0)
                            {
                                throw new ArgumentException($"Expression '{lambda}' refers to indexed property '{member.Name}', which is not supported.", nameof(lambda));
                            }

                            properties.Push(propertyInfo);
                            expr = RemoveConvert(memberExpression.Expression);
                            break;
                        }

                        throw new ArgumentException($"Expression '{lambda}' refers to member '{member.Name}', which is not a property.", nameof(lambda));

                    case ExpressionType.Parameter:
                        if (properties.Count == 0)
                        {
                            throw new ArgumentException($"Expression '{lambda}' must resolve to a property path.", nameof(lambda));
                        }

                        return MemberPath.FromProperties(properties);

                    default:
                        throw new ArgumentException($"Expression '{lambda}' must resolve to a property path.", nameof(lambda));
                }
            }
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
