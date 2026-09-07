using System;
using System.Data;
using Xunit;

namespace Dapper.FluentMap.Tests
{
    public class GeneratedMaterializerSpikeTests
    {
        [Fact]
        public void GeneratedLikeMaterializerShouldMaterializeSimpleEntityWithNestedMutableObjectAndDbNull()
        {
            using (var reader = CreateReader(
                new[] { "customer_id", "full_name", "city", "note" },
                new object[] { 1, "Ada Lovelace", "London", DBNull.Value },
                new object[] { 2, "Grace Hopper", DBNull.Value, "compiler" }))
            {
                Assert.True(reader.Read());
                var first = GeneratedCustomerMaterializer.ReadDefault(reader);

                Assert.Equal(1, first.Id);
                Assert.Equal("Ada Lovelace", first.Name);
                Assert.NotNull(first.Address);
                Assert.Equal("London", first.Address.City);
                Assert.Null(first.Note);

                Assert.True(reader.Read());
                var second = GeneratedCustomerMaterializer.ReadDefault(reader);

                Assert.Equal(2, second.Id);
                Assert.Equal("Grace Hopper", second.Name);
                Assert.Null(second.Address);
                Assert.Equal("compiler", second.Note);
            }
        }

        [Fact]
        public void GeneratedLikeMaterializerShouldSupportImmutableValueObjectConstructorAndProfiles()
        {
            using (var reader = CreateReader(
                new[] { "legacy_id", "legacy_cpf", "legal_name" },
                new object[] { 7, "12345678909", "Legacy Ada" },
                new object[] { 8, DBNull.Value, "Legacy Grace" }))
            {
                Assert.True(reader.Read());
                var first = GeneratedCustomerMaterializer.ReadLegacyProfile(reader);

                Assert.Equal(7, first.Id);
                Assert.Equal("Legacy Ada", first.Name);
                Assert.NotNull(first.Cpf);
                Assert.Equal("12345678909", first.Cpf.Number);

                Assert.True(reader.Read());
                var second = GeneratedCustomerMaterializer.ReadLegacyProfile(reader);

                Assert.Equal(8, second.Id);
                Assert.Equal("Legacy Grace", second.Name);
                Assert.Null(second.Cpf);
            }
        }

        private static IDataReader CreateReader(string[] columns, params object[][] rows)
        {
            var table = new DataTable();
            foreach (var column in columns)
            {
                table.Columns.Add(column, typeof(object));
            }

            foreach (var row in rows)
            {
                table.Rows.Add(row);
            }

            return table.CreateDataReader();
        }

        private static class GeneratedCustomerMaterializer
        {
            internal static GeneratedCustomer ReadDefault(IDataRecord record)
            {
                var customer = new GeneratedCustomer
                {
                    Id = ReadInt32(record, 0),
                    Name = ReadString(record, 1),
                    Note = ReadString(record, 3)
                };

                if (!record.IsDBNull(2))
                {
                    customer.Address = new GeneratedAddress
                    {
                        City = ReadString(record, 2)
                    };
                }

                return customer;
            }

            internal static GeneratedCustomer ReadLegacyProfile(IDataRecord record)
            {
                return new GeneratedCustomer(
                    ReadInt32(record, 0),
                    record.IsDBNull(1) ? null : new GeneratedCpf(ReadString(record, 1)),
                    ReadString(record, 2));
            }

            private static int ReadInt32(IDataRecord record, int ordinal)
            {
                return record.IsDBNull(ordinal) ? default : Convert.ToInt32(record.GetValue(ordinal));
            }

            private static string ReadString(IDataRecord record, int ordinal)
            {
                return record.IsDBNull(ordinal) ? null : Convert.ToString(record.GetValue(ordinal));
            }
        }

        private sealed class GeneratedCustomer
        {
            public GeneratedCustomer()
            {
            }

            public GeneratedCustomer(int id, GeneratedCpf cpf, string name)
            {
                Id = id;
                Cpf = cpf;
                Name = name;
            }

            public int Id { get; set; }

            public string Name { get; set; }

            public string Note { get; set; }

            public GeneratedAddress Address { get; set; }

            public GeneratedCpf Cpf { get; }
        }

        private sealed class GeneratedAddress
        {
            public string City { get; set; }
        }

        private sealed class GeneratedCpf
        {
            public GeneratedCpf(string number)
            {
                if (string.IsNullOrWhiteSpace(number))
                {
                    throw new ArgumentException("CPF cannot be empty.", nameof(number));
                }

                Number = number;
            }

            public string Number { get; }
        }
    }
}
