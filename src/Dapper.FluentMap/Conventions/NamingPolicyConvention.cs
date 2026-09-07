using Dapper.FluentMap.Naming;

namespace Dapper.FluentMap.Conventions
{
    internal sealed class NamingPolicyConvention : Convention
    {
        internal NamingPolicyConvention(NamingPolicy namingPolicy, bool caseSensitive)
        {
            Properties()
                .Configure(c =>
                {
                    c.Transform(namingPolicy.GetColumnName);
                    if (!caseSensitive)
                    {
                        c.IsCaseInsensitive();
                    }
                });
        }
    }
}
