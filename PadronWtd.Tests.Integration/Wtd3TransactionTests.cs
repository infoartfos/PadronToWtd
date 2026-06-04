using System;
using FluentAssertions;
using PadronWtd.Repository.DI;
using PadronWtd.Tests.Integration.Fixtures;
using SAPbobsCOM;
using Xunit;

namespace PadronWtd.Tests.Integration
{
    public class Wtd3TransactionTests : IClassFixture<SapConnectionFixture>
    {
        private readonly Company _company;
        private readonly PSaltaRepository _repository;

        public Wtd3TransactionTests(SapConnectionFixture fixture)
        {
            _company = fixture.Company;
            _repository = new PSaltaRepository(_company);
        }

        [Fact]
        public void StartAndCommitTransaction_ShouldPersistMultipleInserts()
        {
            // Arrange
            string codigo1 = "TST_A";
            string codigo2 = "TST_B";
            int entry = TestData.AbsEntry;

            _company.StartTransaction();

            try
            {
                // Act
                var (ok1, err1) = _repository.InsertWtd3Direct(
                    _company, entry, codigo1,
                    TestData.ExistingCuit, TestData.Desde, TestData.Hasta, "80", "A");
                ok1.Should().BeTrue();

                var (ok2, err2) = _repository.InsertWtd3Direct(
                    _company, entry, codigo2,
                    TestData.ExistingCuit, TestData.Desde, TestData.Hasta, "80", "A");
                ok2.Should().BeTrue();

                _company.EndTransaction(BoWfTransOpt.wf_Commit);

                // Assert
                bool exists1 = _repository.CheckWtd3Exists(entry, codigo1, TestData.ExistingCuit, TestData.Desde);
                bool exists2 = _repository.CheckWtd3Exists(entry, codigo2, TestData.ExistingCuit, TestData.Desde);
                exists1.Should().BeTrue("TST_A debería persistir después del commit");
                exists2.Should().BeTrue("TST_B debería persistir después del commit");

                // Cleanup: borrar los registros de prueba
                CleanupTestData(entry, codigo1, codigo2);
            }
            catch
            {
                if (_company.InTransaction)
                    _company.EndTransaction(BoWfTransOpt.wf_RollBack);
                throw;
            }
        }

        [Fact]
        public void RollbackTransaction_ShouldDiscardAllInserts()
        {
            // Arrange
            string codigo = "TST_RB";

            _company.StartTransaction();

            _repository.InsertWtd3Direct(
                _company, TestData.AbsEntry, codigo,
                TestData.ExistingCuit, TestData.Desde, TestData.Hasta, "80", "A");

            // Act
            _company.EndTransaction(BoWfTransOpt.wf_RollBack);

            // Assert
            bool exists = _repository.CheckWtd3Exists(
                TestData.AbsEntry, codigo, TestData.ExistingCuit, TestData.Desde);
            exists.Should().BeFalse("rollback debería descartar todo");
        }

        private void CleanupTestData(int entry, params string[] codigos)
        {
            Recordset rs = null;
            try
            {
                rs = (Recordset)_company.GetBusinessObject(BoObjectTypes.BoRecordset);
                foreach (var cod in codigos)
                {
                    string sql = $@"
                        DELETE FROM ""WTD3""
                        WHERE ""AbsEntry"" = {entry}
                          AND ""WTCode"" = '{cod}'
                          AND ""KeyPart1"" = '{TestData.ExistingCuit}'
                          AND ""DateFrom"" = TO_DATE('{TestData.Desde:yyyyMMdd}', 'YYYYMMDD')";
                    rs.DoQuery(sql);
                }
            }
            finally
            {
                if (rs != null) System.Runtime.InteropServices.Marshal.ReleaseComObject(rs);
            }
        }
    }
}
